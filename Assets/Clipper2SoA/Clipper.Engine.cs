/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Date      :  3 March 2023                                                    *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2023                                         *
* Purpose   :  This is the main polygon clipping module                        *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/

using Chart3D.MathExtensions;
using Chart3D.MinHeap;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Clipper2SoA
{
    //Vertex: a pre-clipping data structure. It is used to separate polygons
    //into ascending and descending 'bounds' (or sides) that start at local
    //minima and ascend to a local maxima, before descending again.

    [Flags]
    public enum PointInPolygonResult { IsOn = 0, IsInside = 1, IsOutside = 2 };
    public enum VertexFlags { None = 0, OpenStart = 1, OpenEnd = 2, LocalMax = 4, LocalMin = 8 };
    public struct ClipperD
    {
        ClipType cliptype;
        FillRule fillrule;
        ActiveLL actives;
        int actives_ID;
        int sel_ID;
        NativeList<LocalMinima> minimaList;
        NativeList<IntersectNode> intersectList;
        VertexLL vertexList;
        OutRecLL outrecList;
        OutPtLL outPtList;
        MinHeap<long> scanlineList;
        NativeList<HorzSegment> horzSegList;
        NativeList<HorzJoin> horzJoinList;
        int currentLocMin;
        long currentBotY;
        bool isSortedMinimaList;
        bool hasOpenPaths;
        internal bool _using_polytree;
        internal bool _succeeded;
        private readonly double _scale;
        private readonly double _invScale;
        public bool PreserveCollinear { get; set; }
        public bool ReverseSolution { get; set; }


        public ClipperD(Allocator allocator, int roundingDecimalPrecision = 2)
        {
            cliptype = ClipType.None;
            fillrule = FillRule.EvenOdd;
            actives = new ActiveLL(16, allocator);
            actives_ID = -1;
            sel_ID = -1;
            minimaList = new NativeList<LocalMinima>(1024, allocator);
            intersectList = new NativeList<IntersectNode>(1024, allocator);
            vertexList = new VertexLL(128, allocator);
            outrecList = new OutRecLL(16, allocator);
            outPtList = new OutPtLL(16, allocator);
            scanlineList = new MinHeap<long>(64, allocator, Comparison.Max);
            horzSegList = new NativeList<HorzSegment>(64, allocator);
            horzJoinList = new NativeList<HorzJoin>(64, allocator);
            currentLocMin = 0;
            currentBotY = long.MaxValue;
            isSortedMinimaList = false;
            hasOpenPaths = false;
            _using_polytree = false;
            _succeeded = false;
            PreserveCollinear = true;
            ReverseSolution = false;
            _scale = math.pow(10, roundingDecimalPrecision);
            _invScale = 1 / _scale;
        }
        public void Dispose()
        {
            if (actives.IsCreated) actives.Dispose();
            if (minimaList.IsCreated) minimaList.Dispose();
            if (intersectList.IsCreated) intersectList.Dispose();
            if (vertexList.IsCreated) vertexList.Dispose();
            if (outrecList.IsCreated) outrecList.Dispose();
            if (outPtList.IsCreated) outPtList.Dispose();
            if (scanlineList.IsCreated) scanlineList.Dispose();
            if (horzSegList.IsCreated) horzSegList.Dispose();
            if (horzJoinList.IsCreated) horzJoinList.Dispose();
        }
        public void Dispose(JobHandle jobHandle)
        {
            if (actives.IsCreated) actives.Dispose(jobHandle);
            if (minimaList.IsCreated) minimaList.Dispose(jobHandle);
            if (intersectList.IsCreated) intersectList.Dispose(jobHandle);
            if (vertexList.IsCreated) vertexList.Dispose(jobHandle);
            if (outrecList.IsCreated) outrecList.Dispose(jobHandle);
            if (outPtList.IsCreated) outPtList.Dispose(jobHandle);
            if (scanlineList.IsCreated) scanlineList.Dispose(jobHandle);
            if (horzSegList.IsCreated) horzSegList.Dispose(jobHandle);
            if (horzJoinList.IsCreated) horzJoinList.Dispose(jobHandle);
        }
        public struct HorzSegSorter : IComparer<HorzSegment>
        {
            public OutPtLL m_outPtList;
            public HorzSegSorter(OutPtLL outPtList)
            {
                m_outPtList = outPtList;
            }
            public int Compare(HorzSegment hs1, HorzSegment hs2)
            {
                //if (hs1 == null || hs2 == null) return 0;
                if (hs1.rightOp == -1)
                {
                    return hs2.rightOp == -1 ? 0 : 1;
                }
                else if (hs2.rightOp == -1)
                    return -1;
                else
                    return m_outPtList.pt[hs1.leftOp].x.CompareTo(m_outPtList.pt[hs2.leftOp].x);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOdd(int val)
        {
            return ((val & 1) != 0);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHotEdge(int ae)
        {
            return actives.outrec[ae] != -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsOpen(int ae)
        {
            return minimaList[actives.localMin[ae]].isOpen;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsOpenEnd(int ae)
        {
            return minimaList[actives.localMin[ae]].isOpen && IsOpenEndVertex(actives.vertexTop[ae]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsOpenEndVertex(int vertexID)
        {
            return (vertexList.flags[vertexID] & (VertexFlags.OpenStart | VertexFlags.OpenEnd)) != VertexFlags.None;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetPrevHotEdge(int ae)
        {
            int prev = actives.prevInAEL[ae];
            while (prev != -1 && (IsOpen(prev) || !IsHotEdge(prev)))
                prev = actives.prevInAEL[prev];
            return prev;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsFront(int ae)
        {
            return (ae == outrecList.frontEdge[actives.outrec[ae]]);
        }

        /*******************************************************************************
        *  Dx:                             0(90deg)                                    *
        *                                  |                                           *
        *               +inf (180deg) <--- o --. -inf (0deg)                          *
        *******************************************************************************/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double GetDx(long2 pt1, long2 pt2)
        {
            double dy = pt2.y - pt1.y;
            if (dy != 0)
                return (pt2.x - pt1.x) / dy;
            if (pt2.x > pt1.x)
                return double.NegativeInfinity;
            return double.PositiveInfinity;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long TopX(int ae, long currentY)
        {
            var aeTop = actives.top[ae];
            var aeBot = actives.bot[ae];
            if ((currentY == aeTop.y) || (aeTop.x == aeBot.x)) return aeTop.x;
            if (currentY == aeBot.y) return aeBot.x;
            return aeBot.x + (long)math.round(actives.dx[ae] * (currentY - aeBot.y));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsHorizontal(in Active ae)
        {
            return (ae.top.y == ae.bot.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsHorizontal(int ae)
        {
            return (actives.top[ae].y == actives.bot[ae].y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHeadingRightHorz(in Active ae)
        {
            return (double.IsNegativeInfinity(ae.dx));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHeadingLeftHorz(in Active ae)
        {
            return (double.IsPositiveInfinity(ae.dx));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapActives(ref Active ae1, ref Active ae2)
        {
            (ae2, ae1) = (ae1, ae2); //Active ae = ae1; //ae1 = ae2; //ae2 = ae;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapActives(ref int ae1, ref int ae2)
        {
            (ae2, ae1) = (ae1, ae2);// int temp = ae1; //ae1 = ae2; //ae2 = temp;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        PathType GetPolyType(int ae)
        {
            return minimaList[actives.localMin[ae]].polytype;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsSamePolyType(int ae1, int ae2)
        {
            return minimaList[actives.localMin[ae1]].polytype == minimaList[actives.localMin[ae2]].polytype;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetDx(ref Active ae)
        {
            ae.dx = GetDx(ae.bot, ae.top);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetDx(int ae)
        {
            actives.dx[ae] = GetDx(actives.bot[ae], actives.top[ae]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int NextVertex(int ae)
        {
            if (actives.windDx[ae] > 0)
                return vertexList.next[actives.vertexTop[ae]];
            return vertexList.prev[actives.vertexTop[ae]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int PrevPrevVertex(int ae)
        {
            if (actives.windDx[ae] > 0)
                return vertexList.prev[vertexList.prev[actives.vertexTop[ae]]];
            return vertexList.next[vertexList.next[actives.vertexTop[ae]]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsMaximaVertex(int vertexID)
        {
            return (vertexList.flags[vertexID] & VertexFlags.LocalMax) != VertexFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsMaxima(int ae)
        {
            return IsMaximaVertex(actives.vertexTop[ae]);
        }
        int GetMaximaPair(int ae)
        {
            int ae2;
            ae2 = actives.nextInAEL[ae];
            while (ae2 != -1)
            {
                if (actives.vertexTop[ae2] == actives.vertexTop[ae]) return ae2;  //Found!
                ae2 = actives.nextInAEL[ae2];
            }
            return -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCurrYMaximaVertex_Open(int ae)
        {
            int result = actives.vertexTop[ae];
            if (actives.windDx[ae] > 0)
                while (vertexList.pt[vertexList.next[result]].y == vertexList.pt[result].y &&
                  ((vertexList.flags[result] & (VertexFlags.OpenEnd |
                  VertexFlags.LocalMax)) == VertexFlags.None))

                    result = vertexList.next[result];
            else
                while (vertexList.pt[vertexList.prev[result]].y == vertexList.pt[result].y &&
                  ((vertexList.flags[result] & (VertexFlags.OpenEnd |
                  VertexFlags.LocalMax)) == VertexFlags.None))
                    result = vertexList.prev[result];
            if (!IsMaximaVertex(result)) result = -1; // not a maxima
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCurrYMaximaVertex(int ae)
        {
            int result = actives.vertexTop[ae];
            if (actives.windDx[ae] > 0)
                while (vertexList.pt[vertexList.next[result]].y == vertexList.pt[result].y) result = vertexList.next[result];
            else
                while (vertexList.pt[vertexList.prev[result]].y == vertexList.pt[result].y) result = vertexList.prev[result];
            if (!IsMaximaVertex(result)) result = -1; // not a maxima
            return result;
        }

        public struct IntersectListSort : IComparer<IntersectNode>
        {
            public int Compare(IntersectNode a, IntersectNode b)
            {
                if (a.pt.y == b.pt.y)
                {
                    if (a.pt.x == b.pt.x)
                        return 0;
                    return a.pt.x < b.pt.x ? -1 : 1;
                }
                return a.pt.y > b.pt.y ? -1 : 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetSides(int outrec, int startEdge, int endEdge)
        {
            outrecList.frontEdge[outrec] = startEdge;
            outrecList.backEdge[outrec] = endEdge;
        }
        void SwapOutrecs(int ae1, int ae2)
        {
            int or1 = actives.outrec[ae1]; // at least one edge has 
            int or2 = actives.outrec[ae2]; // an assigned outrec
            if (or1 == or2)
            {
                int ae = outrecList.frontEdge[or1];
                outrecList.frontEdge[or1] = outrecList.backEdge[or1];
                outrecList.backEdge[or1] = ae;
                return;
            }

            if (or1 != -1)
            {
                if (ae1 == outrecList.frontEdge[or1])
                    outrecList.frontEdge[or1] = ae2;
                else
                    outrecList.backEdge[or1] = ae2;
            }

            if (or2 != -1)
            {
                if (ae2 == outrecList.frontEdge[or2])
                    outrecList.frontEdge[or2] = ae1;
                else
                    outrecList.backEdge[or2] = ae1;
            }

            actives.outrec[ae1] = or2;
            actives.outrec[ae2] = or1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetOwner(int outrec, int newOwner)
        {
            //precondition1: new_owner is never null
            while (outrecList.owner[newOwner] != -1 && outrecList.pts[outrecList.owner[newOwner]] == -1)
                outrecList.owner[newOwner] = outrecList.owner[outrecList.owner[newOwner]];

            //make sure that outrec isn't an owner of newOwner
            int tmp = newOwner;
            while (tmp != -1 && tmp != outrec)
                tmp = outrecList.owner[tmp];
            if (tmp != -1)
                outrecList.owner[newOwner] = outrecList.owner[outrec];
            outrecList.owner[outrec] = newOwner;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double Area(int op)
        {
            // https://en.wikipedia.org/wiki/Shoelace_formula
            double area = 0.0;
            int op2 = op;
            do
            {
                var op2Pt = outPtList.pt[op2];
                var op2prevPt = outPtList.pt[outPtList.prev[op2]];

                area += (double)(op2prevPt.y + op2Pt.y) *
                    (op2prevPt.x - op2Pt.x);
                op2 = outPtList.next[op2];
            } while (op2 != op);

            return area * 0.5;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AreaTriangle(long2 pt1, long2 pt2, long2 pt3)
        {
            return (double)(pt3.y + pt1.y) * (pt3.x - pt1.x) +
              (double)(pt1.y + pt2.y) * (pt1.x - pt2.x) +
              (double)(pt2.y + pt3.y) * (pt2.x - pt3.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetRealOutRec(int outrec)
        {
            while ((outrec != -1) && (outrecList.pts[outrec] == -1))
                outrec = outrecList.owner[outrec];
            return outrec;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UncoupleOutRec(int ae)
        {
            int outrec = actives.outrec[ae];
            if (outrec == -1) return;
            actives.outrec[outrecList.frontEdge[outrec]] = -1;
            actives.outrec[outrecList.backEdge[outrec]] = -1;
            outrecList.frontEdge[outrec] = -1;
            outrecList.backEdge[outrec] = -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool OutrecIsAscending(int hotEdge)
        {
            return (hotEdge == outrecList.frontEdge[actives.outrec[hotEdge]]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapFrontBackSides(int outrec)
        {
            // while this proc. is needed for open paths
            // it's almost never needed for closed paths
            int ae2 = outrecList.frontEdge[outrec];
            outrecList.frontEdge[outrec] = outrecList.backEdge[outrec];
            outrecList.backEdge[outrec] = ae2;
            outrecList.pts[outrec] = outPtList.next[outrecList.pts[outrec]];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool EdgesAdjacentInAEL(IntersectNode inode)
        {
            return (actives.nextInAEL[inode.edge1] == inode.edge2) || (actives.prevInAEL[inode.edge1] == inode.edge2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ClearSolutionOnly()
        {
            if (actives.IsCreated)
            {
                actives.Clear();
                actives_ID = -1;
            }
            if (intersectList.IsCreated) intersectList.Clear();
            if (outrecList.IsCreated) outrecList.Clear();
            if (outPtList.IsCreated) outPtList.Clear();
            if (scanlineList.IsCreated) scanlineList.Clear();
            if (horzSegList.IsCreated) horzSegList.Clear();
            if (horzJoinList.IsCreated) horzJoinList.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ClearSolutionOnly();
            if (minimaList.IsCreated) minimaList.Clear();
            if (vertexList.IsCreated) vertexList.Clear();
            currentLocMin = 0;
            isSortedMinimaList = false;
            hasOpenPaths = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            if (!isSortedMinimaList)
            {
                minimaList.Sort(default(LocMinSorter));
                isSortedMinimaList = true;
            }

            scanlineList._stack.Capacity = minimaList.Length;
            for (int i = minimaList.Length - 1; i >= 0; i--)
                InsertScanline(minimaList[i].vertex.y);

            currentBotY = 0;
            currentLocMin = 0;
            if (actives.IsCreated)
            {
                actives.Clear();
                actives_ID = -1;
            }
            sel_ID = -1;
            _succeeded = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void InsertScanline(long y)
        {
            var index = scanlineList._stack.BinarySearch(y);
            if (index >= 0)
                return;
            scanlineList.Push(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool PopScanline(out long y)
        {
            if (scanlineList.IsEmpty)
            {
                y = 0;
                return false;
            }

            y = scanlineList.Pop();
            while (!scanlineList.IsEmpty && y == scanlineList.Peek())
                scanlineList.Pop();  // Pop duplicates.
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasLocMinAtY(long y)
        {
            return (currentLocMin < minimaList.Length && minimaList[currentLocMin].vertex.y == y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LocalMinima PopLocalMinima()
        {
            return minimaList[currentLocMin++];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddLocMin(int vert, PathType polytype, bool isOpen)
        {
            // make sure the vertex is added only once ...
            if ((vertexList.flags[vert] & VertexFlags.LocalMin) != VertexFlags.None) return;
            vertexList.flags[vert] |= VertexFlags.LocalMin;

            LocalMinima lm = new LocalMinima(vert, vertexList.pt[vert], polytype, isOpen);
            minimaList.Add(lm);
        }
        void EnsureVertexListCapacity(int additionalVertextCount)
        {
            int newSize = vertexList.pt.Length + additionalVertextCount;
            vertexList.pt.Capacity = newSize;
            vertexList.flags.Capacity = newSize;
            vertexList.prev.Capacity = newSize;
            vertexList.next.Capacity = newSize;
        }
        void AddPathsToVertexList(NativeArray<int2> nodes, NativeArray<int> startIDs, PathType polytype, bool isOpen)
        {
            for (int ComponentID = 0, pathCnt = startIDs.Length - 1; ComponentID < pathCnt; ComponentID++) //for each component of Poly
            {
                int start = startIDs[ComponentID];
                int end = startIDs[ComponentID + 1];
                AddPathToVertexList(nodes, start, end, polytype, isOpen);
            }
        }
        void AddPathToVertexList(NativeArray<int2> nodes, int start, int end, PathType polytype, bool isOpen)
        {
            int v0 = -1, prev_v = -1, curr_v;
            for (int i = start; i < end; i++)
            {
                var pt = new long2(nodes[i], _scale); //only needed when input data is float or double
                //var pt = new long2(nodes[i]);
                if (v0 == -1)
                {
                    v0 = vertexList.AddVertex(pt, VertexFlags.None, true);
                    prev_v = v0;
                }
                else if (vertexList.pt[prev_v] != pt) // ie skips duplicates
                    prev_v = vertexList.AddVertex(pt, VertexFlags.None, false, v0);
            }
            if (prev_v == -1 || vertexList.prev[prev_v] == -1) return;
            //the following eliminates the end point (identical with start) for closed polygons fropm the linked list
            if (!isOpen && vertexList.pt[prev_v] == vertexList.pt[v0]) prev_v = vertexList.prev[prev_v];
            vertexList.next[prev_v] = v0;
            vertexList.prev[v0] = prev_v;
            if (!isOpen && vertexList.next[prev_v] == prev_v) return;

            // OK, we have a valid path
            bool going_up, going_up0;
            if (isOpen)
            {
                curr_v = vertexList.next[v0];
                while (curr_v != v0 && vertexList.pt[curr_v].y == vertexList.pt[v0].y)
                    curr_v = vertexList.next[curr_v];
                going_up = vertexList.pt[curr_v].y <= vertexList.pt[v0].y;
                if (going_up)
                {
                    vertexList.flags[v0] = VertexFlags.OpenStart;
                    AddLocMin(v0, polytype, true);
                }
                else
                    vertexList.flags[v0] = VertexFlags.OpenStart | VertexFlags.LocalMax;
            }
            else // closed path
            {
                prev_v = vertexList.prev[v0];
                while (prev_v != v0 && vertexList.pt[prev_v].y == vertexList.pt[v0].y)
                    prev_v = vertexList.prev[prev_v];
                if (prev_v == v0)
                    return; // only open paths can be completely flat
                going_up = vertexList.pt[prev_v].y > vertexList.pt[v0].y;
            }

            going_up0 = going_up;
            prev_v = v0;
            curr_v = vertexList.next[v0];
            while (curr_v != v0)
            {
                if (vertexList.pt[curr_v].y > vertexList.pt[prev_v].y && going_up)
                {
                    vertexList.flags[prev_v] |= VertexFlags.LocalMax;
                    going_up = false;
                }
                else if (vertexList.pt[curr_v].y < vertexList.pt[prev_v].y && !going_up)
                {
                    going_up = true;
                    AddLocMin(prev_v, polytype, isOpen);
                }
                prev_v = curr_v;
                curr_v = vertexList.next[curr_v];
            }

            if (isOpen)
            {
                vertexList.flags[prev_v] |= VertexFlags.OpenEnd;
                if (going_up)
                    vertexList.flags[prev_v] |= VertexFlags.LocalMax;
                else
                    AddLocMin(prev_v, polytype, isOpen);
            }
            else if (going_up != going_up0)
            {
                if (going_up0) AddLocMin(prev_v, polytype, false);
                else vertexList.flags[prev_v] |= VertexFlags.LocalMax;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddSubject(ref PolygonInt paths)
        {
            AddPaths(ref paths, PathType.Subject);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddSubject(NativeArray<int2> nodes, NativeArray<int> startIDs)
        {
            AddPaths(nodes, startIDs, PathType.Subject);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddOpenSubject(ref PolygonInt paths)
        {
            AddPaths(ref paths, PathType.Subject, true);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddClip(ref PolygonInt paths)
        {
            AddPaths(ref paths, PathType.Clip);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddClip(NativeArray<int2> nodes, NativeArray<int> startIDs)
        {
            AddPaths(nodes, startIDs, PathType.Clip);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPath(NativeArray<int2> nodes, int start, int end, PathType polytype, bool isOpen = false)
        {
            hasOpenPaths = isOpen;
            isSortedMinimaList = false;
            EnsureVertexListCapacity(end - start);
            AddPathToVertexList(nodes, start, end, polytype, isOpen);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPaths(ref PolygonInt path, PathType polytype, bool isOpen = false)
        {
            if (isOpen) hasOpenPaths = true;
            isSortedMinimaList = false;
            EnsureVertexListCapacity(path.nodes.Length);
            AddPathsToVertexList(path.nodes.AsArray(), path.startIDs.AsArray(), polytype, isOpen);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPaths(NativeArray<int2> nodes, NativeArray<int> startIDs, PathType polytype, bool isOpen = false)
        {
            if (isOpen) hasOpenPaths = true;
            isSortedMinimaList = false;
            EnsureVertexListCapacity(nodes.Length);
            AddPathsToVertexList(nodes, startIDs, polytype, isOpen);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsContributingClosed(int ae)
        {
            switch (fillrule)
            {
                case FillRule.Positive:
                    if (actives.windCount[ae] != 1) return false;
                    break;
                case FillRule.Negative:
                    if (actives.windCount[ae] != -1) return false;
                    break;
                case FillRule.NonZero:
                    if (math.abs(actives.windCount[ae]) != 1) return false;
                    break;
            }
            switch (cliptype)
            {
                case ClipType.Intersection:
                    return fillrule switch
                    {
                        FillRule.Positive => actives.windCount2[ae] > 0,
                        FillRule.Negative => actives.windCount2[ae] < 0,
                        _ => actives.windCount2[ae] != 0,
                    };

                case ClipType.Union:
                    return fillrule switch
                    {
                        FillRule.Positive => actives.windCount2[ae] <= 0,
                        FillRule.Negative => actives.windCount2[ae] >= 0,
                        _ => actives.windCount2[ae] == 0,
                    };

                case ClipType.Difference:
                    bool result = fillrule switch
                    {
                        FillRule.Positive => actives.windCount2[ae] <= 0,
                        FillRule.Negative => actives.windCount2[ae] >= 0,
                        _ => actives.windCount2[ae] == 0,
                    };
                    return (GetPolyType(ae) == PathType.Subject) ? result : !result;

                case ClipType.Xor:
                    return true; // XOr is always contributing unless open

                default:
                    return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsContributingOpen(int ae)
        {
            bool isInClip, isInSubj;
            switch (fillrule)
            {
                case FillRule.Positive:
                    isInSubj = actives.windCount[ae] > 0;
                    isInClip = actives.windCount2[ae] > 0;
                    break;
                case FillRule.Negative:
                    isInSubj = actives.windCount[ae] < 0;
                    isInClip = actives.windCount2[ae] < 0;
                    break;
                default:
                    isInSubj = actives.windCount[ae] != 0;
                    isInClip = actives.windCount2[ae] != 0;
                    break;
            }

            bool result = cliptype switch
            {
                ClipType.Intersection => isInClip,
                ClipType.Union => !isInSubj && !isInClip,
                _ => !isInClip
            };
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetWindCountForClosedPathEdge(int ae)
        {
            // Wind counts refer to polygon regions not edges, so here an edge's WindCnt
            // indicates the higher of the wind counts for the two regions touching the
            // edge. (nb: Adjacent regions can only ever have their wind counts differ by
            // one. Also, open paths have no meaningful wind directions or counts.)

            int ae2 = actives.prevInAEL[ae];
            // find the nearest closed path edge of the same PolyType in AEL (heading left)
            PathType pt = GetPolyType(ae);
            while (ae2 != -1 && (GetPolyType(ae2) != pt || IsOpen(ae2))) ae2 = actives.prevInAEL[ae2];

            if (ae2 == -1)
            {
                actives.windCount[ae] = actives.windDx[ae];
                ae2 = actives_ID;
            }
            else if (fillrule == FillRule.EvenOdd)
            {
                actives.windCount[ae] = actives.windDx[ae];
                actives.windCount2[ae] = actives.windCount2[ae2];
                ae2 = actives.nextInAEL[ae2];
            }
            else
            {
                // NonZero, positive, or negative filling here ...
                // when e2's WindCnt is in the SAME direction as its WindDx,
                // then polygon will fill on the right of 'e2' (and 'e' will be inside)
                // nb: neither e2.WindCnt nor e2.WindDx should ever be 0.
                if (actives.windCount[ae2] * actives.windDx[ae2] < 0)
                {
                    // opposite directions so 'ae' is outside 'ae2' ...
                    if (math.abs(actives.windCount[ae2]) > 1)
                    {
                        // outside prev poly but still inside another.
                        if (actives.windDx[ae2] * actives.windDx[ae] < 0)
                            // reversing direction so use the same WC
                            actives.windCount[ae] = actives.windCount[ae2];
                        else
                            // otherwise keep 'reducing' the WC by 1 (ie towards 0)
                            actives.windCount[ae] = actives.windCount[ae2] + actives.windDx[ae];
                    }
                    else
                        // now outside all polys of same polytype so set own WC ...
                        actives.windCount[ae] = (IsOpen(ae) ? 1 : actives.windDx[ae]);
                }
                else
                {
                    // 'ae' must be inside 'ae2'
                    if (actives.windDx[ae2] * actives.windDx[ae] < 0)
                        //reversing direction so use the same WC
                        actives.windCount[ae] = actives.windCount[ae2];
                    else
                        // otherwise keep 'increasing' the WC by 1 (ie away from 0) ...
                        actives.windCount[ae] = actives.windCount[ae2] + actives.windDx[ae];
                }

                actives.windCount2[ae] = actives.windCount2[ae2];
                ae2 = actives.nextInAEL[ae2];  //ie get ready to calc WindCnt2
            }

            // update wind_cnt2 ...
            if (fillrule == FillRule.EvenOdd)
                while (ae2 != ae)
                {
                    if (GetPolyType(ae2) != pt && !IsOpen(ae2))
                        actives.windCount2[ae] = (actives.windCount2[ae] == 0 ? 1 : 0);
                    ae2 = actives.nextInAEL[ae2];
                }
            else
                while (ae2 != ae)
                {
                    if (GetPolyType(ae2) != pt && !IsOpen(ae2))
                        actives.windCount2[ae] += actives.windDx[ae2];
                    ae2 = actives.nextInAEL[ae2];
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetWindCountForOpenPathEdge(int ae)
        {
            int ae2 = actives_ID;
            if (fillrule == FillRule.EvenOdd)
            {
                int cnt1 = 0, cnt2 = 0;
                while (ae2 != ae)
                {
                    if (GetPolyType(ae2) == PathType.Clip)
                        cnt2++;
                    else if (!IsOpen(ae2))
                        cnt1++;
                    ae2 = actives.nextInAEL[ae2];
                }

                actives.windCount[ae] = (IsOdd(cnt1) ? 1 : 0);
                actives.windCount2[ae] = (IsOdd(cnt2) ? 1 : 0);
            }
            else
            {
                while (ae2 != ae)
                {
                    if (GetPolyType(ae2) == PathType.Clip)
                        actives.windCount2[ae] += actives.windDx[ae2];
                    else if (!IsOpen(ae2))
                        actives.windCount[ae] += actives.windDx[ae2];
                    ae2 = actives.nextInAEL[ae2];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidAelOrder(int resident, int newcomer)
        {
            var residentCurX = actives.curX[resident];
            var newcomerCurX = actives.curX[newcomer];
            if (newcomerCurX != residentCurX)
                return newcomerCurX > residentCurX;

            var residentTop = actives.top[resident];
            var residentBot = actives.bot[resident];
            var newcomerTop = actives.top[newcomer];
            var newcomerBot = actives.bot[newcomer];

            // get the turning direction  resident.top, newcomer.bot, newcomer.top
            double d = InternalClipper.CrossProduct(residentTop, newcomerBot, newcomerTop);
            if (d != 0) return d < 0;

            // edges must be collinear to get here

            // for starting open paths, place them according to
            // the direction they're about to turn
            if (!IsMaxima(resident) && (residentTop.y > newcomerTop.y))
            {
                return InternalClipper.CrossProduct(newcomerBot,
                    residentTop, vertexList.pt[NextVertex(resident)]) <= 0;
            }

            if (!IsMaxima(newcomer) && (newcomerTop.y > residentTop.y))
            {
                return InternalClipper.CrossProduct(newcomerBot,
                    newcomerTop, vertexList.pt[NextVertex(newcomer)]) >= 0;
            }

            long y = newcomerBot.y;
            bool newcomerIsLeft = actives.isLeftBound[newcomer];

            if (residentBot.y != y || minimaList[actives.localMin[resident]].vertex.y != y)
                return newcomerIsLeft;
            // resident must also have just been inserted
            if (actives.isLeftBound[resident] != newcomerIsLeft)
                return newcomerIsLeft;
            if (InternalClipper.CrossProduct(vertexList.pt[PrevPrevVertex(resident)],
                residentBot, residentTop) == 0) return true;
            // compare turning direction of the alternate bound
            return (InternalClipper.CrossProduct(vertexList.pt[PrevPrevVertex(resident)],
                newcomerBot, vertexList.pt[PrevPrevVertex(newcomer)]) > 0) == newcomerIsLeft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int InsertLeftEdge(in Active ae)
        {
            int ae2_ID;
            int ae_ID = actives.AddActive(ae);
            if (actives_ID == -1)
            {
                actives.prevInAEL[ae_ID] = -1;
                actives.nextInAEL[ae_ID] = -1;
                actives_ID = ae_ID;
            }
            else if (!IsValidAelOrder(actives_ID, ae_ID))
            {
                actives.prevInAEL[ae_ID] = -1;
                actives.nextInAEL[ae_ID] = actives_ID;
                actives.prevInAEL[actives_ID] = ae_ID;
                actives_ID = ae_ID;
            }
            else
            {
                ae2_ID = actives_ID;
                while (actives.nextInAEL[ae2_ID] != -1 && IsValidAelOrder(actives.nextInAEL[ae2_ID], ae_ID))
                    ae2_ID = actives.nextInAEL[ae2_ID];
                actives.nextInAEL[ae_ID] = actives.nextInAEL[ae2_ID];
                if (actives.nextInAEL[ae2_ID] != -1) actives.prevInAEL[actives.nextInAEL[ae2_ID]] = ae_ID;
                actives.prevInAEL[ae_ID] = ae2_ID;
                actives.nextInAEL[ae2_ID] = ae_ID;
            }
            return ae_ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int InsertRightEdge(int ae, in Active ae2)
        {
            int ae2_ID = actives.AddActive(ae2);

            actives.nextInAEL[ae2_ID] = actives.nextInAEL[ae];
            if (actives.nextInAEL[ae] != -1) actives.prevInAEL[actives.nextInAEL[ae]] = ae2_ID;
            actives.prevInAEL[ae2_ID] = ae;
            actives.nextInAEL[ae] = ae2_ID;

            return ae2_ID;
        }

        void InsertLocalMinimaIntoAEL(long botY)
        {
            LocalMinima localMinima;
            Active leftBound = default, rightBound = default;
            bool leftBoundExists = true, rightBoundExists = true;
            // Add any local minima (if any) at BotY ...
            // NB horizontal local minima edges should contain locMin.vertex.prev
            while (HasLocMinAtY(botY))
            {
                var localMinima_ID = currentLocMin;
                localMinima = PopLocalMinima();
                if ((vertexList.flags[localMinima.vertex_ID] & VertexFlags.OpenStart) != VertexFlags.None)
                    leftBoundExists = false;
                else
                {
                    leftBound = new Active
                    {
                        bot = localMinima.vertex,
                        curX = localMinima.vertex.x,
                        windDx = -1,
                        vertexTop = vertexList.prev[localMinima.vertex_ID],
                        top = vertexList.pt[vertexList.prev[localMinima.vertex_ID]],
                        outrec = -1,
                        locMin_ID = localMinima_ID
                    };
                    SetDx(ref leftBound);
                }

                if ((vertexList.flags[localMinima.vertex_ID] & VertexFlags.OpenEnd) != VertexFlags.None)
                    rightBoundExists = false;
                else
                {
                    rightBound = new Active
                    {
                        bot = localMinima.vertex,
                        curX = localMinima.vertex.x,
                        windDx = 1,
                        vertexTop = vertexList.next[localMinima.vertex_ID], // i.e. ascending
                        top = vertexList.pt[vertexList.next[localMinima.vertex_ID]],
                        outrec = -1,
                        locMin_ID = localMinima_ID
                    };
                    SetDx(ref rightBound);
                }

                // Currently LeftB is just the descending bound and RightB is the ascending.
                // Now if the LeftB isn't on the left of RightB then we need swap them.
                if (leftBoundExists && rightBoundExists)
                {
                    if (IsHorizontal(leftBound))
                    {
                        if (IsHeadingRightHorz(leftBound)) SwapActives(ref leftBound, ref rightBound);
                    }
                    else if (IsHorizontal(rightBound))
                    {
                        if (IsHeadingLeftHorz(rightBound)) SwapActives(ref leftBound, ref rightBound);
                    }
                    else if (leftBound.dx < rightBound.dx)
                        SwapActives(ref leftBound, ref rightBound);
                    // so when leftBound has windDx == 1, the polygon will be oriented
                    // counter-clockwise in Cartesian coords (clockwise with inverted Y).
                }
                else if (!leftBoundExists)
                {
                    leftBound = rightBound;
                    rightBound = default;
                    rightBoundExists = false;
                }

                bool contributing;
                leftBound.isleftBound = true;
                var leftBound_ID = InsertLeftEdge(leftBound);

                if (IsOpen(leftBound_ID))
                {
                    SetWindCountForOpenPathEdge(leftBound_ID);
                    contributing = IsContributingOpen(leftBound_ID);
                }
                else
                {
                    SetWindCountForClosedPathEdge(leftBound_ID);
                    contributing = IsContributingClosed(leftBound_ID);
                }

                if (rightBoundExists)
                {
                    rightBound.windCount = actives.windCount[leftBound_ID];
                    rightBound.windCount2 = actives.windCount2[leftBound_ID];
                    int rightBound_ID = InsertRightEdge(leftBound_ID, rightBound);
                    //Debug.Log($"Rightbound: {actives.curX[rightBound_ID]} Outrec: {actives.outrec[rightBound_ID]}");
                    if (contributing)
                    {
                        AddLocalMinPoly(leftBound_ID, rightBound_ID, actives.bot[leftBound_ID], true);
                        if (!IsHorizontal(leftBound_ID))
                            CheckJoinLeft(leftBound_ID, leftBound.bot);
                    }

                    while (actives.nextInAEL[rightBound_ID] != -1 &&
                            IsValidAelOrder(actives.nextInAEL[rightBound_ID], rightBound_ID))
                    {
                        IntersectEdges(rightBound_ID, actives.nextInAEL[rightBound_ID], actives.bot[rightBound_ID]);
                        SwapPositionsInAEL(rightBound_ID, actives.nextInAEL[rightBound_ID]);
                    }

                    if (IsHorizontal(rightBound_ID))
                        PushHorz(rightBound_ID);
                    else
                    {
                        CheckJoinRight(rightBound_ID, rightBound.bot);
                        InsertScanline(actives.top[rightBound_ID].y);
                    }
                }
                else if (contributing)
                    StartOpenPath(leftBound_ID, actives.bot[leftBound_ID]);

                if (IsHorizontal(leftBound_ID))
                    PushHorz(leftBound_ID);
                else
                    InsertScanline(leftBound.top.y);
            } // while (HasLocMinAtY())
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushHorz(int ae)
        {
            actives.nextInSEL[ae] = sel_ID;
            sel_ID = ae;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PopHorz(out int ae)
        {
            ae = sel_ID;
            if (sel_ID == -1) return false;
            sel_ID = actives.nextInSEL[sel_ID];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int AddLocalMinPoly(int ae1, int ae2, long2 pt, bool isNew = false)
        {
            int outrec = outrecList.AddOutRec(-1, true, -1);
            outrecList.polypath[outrec] = -1;

            actives.outrec[ae1] = outrec;
            actives.outrec[ae2] = outrec;

            if (IsOpen(ae1))
            {
                //outrecList.owner[outrec] = -1; //redudant because already set like this when adding
                //outrecList.isOpen[outrec] = true; //redudant because already set like this when adding
                if (actives.windDx[ae1] > 0)
                    SetSides(outrec, ae1, ae2);
                else
                    SetSides(outrec, ae2, ae1);
            }
            else
            {
                outrecList.isOpen[outrec] = false;
                int prevHotEdge = GetPrevHotEdge(ae1);
                // e.windDx is the winding direction of the **input** paths
                // and unrelated to the winding direction of output polygons.
                // Output orientation is determined by e.outrec.frontE which is
                // the ascending edge (see AddLocalMinPoly).
                if (prevHotEdge != -1)
                {
                    if (_using_polytree)
                        SetOwner(outrec, actives.outrec[prevHotEdge]);
                    outrecList.owner[outrec] = actives.outrec[prevHotEdge];
                    if (OutrecIsAscending(prevHotEdge) == isNew)
                        SetSides(outrec, ae2, ae1);
                    else
                        SetSides(outrec, ae1, ae2);
                }
                else
                {
                    //outrecList.owner[outrec] = -1; //redudant because already set like this when adding
                    if (isNew)
                        SetSides(outrec, ae1, ae2);
                    else
                        SetSides(outrec, ae2, ae1);
                }
            }

            int op = outPtList.NewOutPt(pt, outrec);
            outrecList.pts[outrec] = op;
            return op;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddLocalMaxPoly(int ae1, int ae2, long2 pt)
        {
            if (IsJoined(ae1)) Split(ae1, pt);
            if (IsJoined(ae2)) Split(ae2, pt);

            if (IsFront(ae1) == IsFront(ae2))
            {
                if (IsOpenEnd(ae1))
                    SwapFrontBackSides(actives.outrec[ae1]);
                else if (IsOpenEnd(ae2))
                    SwapFrontBackSides(actives.outrec[ae2]);
                else
                {
                    _succeeded = false;
                    return -1;
                }
            }

            int result = AddOutPt(ae1, pt);
            if (actives.outrec[ae1] == actives.outrec[ae2])
            {
                var outrec = actives.outrec[ae1];
                outrecList.pts[outrec] = result;

                if (_using_polytree)
                {
                    int e = GetPrevHotEdge(ae1);
                    if (e == -1)
                        outrecList.owner[outrec] = -1;
                    else
                        SetOwner(outrec, actives.outrec[e]);
                    // nb: outRec.owner here is likely NOT the real
                    // owner but this will be fixed in DeepCheckOwner()
                }
                UncoupleOutRec(ae1);
            }
            // and to preserve the winding orientation of outrec ...
            else if (IsOpen(ae1))
            {
                if (actives.windDx[ae1] < 0)
                    JoinOutrecPaths(ae1, ae2);
                else
                    JoinOutrecPaths(ae2, ae1);
            }
            else if (actives.outrec[ae1] < actives.outrec[ae2])
                JoinOutrecPaths(ae1, ae2);
            else
                JoinOutrecPaths(ae2, ae1);

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JoinOutrecPaths(int ae1, int ae2)
        {
            // join ae2 outrec path onto ae1 outrec path and then delete ae2 outrec path
            // pointers. (NB Only very rarely do the joining ends share the same coords.)
            var ae1outrec = actives.outrec[ae1];
            var ae2outrec = actives.outrec[ae2];
            var p1Start = outrecList.pts[ae1outrec];
            var p2Start = outrecList.pts[ae2outrec];
            var p1End = outPtList.next[p1Start];
            var p2End = outPtList.next[p2Start];
            if (IsFront(ae1))
            {
                outPtList.prev[p2End] = p1Start;
                outPtList.next[p1Start] = p2End;
                outPtList.next[p2Start] = p1End;
                outPtList.prev[p1End] = p2Start;
                outrecList.pts[ae1outrec] = p2Start;
                // nb: if IsOpen(e1) then e1 & e2 must be a 'maximaPair'
                outrecList.frontEdge[ae1outrec] = outrecList.frontEdge[ae2outrec];
                if (outrecList.frontEdge[ae1outrec] != -1)
                    actives.outrec[outrecList.frontEdge[ae1outrec]] = ae1outrec;
            }
            else
            {
                outPtList.prev[p1End] = p2Start;
                outPtList.next[p2Start] = p1End;
                outPtList.next[p1Start] = p2End;
                outPtList.prev[p2End] = p1Start;

                outrecList.backEdge[ae1outrec] = outrecList.backEdge[ae2outrec];
                if (outrecList.backEdge[ae1outrec] != -1)
                    actives.outrec[outrecList.backEdge[ae1outrec]] = ae1outrec;
            }

            // after joining, the ae2.OutRec must contains no vertices ...
            outrecList.frontEdge[ae2outrec] = -1;
            outrecList.backEdge[ae2outrec] = -1;
            outrecList.pts[ae2outrec] = -1;
            SetOwner(actives.outrec[ae2], actives.outrec[ae1]);

            if (IsOpenEnd(ae1))
            {
                outrecList.pts[ae2outrec] = outrecList.pts[ae1outrec];
                outrecList.pts[ae1outrec] = -1;
            }

            // and ae1 and ae2 are maxima and are about to be dropped from the Actives list.
            actives.outrec[ae1] = -1;
            actives.outrec[ae2] = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddOutPt(int ae, long2 pt)
        {
            // Outrec.OutPts: a circular doubly-linked-list of POutPt where ...
            // opFront[.Prev]* ~~~> opBack & opBack == opFront.Next
            var outrec = actives.outrec[ae];
            bool toFront = IsFront(ae);
            var opFront = outrecList.pts[outrec];
            var opBack = outPtList.next[opFront];

            if (toFront && (pt == outPtList.pt[opFront])) return opFront;
            else if (!toFront && (pt == outPtList.pt[opBack])) return opBack;

            int newOp = outPtList.NewOutPt(pt, outrec);
            outPtList.prev[opBack] = newOp;
            outPtList.prev[newOp] = opFront;
            outPtList.next[newOp] = opBack;
            outPtList.next[opFront] = newOp;
            if (toFront) outrecList.pts[outrec] = newOp;

            return newOp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int StartOpenPath(int ae, long2 pt)
        {
            int outrec = outrecList.AddOutRec(-1, true, -1);
            if (actives.windDx[ae] > 0)
            {
                outrecList.frontEdge[outrec] = ae;
                //outrecList.backEdge[outrec] = -1; //redudant because already set like this when adding
            }
            else
            {
                //outrecList.frontEdge[outrec] = -1; //redudant because already set like this when adding
                outrecList.backEdge[outrec] = ae;
            }

            actives.outrec[ae] = outrec;

            int op = outPtList.NewOutPt(pt, outrec);
            outrecList.pts[outrec] = op;
            return op;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateEdgeIntoAEL(int ae)
        {
            actives.bot[ae] = actives.top[ae];
            actives.vertexTop[ae] = NextVertex(ae);
            actives.top[ae] = vertexList.pt[actives.vertexTop[ae]];
            actives.curX[ae] = actives.bot[ae].x;
            SetDx(ae);

            if (IsJoined(ae)) Split(ae, actives.bot[ae]);

            if (IsHorizontal(ae)) return;
            InsertScanline(actives.top[ae].y);

            CheckJoinLeft(ae, actives.bot[ae]);
            CheckJoinRight(ae, actives.bot[ae], true); // (#500
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEdgeWithMatchingLocMin(int e)
        {
            int result = actives.nextInAEL[e];
            while (result != -1)
            {
                //if (minimaList[actives.localMin[result]] == minimaList[actives.localMin[e]]) return result; //CHECK: original checks ReferenceEquals (not by value) 
                if (actives.localMin[result] == actives.localMin[e]) return result;
                else if (!IsHorizontal(result) && actives.bot[e] != actives.bot[result]) result = -1;
                else result = actives.nextInAEL[result];
            }
            result = actives.prevInAEL[e];
            while (result != -1)
            {
                //if (minimaList[actives.localMin[result]] == minimaList[actives.localMin[e]]) return result; //CHECK: original checks ReferenceEquals (not by value) 
                if (actives.localMin[result] == actives.localMin[e]) return result;
                else if (!IsHorizontal(result) && actives.bot[e] != actives.bot[result]) return -1;
                else result = actives.prevInAEL[result];
            }
            return result;
        }

        private int IntersectEdges(int ae1, int ae2, long2 pt)
        {
            int resultOp = -1;
            var ae1IsOpen = IsOpen(ae1);
            var ae2IsOpen = IsOpen(ae2);
            // MANAGE OPEN PATH INTERSECTIONS SEPARATELY ...
            if (hasOpenPaths && (ae1IsOpen || ae2IsOpen))
            {
                if (ae1IsOpen && ae2IsOpen) return -1;
                // the following line avoids duplicating quite a bit of code
                if (ae2IsOpen) SwapActives(ref ae1, ref ae2);
                if (IsJoined(ae2)) Split(ae2, pt); // needed for safety

                if (cliptype == ClipType.Union)
                {
                    if (!IsHotEdge(ae2)) return -1;
                }
                else if (minimaList[actives.localMin[ae2]].polytype == PathType.Subject)
                    return -1;

                switch (fillrule)
                {
                    case FillRule.Positive:
                        if (actives.windCount[ae2] != 1) return -1; break;
                    case FillRule.Negative:
                        if (actives.windCount[ae2] != -1) return -1; break;
                    default:
                        if (math.abs(actives.windCount[ae2]) != 1) return -1; break;
                }


                // toggle contribution ...
                if (IsHotEdge(ae1))
                {
                    resultOp = AddOutPt(ae1, pt);
                    if (IsFront(ae1))
                        outrecList.frontEdge[actives.outrec[ae1]] = -1;
                    else
                        outrecList.backEdge[actives.outrec[ae1]] = -1;
                    actives.outrec[ae1] = -1;
                }

                // horizontal edges can pass under open paths at a LocMins
                else if (pt == minimaList[actives.localMin[ae1]].vertex &&
                  !IsOpenEndVertex(minimaList[actives.localMin[ae1]].vertex_ID))
                {
                    // find the other side of the LocMin and
                    // if it's 'hot' join up with it ...
                    int ae3 = FindEdgeWithMatchingLocMin(ae1);
                    if (ae3 != -1 && IsHotEdge(ae3))
                    {
                        actives.outrec[ae1] = actives.outrec[ae3];
                        if (actives.windDx[ae1] > 0)
                            SetSides(actives.outrec[ae3], ae1, ae3);
                        else
                            SetSides(actives.outrec[ae3], ae3, ae1);
                        return outrecList.pts[actives.outrec[ae3]];
                    }

                    resultOp = StartOpenPath(ae1, pt);
                }
                else
                    resultOp = StartOpenPath(ae1, pt);

                return resultOp;
            }

            // MANAGING CLOSED PATHS FROM HERE ON
            if (IsJoined(ae1)) Split(ae1, pt);
            if (IsJoined(ae2)) Split(ae2, pt);

            //UPDATE WINDING COUNTS...

            int oldE1WindCount, oldE2WindCount;
            var ae1PolyType = minimaList[actives.localMin[ae1]].polytype;
            var ae2PolyType = minimaList[actives.localMin[ae2]].polytype;
            if (ae1PolyType == ae2PolyType)
            {
                if (fillrule == FillRule.EvenOdd)
                {
                    oldE1WindCount = actives.windCount[ae1];
                    actives.windCount[ae1] = actives.windCount[ae2];
                    actives.windCount[ae2] = oldE1WindCount;
                }
                else
                {
                    if (actives.windCount[ae1] + actives.windDx[ae2] == 0)
                        actives.windCount[ae1] = -actives.windCount[ae1];
                    else
                        actives.windCount[ae1] += actives.windDx[ae2];
                    if (actives.windCount[ae2] - actives.windDx[ae1] == 0)
                        actives.windCount[ae2] = -actives.windCount[ae2];
                    else
                        actives.windCount[ae2] -= actives.windDx[ae1];
                }
            }
            else
            {
                if (fillrule != FillRule.EvenOdd)
                    actives.windCount2[ae1] += actives.windDx[ae2];
                else
                    actives.windCount2[ae1] = (actives.windCount2[ae1] == 0 ? 1 : 0);
                if (fillrule != FillRule.EvenOdd)
                    actives.windCount2[ae2] -= actives.windDx[ae1];
                else
                    actives.windCount2[ae2] = (actives.windCount2[ae2] == 0 ? 1 : 0);
            }

            switch (fillrule)
            {
                case FillRule.Positive:
                    oldE1WindCount = actives.windCount[ae1];
                    oldE2WindCount = actives.windCount[ae2];
                    break;
                case FillRule.Negative:
                    oldE1WindCount = -actives.windCount[ae1];
                    oldE2WindCount = -actives.windCount[ae2];
                    break;
                default:
                    oldE1WindCount = math.abs(actives.windCount[ae1]);
                    oldE2WindCount = math.abs(actives.windCount[ae2]);
                    break;
            }

            bool e1WindCountIs0or1 = oldE1WindCount == 0 || oldE1WindCount == 1;
            bool e2WindCountIs0or1 = oldE2WindCount == 0 || oldE2WindCount == 1;

            if ((!IsHotEdge(ae1) && !e1WindCountIs0or1) || (!IsHotEdge(ae2) && !e2WindCountIs0or1)) return -1;

            // NOW PROCESS THE INTERSECTION ...

            // if both edges are 'hot' ...
            if (IsHotEdge(ae1) && IsHotEdge(ae2))
            {
                if ((oldE1WindCount != 0 && oldE1WindCount != 1) || (oldE2WindCount != 0 && oldE2WindCount != 1) ||
                    (ae1PolyType != ae2PolyType && cliptype != ClipType.Xor))
                {
                    resultOp = AddLocalMaxPoly(ae1, ae2, pt);
                }
                else if (IsFront(ae1) || (actives.outrec[ae1] == actives.outrec[ae2]))
                {
                    // this 'else if' condition isn't strictly needed but
                    // it's sensible to split polygons that ony touch at
                    // a common vertex (not at common edges).
                    resultOp = AddLocalMaxPoly(ae1, ae2, pt);
                    var op2 = AddLocalMinPoly(ae1, ae2, pt);
                }
                else
                {
                    // can't treat as maxima & minima
                    resultOp = AddOutPt(ae1, pt);
                    AddOutPt(ae2, pt);
                    SwapOutrecs(ae1, ae2);
                }
            }

            // if one or other edge is 'hot' ...
            else if (IsHotEdge(ae1))
            {
                resultOp = AddOutPt(ae1, pt);
                SwapOutrecs(ae1, ae2);
            }
            else if (IsHotEdge(ae2))
            {
                resultOp = AddOutPt(ae2, pt);
                SwapOutrecs(ae1, ae2);
            }
            // neither edge is 'hot'
            else
            {
                long e1Wc2, e2Wc2;
                switch (fillrule)
                {
                    case FillRule.Positive:
                        e1Wc2 = actives.windCount2[ae1];
                        e2Wc2 = actives.windCount2[ae2];
                        break;
                    case FillRule.Negative:
                        e1Wc2 = -actives.windCount2[ae1];
                        e2Wc2 = -actives.windCount2[ae2];
                        break;
                    default:
                        e1Wc2 = math.abs(actives.windCount2[ae1]);
                        e2Wc2 = math.abs(actives.windCount2[ae2]);
                        break;
                }

                if (!IsSamePolyType(ae1, ae2))
                {
                    resultOp = AddLocalMinPoly(ae1, ae2, pt);
                }
                else if (oldE1WindCount == 1 && oldE2WindCount == 1)
                {
                    resultOp = -1;
                    switch (cliptype)
                    {
                        case ClipType.Union:
                            if (e1Wc2 > 0 && e2Wc2 > 0) return -1;
                            resultOp = AddLocalMinPoly(ae1, ae2, pt);
                            break;

                        case ClipType.Difference:
                            if (((GetPolyType(ae1) == PathType.Clip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((GetPolyType(ae1) == PathType.Subject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                            {
                                resultOp = AddLocalMinPoly(ae1, ae2, pt);
                            }

                            break;

                        case ClipType.Xor:
                            resultOp = AddLocalMinPoly(ae1, ae2, pt);
                            break;

                        default: //ClipType.Intersection:
                            if (e1Wc2 <= 0 || e2Wc2 <= 0) return -1;
                            resultOp = AddLocalMinPoly(ae1, ae2, pt);
                            break;
                    }
                }
            }

            return resultOp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeleteFromAEL(int ae)
        {
            var prev = actives.prevInAEL[ae];
            var next = actives.nextInAEL[ae];
            if (prev == -1 && next == -1 && (ae != actives_ID)) return; // already deleted
            if (prev != -1)
                actives.nextInAEL[prev] = next;
            else
                actives_ID = next;
            if (next != -1) actives.prevInAEL[next] = prev;
            // delete &ae;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdjustCurrXAndCopyToSEL(long topY)
        {
            var ae = actives_ID;
            sel_ID = ae;
            while (ae != -1)
            {
                actives.prevInSEL[ae] = actives.prevInAEL[ae];
                actives.nextInSEL[ae] = actives.nextInAEL[ae];
                actives.jump[ae] = actives.nextInSEL[ae];
                if (actives.joinWith[ae] == JoinWith.Left)
                    actives.curX[ae] = actives.curX[actives.prevInAEL[ae]]; // this also avoids complications
                else
                    actives.curX[ae] = TopX(ae, topY);
                // NB don't update ae.curr.y yet (see AddNewIntersectNode)
                ae = actives.nextInAEL[ae];
            }
        }

        void ExecuteInternal(ClipType ct, FillRule fillRule)
        {
            //Debug.Log($"Vertices: {vertexList.pt.Length}");
            if (ct == ClipType.None) return;
            fillrule = fillRule;
            cliptype = ct;
            Reset();
            if (!PopScanline(out long y)) return;
            while (_succeeded)
            {
                InsertLocalMinimaIntoAEL(y);
                int ae;
                while (PopHorz(out ae)) DoHorizontal(ae);
                if (horzSegList.Length > 0)
                {
                    ConvertHorzSegsToJoins();
                    horzSegList.Clear();
                }
                currentBotY = y; // bottom of scanbeam
                if (!PopScanline(out y))
                    break; // y new top of scanbeam
                DoIntersections(y);
                DoTopOfScanbeam(y);
                while (PopHorz(out ae)) DoHorizontal(ae);
            }

            if (_succeeded) ProcessHorzJoins();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoIntersections(long topY)
        {
            if (BuildIntersectList(topY))
            {
                ProcessIntersectList();
                intersectList.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNewIntersectNode(int ae1, int ae2, long topY)
        {
            if (!InternalClipper.GetIntersectPt(
              actives.bot[ae1], actives.top[ae1], actives.bot[ae2], actives.top[ae2], out long2 ip))
                ip = new long2(actives.curX[ae1], topY);

            if (ip.y > currentBotY || ip.y < topY)
            {
                double absDx1 = math.abs(actives.dx[ae1]);
                double absDx2 = math.abs(actives.dx[ae2]);
                if (absDx1 > 100 && absDx2 > 100)
                {
                    if (absDx1 > absDx2)
                        ip = InternalClipper.GetClosestPtOnSegment(ip, actives.bot[ae1], actives.top[ae1]);
                    else
                        ip = InternalClipper.GetClosestPtOnSegment(ip, actives.bot[ae2], actives.top[ae2]);
                }
                else if (absDx1 > 100)
                    ip = InternalClipper.GetClosestPtOnSegment(ip, actives.bot[ae1], actives.top[ae1]);
                else if (absDx2 > 100)
                    ip = InternalClipper.GetClosestPtOnSegment(ip, actives.bot[ae2], actives.top[ae2]);
                else
                {
                    if (ip.y < topY) ip.y = topY;
                    else ip.y = currentBotY;
                    if (absDx1 < absDx2) ip.x = TopX(ae1, ip.y);
                    else ip.x = TopX(ae2, ip.y);
                }
            }
            IntersectNode node = new IntersectNode(ip, ae1, ae2);
            intersectList.Add(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ExtractFromSEL(int ae)
        {
            int aePrevInSEL = actives.prevInSEL[ae];
            int res = actives.nextInSEL[ae];
            if (res != -1)
                actives.prevInSEL[res] = aePrevInSEL;
            actives.nextInSEL[aePrevInSEL] = res;
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Insert1Before2InSEL(int ae1, int ae2)
        {
            actives.prevInSEL[ae1] = actives.prevInSEL[ae2];
            if (actives.prevInSEL[ae1] != -1)
                actives.nextInSEL[actives.prevInSEL[ae1]] = ae1;
            actives.nextInSEL[ae1] = ae2;
            actives.prevInSEL[ae2] = ae1;
        }

        private bool BuildIntersectList(long topY)
        {
            if (actives_ID == -1 || actives.nextInAEL[actives_ID] == -1) return false;

            // Calculate edge positions at the top of the current scanbeam, and from this
            // we will determine the intersections required to reach these new positions.
            AdjustCurrXAndCopyToSEL(topY);

            // Find all edge intersections in the current scanbeam using a stable merge
            // sort that ensures only adjacent edges are intersecting. Intersect info is
            // stored in FIntersectList ready to be processed in ProcessIntersectList.
            // Re merge sorts see https://stackoverflow.com/a/46319131/359538

            int left = sel_ID, right, lEnd, rEnd, currBase, prevBase, tmp;

            while (actives.jump[left] != -1)
            {
                prevBase = -1;
                while (left != -1 && actives.jump[left] != -1)
                {
                    currBase = left;
                    right = actives.jump[left];
                    lEnd = right;
                    rEnd = actives.jump[right];
                    actives.jump[left] = rEnd;
                    while (left != lEnd && right != rEnd)
                    {
                        if (actives.curX[right] < actives.curX[left])
                        {
                            tmp = actives.prevInSEL[right];
                            for (; ; )
                            {
                                AddNewIntersectNode(tmp, right, topY);
                                if (tmp == left) break;
                                tmp = actives.prevInSEL[tmp];
                            }

                            tmp = right;
                            right = ExtractFromSEL(tmp);
                            lEnd = right;
                            Insert1Before2InSEL(tmp, left);
                            if (left == currBase)
                            {
                                currBase = tmp;
                                actives.jump[currBase] = rEnd;
                                if (prevBase == -1) sel_ID = currBase;
                                else actives.jump[prevBase] = currBase;
                            }
                        }
                        else left = actives.nextInSEL[left];
                    }

                    prevBase = currBase;
                    left = rEnd;
                }
                left = sel_ID;
            }

            return intersectList.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessIntersectList()
        {
            // We now have a list of intersections required so that edges will be
            // correctly positioned at the top of the scanbeam. However, it's important
            // that edge intersections are processed from the bottom up, but it's also
            // crucial that intersections only occur between adjacent edges.

            // First we do a quicksort so intersections proceed in a bottom up order ...
            intersectList.Sort(default(IntersectListSort));

            // Now as we process these intersections, we must sometimes adjust the order
            // to ensure that intersecting edges are always adjacent ...
            for (int i = 0; i < intersectList.Length; ++i)
            {
                if (!EdgesAdjacentInAEL(intersectList[i]))
                {
                    int j = i + 1;
                    while (!EdgesAdjacentInAEL(intersectList[j])) j++;
                    // swap
                    (intersectList[j], intersectList[i]) =
                            (intersectList[i], intersectList[j]); // IntersectNode n = intersectList[i]; //intersectList[i] = intersectList[j]; //intersectList[j] = n;                    
                }

                IntersectNode node = intersectList[i];
                IntersectEdges(node.edge1, node.edge2, node.pt);
                SwapPositionsInAEL(node.edge1, node.edge2);

                actives.curX[node.edge1] = node.pt.x;
                actives.curX[node.edge2] = node.pt.x;
                CheckJoinLeft(node.edge2, node.pt, true);
                CheckJoinRight(node.edge1, node.pt, true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapPositionsInAEL(int ae1, int ae2)
        {
            // preconditon: ae1 must be immediately to the left of ae2
            int next = actives.nextInAEL[ae2];
            if (next != -1) actives.prevInAEL[next] = ae1;
            int prev = actives.prevInAEL[ae1];
            if (prev != -1) actives.nextInAEL[prev] = ae2;
            actives.prevInAEL[ae2] = prev;
            actives.nextInAEL[ae2] = ae1;
            actives.prevInAEL[ae1] = ae2;
            actives.nextInAEL[ae1] = next;
            if (actives.prevInAEL[ae2] == -1) actives_ID = ae2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool ResetHorzDirection(int horz, int vertexMax,
            out long leftX, out long rightX)
        {
            if (actives.bot[horz].x == actives.top[horz].x)
            {
                // the horizontal edge is going nowhere ...
                leftX = actives.curX[horz];
                rightX = actives.curX[horz];
                var ae = actives.nextInAEL[horz];
                while (ae != -1 && actives.vertexTop[ae] != vertexMax)
                    ae = actives.nextInAEL[ae];
                return ae != -1;
            }

            if (actives.curX[horz] < actives.top[horz].x)
            {
                leftX = actives.curX[horz];
                rightX = actives.top[horz].x;
                return true;
            }
            leftX = actives.top[horz].x;
            rightX = actives.curX[horz];
            return false;  // right to left
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HorzIsSpike(int horz)
        {
            long2 nextPt = vertexList.pt[NextVertex(horz)];
            return (actives.bot[horz].x < actives.top[horz].x) != (actives.top[horz].x < nextPt.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void TrimHorz(int horzEdge, bool preserveCollinear)
        {
            bool wasTrimmed = false;
            long2 pt = vertexList.pt[NextVertex(horzEdge)];
            while (pt.y == actives.top[horzEdge].y)
            {
                // always trim 180 deg. spikes (in closed paths)
                // but otherwise break if preserveCollinear = true
                if (preserveCollinear &&
                (pt.x < actives.top[horzEdge].x) != (actives.bot[horzEdge].x < actives.top[horzEdge].x))
                    break;

                actives.vertexTop[horzEdge] = NextVertex(horzEdge);
                actives.top[horzEdge] = pt;
                wasTrimmed = true;
                if (IsMaxima(horzEdge)) break;
                pt = vertexList.pt[NextVertex(horzEdge)];
            }

            if (wasTrimmed) SetDx(horzEdge); // +/-infinity
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToHorzSegList(int op)
        {
            if (outrecList.isOpen[outPtList.outrec[op]]) return;
            horzSegList.Add(new HorzSegment(op));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLastOp(int hotEdge)
        {
            var outrec = actives.outrec[hotEdge];
            return (hotEdge == outrecList.frontEdge[outrec]) ?
              outrecList.pts[outrec] : outPtList.next[outrecList.pts[outrec]];
        }

        private void DoHorizontal(int horz)
        /*******************************************************************************
        * Notes: Horizontal edges (HEs) at scanline intersections (i.e. at the top or    *
        * bottom of a scanbeam) are processed as if layered.The order in which HEs     *
        * are processed doesn't matter. HEs intersect with the bottom vertices of      *
        * other HEs[#] and with non-horizontal edges [*]. Once these intersections     *
        * are completed, intermediate HEs are 'promoted' to the next edge in their     *
        * bounds, and they in turn may be intersected[%] by other HEs.                 *
        *                                                                              *
        * eg: 3 horizontals at a scanline:    /   |                     /           /  *
        *              |                     /    |     (HE3)o ========%========== o   *
        *              o ======= o(HE2)     /     |         /         /                *
        *          o ============#=========*======*========#=========o (HE1)           *
        *         /              |        /       |       /                            *
        *******************************************************************************/
        {
            long2 pt;
            bool horzIsOpen = IsOpen(horz);
            long Y = actives.bot[horz].y;

            int vertex_max = horzIsOpen ?
              GetCurrYMaximaVertex_Open(horz) :
              GetCurrYMaximaVertex(horz);

            // remove 180 deg.spikes and also simplify
            // consecutive horizontals when PreserveCollinear = true
            if (vertex_max != -1 &&
              !horzIsOpen && vertex_max != actives.vertexTop[horz])
                TrimHorz(horz, PreserveCollinear);

            bool isLeftToRight =
                ResetHorzDirection(horz, vertex_max, out long leftX, out long rightX);

            int op;
            if (IsHotEdge(horz))
                op = AddOutPt(horz, new long2(actives.curX[horz], Y));
            int currOutrec = actives.outrec[horz];

            for (; ; )
            {
                // loops through consec. horizontal edges (if open)
                int ae = isLeftToRight ? actives.nextInAEL[horz] : actives.prevInAEL[horz];

                while (ae != -1)
                {
                    if (actives.vertexTop[ae] == vertex_max)
                    {
                        // do this first!!
                        if (IsHotEdge(horz) && IsJoined(ae!)) Split(ae, actives.top[ae]);

                        if (IsHotEdge(horz))
                        {
                            while (actives.vertexTop[horz] != actives.vertexTop[ae])
                            {
                                AddOutPt(horz, actives.top[horz]);
                                UpdateEdgeIntoAEL(horz);
                            }
                            if (isLeftToRight)
                                AddLocalMaxPoly(horz, ae, actives.top[horz]);
                            else
                                AddLocalMaxPoly(ae, horz, actives.top[horz]);
                        }
                        DeleteFromAEL(ae);
                        DeleteFromAEL(horz);
                        return;
                    }

                    // if horzEdge is a maxima, keep going until we reach
                    // its maxima pair, otherwise check for break conditions
                    if (vertex_max != actives.vertexTop[horz] || IsOpenEnd(horz))
                    {
                        // otherwise stop when 'ae' is beyond the end of the horizontal line
                        if ((isLeftToRight && actives.curX[ae] > rightX) ||
                            (!isLeftToRight && actives.curX[ae] < leftX)) break;

                        if (actives.curX[ae] == actives.top[horz].x && !IsHorizontal(ae))
                        {
                            pt = vertexList.pt[NextVertex(horz)];

                            // to maximize the possibility of putting open edges into
                            // solutions, we'll only break if it's past HorzEdge's end
                            if (IsOpen(ae) && !IsSamePolyType(ae, horz) && !IsHotEdge(ae))
                            {
                                if ((isLeftToRight && (TopX(ae, pt.y) > pt.x)) ||
                                  (!isLeftToRight && (TopX(ae, pt.y) < pt.x))) break;
                            }
                            // otherwise for edges at horzEdge's end, only stop when horzEdge's
                            // outslope is greater than e's slope when heading right or when
                            // horzEdge's outslope is less than e's slope when heading left.
                            else if ((isLeftToRight && (TopX(ae, pt.y) >= pt.x)) ||
                                (!isLeftToRight && (TopX(ae, pt.y) <= pt.x))) break;
                        }
                    }

                    pt = new long2(actives.curX[ae], Y);

                    if (isLeftToRight)
                    {
                        IntersectEdges(horz, ae, pt);
                        SwapPositionsInAEL(horz, ae);
                        actives.curX[horz] = actives.curX[ae];
                        ae = actives.nextInAEL[horz];
                    }
                    else
                    {
                        IntersectEdges(ae, horz, pt);
                        SwapPositionsInAEL(ae, horz);
                        actives.curX[horz] = actives.curX[ae];
                        ae = actives.prevInAEL[horz];
                    }

                    if (IsHotEdge(horz) && (actives.outrec[horz] != currOutrec))
                    {
                        currOutrec = actives.outrec[horz];
                        AddToHorzSegList(GetLastOp(horz));
                    }

                } // we've reached the end of this horizontal

                // check if we've finished looping through consecutive horizontals
                if (horzIsOpen && IsOpenEnd(horz)) // ie open at top
                {
                    if (IsHotEdge(horz))
                    {
                        AddOutPt(horz, actives.top[horz]);
                        if (IsFront(horz))
                            outrecList.frontEdge[actives.outrec[horz]] = -1;
                        else
                            outrecList.backEdge[actives.outrec[horz]] = -1;
                        actives.outrec[horz] = -1;
                    }
                    DeleteFromAEL(horz); // ie open at top
                    return;
                }
                else if (vertexList.pt[NextVertex(horz)].y != actives.top[horz].y)
                    break;

                //still more horizontals in bound to process ...
                if (IsHotEdge(horz))
                    AddOutPt(horz, actives.top[horz]);
                UpdateEdgeIntoAEL(horz);

                if (PreserveCollinear && HorzIsSpike(horz))
                    TrimHorz(horz, true);

                isLeftToRight = ResetHorzDirection(horz, vertex_max, out leftX, out rightX);

            } // end for loop and end of (possible consecutive) horizontals

            if (IsHotEdge(horz)) AddOutPt(horz, actives.top[horz]);
            UpdateEdgeIntoAEL(horz); // this is the end of an intermediate horiz.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoTopOfScanbeam(long y)
        {
            sel_ID = -1; // sel_ is reused to flag horizontals (see PushHorz below)
            int ae = actives_ID;
            while (ae != -1)
            {
                // NB 'ae' will never be horizontal here
                var aeTop = actives.top[ae];
                if (aeTop.y == y)
                {
                    actives.curX[ae] = aeTop.x;
                    if (IsMaxima(ae))
                    {
                        ae = DoMaxima(ae); // TOP OF BOUND (MAXIMA)
                        continue;
                    }

                    // INTERMEDIATE VERTEX ...
                    if (IsHotEdge(ae))
                        AddOutPt(ae, aeTop);
                    UpdateEdgeIntoAEL(ae);
                    if (IsHorizontal(ae))
                        PushHorz(ae); // horizontals are processed later
                }
                else // i.e. not the top of the edge
                    actives.curX[ae] = TopX(ae, y);

                ae = actives.nextInAEL[ae];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DoMaxima(int ae)
        {
            int prevE;
            int nextE, maxPair;
            prevE = actives.prevInAEL[ae];
            nextE = actives.nextInAEL[ae];

            if (IsOpenEnd(ae))
            {
                if (IsHotEdge(ae)) AddOutPt(ae, actives.top[ae]);
                if (!IsHorizontal(ae))
                {
                    if (IsHotEdge(ae))
                    {
                        if (IsFront(ae))
                            outrecList.frontEdge[actives.outrec[ae]] = -1;
                        else
                            outrecList.backEdge[actives.outrec[ae]] = -1;
                        actives.outrec[ae] = -1;
                    }
                    DeleteFromAEL(ae);
                }
                return nextE;
            }

            maxPair = GetMaximaPair(ae);
            if (maxPair == -1) return nextE; // eMaxPair is horizontal

            if (IsJoined(ae)) Split(ae, actives.top[ae]);
            if (IsJoined(maxPair)) Split(maxPair, actives.top[maxPair]);

            // only non-horizontal maxima here.
            // process any edges between maxima pair ...
            while (nextE != maxPair)
            {
                IntersectEdges(ae, nextE, actives.top[ae]);
                SwapPositionsInAEL(ae, nextE);
                nextE = actives.nextInAEL[ae];
            }

            if (IsOpen(ae))
            {
                if (IsHotEdge(ae))
                    AddLocalMaxPoly(ae, maxPair, actives.top[ae]);
                DeleteFromAEL(maxPair);
                DeleteFromAEL(ae);
                return (prevE != -1 ? actives.nextInAEL[prevE] : actives_ID);
            }

            // here ae.nextInAel == ENext == EMaxPair ...
            if (IsHotEdge(ae))
                AddLocalMaxPoly(ae, maxPair, actives.top[ae]);

            DeleteFromAEL(ae);
            DeleteFromAEL(maxPair);
            return prevE != -1 ? actives.nextInAEL[prevE] : actives_ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsJoined(int e)
        {
            return actives.joinWith[e] != JoinWith.None;
        }

        void Split(int e, long2 currPt)
        {
            if (actives.joinWith[e] == JoinWith.Right)
            {
                actives.joinWith[e] = JoinWith.None;
                actives.joinWith[actives.nextInAEL[e]] = JoinWith.None;
                AddLocalMinPoly(e, actives.nextInAEL[e], currPt, true);
            }
            else
            {
                actives.joinWith[e] = JoinWith.None;
                actives.joinWith[actives.prevInAEL[e]] = JoinWith.None;
                AddLocalMinPoly(actives.prevInAEL[e], e, currPt, true);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckJoinLeft(int e,
            long2 pt, bool checkCurrX = false)
        {
            int prev = actives.prevInAEL[e];
            if (prev == -1 || IsOpen(e) || IsOpen(prev) ||
              !IsHotEdge(e) || !IsHotEdge(prev)) return;
            if ((pt.y < actives.top[e].y + 2 || pt.y < actives.top[prev].y + 2) &&  //avoid trivial joins
              ((actives.bot[e].y > pt.y) || actives.bot[prev].y > pt.y)) return;    // (#490)

            if (checkCurrX)
            {
                if (ClipperFunc.PerpendicDistFromLineSqrd(pt, actives.bot[prev], actives.top[prev]) > 0.25) return;
            }
            else if (actives.curX[e] != actives.curX[prev]) return;
            if (InternalClipper.CrossProduct(actives.top[e], pt, actives.top[prev]) != 0) return;

            if (actives.outrec[e] == actives.outrec[prev])
                AddLocalMaxPoly(prev, e, pt);
            else if (actives.outrec[e] < actives.outrec[prev])
                JoinOutrecPaths(e, prev);
            else
                JoinOutrecPaths(prev, e);
            actives.joinWith[prev] = JoinWith.Right;
            actives.joinWith[e] = JoinWith.Left;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckJoinRight(int e,
            long2 pt, bool checkCurrX = false)
        {
            int next = actives.nextInAEL[e];
            if (IsOpen(e) || !IsHotEdge(e) || IsJoined(e) ||
                    next == -1 || IsOpen(next) || !IsHotEdge(next)) return;
            if ((pt.y < actives.top[e].y + 2 || pt.y < actives.top[next].y + 2) &&  //avoid trivial joins
              ((actives.bot[e].y > pt.y) || actives.bot[next].y > pt.y)) return;    // (#490)

            if (checkCurrX)
            {
                if (ClipperFunc.PerpendicDistFromLineSqrd(pt, actives.bot[next], actives.top[next]) > 0.25) return;
            }
            else if (actives.curX[e] != actives.curX[next]) return;
            if (InternalClipper.CrossProduct(actives.top[e], pt, actives.top[next]) != 0)
                return;

            if (actives.outrec[e] == actives.outrec[next])
                AddLocalMaxPoly(e, next, pt);
            else if (actives.outrec[e] < actives.outrec[next])
                JoinOutrecPaths(e, next);
            else
                JoinOutrecPaths(next, e);
            actives.joinWith[e] = JoinWith.Right;
            actives.joinWith[next] = JoinWith.Left;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixOutRecPts(int outrec)
        {
            int op = outrecList.pts[outrec];
            do
            {
                outPtList.outrec[op] = outrec;
                op = outPtList.next[op];
            } while (op != outrecList.pts[outrec]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SetHorzSegHeadingForward(int hsID, int opP, int opN)
        {
            if (outPtList.pt[opP].x == outPtList.pt[opN].x) return false;
            var hs = horzSegList[hsID];
            if (outPtList.pt[opP].x < outPtList.pt[opN].x)
            {
                hs.leftOp = opP;
                hs.rightOp = opN;
                hs.leftToRight = true;
            }
            else
            {
                hs.leftOp = opN;
                hs.rightOp = opP;
                hs.leftToRight = false;
            }
            horzSegList[hsID] = hs;
            return true;
        }
        private bool UpdateHorzSegment(int hsID)
        {
            int op = horzSegList[hsID].leftOp!;
            int outrec = GetRealOutRec(outPtList.outrec[op]);
            bool outrecHasEdges = outrecList.frontEdge[outrec] != -1;
            long curr_y = outPtList.pt[op].y;
            int opP = op, opN = op;
            if (outrecHasEdges)
            {
                int opA = outrecList.pts[outrec], opZ = outPtList.next[opA];
                while (opP != opZ && outPtList.pt[outPtList.prev[opP]].y == curr_y)
                    opP = outPtList.prev[opP];
                while (opN != opA && outPtList.pt[outPtList.next[opN]].y == curr_y)
                    opN = outPtList.next[opN];
            }
            else
            {
                while (outPtList.prev[opP] != opN && outPtList.pt[outPtList.prev[opP]].y == curr_y)
                    opP = outPtList.prev[opP];
                while (outPtList.next[opN] != opP && outPtList.pt[outPtList.next[opN]].y == curr_y)
                    opN = outPtList.next[opN];
            }
            bool result =
              SetHorzSegHeadingForward(hsID, opP, opN) &&
              outPtList.horz[horzSegList[hsID].leftOp] == -1;

            if (result)
                outPtList.horz[horzSegList[hsID].leftOp] = hsID;
            else
            {
                var hs = horzSegList[hsID];
                hs.rightOp = -1; // (for sorting)
                horzSegList[hsID] = hs;
            }
            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int DuplicateOp(int op, bool insert_after)
        {
            int result = outPtList.NewOutPt(outPtList.pt[op], outPtList.outrec[op]);
            if (insert_after)
            {
                outPtList.next[result] = outPtList.next[op];
                outPtList.prev[outPtList.next[result]] = result;
                outPtList.prev[result] = op;
                outPtList.next[op] = result;
            }
            else
            {
                outPtList.prev[result] = outPtList.prev[op];
                outPtList.next[outPtList.prev[result]] = result;
                outPtList.next[result] = op;
                outPtList.prev[op] = result;
            }
            return result;
        }
        private void ConvertHorzSegsToJoins()
        {
            int k = 0;
            for (int hsID = 0, length = horzSegList.Length; hsID < length; hsID++)
                if (UpdateHorzSegment(hsID)) k++;
            if (k < 2) return;
            horzSegList.Sort(new HorzSegSorter(outPtList));

            for (int i = 0; i < k - 1; i++)
            {
                HorzSegment hs1 = horzSegList[i];
                // for each HorzSegment, find others that overlap
                for (int j = i + 1; j < k; j++)
                {
                    HorzSegment hs2 = horzSegList[j];
                    if (outPtList.pt[hs2.leftOp].x >= outPtList.pt[hs1.rightOp].x) break;
                    if (hs2.leftToRight == hs1.leftToRight ||
                      (outPtList.pt[hs2.rightOp].x <= outPtList.pt[hs1.leftOp].x)) continue;
                    long curr_y = outPtList.pt[hs1.leftOp].y;
                    if (hs1.leftToRight)
                    {
                        while (outPtList.pt[outPtList.next[hs1.leftOp]].y == curr_y &&
                          outPtList.pt[outPtList.next[hs1.leftOp]].x <= outPtList.pt[hs2.leftOp].x)
                            hs1.leftOp = outPtList.next[hs1.leftOp];
                        while (outPtList.pt[outPtList.prev[hs2.leftOp]].y == curr_y &&
                          outPtList.pt[outPtList.prev[hs2.leftOp]].x <= outPtList.pt[hs1.leftOp].x)
                            hs2.leftOp = outPtList.prev[hs2.leftOp];
                        horzSegList[j] = hs2;
                        horzSegList[i] = hs1;
                        HorzJoin join = new HorzJoin(
                          DuplicateOp(hs1.leftOp, true),
                          DuplicateOp(hs2.leftOp, false));
                        horzJoinList.Add(join);
                    }
                    else
                    {
                        while (outPtList.pt[outPtList.prev[hs1.leftOp]].y == curr_y &&
                          outPtList.pt[outPtList.prev[hs1.leftOp]].x <= outPtList.pt[hs2.leftOp].x)
                            hs1.leftOp = outPtList.prev[hs1.leftOp];
                        while (outPtList.pt[outPtList.next[hs2.leftOp]].y == curr_y &&
                          outPtList.pt[outPtList.next[hs2.leftOp]].x <= outPtList.pt[hs1.leftOp].x)
                            hs2.leftOp = outPtList.next[hs2.leftOp];
                        horzSegList[j] = hs2;
                        horzSegList[i] = hs1;
                        HorzJoin join = new HorzJoin(
                          DuplicateOp(hs2.leftOp, true),
                          DuplicateOp(hs1.leftOp, false));
                        horzJoinList.Add(join);
                    }
                }
            }
        }
        private Rect64 GetBounds(int or)
        {
            int op = outrecList.pts[or], next;
            if (InternalClipper.PointCount(in outPtList, op) == 0) return new Rect64();
            Rect64 result = ClipperFunc.MaxInvalidRect64();
            next = op;
            do
            {
                var nextPt = outPtList.pt[next];
                if (nextPt.x < result.left) result.left = nextPt.x;
                if (nextPt.x > result.right) result.right = nextPt.x;
                if (nextPt.y < result.top) result.top = nextPt.y;
                if (nextPt.y > result.bottom) result.bottom = nextPt.y;
                next = outPtList.next[next];
            } while (next != op);
            return result;
        }

        private bool Path1InsidePath2(int or1, int or2)
        {
            PointInPolygonResult result;
            int op = outrecList.pts[or1];
            int op2 = outrecList.pts[or2];
            int startOp = op;
            do
            {
                result = InternalClipper.PointInPolygon(outPtList.pt[op], ref outPtList, op2);
                if (result != PointInPolygonResult.IsOn) break;
                op = outPtList.next[op];
            } while (op != startOp);
            return result == PointInPolygonResult.IsInside;
        }

        private void ProcessHorzJoins()
        {
            for (int j = 0, length = horzJoinList.Length; j < length; j++)
            {

                int or1 = GetRealOutRec(outPtList.outrec[horzJoinList[j].op1]);
                int or2 = GetRealOutRec(outPtList.outrec[horzJoinList[j].op2]);

                int op1b = outPtList.next[horzJoinList[j].op1];
                int op2b = outPtList.prev[horzJoinList[j].op1];
                outPtList.next[horzJoinList[j].op1] = horzJoinList[j].op2;
                outPtList.prev[horzJoinList[j].op2] = horzJoinList[j].op1;
                outPtList.prev[op1b] = op2b;
                outPtList.next[op2b] = op1b;

                if (or1 == or2)
                {
                    or2 = outrecList.AddOutRec(-1, true, -1);
                    outrecList.pts[or2] = op1b;

                    FixOutRecPts(or2);
                    if (outPtList.outrec[outrecList.pts[or1]] == or2)
                    {
                        outrecList.pts[or1] = horzJoinList[j].op1;
                        outPtList.outrec[outrecList.pts[or1]] = or1;
                    }

                    if (_using_polytree)
                    {
                        if (Path1InsidePath2(or2, or1))
                            SetOwner(or2, or1);
                        else if (Path1InsidePath2(or1, or2))
                            SetOwner(or1, or2);
                        else
                        {
                            outrecList.AddSplit(or1, or2); // (#498)
                            outrecList.owner[or2] = or1;
                        }
                    }
                    else
                        outrecList.owner[or2] = or1;
                }
                else
                {
                    outrecList.pts[or2] = -1;
                    if (_using_polytree)
                        SetOwner(or2, or1);
                    else
                        outrecList.owner[or2] = or1;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PtsReallyClose(long2 pt1, long2 pt2)
        {
            return (math.abs(pt1.x - pt2.x) < 2) && (math.abs(pt1.y - pt2.y) < 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsVerySmallTriangle(int op)
        {
            return outPtList.next[outPtList.next[op]] == outPtList.prev[op] &&
              (PtsReallyClose(outPtList.pt[outPtList.prev[op]], outPtList.pt[outPtList.next[op]]) ||
                  PtsReallyClose(outPtList.pt[op], outPtList.pt[outPtList.next[op]]) ||
                  PtsReallyClose(outPtList.pt[op], outPtList.pt[outPtList.prev[op]]));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidClosedPath(int op)
        {
            return (op != -1 && outPtList.next[op] != op &&
              (outPtList.next[op] != outPtList.prev[op] || !IsVerySmallTriangle(op)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DisposeOutPt(int op)
        {
            int result = (outPtList.next[op] == op ? -1 : outPtList.next[op]);
            outPtList.next[outPtList.prev[op]] = outPtList.next[op];
            outPtList.prev[outPtList.next[op]] = outPtList.prev[op];
            //op == null;
            return result;
        }

        private void CleanCollinear(int outrec)
        {
            outrec = GetRealOutRec(outrec);

            if (outrec == -1 || outrecList.isOpen[outrec]) return;

            if (!IsValidClosedPath(outrecList.pts[outrec]))
            {
                outrecList.pts[outrec] = -1;
                return;
            }

            var startOp = outrecList.pts[outrec];
            var op2 = startOp;
            for (; ; )
            {
                // NB if preserveCollinear == true, then only remove 180 deg. spikes
                var prevOP2 = outPtList.prev[op2];
                var nextOP2 = outPtList.next[op2];
                if ((InternalClipper.CrossProduct(outPtList.pt[prevOP2], outPtList.pt[op2], outPtList.pt[nextOP2]) == 0) &&
                    ((outPtList.pt[op2] == outPtList.pt[prevOP2]) || (outPtList.pt[op2] == outPtList.pt[nextOP2]) || !PreserveCollinear ||
                    (InternalClipper.DotProduct(outPtList.pt[prevOP2], outPtList.pt[op2], outPtList.pt[nextOP2]) < 0)))
                {
                    if (op2 == outrecList.pts[outrec])
                        outrecList.pts[outrec] = prevOP2;
                    op2 = DisposeOutPt(op2);
                    if (!IsValidClosedPath(op2))
                    {
                        outrecList.pts[outrec] = -1;
                        return;
                    }
                    startOp = op2;
                    continue;
                }
                op2 = outPtList.next[op2];
                if (op2 == startOp) break;
            }
            FixSelfIntersects(outrec);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoSplitOp(int outrec, int splitOp)
        {
            // splitOp.prev <=> splitOp &&
            // splitOp.next <=> splitOp.next.next are intersecting
            int prevOp = outPtList.prev[splitOp];
            int nextNextOp = outPtList.next[outPtList.next[splitOp]];
            outrecList.pts[outrec] = prevOp;
            int result = prevOp;

            InternalClipper.GetIntersectPoint(
                outPtList.pt[prevOp], outPtList.pt[splitOp], outPtList.pt[outPtList.next[splitOp]], outPtList.pt[nextNextOp], out double2 tmp);
            long2 ip = new long2(tmp);

            double area1 = Area(prevOp);
            double absArea1 = math.abs(area1);

            if (absArea1 < 2)
            {
                outrecList.pts[outrec] = -1;
                return;
            }

            // nb: area1 is the path's area *before* splitting, whereas area2 is
            // the area of the triangle containing splitOp & splitOp.next.
            // So the only way for these areas to have the same sign is if
            // the split triangle is larger than the path containing prevOp or
            // if there's more than one self=intersection.
            double area2 = AreaTriangle(ip, outPtList.pt[splitOp], outPtList.pt[outPtList.next[splitOp]]);
            double absArea2 = math.abs(area2);

            // de-link splitOp and splitOp.next from the path
            // while inserting the intersection point
            if (ip == outPtList.pt[prevOp] || ip == outPtList.pt[nextNextOp])
            {
                outPtList.prev[nextNextOp] = prevOp;
                outPtList.next[prevOp] = nextNextOp;
            }
            else
            {
                var newOp2 = outPtList.NewOutPt(ip, outrec);
                outPtList.prev[newOp2] = prevOp;
                outPtList.next[newOp2] = nextNextOp;

                outPtList.prev[nextNextOp] = newOp2;
                outPtList.next[prevOp] = newOp2;
            }

            if (absArea2 > 1 &&
                (absArea2 > absArea1 ||
                 ((area2 > 0) == (area1 > 0))))
            {
                var newOutRec = outrecList.AddOutRec(-1, false, -1);
                outrecList.owner[newOutRec] = outrecList.owner[outrec];
                outPtList.outrec[splitOp] = newOutRec;
                outPtList.outrec[outPtList.next[splitOp]] = newOutRec;

                if (_using_polytree)
                {
                    outrecList.AddSplit(outrec, newOutRec);
                }

                int newOp = outPtList.NewOutPt(ip, newOutRec);
                outPtList.prev[newOp] = outPtList.next[splitOp];
                outPtList.next[newOp] = splitOp;

                outrecList.pts[newOutRec] = newOp;
                outPtList.prev[splitOp] = newOp;
                outPtList.next[outPtList.next[splitOp]] = newOp;
            }
            //else { splitOp = null; splitOp.next = null; }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixSelfIntersects(int outrec)
        {
            int op2 = outrecList.pts[outrec];
            for (; ; )
            {
                // triangles can't self-intersect
                var op2Next = outPtList.next[op2];
                var op2Prev = outPtList.prev[op2];
                if (op2Prev == outPtList.next[op2Next]) break;
                if (InternalClipper.SegsIntersect(outPtList.pt[op2Prev],
                         outPtList.pt[op2], outPtList.pt[op2Next], outPtList.pt[outPtList.next[op2Next]]))
                {
                    DoSplitOp(outrec, op2);
                    if (outrecList.pts[outrec] == -1) return;
                    op2 = outrecList.pts[outrec];
                    continue;
                }
                else
                    op2 = outPtList.next[op2];
                if (op2 == outrecList.pts[outrec]) break;
            }
        }
        internal bool BuildPath(int op, bool reverse, bool isOpen, ref PolygonInt path)
        {
            if (op == -1 || outPtList.next[op] == op || (!isOpen && outPtList.next[op] == outPtList.prev[op])) return false;

            path.AddComponent();

            long2 lastPt;
            int op2;
            if (reverse)
            {
                lastPt = outPtList.pt[op];
                op2 = outPtList.prev[op];
            }
            else
            {
                op = outPtList.next[op];
                lastPt = outPtList.pt[op];
                op2 = outPtList.next[op];
            }
            var firstPt = lastPt;
            //path.nodes.Add(lastPt);
            path.nodes.Add((int2)(_invScale * lastPt)); //only needed when input Polygon was float or double


            while (op2 != op)
            {
                var op2Pt = outPtList.pt[op2];
                if (op2Pt != lastPt)
                {
                    lastPt = op2Pt;
                    //path.nodes.Add(lastPt);
                    path.nodes.Add((int2)(_invScale * lastPt));//only needed when input Polygon was float or double

                }
                if (reverse)
                    op2 = outPtList.prev[op2];
                else
                    op2 = outPtList.next[op2];
            }
            if (!isOpen)
            {
                if (firstPt != lastPt)
                    //path.nodes.Add(lastPt);
                    path.nodes.Add((int2)(_invScale * firstPt)); //only needed when input Polygon was float or double
            }
            return true;
        }
        bool BuildPaths(ref PolygonInt solutionClosed, ref PolygonInt solutionOpen)
        {

            solutionClosed.Clear();
            solutionOpen.Clear();
            solutionClosed.nodes.Capacity = outPtList.pt.Length;
            solutionOpen.nodes.Capacity = outPtList.pt.Length;
            solutionClosed.startIDs.Capacity = outrecList.owner.Length;
            solutionOpen.startIDs.Capacity = outrecList.owner.Length;

            int i = 0;
            // _outrecList.Count is not static here because
            // CleanCollinear can indirectly add additional OutRec
            while (i < outrecList.owner.Length)
            {
                int outrec = i++;

                if (outrecList.pts[outrec] == -1) continue;

                if (outrecList.isOpen[outrec])
                    BuildPath(outrecList.pts[outrec], ReverseSolution, true, ref solutionOpen);
                else
                {
                    CleanCollinear(outrec);
                    // closed paths should always return a Positive orientation
                    // except when ReverseSolution == true
                    BuildPath(outrecList.pts[outrec], ReverseSolution, false, ref solutionClosed);
                }
            }
            if (solutionOpen.nodes.Length > 0)
                solutionOpen.ClosePolygon();
            if (solutionClosed.nodes.Length > 0)
                solutionClosed.ClosePolygon();

            return true;
        }

        private bool DeepCheckOwner(int outrec, int owner)
        {
            if (outrecList.bounds[owner].IsEmpty())
                outrecList.bounds[owner] = GetBounds(owner);
            bool isInsideOwnerBounds = outrecList.bounds[owner].Contains(outrecList.bounds[outrec]);

            // while looking for the correct owner, check the owner's
            // splits **before** checking the owner itself because
            // splits can occur internally, and checking the owner
            // first would miss the inner split's true ownership
            var asplitID = outrecList.splitStartIDs[owner];
            if (asplitID != -1)
            {
                do
                {
                    var asplit = outrecList.splits[asplitID];
                    //Debug.Log($"Deepcheck split ownership for outrec {asplit}");
                    int split = GetRealOutRec(asplit);
                    if (split == -1 || split <= owner || split == outrec)
                    {
                        asplitID = outrecList.nextSplit[asplitID];
                        continue;
                    }
                    if (outrecList.splitStartIDs[split] != -1 && DeepCheckOwner(outrec, split)) return true;

                    if (outrecList.bounds[split].IsEmpty()) outrecList.bounds[split] = GetBounds(split);

                    if (outrecList.bounds[split].Contains(outrecList.bounds[outrec]) && Path1InsidePath2(outrec, split))
                    {
                        outrecList.owner[outrec] = split;
                        return true;
                    }
                    asplitID = outrecList.nextSplit[asplitID];
                } while (asplitID != -1);
            }

            // only continue when not inside recursion
            if (owner != outrecList.owner[outrec]) return false;

            for (; ; )
            {
                if (isInsideOwnerBounds && Path1InsidePath2(outrec, outrecList.owner[outrec]))
                    return true;

                outrecList.owner[outrec] = outrecList.owner[outrecList.owner[outrec]];
                if (outrecList.owner[outrec] == -1) return false;
                isInsideOwnerBounds = outrecList.bounds[outrecList.owner[outrec]].Contains(outrecList.bounds[outrec]);
            }
        }

        bool BuildTree(ref PolyTree polytree, ref PolygonInt solutionOpen)
        {
            polytree.Clear(outrecList.owner.Length, Allocator.Temp);
            solutionOpen.Clear();
            solutionOpen.nodes.Capacity = outPtList.pt.Length;
            solutionOpen.startIDs.Capacity = outrecList.owner.Length;
            var components = polytree.components;
            var exteriorIDs = polytree.exteriorIDs;
            for (int i = 0, length = components.Length; i < length; i++)
                components[i] = new TreeNode(i); //initialize

            for (int outrec = 0, length = outrecList.owner.Length; outrec < length; outrec++)
            {
                if (outrecList.pts[outrec] == -1) continue;

                if (outrecList.isOpen[outrec])
                {
                    BuildPath(outrecList.pts[outrec], ReverseSolution, true, ref solutionOpen);
                    continue;
                }
                if (!IsValidClosedPath(outrecList.pts[outrec]))
                    continue;
                if (outrecList.bounds[outrec].IsEmpty()) outrecList.bounds[outrec] = GetBounds(outrec);

                var owner = GetRealOutRec(outrecList.owner[outrec]);
                outrecList.owner[outrec] = owner;

                if (outrecList.owner[outrec] != -1)
                    DeepCheckOwner(outrec, outrecList.owner[outrec]);

                if (outrecList.owner[outrec] == -1) //if no owner, definitely an outer polygon
                {
                    //Debug.Log($"Outrec {outrec}: outer with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[outrec])} nodes");
                    exteriorIDs.Add(outrec);
                }
                else
                {
                    var node = components[outrec];
                    polytree.AddChildComponent(outrecList.owner[outrec], node);
                    //Debug.Log($"Outrec {outrec}: child of {outrecList.owner[outrec]} {node.orientation} with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[outrec])} nodes.");
                }
            }
            return true;
        }

        public void GetPolygonWithHoles(in PolyTree polyTree, int outrec, ref PolygonInt outPolygon)
        {
            //Debug.Log($"taking Exterior {outrec} with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[outrec])} nodes as is ");
            BuildPath(outrecList.pts[outrec], false, false, ref outPolygon);

            int hole;
            int next = outrec;
            while (polyTree.GetNextComponent(next, out hole))
            {
                if (outrecList.owner[hole] != outrec)
                {
                    next = hole;
                    //Debug.Log($"Hole {hole} is an island, tesselate those islands separately!"); //TO-DO: implement (e.g. returning a list with island ID's)
                    continue;
                }
                //Debug.Log($"taking Hole {holeID} with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[holeID])} nodes as is");
                BuildPath(outrecList.pts[hole], false, false, ref outPolygon);

                next = hole;
            }
            outPolygon.ClosePolygon(); //abuse StartID to store end of last Component
        }

        public bool Execute(ClipType clipType, FillRule fillRule, ref PolygonInt solutionClosed, ref PolygonInt solutionOpen)
        {
            _succeeded = true;
            solutionClosed.Clear();
            solutionOpen.Clear();
            ExecuteInternal(clipType, fillRule);
            BuildPaths(ref solutionClosed, ref solutionOpen);

            ClearSolutionOnly();
            return _succeeded;
        }
        public bool Execute(ClipType clipType, FillRule fillRule, ref PolygonInt solutionClosed)
        {
            var solutionOpen = new PolygonInt(0, Allocator.Temp);
            return Execute(clipType, fillRule, ref solutionClosed, ref solutionOpen);
        }
        public bool Execute(ClipType clipType, FillRule fillRule, ref PolyTree polytree, ref PolygonInt openPaths)
        {
            _succeeded = true;
            _using_polytree = true;
            ExecuteInternal(clipType, fillRule);
            BuildTree(ref polytree, ref openPaths);

            //ClearSolution();
            return _succeeded;
        }
        public void PrintVertices()
        {
            Debug.Log($"Vertex List Size: {vertexList.pt.Length} ");
            Debug.Log($"Minimalist List Size: {minimaList.Length} ");
        }
        public void PrintSize()
        {
            Debug.Log($"Vertex List Size: pt {vertexList.pt.Length} flags {vertexList.flags.Length} next {vertexList.next.Length} prev {vertexList.prev.Length}");
            Debug.Log($"Minimalist List Size: {minimaList.Length} ");
            Debug.Log($"OutPoint List Size: pt {outPtList.pt.Length} outrec {outPtList.outrec.Length} joiner {outPtList.horz.Length} next {outPtList.next.Length} prev {outPtList.prev.Length}");
            Debug.Log($"OutRec List Size: pts {outrecList.pts.Length} backEdge {outrecList.backEdge.Length} frontEdge {outrecList.frontEdge.Length} owner {outrecList.owner.Length} state {outrecList.isOpen.Length} polypath {outrecList.polypath.Length}");
            Debug.Log($"Actives List Size: bot {actives.bot.Length} top {actives.top.Length} prevInAEL {actives.prevInAEL.Length} nextInAEL {actives.nextInAEL.Length} prevInSEL {actives.prevInSEL.Length} nextInSEL {actives.nextInSEL.Length} outrec {actives.outrec.Length} vertexTop {actives.vertexTop.Length} windCount {actives.windCount.Length} windCount2 {actives.windCount2.Length} windDx {actives.windDx.Length}");
            Debug.Log($"Intersect List Size: {intersectList.Length} ");
        }

    } //ClipperBase class

    public class ClipperLibException : Exception
    {
        public ClipperLibException(string description) : base(description) { }
    }
} //namespace