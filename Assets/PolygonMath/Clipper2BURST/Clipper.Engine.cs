/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - also known as Clipper2                            *
* Date      :  20 April 2022                                                   *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This is the main polygon clipping module                        *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/

using Chart3D.Helper.MinHeap;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    //Vertex: a pre-clipping data structure. It is used to separate polygons
    //into ascending and descending 'bounds' (or sides) that start at local
    //minima and ascend to a local maxima, before descending again.

    [Flags]
    public enum VertexFlags { None = 0, OpenStart = 1, OpenEnd = 2, LocalMax = 4, LocalMin = 8 };
    public struct ClipperD
    {
        ClipType cliptype;
        FillRule fillrule;
        ActiveLL actives;
        int actives_ID;
        int sel_ID;
        int horzJoiners;
        NativeList<LocalMinima> minimaList;
        NativeList<IntersectNode> intersectList;
        VertexLL vertexList;
        OutRecLL outrecList;
        OutPtLL outPtList;
        JoinerLL joinerList;
        MinHeap<long, LongComparerMaxFirst> scanlineList;
        int currentLocMin;
        long currentBotY;
        bool isSortedMinimaList;
        bool hasOpenPaths;
        private readonly double _scale;
        private readonly double _invScale;
        public bool PreserveCollinear { get; set; }

        public ClipperD(Allocator allocator, int roundingDecimalPrecision = 2)
        {
            cliptype = ClipType.None;
            fillrule = FillRule.EvenOdd;
            actives = new ActiveLL(16, allocator);
            actives_ID = -1;
            sel_ID = -1;
            horzJoiners = -1;
            minimaList = new NativeList<LocalMinima>(1024, allocator);
            intersectList = new NativeList<IntersectNode>(1024, allocator);
            vertexList = new VertexLL(128, allocator);
            outrecList = new OutRecLL(16, allocator);
            outPtList = new OutPtLL(16, allocator);
            joinerList = new JoinerLL(16, allocator);
            scanlineList = new MinHeap<long, LongComparerMaxFirst>(64, allocator, default(LongComparerMaxFirst));
            currentLocMin = 0;
            currentBotY = long.MaxValue;
            isSortedMinimaList = false;
            hasOpenPaths = false;
            PreserveCollinear = true;
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
            if (joinerList.IsCreated) joinerList.Dispose();
            if (scanlineList.IsCreated) scanlineList.Dispose();
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
            return minimaList[actives.localMin[ae]].isOpen &&
                (vertexList.flags[actives.vertexTop[ae]] & (VertexFlags.OpenStart | VertexFlags.OpenEnd)) != VertexFlags.None;
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
        bool IsOuter(int outrec)
        {
            return outrecList.state[outrec] == OutRecState.Outer;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetAsOuter(int outrec)
        {
            outrecList.state[outrec] = OutRecState.Outer;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsInner(int outrec)
        {
            return outrecList.state[outrec] == OutRecState.Inner;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SetAsInner(int outrec)
        {
            outrecList.state[outrec] = OutRecState.Inner;
        }
        bool IsFront(int ae)
        {
            //the front edge will be the LEFT edge when it's an OUTER polygon
            //so that outer polygons will be orientated clockwise
            if (outrecList.state[actives.outrec[ae]] == OutRecState.Open)
                return actives.windDx[ae] > 0;
            else
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
            else if (pt2.x > pt1.x)
                return double.NegativeInfinity;
            else
                return double.PositiveInfinity;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        long TopX(int ae, long currentY)
        {
            var aeTop = actives.top[ae];
            var aeBot = actives.bot[ae];
            if ((currentY == aeTop.y) || (aeTop.x == aeBot.x)) return aeTop.x;
            else if (currentY == aeBot.y) return aeBot.x;
            else return aeBot.x + (long)math.round(actives.dx[ae] * (currentY - aeBot.y));
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
        bool IsHeadingLeftHorz(int ae)
        {
            return double.IsPositiveInfinity(actives.dx[ae]);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHeadingLeftHorz(in Active ae)
        {
            return (double.IsPositiveInfinity(ae.dx));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapActives(ref Active ae1, ref Active ae2)
        {
            Active ae = ae1;
            ae1 = ae2;
            ae2 = ae;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapActives(ref int ae1, ref int ae2)
        {
            //Then swap references for calling function
            int temp = ae1;
            ae1 = ae2;
            ae2 = temp;
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
        long2 GetIntersectPoint(int ae1, int ae2)
        {
            double b1, b2;
            var ae1Dx = actives.dx[ae1];
            var ae2Dx = actives.dx[ae2];
            var ae1Bot = actives.bot[ae1];
            var ae2Bot = actives.bot[ae2];
            if (ae1Dx == ae2Dx) return actives.top[ae1];

            if (ae1Dx == 0)
            {
                if (IsHorizontal(ae2)) return new long2(ae1Bot.x, ae2Bot.y);
                b2 = ae2Bot.y - (ae2Bot.x / ae2Dx);
                return new long2(ae1Bot.x, (long)math.round(ae1Bot.x / ae2Dx + b2));
            }
            else if (ae2Dx == 0)
            {
                if (IsHorizontal(ae1)) return new long2(ae2Bot.x, ae1Bot.y);
                b1 = ae1Bot.y - (ae1Bot.x / ae1Dx);
                return new long2(ae2Bot.x, (long)math.round(ae2Bot.x / ae1Dx + b1));
            }
            else
            {
                b1 = ae1Bot.x - ae1Bot.y * ae1Dx;
                b2 = ae2Bot.x - ae2Bot.y * ae2Dx;
                double q = (b2 - b1) / (ae1Dx - ae2Dx);
                return (math.abs(ae1Dx) < math.abs(ae2Dx))
                    ? new long2((long)math.round(ae1Dx * q + b1), (long)math.round(q))
                    : new long2((long)math.round(ae2Dx * q + b2), (long)math.round(q));
            }
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
        bool IsLeftBound(int ae)
        {
            return actives.leftBound[ae];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int NextVertex(int ae)
        {
            if (actives.windDx[ae] < 0)
                return vertexList.next[actives.vertexTop[ae]];
            else
                return vertexList.prev[actives.vertexTop[ae]];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int PrevPrevVertex(int ae)
        {
            if (actives.windDx[ae] < 0)
                return vertexList.prev[vertexList.prev[actives.vertexTop[ae]]];
            else
                return vertexList.next[vertexList.next[actives.vertexTop[ae]]];
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool IsMaxima(int ae)
        {
            return (vertexList.flags[actives.vertexTop[ae]] & VertexFlags.LocalMax) != VertexFlags.None;
        }
        int GetMaximaPair(int ae)
        {
            int ae2;
            if (IsHorizontal(ae))
            {
                //we can't be sure whether the MaximaPair is on the left or right, so ...
                ae2 = actives.prevInAEL[ae];
                while (ae2 != -1 && actives.curX[ae2] >= actives.top[ae].x)
                {
                    if (actives.vertexTop[ae2] == actives.vertexTop[ae]) return ae2;  //Found!
                    ae2 = actives.prevInAEL[ae2];
                }

                ae2 = actives.nextInAEL[ae];
                while (ae2 != -1 && TopX(ae2, actives.top[ae].y) <= actives.top[ae].x)
                {
                    if (actives.vertexTop[ae2] == actives.vertexTop[ae]) return ae2;  //Found!
                    ae2 = actives.nextInAEL[ae2];
                }
            }
            else
            {
                ae2 = actives.nextInAEL[ae];
                while (ae2 != -1)
                {
                    if (actives.vertexTop[ae2] == actives.vertexTop[ae]) return ae2;  //Found!
                    ae2 = actives.nextInAEL[ae2];
                }
            }

            return -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int PointCount(int op)
        {
            int p = op;
            int cnt = 0;
            do
            {
                cnt++;
                p = outPtList.next[p];
            } while (p != op);
            return cnt;
        }
        struct IntersectListSort : IComparer<IntersectNode>
        {
            public int Compare(IntersectNode a, IntersectNode b)
            {
                if (a.pt.y == b.pt.y)
                {
                    return a.pt.x < b.pt.x ? -1 : 1;
                }
                else
                {
                    return a.pt.y > b.pt.y ? -1 : 1;
                }
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
            int or1 = actives.outrec[ae1]; //at least one edge has 
            int or2 = actives.outrec[ae2]; //an assigned outrec
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
        private double Area(int op)
        {
            double area = 0.0;
            int op2 = op;
            do
            {
                var op2Pt = outPtList.pt[op2];
                var op2prevPt = outPtList.pt[outPtList.prev[op2]];

                area += (double)(op2Pt.y - op2prevPt.y) *
                    (op2Pt.x + op2prevPt.x);
                op2 = outPtList.next[op2];
            } while (op2 != op);

            return area * 0.5;
        }
        void ReverseOutPts(int op)
        {
            int op1 = op;
            int op2;

            do
            {
                op2 = outPtList.next[op1];
                outPtList.next[op1] = outPtList.prev[op1];
                outPtList.prev[op1] = op2;
                op1 = op2;
            } while (op1 != op);
        }


        private bool CheckFixInnerOuter(int ae)
        {
            bool wasOuter = IsOuter(actives.outrec[ae]), isOuter = true;

            int ae2 = actives.prevInAEL[ae];
            while (ae2 != -1)
            {
                if (IsHotEdge(ae2) && !IsOpen(ae2)) isOuter = !isOuter;
                ae2 = actives.prevInAEL[ae2];
            }

            if (isOuter == wasOuter) return false;

            if (isOuter)
                SetAsOuter(actives.outrec[ae]);
            else
                SetAsInner(actives.outrec[ae]);

            //now check and fix ownership
            ae2 = GetPrevHotEdge(ae);
            if (isOuter)
            {
                if (ae2 != -1 && IsInner(actives.outrec[ae2]))
                    outrecList.owner[actives.outrec[ae]] = actives.outrec[ae2];
                else
                    outrecList.owner[actives.outrec[ae]] = -1;
            }
            else
            {
                if (ae2 == -1)
                    SetAsOuter(actives.outrec[ae]);
                else if (IsInner(actives.outrec[ae2]))
                    outrecList.owner[actives.outrec[ae]] = outrecList.owner[actives.outrec[ae2]];
                else
                    outrecList.owner[actives.outrec[ae]] = actives.outrec[ae2];
            }

            if ((Area(outrecList.pts[actives.outrec[ae]]) < 0.0) == isOuter)
                ReverseOutPts(outrecList.pts[actives.outrec[ae]]);
            return true;
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
        void SwapSides(int outrec)
        {
            int ae2 = outrecList.frontEdge[outrec];
            outrecList.frontEdge[outrec] = outrecList.backEdge[outrec];
            outrecList.backEdge[outrec] = ae2;
            outrecList.pts[outrec] = outPtList.next[outrecList.pts[outrec]];
        }

        private bool FixSides(int ae1, int ae2)
        {
            if (ValidateClosedPathEx(outrecList.pts[actives.outrec[ae1]]) &&
                ValidateClosedPathEx(outrecList.pts[actives.outrec[ae2]]))
            {
                if (CheckFixInnerOuter(ae1) &&
                    IsOuter(actives.outrec[ae1]) == IsFront(ae1))
                    SwapSides(actives.outrec[ae1]);
                else if (CheckFixInnerOuter(ae2) &&
                    IsOuter(actives.outrec[ae2]) == IsFront(ae2))
                    SwapSides(actives.outrec[ae2]);
                else
                    throw new ClipperLibException("Error in Clipper.AddLocalMaxPoly");
                return true;
            }
            else if (outrecList.pts[actives.outrec[ae1]] == -1)
            {
                if (outrecList.pts[actives.outrec[ae2]] != -1 && ValidateClosedPathEx(outrecList.pts[actives.outrec[ae2]]))
                    throw new ClipperLibException("Error in Clipper.AddLocalMaxPoly");
                UncoupleOutRec(ae1);
                UncoupleOutRec(ae2);
                //fixed, but there's nothing to terminate in AddLocalMaxPoly
                return false;
            }
            else
                throw new ClipperLibException("Error in Clipper.AddLocalMaxPoly");
        }

        private void SetOwnerAndInnerOuterState(int ae)
        {
            int ae2;
            int outrec = actives.outrec[ae];

            if (IsOpen(ae))
            {
                outrecList.owner[outrec] = -1;
                outrecList.state[outrec] = OutRecState.Open;
                return;
            }

            //set owner ...
            if (IsHeadingLeftHorz(ae))
            {
                ae2 = actives.nextInAEL[ae];  //ie assess state from opposite direction
                while (ae2 != -1 && (!IsHotEdge(ae2) || IsOpen(ae2)))
                    ae2 = actives.nextInAEL[ae2];
                if (ae2 == -1)
                    outrecList.owner[outrec] = -1;
                else
                {
                    var ae2Outrec = actives.outrec[ae2];
                    if ((outrecList.state[ae2Outrec] == OutRecState.Outer) == (outrecList.frontEdge[ae2Outrec] == ae2))
                        outrecList.owner[outrec] = outrecList.owner[ae2Outrec];
                    else
                        outrecList.owner[outrec] = ae2Outrec;
                }

            }
            else
            {
                ae2 = GetPrevHotEdge(ae);
                while (ae2 != -1 && (!IsHotEdge(ae2) || IsOpen(ae2)))
                    ae2 = actives.prevInAEL[ae2];
                if (ae2 == -1)
                    outrecList.owner[outrec] = -1;
                else
                {
                    var ae2Outrec = actives.outrec[ae2];
                    if (IsOuter(ae2Outrec) == (outrecList.backEdge[ae2Outrec] == ae2))
                        outrecList.owner[outrec] = outrecList.owner[ae2Outrec];
                    else
                        outrecList.owner[outrec] = ae2Outrec;
                }
            }

            //set inner/outer ...
            if (outrecList.owner[outrec] == -1 || IsInner(outrecList.owner[outrec]))
                outrecList.state[outrec] = OutRecState.Outer;
            else
                outrecList.state[outrec] = OutRecState.Inner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool EdgesAdjacentInAEL(IntersectNode inode)
        {
            return actives.nextInAEL[inode.edge1] == inode.edge2 || actives.prevInAEL[inode.edge1] == inode.edge2;
        }

        void ClearSolution()
        {
            if (actives.IsCreated)
            {
                actives.Clear();
                actives_ID = -1;
                sel_ID = -1;
            }
            if (joinerList.IsCreated)
            {
                joinerList.Clear();
                horzJoiners = -1;
            }
            if (intersectList.IsCreated) intersectList.Clear();
            if (outrecList.IsCreated) outrecList.Clear();
            if (outPtList.IsCreated) outPtList.Clear();
            if (scanlineList.IsCreated) scanlineList.Clear();
        }

        public void Clear()
        {
            ClearSolution();
            if (minimaList.IsCreated) minimaList.Clear();
            if (vertexList.IsCreated) vertexList.Clear();
            currentLocMin = 0;
            isSortedMinimaList = false;
            hasOpenPaths = false;
        }

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
                sel_ID = -1;
            }
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
            //make sure the vertex is added only once ...
            if ((vertexList.flags[vert] & VertexFlags.LocalMin) != VertexFlags.None) return;
            vertexList.flags[vert] |= VertexFlags.LocalMin;

            LocalMinima lm = new LocalMinima(vert, vertexList.pt[vert], polytype, isOpen);
            minimaList.Add(lm);
        }
        void AddPathsToVertexList(in Polygon path, PathType polytype, bool isOpen)
        {
            int pathCnt = path.startIDs.Length - 1;
            int totalVertCnt = path.nodes.Length;
            int newSize = vertexList.pt.Length + totalVertCnt;
            vertexList.pt.Capacity = newSize;
            vertexList.flags.Capacity = newSize;
            vertexList.prev.Capacity = newSize;
            vertexList.next.Capacity = newSize;

            for (int ComponentID = 0; ComponentID < pathCnt; ComponentID++) //for each component of Poly
            {
                int v0 = -1, prev_v = -1, curr_v;
                int start = path.startIDs[ComponentID];
                int end = path.startIDs[ComponentID + 1];
                for (int i = start; i < end; i++)
                {
                    var pt = new long2(path.nodes[i], _scale);
                    if (v0 == -1)
                    {
                        v0 = vertexList.AddVertex(pt, VertexFlags.None, true);
                        prev_v = v0;
                    }
                    else if (vertexList.pt[prev_v] != pt) //ie skips duplicates
                    {
                        prev_v = vertexList.AddVertex(pt, VertexFlags.None, false, v0);
                    }
                }
                if (prev_v == -1 || vertexList.prev[prev_v] == -1) continue;
                if (!isOpen && vertexList.pt[prev_v] == vertexList.pt[v0]) prev_v = vertexList.prev[prev_v]; //this is not needed (Addvertex does this already)
                vertexList.next[prev_v] = v0; //this is not needed (Addvertex does this already)
                vertexList.prev[v0] = prev_v;//this is not needed (Addvertex does this already)
                if (!isOpen && vertexList.next[prev_v] == vertexList.prev[prev_v]) continue;

                //OK, we have a valid path
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
                else //closed path
                {
                    prev_v = vertexList.prev[v0];
                    while (prev_v != v0 && vertexList.pt[prev_v].y == vertexList.pt[v0].y)
                        prev_v = vertexList.prev[prev_v];
                    if (prev_v == v0)
                        continue; //only open paths can be completely flat
                    going_up = vertexList.pt[prev_v].y > vertexList.pt[v0].y;
                }

                going_up0 = going_up;
                prev_v = v0;
                curr_v = vertexList.next[v0];
                while (curr_v != v0)
                {
                    if (vertexList.pt[curr_v].y > vertexList.pt[prev_v].y && going_up)
                    {
                        vertexList.flags[prev_v] = (vertexList.flags[prev_v] | VertexFlags.LocalMax);
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
                    vertexList.flags[prev_v] = vertexList.flags[prev_v] | VertexFlags.OpenEnd;
                    if (going_up)
                        vertexList.flags[prev_v] = vertexList.flags[prev_v] | VertexFlags.LocalMax;
                    else
                        AddLocMin(prev_v, polytype, isOpen);
                }
                else if (going_up != going_up0)
                {
                    if (going_up0) AddLocMin(prev_v, polytype, false);
                    else vertexList.flags[prev_v] = vertexList.flags[prev_v] | VertexFlags.LocalMax;
                }
            }
        }
        void AddPathToVertexList(in NativeSlice<int2> nodes, PathType polytype, bool isOpen)
        {
            int totalVertCnt = nodes.Length;
            int newSize = vertexList.pt.Length + totalVertCnt;
            vertexList.pt.Capacity = newSize;
            vertexList.flags.Capacity = newSize;
            vertexList.prev.Capacity = newSize;
            vertexList.next.Capacity = newSize;

            int v0 = -1, prev_v = -1, curr_v;
            for (int i = 0; i < totalVertCnt; i++)
            {
                var pt = new long2(nodes[i], _scale);
                if (v0 == -1)
                {
                    v0 = vertexList.AddVertex(pt, VertexFlags.None, true);
                    prev_v = v0;
                }
                else if (vertexList.pt[prev_v] != pt) //ie skips duplicates
                {
                    prev_v = vertexList.AddVertex(pt, VertexFlags.None, false, v0);
                }
            }
            if (prev_v == -1 || vertexList.prev[prev_v] == -1) return;
            if (!isOpen && vertexList.pt[prev_v] == vertexList.pt[v0]) prev_v = vertexList.prev[prev_v]; //this is not needed (Addvertex does this already)
            vertexList.next[prev_v] = v0; //this is not needed (Addvertex does this already)
            vertexList.prev[v0] = prev_v;//this is not needed (Addvertex does this already)
            if (!isOpen && vertexList.next[prev_v] == vertexList.prev[prev_v]) return;

            //OK, we have a valid path
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
            else //closed path
            {
                prev_v = vertexList.prev[v0];
                while (prev_v != v0 && vertexList.pt[prev_v].y == vertexList.pt[v0].y)
                    prev_v = vertexList.prev[prev_v];
                if (prev_v == v0)
                    return; //only open paths can be completely flat
                going_up = vertexList.pt[prev_v].y > vertexList.pt[v0].y;
            }

            going_up0 = going_up;
            prev_v = v0;
            curr_v = vertexList.next[v0];
            while (curr_v != v0)
            {
                if (vertexList.pt[curr_v].y > vertexList.pt[prev_v].y && going_up)
                {
                    vertexList.flags[prev_v] = (vertexList.flags[prev_v] | VertexFlags.LocalMax);
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
                vertexList.flags[prev_v] = vertexList.flags[prev_v] | VertexFlags.OpenEnd;
                if (going_up)
                    vertexList.flags[prev_v] = vertexList.flags[prev_v] | VertexFlags.LocalMax;
                else
                    AddLocMin(prev_v, polytype, isOpen);
            }
            else if (going_up != going_up0)
            {
                if (going_up0) AddLocMin(prev_v, polytype, false);
                else vertexList.flags[prev_v] = vertexList.flags[prev_v] | VertexFlags.LocalMax;
            }

        }
        public void AddSubject(in Polygon paths)
        {
            AddPaths(paths, PathType.Subject, false);
        }
        public void AddOpenSubject(in Polygon paths)
        {
            AddPaths(paths, PathType.Subject, true);
        }
        public void AddClip(in Polygon paths)
        {
            AddPaths(paths, PathType.Clip, false);
        }
        public void AddPath(in NativeSlice<int2> path, PathType polytype, bool isOpen = false)
        {
            hasOpenPaths = isOpen;
            isSortedMinimaList = false;
            AddPathToVertexList(path, polytype, isOpen);
        }
        public void AddPaths(in Polygon path, PathType polytype, bool isOpen = false)
        {
            if (isOpen) hasOpenPaths = true;
            isSortedMinimaList = false;
            AddPathsToVertexList(path, polytype, isOpen);
        }
        bool IsContributingClosed(int ae)
        {
            switch (fillrule)
            {
                case FillRule.NonZero:
                    if (math.abs(actives.windCount[ae]) != 1) return false;
                    break;
                case FillRule.Positive:
                    if (actives.windCount[ae] != 1) return false;
                    break;
                case FillRule.Negative:
                    if (actives.windCount[ae] != -1) return false;
                    break;
                case FillRule.EvenOdd:
                    break;
            }
            switch (cliptype)
            {
                case ClipType.Intersection:
                    switch (fillrule)
                    {
                        case FillRule.EvenOdd:
                        case FillRule.NonZero: return actives.windCount2[ae] != 0;
                        case FillRule.Positive: return actives.windCount2[ae] > 0;
                        case FillRule.Negative: return actives.windCount2[ae] < 0;
                    }
                    break;
                case ClipType.Union:
                    switch (fillrule)
                    {
                        case FillRule.EvenOdd:
                        case FillRule.NonZero: return actives.windCount2[ae] == 0;
                        case FillRule.Positive: return actives.windCount2[ae] <= 0;
                        case FillRule.Negative: return actives.windCount2[ae] >= 0;
                    }
                    break;
                case ClipType.Difference:
                    if (GetPolyType(ae) == PathType.Subject)
                        switch (fillrule)
                        {
                            case FillRule.EvenOdd:
                            case FillRule.NonZero: return actives.windCount2[ae] == 0;
                            case FillRule.Positive: return actives.windCount2[ae] <= 0;
                            case FillRule.Negative: return actives.windCount2[ae] >= 0;
                        }
                    else
                        switch (fillrule)
                        {
                            case FillRule.EvenOdd:
                            case FillRule.NonZero: return actives.windCount2[ae] != 0;
                            case FillRule.Positive: return actives.windCount2[ae] > 0;
                            case FillRule.Negative: return actives.windCount2[ae] < 0;
                        }
                    break;
                case ClipType.Xor:
                    return true; //XOR is always contributing unless open
                default:
                    return false;
            }

            return false;  //we should never get here
        }

        bool IsContributingOpen(int ae)
        {
            return cliptype switch
            {
                ClipType.Intersection => (actives.windCount2[ae] != 0),
                ClipType.Union => (actives.windCount[ae] == 0 && actives.windCount2[ae] == 0),
                ClipType.Difference => (actives.windCount2[ae] == 0),
                ClipType.Xor => (actives.windCount[ae] != 0) != (actives.windCount2[ae] != 0),
                _ => false,
            };
        }
        void SetWindCountForClosedPathEdge(int ae)
        {
            //Wind counts refer to polygon regions not edges, so here an edge's WindCnt
            //indicates the higher of the wind counts for the two regions touching the
            //edge. (nb: Adjacent regions can only ever have their wind counts differ by
            //one. Also, open paths have no meaningful wind directions or counts.)

            int ae2 = actives.prevInAEL[ae];
            //find the nearest closed path edge of the same PolyType in AEL (heading left)
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
                //NonZero, positive, or negative filling here ...
                //if e's WindCnt is in the SAME direction as its WindDx, then polygon
                //filling will be on the right of 'e'.
                //nb: neither e2.WindCnt nor e2.WindDx should ever be 0.
                if (actives.windCount[ae2] * actives.windDx[ae2] < 0)
                {
                    //opposite directions so 'e' is outside 'e2' ...
                    if (math.abs(actives.windCount[ae2]) > 1)
                    {
                        //outside prev poly but still inside another.
                        if (actives.windDx[ae2] * actives.windDx[ae] < 0)
                            //reversing direction so use the same WC
                            actives.windCount[ae] = actives.windCount[ae2];
                        else
                            //otherwise keep 'reducing' the WC by 1 (ie towards 0)
                            actives.windCount[ae] = actives.windCount[ae2] + actives.windDx[ae];
                    }
                    else
                        //now outside all polys of same polytype so set own WC ...
                        actives.windCount[ae] = (IsOpen(ae) ? 1 : actives.windDx[ae]);
                }
                else
                {
                    //'ae' must be inside 'e2'
                    if (actives.windDx[ae2] * actives.windDx[ae] < 0)
                        //reversing direction so use the same WC
                        actives.windCount[ae] = actives.windCount[ae2];
                    else
                        //otherwise keep 'increasing' the WC by 1 (ie away from 0) ...
                        actives.windCount[ae] = actives.windCount[ae2] + actives.windDx[ae];
                }

                actives.windCount2[ae] = actives.windCount2[ae2];
                ae2 = actives.nextInAEL[ae2];  //ie get ready to calc WindCnt2
            }

            //update wind_cnt2 ...
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

        void SetWindCountForOpenPathEdge(int ae)
        {
            int ae2 = ae;
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

        private bool IsValidAelOrder(int a1, int a2)
        {
            //a2 is always the new edge being inserted
            var a1CurX = actives.curX[a1];
            var a2CurX = actives.curX[a2];
            if (a2CurX != a1CurX)
                return a2CurX > a1CurX;

            var a1Top = actives.top[a1];
            var a1Bot = actives.bot[a1];
            var a2Top = actives.top[a2];
            var a2Bot = actives.bot[a2];

            //get the turning direction  a1.top, a2.bot, a2.top
            double d = InternalClipperFunc.CrossProduct(a1Top, a2Bot, a2Top);

            if (d < 0) return true;
            else if (d > 0) return false;

            //edges must be collinear to get here

            //for starting open paths, place them according to
            //the direction they're about to turn
            if (IsOpen(a1) && !IsMaxima(a1) && (a1Bot.y <= a2Bot.y) &&
                !IsSamePolyType(a1, a2) && (a1Top.y > a2Top.y))
                return InternalClipperFunc.CrossProduct(
                    a1Bot, a1Top, vertexList.pt[NextVertex(a1)]) <= 0;
            else if (IsOpen(a2) && !IsMaxima(a2) && (a2Bot.y <= a1Bot.y) &&
                !IsSamePolyType(a1, a2) && (a2Top.y > a1Top.y))
                return InternalClipperFunc.CrossProduct(
                    a2Bot, a2Top, vertexList.pt[NextVertex(a2)]) >= 0;

            bool a2IsLeftBound = IsLeftBound(a2);
            bool a1IsNewEdge = !IsOpen(a1) &&
                                (a1Bot.y == a2Bot.y) && (minimaList[actives.localMin[a1]].vertex.y == a2Bot.y);

            if (a1IsNewEdge)
            {
                if (IsLeftBound(a1) != a2IsLeftBound)
                    return a2IsLeftBound;
                else if (InternalClipperFunc.CrossProduct(vertexList.pt[PrevPrevVertex(a1)], a1Bot, a1Top) == 0)
                    return true; //a1 is a spike so effectively we can ignore it 
                else
                    //compare turning direction of alternate bound
                    return (InternalClipperFunc.CrossProduct(vertexList.pt[PrevPrevVertex(a1)],
                        a2Bot, vertexList.pt[PrevPrevVertex(a2)]) > 0) == a2IsLeftBound;

            }

            return a2IsLeftBound;
        }

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
            //Add any local minima (if any) at BotY ...
            //NB horizontal local minima edges should contain locMin.vertex.prev
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
                        windDx = 1,
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
                        windDx = -1,
                        vertexTop = vertexList.next[localMinima.vertex_ID], //i.e. ascending
                        top = vertexList.pt[vertexList.next[localMinima.vertex_ID]],
                        outrec = -1,
                        locMin_ID = localMinima_ID
                    };
                    SetDx(ref rightBound);
                }

                //Currently LeftB is just the descending bound and RightB is the ascending.
                //Now if the LeftB isn't on the left of RightB then we need swap them.
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
                }
                else if (!leftBoundExists)
                {
                    leftBound = rightBound;
                    rightBound = default;
                    rightBoundExists = false;
                }

                bool contributing;
                leftBound.leftBound = true;
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
                        if (!IsHorizontal(leftBound_ID) && TestJoinWithPrev1(leftBound_ID, botY))
                        {
                            var op = AddOutPt(actives.prevInAEL[leftBound_ID], actives.bot[leftBound_ID]);
                            AddJoin(op, outrecList.pts[actives.outrec[leftBound_ID]]);
                        }
                    }

                    while (actives.nextInAEL[rightBound_ID] != -1 &&
                            IsValidAelOrder(actives.nextInAEL[rightBound_ID], rightBound_ID))
                    {
                        IntersectEdges(rightBound_ID, actives.nextInAEL[rightBound_ID], actives.bot[rightBound_ID]);
                        SwapPositionsInAEL(rightBound_ID, actives.nextInAEL[rightBound_ID]);
                    }

                    if (!IsHorizontal(rightBound_ID) && TestJoinWithNext1(rightBound_ID, botY))
                    {
                        var op = AddOutPt(actives.nextInAEL[rightBound_ID], actives.bot[rightBound_ID]);
                        AddJoin(outrecList.pts[actives.outrec[rightBound_ID]], op);
                    }

                    if (IsHorizontal(rightBound_ID))
                        PushHorz(rightBound_ID);
                    else
                        InsertScanline(actives.top[rightBound_ID].y);
                }
                else if (contributing)
                    StartOpenPath(leftBound_ID, actives.bot[leftBound_ID]);

                if (IsHorizontal(leftBound_ID))
                    PushHorz(leftBound_ID);
                else
                    InsertScanline(leftBound.top.y);
            } //while (HasLocMinAtY())
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
        private bool TestJoinWithPrev1(int e, long currY)
        {
            //this is marginally quicker than TestJoinWithPrev2
            //but can only be used when e.PrevInAEL.currX is accurate
            var prevInAEL = actives.prevInAEL[e];
            return IsHotEdge(e) && !IsOpen(e) &&
                    (prevInAEL != -1) && (actives.curX[prevInAEL] == actives.curX[e]) &&
                    IsHotEdge(prevInAEL) && !IsOpen(prevInAEL) &&
                    (currY - actives.top[e].y > 1) && (currY - actives.top[prevInAEL].y > 1) &&
                    (InternalClipperFunc.CrossProduct(actives.top[prevInAEL], actives.bot[e], actives.top[e]) == 0);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TestJoinWithPrev2(int e, long2 currPt)
        {
            var prevInAEL = actives.prevInAEL[e];
            return IsHotEdge(e) && !IsOpen(e) &&
                    (prevInAEL != -1) && !IsOpen(prevInAEL) &&
                    IsHotEdge(prevInAEL) && (actives.top[prevInAEL].y < actives.bot[e].y) &&
                    (math.abs(TopX(prevInAEL, currPt.y) - currPt.x) < 2) &&
                    (InternalClipperFunc.CrossProduct(actives.top[prevInAEL], currPt, actives.top[e]) == 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TestJoinWithNext1(int e, long currY)
        {
            //this is marginally quicker than TestJoinWithNext2
            //but can only be used when e.NextInAEL.currX is accurate
            var nextInAEL = actives.nextInAEL[e];
            return IsHotEdge(e) && !IsOpen(e) &&
                    (nextInAEL != -1) && (actives.curX[nextInAEL] == actives.curX[e]) &&
                    IsHotEdge(nextInAEL) && !IsOpen(nextInAEL) &&
                    (currY - actives.top[e].y > 1) && (currY - actives.top[nextInAEL].y > 1) &&
                    (InternalClipperFunc.CrossProduct(actives.top[nextInAEL], actives.bot[e], actives.top[e]) == 0);
        }

        private bool TestJoinWithNext2(int e, long2 currPt)
        {
            var nextInAEL = actives.nextInAEL[e];
            return IsHotEdge(e) && !IsOpen(e) &&
                    (nextInAEL != -1) && !IsOpen(nextInAEL) &&
                    IsHotEdge(nextInAEL) && (actives.top[nextInAEL].y < actives.bot[e].y) &&
                    (math.abs(TopX(nextInAEL, currPt.y) - currPt.x) < 2) &&
                    (InternalClipperFunc.CrossProduct(actives.top[nextInAEL], currPt, actives.top[e]) == 0);
        }

        int AddLocalMinPoly(int ae1, int ae2, long2 pt, bool isNew = false)
        {
            int outrec = outrecList.AddOutRec(-1, OutRecState.Undefined, -1);
            outrecList.polypath[outrec] = -1;

            actives.outrec[ae1] = outrec;
            SetOwnerAndInnerOuterState(ae1);
            //flag when orientation needs to be rechecked later ...
            actives.outrec[ae2] = outrec;

            if (!IsOpen(ae1))
            {
                //Setting the owner and inner/outer states (above) is an essential
                //precursor to setting edge 'sides' (i.e. left and right sides of output
                //polygons) and hence the orientation of output paths ...
                if (IsOuter(outrec) == isNew)
                    SetSides(outrec, ae1, ae2);
                else
                    SetSides(outrec, ae2, ae1);
            }

            int op = outPtList.NewOutPt(pt, outrec);
            outrecList.pts[outrec] = op;
            return op;
        }

        private int AddLocalMaxPoly(int ae1, int ae2, long2 pt)
        {
            if (IsFront(ae1) == IsFront(ae2))
            {
                //we should practically never get here but in case something is wrong
                if (IsOpen(ae1))
                    SwapSides(actives.outrec[ae2]);
                else if (!FixSides(ae1, ae2))
                    return -1;
            }

            int result = AddOutPt(ae1, pt);
            if (actives.outrec[ae1] == actives.outrec[ae2])
            {
                var outrec = actives.outrec[ae1];
                outrecList.pts[outrec] = result;
                UncoupleOutRec(ae1);
                if (!IsOpen(ae1))
                    CleanCollinear(outrec);
                result = outrecList.pts[outrec];
            }
            //and to preserve the winding orientation of outrec ...
            else if (actives.outrec[ae1] < actives.outrec[ae2])
                JoinOutrecPaths(ae1, ae2);
            else
                JoinOutrecPaths(ae2, ae1);

            return result;
        }

        private void JoinOutrecPaths(int ae1, int ae2)
        {
            //join ae2 outrec path onto ae1 outrec path and then delete ae2 outrec path
            //pointers. (NB Only very rarely do the joining ends share the same coords.)
            var p1Start = outrecList.pts[actives.outrec[ae1]];
            var p2Start = outrecList.pts[actives.outrec[ae2]];
            var p1End = outPtList.next[p1Start];
            var p2End = outPtList.next[p2Start];
            if (IsFront(ae1))
            {
                outPtList.prev[p2End] = p1Start;
                outPtList.next[p1Start] = p2End;
                outPtList.next[p2Start] = p1End;
                outPtList.prev[p1End] = p2Start;
                outrecList.pts[actives.outrec[ae1]] = p2Start;
                if (!IsOpen(ae1))
                {
                    outrecList.frontEdge[actives.outrec[ae1]] = outrecList.frontEdge[actives.outrec[ae2]];
                    actives.outrec[outrecList.frontEdge[actives.outrec[ae1]]] = actives.outrec[ae1];
                }
            }
            else
            {
                outPtList.prev[p1End] = p2Start;
                outPtList.next[p2Start] = p1End;
                outPtList.next[p1Start] = p2End;
                outPtList.prev[p2End] = p1Start;

                if (!IsOpen(ae1))
                {
                    outrecList.backEdge[actives.outrec[ae1]] = outrecList.backEdge[actives.outrec[ae2]];
                    actives.outrec[outrecList.backEdge[actives.outrec[ae1]]] = actives.outrec[ae1];
                }
            }

            var ae2outrec = actives.outrec[ae2];
            var ae1outrec = actives.outrec[ae1];

            //after joining, the ae2.OutRec must contains no vertices ...
            outrecList.frontEdge[ae2outrec] = -1;
            outrecList.backEdge[ae2outrec] = -1;
            outrecList.pts[ae2outrec] = -1;
            outrecList.owner[ae2outrec] = ae1outrec; //this may be redundant

            if (IsOpenEnd(ae1))
            {
                outrecList.pts[ae2outrec] = outrecList.pts[ae1outrec];
                outrecList.pts[ae1outrec] = -1;
            }

            //and ae1 and ae2 are maxima and are about to be dropped from the Actives list.
            actives.outrec[ae1] = -1;
            actives.outrec[ae2] = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddOutPt(int ae, long2 pt)
        {
            int newOp;
            //Debug.Log(pt);
            //if (pt.x == -76616333000 && pt.y == 34866828900)
            //{ }
            //Outrec.OutPts: a circular doubly-linked-list of POutPt where ...
            //opFront[.Prev]* ~~~> opBack & opBack == opFront.Next
            var outrec = actives.outrec[ae];
            bool toFront = IsFront(ae);
            var opFront = outrecList.pts[outrec];
            var opBack = outPtList.next[opFront];

            if (toFront && (pt == outPtList.pt[opFront])) newOp = opFront;
            else if (!toFront && (pt == outPtList.pt[opBack])) newOp = opBack;
            else
            {
                newOp = outPtList.NewOutPt(pt, outrec);
                outPtList.prev[opBack] = newOp;
                outPtList.prev[newOp] = opFront;
                outPtList.next[newOp] = opBack;
                outPtList.next[opFront] = newOp;
                if (toFront) outrecList.pts[outrec] = newOp;
            }
            return newOp;
        }

        int StartOpenPath(int ae, long2 pt)
        {
            int outrec = outrecList.AddOutRec(-1, OutRecState.Open, -1);

            actives.outrec[ae] = outrec;

            int op = outPtList.NewOutPt(pt, outrec);
            outrecList.pts[outrec] = op;
            return op;
        }

        private void UpdateEdgeIntoAEL(int ae)
        {
            actives.bot[ae] = actives.top[ae];
            actives.vertexTop[ae] = NextVertex(ae);
            actives.top[ae] = vertexList.pt[actives.vertexTop[ae]];
            actives.curX[ae] = actives.bot[ae].x;
            SetDx(ae);
            if (IsHorizontal(ae)) return;
            InsertScanline(actives.top[ae].y);
            if (TestJoinWithPrev1(ae, actives.bot[ae].y))
            {
                var op1 = AddOutPt(actives.prevInAEL[ae], actives.bot[ae]);
                var op2 = AddOutPt(ae, actives.bot[ae]);
                AddJoin(op1, op2);
            }
        }

        private int IntersectEdges(int ae1, int ae2, long2 pt)
        {
            int resultOp = -1;
            var ae1IsOpen = IsOpen(ae1);
            var ae2IsOpen = IsOpen(ae2);
            //MANAGE OPEN PATH INTERSECTIONS SEPARATELY ...
            if (hasOpenPaths && (ae1IsOpen || ae2IsOpen))
            {
                if (ae1IsOpen && ae2IsOpen) return -1;
                if (ae2IsOpen)
                    SwapActives(ref ae1, ref ae2);
                switch (cliptype)
                {
                    case ClipType.Intersection:
                    case ClipType.Difference:
                        if (IsSamePolyType(ae1, ae2) || (math.abs(actives.windCount[ae2]) != 1)) return -1;
                        break;
                    case ClipType.Union:
                        if (IsHotEdge(ae1) != ((math.abs(actives.windCount[ae2]) != 1) ||
                                                (IsHotEdge(ae1) != (actives.windCount[ae2] != 0)))) return -1;
                        //it just works!
                        break;
                    case ClipType.Xor:
                        if (math.abs(actives.windCount[ae2]) != 1) return -1;
                        break;
                    case ClipType.None:
                        throw new ClipperLibException("Error in IntersectEdges - ClipType is None!");
                }

                //toggle contribution ...
                if (IsHotEdge(ae1))
                {
                    resultOp = AddOutPt(ae1, pt);
                    actives.outrec[ae1] = -1;
                }
                else
                {
                    resultOp = StartOpenPath(ae1, pt);
                }

                return resultOp;
            }

            //UPDATE WINDING COUNTS...

            int oldE1WindCount, oldE2WindCount;
            var ae1PathType= minimaList[actives.localMin[ae1]].polytype;
            var ae2PathType = minimaList[actives.localMin[ae2]].polytype;
            if (ae1PathType == ae2PathType)
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

            //NOW PROCESS THE INTERSECTION ...

            //if both edges are 'hot' ...
            if (IsHotEdge(ae1) && IsHotEdge(ae2))
            {
                if ((oldE1WindCount != 0 && oldE1WindCount != 1) || (oldE2WindCount != 0 && oldE2WindCount != 1) ||
                    (ae1PathType != ae2PathType && cliptype != ClipType.Xor))
                {
                    resultOp = AddLocalMaxPoly(ae1, ae2, pt);
                }
                else if (IsFront(ae1) || (actives.outrec[ae1] == actives.outrec[ae2]))
                {
                    //this else condition isn't strictly needed but
                    //it's easier to join polygons than break apart complex ones
                    resultOp = AddLocalMaxPoly(ae1, ae2, pt);
                    var op2 = AddLocalMinPoly(ae1, ae2, pt);

                    if (resultOp != -1 && outPtList.pt[resultOp] == outPtList.pt[op2] &&
                        !IsHorizontal(ae1) && !IsHorizontal(ae2) &&
                        (InternalClipperFunc.CrossProduct(actives.bot[ae1], outPtList.pt[resultOp], actives.bot[ae2]) == 0))
                        AddJoin(resultOp, op2);
                }
                else
                {
                    //can't treat as maxima & minima
                    resultOp = AddOutPt(ae1, pt);
                    var op2 = AddOutPt(ae2, pt);
                    SwapOutrecs(ae1, ae2);
                }
            }

            //if one or other edge is 'hot' ...
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
            else
            {
                //neither edge is 'hot'
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
                    resultOp = AddLocalMinPoly(ae1, ae2, pt, false);
                }
                else if (oldE1WindCount == 1 && oldE2WindCount == 1)
                {
                    resultOp = -1;
                    switch (cliptype)
                    {
                        case ClipType.Union:
                            if (e1Wc2 > 0 && e2Wc2 > 0) return -1;
                            resultOp = AddLocalMinPoly(ae1, ae2, pt, false);
                            break;

                        case ClipType.Difference:
                            if (((GetPolyType(ae1) == PathType.Clip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((GetPolyType(ae1) == PathType.Subject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                            {
                                resultOp = AddLocalMinPoly(ae1, ae2, pt, false);
                            }

                            break;

                        case ClipType.Xor:
                            resultOp = AddLocalMinPoly(ae1, ae2, pt, false);
                            break;

                        default: //ClipType.Intersection:
                            if (e1Wc2 <= 0 || e2Wc2 <= 0) return -1;
                            resultOp = AddLocalMinPoly(ae1, ae2, pt, false);
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
            if (prev == -1 && next == -1 && (ae != actives_ID)) return; //already deleted
            if (prev != -1)
                actives.nextInAEL[prev] = next;
            else
                actives_ID = next;
            if (next != -1) actives.prevInAEL[next] = prev;
            //delete &ae;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdjustCurrXAndCopyToSEL(long topY)
        {
            var ae = actives_ID;
            sel_ID = ae;
            while (ae != -1)
            {
                var nextInAEL = actives.nextInAEL[ae];
                actives.prevInSEL[ae] = actives.prevInAEL[ae];
                actives.nextInSEL[ae] = nextInAEL;
                actives.jump[ae] = nextInAEL;
                actives.curX[ae] = TopX(ae, topY);
                //NB don't update ae.curr.y yet (see AddNewIntersectNode)
                ae = nextInAEL;
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
            for (; ; )
            {
                InsertLocalMinimaIntoAEL(y);
                int ae;
                while (PopHorz(out ae)) DoHorizontal(ae);
                ConvertHorzTrialsToJoins();
                currentBotY = y; //bottom of scanbeam
                if (!PopScanline(out y))
                    break; //y new top of scanbeam
                DoIntersections(y);
                DoTopOfScanbeam(y);
                while (PopHorz(out ae)) DoHorizontal(ae);
            }

            ProcessJoinList();
        }

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
            long2 pt = GetIntersectPoint(ae1, ae2);

            //rounding errors can occasionally place the calculated intersection
            //point either below or above the scanbeam, so check and correct ...
            if (pt.y > currentBotY)
            {
                //ae.curr.y is still the bottom of scanbeam
                //use the more vertical of the 2 edges to derive pt.x ...
                if (math.abs(actives.dx[ae1]) < math.abs(actives.dx[ae2]))
                    pt = new long2(TopX(ae1, currentBotY), currentBotY);
                else
                    pt = new long2(TopX(ae2, currentBotY), currentBotY);
            }
            else if (pt.y < topY)
            {
                //topY is at the top of the scanbeam
                if (actives.top[ae1].y == topY)
                    pt = new long2(actives.top[ae1].x, topY);
                else if (actives.top[ae2].y == topY)
                    pt = new long2(actives.top[ae2].x, topY);
                else if (math.abs(actives.dx[ae1]) < math.abs(actives.dx[ae2]))
                    pt = new long2(actives.curX[ae1], topY);
                else
                    pt = new long2(actives.curX[ae2], topY);
            }

            IntersectNode node = new IntersectNode(pt, ae1, ae2);
            intersectList.Add(node);
        }

        private int ExtractFromSEL(int ae)
        {
            var aeNextInSEL = actives.nextInSEL[ae];
            var aePrevInSEL = actives.prevInSEL[ae];
            if (aeNextInSEL != -1)
                actives.prevInSEL[aeNextInSEL] = aePrevInSEL;
            actives.nextInSEL[aePrevInSEL] = aeNextInSEL;
            return aeNextInSEL;
        }

        private void Insert1Before2InSEL(int ae1, int ae2)
        {
            var ae2PrevInSEL = actives.prevInSEL[ae2];
            actives.prevInSEL[ae1] = ae2PrevInSEL;
            if (ae2PrevInSEL != -1)
                actives.nextInSEL[ae2PrevInSEL] = ae1;
            actives.nextInSEL[ae1] = ae2;
            actives.prevInSEL[ae2] = ae1;
        }

        private bool BuildIntersectList(long topY)
        {
            if (actives_ID == -1 || actives.nextInAEL[actives_ID] == -1) return false;

            //Calculate edge positions at the top of the current scanbeam, and from this
            //we will determine the intersections required to reach these new positions.
            AdjustCurrXAndCopyToSEL(topY);

            //Find all edge intersections in the current scanbeam using a stable merge
            //sort that ensures only adjacent edges are intersecting. Intersect info is
            //stored in FIntersectList ready to be processed in ProcessIntersectList.
            //Re merge sorts see https://stackoverflow.com/a/46319131/359538

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

        private void ProcessIntersectList()
        {
            //We now have a list of intersections required so that edges will be
            //correctly positioned at the top of the scanbeam. However, it's important
            //that edge intersections are processed from the bottom up, but it's also
            //crucial that intersections only occur between adjacent edges.

            //First we do a quicksort so intersections proceed in a bottom up order ...
            intersectList.Sort(default(IntersectListSort));

            //Now as we process these intersections, we must sometimes adjust the order
            //to ensure that intersecting edges are always adjacent ...
            for (int i = 0; i < intersectList.Length; ++i)
            {
                if (!EdgesAdjacentInAEL(intersectList[i]))
                {
                    int j = i + 1;
                    while (j < intersectList.Length && !EdgesAdjacentInAEL(intersectList[j])) j++;
                    if (j < intersectList.Length)
                    {
                        IntersectNode n = intersectList[i];
                        intersectList[i] = intersectList[j];
                        intersectList[j] = n;
                    }
                }

                IntersectNode node = intersectList[i];
                IntersectEdges(node.edge1, node.edge2, node.pt);
                SwapPositionsInAEL(node.edge1, node.edge2);

                if (TestJoinWithPrev2(node.edge2, node.pt))
                {
                    var op1 = AddOutPt(actives.prevInAEL[node.edge2], node.pt);
                    var op2 = AddOutPt(node.edge2, node.pt);
                    if (op1 != op2) AddJoin(op1, op2);
                }
                else if (TestJoinWithNext2(node.edge1, node.pt))
                {
                    var op1 = AddOutPt(node.edge1, node.pt);
                    var op2 = AddOutPt(actives.nextInAEL[node.edge1], node.pt);
                    if (op1 != op2) AddJoin(op1, op2);
                }
            }
        }

        void SwapPositionsInAEL(int ae1, int ae2)
        {
            //preconditon: ae1 must be immediately to the left of ae2
            int ae2Next = actives.nextInAEL[ae2];
            if (ae2Next != -1) actives.prevInAEL[ae2Next] = ae1;
            int ae1Prev = actives.prevInAEL[ae1];
            if (ae1Prev != -1) 
                actives.nextInAEL[ae1Prev] = ae2;
            else 
                actives_ID = ae2;
            actives.prevInAEL[ae2] = ae1Prev;
            actives.nextInAEL[ae2] = ae1;
            actives.prevInAEL[ae1] = ae2;
            actives.nextInAEL[ae1] = ae2Next;
            //if (actives.prevInAEL[ae2] == -1) actives_ID = ae2;
        }
        bool ResetHorzDirection(int horz, int maxPair,
            out long leftX, out long rightX)
        {
            if (actives.bot[horz].x == actives.top[horz].x)
            {
                //the horizontal edge is going nowhere ...
                leftX = actives.curX[horz];
                rightX = actives.curX[horz];
                var ae = actives.nextInAEL[horz];
                while (ae != -1 && ae != maxPair) ae = actives.nextInAEL[ae];
                return ae != -1;
            }
            else if (actives.curX[horz] < actives.top[horz].x)
            {
                leftX = actives.curX[horz];
                rightX = actives.top[horz].x;
                return true;
            }
            else
            {
                leftX = actives.top[horz].x;
                rightX = actives.curX[horz];
                return false;  //right to left
            }
        }

        private bool TrimHorz(int horzEdge, bool preserveCollinear)
        {
            bool result = false;
            long2 pt = vertexList.pt[NextVertex(horzEdge)];
            //trim 180 deg. spikes in closed paths
            while ((pt.y == actives.top[horzEdge].y) &&
                (!preserveCollinear ||
                ((pt.x < actives.top[horzEdge].x) == (actives.bot[horzEdge].x < actives.top[horzEdge].x))))
            {
                actives.vertexTop[horzEdge] = NextVertex(horzEdge);
                actives.top[horzEdge] = pt;
                result = true;
                if (IsMaxima(horzEdge)) break;
                pt = vertexList.pt[NextVertex(horzEdge)];
            }

            if (result) SetDx(horzEdge); // +/-infinity
            return result;
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

            int maxPair = -1;
            bool isMax = IsMaxima(horz);

            //remove 180 deg.spikes and also with closed paths and not PreserveCollinear
            //simplify consecutive horizontals into a 'single' edge ...
            if (!horzIsOpen && !isMax && TrimHorz(horz, PreserveCollinear))
                isMax = IsMaxima(horz);

            if (isMax && !IsOpenEnd(horz))
                maxPair = GetMaximaPair(horz);

            bool isLeftToRight =
                ResetHorzDirection(horz, maxPair, out long leftX, out long rightX);

            if (IsHotEdge(horz))
                AddOutPt(horz, new long2(actives.curX[horz], Y));

            int op;
            for (; ; )
            {
                //loops through consec. horizontal edges (if open)
                int ae;
                if (isLeftToRight) ae = actives.nextInAEL[horz];
                else ae = actives.prevInAEL[horz];

                while (ae != -1)
                {
                    if (ae == maxPair)
                    {
                        if (IsHotEdge(horz))
                        {
                            if (isLeftToRight)
                                op = AddLocalMaxPoly(horz, ae, actives.top[horz]);
                            else
                                op = AddLocalMaxPoly(ae, horz, actives.top[horz]);

                            if (op != -1 && !IsOpen(horz) && outPtList.pt[op] == actives.top[horz])
                                AddTrialHorzJoin(op);
                        }

                        DeleteFromAEL(ae);
                        DeleteFromAEL(horz);
                        return;
                    }

                    //if horzEdge is a maxima, keep going until we reach
                    //its maxima pair, otherwise check for break conditions
                    if (!isMax || IsOpenEnd(horz))
                    {
                        //otherwise stop when 'ae' is beyond the end of the horizontal line
                        if ((isLeftToRight && actives.curX[ae] > rightX) ||
                            (!isLeftToRight && actives.curX[ae] < leftX)) break;

                        if (actives.curX[ae] == actives.top[horz].x && !IsHorizontal(ae))
                        {
                            //for edges at horzEdge's end, only stop when horzEdge's
                            //outslope is greater than e's slope when heading right or when
                            pt = vertexList.pt[NextVertex(horz)];
                            if ((isLeftToRight && TopX(ae, pt.y) >= pt.x) ||
                                (!isLeftToRight && TopX(ae, pt.y) <= pt.x)) break;
                        }
                    }

                    pt = new long2(actives.curX[ae], Y);

                    if (isLeftToRight)
                    {
                        op = IntersectEdges(horz, ae, pt);
                        SwapPositionsInAEL(horz, ae);

                        if (IsHotEdge(horz) && op != -1 &&
                            !IsOpen(horz) && outPtList.pt[op] == pt)
                            AddTrialHorzJoin(op);

                        if (!IsHorizontal(ae) && TestJoinWithPrev1(ae, Y))
                        {
                            op = AddOutPt(actives.prevInAEL[ae], pt);
                            var op2 = AddOutPt(ae, pt);
                            AddJoin(op, op2);
                        }

                        actives.curX[horz] = actives.curX[ae];
                        ae = actives.nextInAEL[horz];
                    }
                    else
                    {
                        op = IntersectEdges(ae, horz, pt);
                        SwapPositionsInAEL(ae, horz);

                        if (IsHotEdge(horz) && op != -1 &&
                            !IsOpen(horz) && outPtList.pt[op] == pt)
                            AddTrialHorzJoin(op);

                        if (!IsHorizontal(ae) && TestJoinWithNext1(ae, Y))
                        {
                            op = AddOutPt(ae, pt);
                            var op2 = AddOutPt(actives.nextInAEL[ae], pt);
                            AddJoin(op, op2);
                        }

                        actives.curX[horz] = actives.curX[ae];
                        ae = actives.prevInAEL[horz];
                    }
                } //we've reached the end of this horizontal

                //check if we've finished looping through consecutive horizontals
                if (isMax || vertexList.pt[NextVertex(horz)].y != actives.top[horz].y) break;

                //there must be a following (consecutive) horizontal
                if (IsHotEdge(horz))
                    AddOutPt(horz, actives.top[horz]);
                UpdateEdgeIntoAEL(horz);
                isMax = IsMaxima(horz);

                //if (!horzIsOpen && !isMax && TrimHorz(horz, PreserveCollinear))
                //    isMax = IsMaxima(horz); //i.e. update after TrimHorz

                isLeftToRight = ResetHorzDirection(horz, maxPair, out leftX, out rightX);

                if (isMax) maxPair = GetMaximaPair(horz);
            } // end for loop and end of (possible consecutive) horizontals

            if (IsHotEdge(horz))
            {
                op = AddOutPt(horz, actives.top[horz]);
                if (!IsOpen(horz))
                    AddTrialHorzJoin(op);
            }
            else
                op = -1;

            if (!isMax)
            {
                UpdateEdgeIntoAEL(horz); //this is the end of an intermediate horiz.
                if (IsOpen(horz)) return;

                if (isLeftToRight && TestJoinWithNext1(horz, Y))
                {
                    var op2 = AddOutPt(actives.nextInAEL[horz], actives.bot[horz]);
                    AddJoin(op, op2);
                }
                else if (!isLeftToRight && TestJoinWithPrev1(horz, Y))
                {
                    var op2 = AddOutPt(actives.prevInAEL[horz], actives.bot[horz]);
                    AddJoin(op2, op);
                }
            }
            else if (maxPair == -1) DeleteFromAEL(horz); //i.e. open at top
            else if (IsHotEdge(horz)) AddLocalMaxPoly(horz, maxPair, actives.top[horz]);
            else
            {
                DeleteFromAEL(maxPair);
                DeleteFromAEL(horz);
            }
        }

        private void DoTopOfScanbeam(long y)
        {
            sel_ID = -1; // sel_ is reused to flag horizontals (see PushHorz below)
            int ae = actives_ID;
            while (ae != -1)
            {
                //NB 'ae' will never be horizontal here
                var aeTop = actives.top[ae];
                if (aeTop.y == y)
                {
                    actives.curX[ae] = aeTop.x;
                    if (IsMaxima(ae))
                    {
                        ae = DoMaxima(ae); //TOP OF BOUND (MAXIMA)
                        continue;
                    }
                    else
                    {
                        //INTERMEDIATE VERTEX ...
                        if (IsHotEdge(ae))
                            AddOutPt(ae, aeTop);
                        UpdateEdgeIntoAEL(ae);
                        if (IsHorizontal(ae))
                            PushHorz(ae); //horizontals are processed later
                    }
                }
                else //i.e. not the top of the edge
                    actives.curX[ae] = TopX(ae, y);

                ae = actives.nextInAEL[ae];
            }
        }

        private int DoMaxima(int ae)
        {
            int prevE;
            int nextE, maxPair;
            prevE = actives.prevInAEL[ae];
            nextE = actives.nextInAEL[ae];

            if (IsOpenEnd(ae))
            {
                if (IsHotEdge(ae))
                    AddOutPt(ae, actives.top[ae]);
                if (!IsHorizontal(ae))
                {
                    if (IsHotEdge(ae)) actives.outrec[ae] = -1;
                    DeleteFromAEL(ae);
                }
                return nextE;
            }
            else
            {
                maxPair = GetMaximaPair(ae);
                if (maxPair == -1) return nextE; //eMaxPair is horizontal
            }

            //only non-horizontal maxima here.
            //process any edges between maxima pair ...
            while (nextE != maxPair)
            {
                IntersectEdges(ae, nextE!, actives.top[ae]);
                SwapPositionsInAEL(ae, nextE!);
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

            //here ae.nextInAel == ENext == EMaxPair ...
            if (IsHotEdge(ae))
                AddLocalMaxPoly(ae, maxPair, actives.top[ae]);

            DeleteFromAEL(ae);
            DeleteFromAEL(maxPair);
            return (prevE != -1 ? actives.nextInAEL[prevE] : actives_ID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidPath(int op)
        {
            return (outPtList.next[op] != op);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidClosedPath(int op)
        {
            var nextOP = outPtList.next[op];
            return (op != -1) && (nextOP != op) && (nextOP != outPtList.prev[op]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValueBetween(long val, long end1, long end2)
        {
            //NB accommodates axis aligned between where end1 == end2
            return ((val != end1) == (val != end2)) &&
            ((val > end1) == (val < end2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ValueEqualOrBetween(long val, long end1, long end2)
        {
            return (val == end1) || (val == end2) || ((val > end1) == (val < end2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PointBetween(long2 pt, long2 corner1, long2 corner2)
        {
            //NB points may not be collinear
            return
            ValueEqualOrBetween(pt.x, corner1.x, corner2.x) &&
            ValueEqualOrBetween(pt.y, corner1.y, corner2.y);
        }

        private static bool CollinearSegsOverlap(long2 seg1a, long2 seg1b,
            long2 seg2a, long2 seg2b)
        {
            //precondition: seg1 and seg2 are collinear      
            if (seg1a.x == seg1b.x)
            {
                if (seg2a.x != seg1a.x || seg2a.x != seg2b.x) return false;
            }
            else if (seg1a.x < seg1b.x)
            {
                if (seg2a.x < seg2b.x)
                {
                    if (seg2a.x >= seg1b.x || seg2b.x <= seg1a.x) return false;
                }
                else
                {
                    if (seg2b.x >= seg1b.x || seg2a.x <= seg1a.x) return false;
                }
            }
            else
            {
                if (seg2a.x < seg2b.x)
                {
                    if (seg2a.x >= seg1a.x || seg2b.x <= seg1b.x) return false;
                }
                else
                {
                    if (seg2b.x >= seg1a.x || seg2a.x <= seg1b.x) return false;
                }
            }

            if (seg1a.y == seg1b.y)
            {
                if (seg2a.y != seg1a.y || seg2a.y != seg2b.y) return false;
            }
            else if (seg1a.y < seg1b.y)
            {
                if (seg2a.y < seg2b.y)
                {
                    if (seg2a.y >= seg1b.y || seg2b.y <= seg1a.y) return false;
                }
                else
                {
                    if (seg2b.y >= seg1b.y || seg2a.y <= seg1a.y) return false;
                }
            }
            else
            {
                if (seg2a.y < seg2b.y)
                {
                    if (seg2a.y >= seg1a.y || seg2b.y <= seg1b.y) return false;
                }
                else
                {
                    if (seg2b.y >= seg1a.y || seg2a.y <= seg1b.y) return false;
                }
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HorzEdgesOverlap(long x1a, long x1b, long x2a, long x2b)
        {
            const long minOverlap = 2;
            if (x1a > x1b + minOverlap)
            {
                if (x2a > x2b + minOverlap)
                    return !((x1a <= x2b) || (x2a <= x1b));
                else
                    return !((x1a <= x2a) || (x2b <= x1b));
            }
            else if (x1b > x1a + minOverlap)
            {
                if (x2a > x2b + minOverlap)
                    return !((x1b <= x2b) || (x2a <= x1a));
                else
                    return !((x1b <= x2a) || (x2b <= x1a));
            }
            else
                return false;
        }

        private int GetHorzTrialParent(int op)
        {
            var joiner = outPtList.joiner[op];
            while (joiner != -1)
            {
                if (joinerList.op1[joiner] == op)
                {
                    if (joinerList.next1[joiner] != -1 &&
                        joinerList.idx[joinerList.next1[joiner]] < 0) return joiner;
                    else joiner = joinerList.next1[joiner];
                }
                else
                {
                    if (joinerList.next2[joiner] != -1 &&
                        joinerList.idx[joinerList.next2[joiner]] < 0) return joiner;
                    else joiner = joinerList.next1[joiner]; //is this a bug?
                }
            }
            return joiner;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool OutPtInTrialHorzList(int op)
        {
            return outPtList.joiner[op] != -1 &&
            ((joinerList.idx[outPtList.joiner[op]] < 0) || GetHorzTrialParent(op) != -1);
        }

        private bool ValidateClosedPathEx(int op)
        {
            if (IsValidClosedPath(op)) return true;
            if (op != -1)
                SafeDisposeOutPts(op);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int InsertOp(long2 pt, int insertAfter)
        {
            int result = outPtList.NewOutPt(pt, outPtList.outrec[insertAfter]);
            outPtList.next[result] = outPtList.next[insertAfter];
            outPtList.prev[outPtList.next[insertAfter]] = result;
            outPtList.next[insertAfter] = result;
            outPtList.prev[result] = insertAfter;
            return result;
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
        private void SafeDisposeOutPts(int op)
        {
            //Debug.Log("Dispose");
            var outRec = GetRealOutRec(outPtList.outrec[op]);
            if (outrecList.frontEdge[outRec] != -1)
                outPtList.outrec[outrecList.frontEdge[outRec]] = -1;
            if (outrecList.backEdge[outRec] != -1)
                outPtList.outrec[outrecList.backEdge[outRec]] = -1;
            outrecList.pts[outRec] = -1;

            outPtList.next[outPtList.prev[op]] = -1;
            var op2 = op;
            while (op2 != -1)
            {
                SafeDeleteOutPtJoiners(op2);
                op2 = outPtList.next[op2];
            }
            //if (actives_ID != -1)
            //{
            //    var ae2 = actives.nextInAEL[actives_ID];
            //    while (ae2 != -1 && ae2 != actives_ID)
            //    {
            //        Debug.Log($"{actives.curX[ae2]} Outrec: {actives.outrec[ae2]}");
            //        ae2 = actives.nextInAEL[ae2];
            //    }
            //}
        }

        private void SafeDeleteOutPtJoiners(int op)
        {
            var joiner = outPtList.joiner[op];
            if (joiner == -1) return;

            while (joiner != -1)
            {
                if (joinerList.idx[joiner] < 0)
                    DeleteTrialHorzJoin(op);
                else if (horzJoiners != -1)
                {
                    if (OutPtInTrialHorzList(joinerList.op1[joiner]))
                        DeleteTrialHorzJoin(joinerList.op1[joiner]);
                    if (OutPtInTrialHorzList(joinerList.op2[joiner]))
                        DeleteTrialHorzJoin(joinerList.op2[joiner]);
                    DeleteJoin(joiner);
                }
                else
                    DeleteJoin(joiner);
                joiner = outPtList.joiner[op];
            }
        }

        private void AddTrialHorzJoin(int op)
        {
            //make sure 'op' isn't added more than once
            if (!OutPtInTrialHorzList(op))
                horzJoiners = AddJoiner(false, op, -1, horzJoiners);
        }

        private int FindTrialJoinParent(ref int joiner, int op)
        {
            var parent = joiner;
            while (parent != -1)
            {
                if (op == joinerList.op1[parent])
                {
                    if (joinerList.next1[parent] != -1 && joinerList.idx[joinerList.next1[parent]] < 0)
                    {
                        joiner = joinerList.next1[parent];
                        return parent;
                    }
                    parent = joinerList.next1[parent];
                }
                else
                {
                    if (joinerList.next2[parent] != -1 && joinerList.idx[joinerList.next2[parent]] < 0)
                    {
                        joiner = joinerList.next2[parent];
                        return parent;
                    }
                    parent = joinerList.next2[parent];
                }
            }
            return -1;
        }

        private void DeleteTrialHorzJoin(int op)
        {
            if (horzJoiners == -1) return;

            var joiner = outPtList.joiner[op];
            int parentH, parentOp = -1;
            while (joiner != -1)
            {
                if (joinerList.idx[joiner] < 0)
                {
                    //first remove joiner from FHorzTrials
                    if (joiner == horzJoiners)
                        horzJoiners = joinerList.nextH[joiner];
                    else
                    {
                        parentH = horzJoiners;
                        while (joinerList.nextH[parentH] != joiner)
                            parentH = joinerList.nextH[parentH];
                        joinerList.nextH[parentH] = joinerList.nextH[joiner];
                    }

                    //now remove joiner from op's joiner list
                    if (parentOp == -1)
                    {
                        //joiner must be first one in list
                        outPtList.joiner[op] = joinerList.next1[joiner];
                        //joiner == null;
                        joiner = outPtList.joiner[op];
                    }
                    else
                    {
                        //the trial joiner isn't first
                        if (op == joinerList.op1[parentOp])
                            joinerList.next1[parentOp] = joinerList.next1[joiner];
                        else
                            joinerList.next2[parentOp] = joinerList.next1[joiner]; //is this a bug?
                        //joiner = null;
                        joiner = parentOp;
                    }
                }
                else
                {
                    //not a trial join so look further along the linked list
                    parentOp = FindTrialJoinParent(ref joiner, op);
                    if (parentOp == -1) break;
                }
                //loop in case there's more than one trial join
            }
        }

        private bool GetHorzExtendedHorzSeg(ref int op, out int op2)
        {
            var outRec = GetRealOutRec(outPtList.outrec[op])!;
            op2 = op;
            if (outrecList.frontEdge[outRec] != -1)
            {
                while (outPtList.prev[op] != outrecList.pts[outRec] &&
                    outPtList.pt[outPtList.prev[op]].y == outPtList.pt[op].y) op = outPtList.prev[op];
                while (op2 != outrecList.pts[outRec] &&
                    outPtList.pt[outPtList.next[op2]].y == outPtList.pt[op2].y) op2 = outPtList.next[op2];
                return op2 != op;
            }
            else
            {
                while (outPtList.prev[op] != op2 && outPtList.pt[outPtList.prev[op]].y == outPtList.pt[op].y)
                    op = outPtList.prev[op];
                while (outPtList.next[op2] != op && outPtList.pt[outPtList.next[op2]].y == outPtList.pt[op2].y)
                    op2 = outPtList.next[op2];
                return op2 != op && outPtList.next[op2] != op;
            }
        }

        private void ConvertHorzTrialsToJoins()
        {
            while (horzJoiners != -1)
            {
                var joiner = horzJoiners;
                horzJoiners = joinerList.nextH[horzJoiners];
                int op1a = joinerList.op1[joiner];
                if (outPtList.joiner[op1a] == joiner)
                {
                    outPtList.joiner[op1a] = joinerList.next1[joiner];
                }
                else
                {
                    var joinerParent = FindJoinParent(joiner, op1a);
                    if (joinerList.op1[joinerParent] == op1a)
                        joinerList.next1[joinerParent] = joinerList.next1[joiner];
                    else
                        joinerList.next2[joinerParent] = joinerList.next1[joiner];
                }
                //joiner = null;

                if (!GetHorzExtendedHorzSeg(ref op1a, out int op1b))
                {
                    if (outrecList.frontEdge[outPtList.outrec[op1a]] == -1)
                        CleanCollinear(outPtList.outrec[op1a]);
                    continue;
                }

                int op2a;
                bool joined = false;
                joiner = horzJoiners;
                while (joiner != -1)
                {
                    op2a = joinerList.op1[joiner];
                    if (GetHorzExtendedHorzSeg(ref op2a, out int op2b) &&
                    HorzEdgesOverlap(outPtList.pt[op1a].x, outPtList.pt[op1b].x, outPtList.pt[op2a].x, outPtList.pt[op2b].x))
                    {
                        //overlap found so promote to a 'real' join
                        var op1aPt = outPtList.pt[op1a];
                        var op1bPt = outPtList.pt[op1b];
                        var op2aPt = outPtList.pt[op2a];
                        var op2bPt = outPtList.pt[op2b];
                        joined = true;
                        if (op1aPt == op2bPt)
                            AddJoin(op1a, op2b);
                        else if (op1bPt == op2aPt)
                            AddJoin(op1b, op2a);
                        else if (op1aPt == op2aPt)
                            AddJoin(op1a, op2a);
                        else if (op1bPt == op2bPt)
                            AddJoin(op1b, op2b);
                        else if (ValueBetween(op1aPt.x, op2aPt.x, op2bPt.x))
                            AddJoin(op1a, InsertOp(op1aPt, op2a));
                        else if (ValueBetween(op1bPt.x, op2aPt.x, op2bPt.x))
                            AddJoin(op1b, InsertOp(op1bPt, op2a));
                        else if (ValueBetween(op2aPt.x, op1aPt.x, op1bPt.x))
                            AddJoin(op2a, InsertOp(op2aPt, op1a));
                        else if (ValueBetween(op2bPt.x, op1aPt.x, op1bPt.x))
                            AddJoin(op2b, InsertOp(op2bPt, op1a));
                        break;
                    }
                    joiner = joinerList.nextH[joiner];
                }
                if (!joined)
                    CleanCollinear(outPtList.outrec[op1a]);
            }
        }

        private void AddJoin(int op1, int op2)
        {
            if ((outPtList.outrec[op1] == outPtList.outrec[op2]) && ((op1 == op2) ||
                //unless op1.next or op1.prev crosses the start-end divide
                //don't waste time trying to join adjacent vertices
                ((outPtList.next[op1] == op2) && (op1 != outrecList.pts[outPtList.outrec[op1]])) ||
                ((outPtList.next[op2] == op1) && (op2 != outrecList.pts[outPtList.outrec[op1]])))) return;

            AddJoiner(true, op1, op2, -1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindJoinParent(int joiner, int op)
        {
            int result = outPtList.joiner[op];
            for (; ; )
            {
                if (op == joinerList.op1[result])
                {
                    if (joinerList.next1[result] == joiner) return result;
                    else result = joinerList.next1[result];
                }
                else
                {
                    if (joinerList.next2[result] == joiner) return result;
                    else result = joinerList.next2[result];
                }
            }
        }

        private void DeleteJoin(int joiner)
        {
            //This method deletes a single join, and it doesn't check for or
            //delete trial horz. joins. For that, use the following method.
            int op1 = joinerList.op1[joiner], op2 = joinerList.op2[joiner];

            int parentJnr;
            if (outPtList.joiner[op1] != joiner)
            {
                parentJnr = FindJoinParent(joiner, op1);
                if (joinerList.op1[parentJnr] == op1)
                    joinerList.next1[parentJnr] = joinerList.next1[joiner];
                else
                    joinerList.next2[parentJnr] = joinerList.next1[joiner];
            }
            else
                outPtList.joiner[op1] = joinerList.next1[joiner];

            if (outPtList.joiner[op2] != joiner)
            {
                parentJnr = FindJoinParent(joiner, op2);
                if (joinerList.op1[parentJnr] == op2)
                    joinerList.next1[parentJnr] = joinerList.next2[joiner];
                else
                    joinerList.next2[parentJnr] = joinerList.next2[joiner];
            }
            else
                outPtList.joiner[op2] = joinerList.next2[joiner];

            joinerList.idx[joiner] = -1;
        }

        private void ProcessJoinList()
        {
            // NB can't use foreach here because list may 
            // contain nulls which can't be enumerated
            for (int i = 0, length = joinerList.idx.Length; i < length; i++)
            {
                var j = joinerList.idx[i];
                if (j == -1) continue;
                var outrec = ProcessJoin(j);
                CleanCollinear(outrec);
            }

            joinerList.Clear();
        }

        private bool CheckDisposeAdjacent(ref int op, int guard, int outRec)
        {
            bool result = false;
            while (outPtList.prev[op] != op)
            {
                if (outPtList.pt[op] == outPtList.pt[outPtList.prev[op]] && op != guard &&
                    outPtList.joiner[outPtList.prev[op]] != -1 && outPtList.joiner[op] == -1)
                {
                    if (op == outrecList.pts[outRec]) outrecList.pts[outRec] = outPtList.prev[op];
                    op = DisposeOutPt(op);
                    op = outPtList.prev[op];
                }
                else if (outPtList.joiner[outPtList.prev[op]] == -1 && outPtList.prev[op] != guard &&
                    (DistanceSqr(outPtList.pt[op], outPtList.pt[outPtList.prev[op]]) < 2.1))
                {
                    if (outPtList.prev[op] == outrecList.pts[outRec]) outrecList.pts[outRec] = op;
                    DisposeOutPt(outPtList.prev[op]);
                    result = true;
                }
                else
                    break;
            }

            while (outPtList.next[op] != op)
            {
                if (outPtList.pt[op] == outPtList.pt[outPtList.next[op]] && op != guard &&
                outPtList.joiner[outPtList.next[op]] != -1 && outPtList.joiner[op] == -1)
                {
                    if (op == outrecList.pts[outRec]) outrecList.pts[outRec] = outPtList.prev[op];
                    op = DisposeOutPt(op);
                    op = outPtList.prev[op];
                }
                else if (outPtList.joiner[outPtList.next[op]] == -1 && outPtList.next[op] != guard &&
                    (DistanceSqr(outPtList.pt[op], outPtList.pt[outPtList.next[op]]) < 2.1))
                {
                    if (outPtList.next[op] == outrecList.pts[outRec]) outrecList.pts[outRec] = op;
                    DisposeOutPt(outPtList.next[op]);
                    result = true;
                }
                else
                    break;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceFromLineSqrd(long2 pt, long2 linePt1, long2 linePt2)
        {
            //perpendicular distance of point (x0,y0) = (a*x0 + b*y0 + C)/Sqrt(a*a + b*b)
            //where ax + by +c = 0 is the equation of the line
            //see https://en.wikipedia.org/wiki/Distance_from_a_point_to_a_line
            double a = (linePt1.y - linePt2.y);
            double b = (linePt2.x - linePt1.x);
            double c = a * linePt1.x + b * linePt1.y;
            double q = a * pt.x + b * pt.y - c;
            return (q * q) / (a * a + b * b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double DistanceSqr(long2 pt1, long2 pt2)
        {
            return (double)(pt1.x - pt2.x) * (pt1.x - pt2.x) +
                   (double)(pt1.y - pt2.y) * (pt1.y - pt2.y);
        }

        private int ProcessJoin(int joiner)
        {
            int op1 = joinerList.op1[joiner], op2 = joinerList.op2[joiner];
            int or1 = GetRealOutRec(outPtList.outrec[op1])!;
            int or2 = GetRealOutRec(outPtList.outrec[op2])!;
            DeleteJoin(joiner);

            if (outrecList.pts[or2] == -1) return or1;
            else if (!IsValidClosedPath(op2))
            {
                CleanCollinear(or2);
                return or1;
            }
            else if ((outrecList.pts[or1] == -1) || !IsValidClosedPath(op1))
            {
                CleanCollinear(or1);
                return or2;
            }
            else if (or1 == or2 &&
                ((op1 == op2) || (outPtList.next[op1] == op2) || (outPtList.prev[op1] == op2))) return or1;

            CheckDisposeAdjacent(ref op1, op2, or1);
            CheckDisposeAdjacent(ref op2, op1, or2);
            if (outPtList.next[op1] == op2 || outPtList.next[op2] == op1) return or1;

            var result = or1;
            for (; ; )
            {
                var op1NextPt = outPtList.pt[outPtList.next[op1]];
                var op1PrevPt = outPtList.pt[outPtList.prev[op1]];
                var op2NextPt = outPtList.pt[outPtList.next[op2]];
                var op2PrevPt = outPtList.pt[outPtList.prev[op2]];
                if (!IsValidPath(op1) || !IsValidPath(op2) ||
                    (or1 == or2 && (outPtList.prev[op1] == op2 || outPtList.next[op1] == op2))) return or1;

                if (op1PrevPt == op2NextPt ||
                    ((InternalClipperFunc.CrossProduct(op1PrevPt, outPtList.pt[op1], op2NextPt) == 0) &&
                        CollinearSegsOverlap(op1PrevPt, outPtList.pt[op1], outPtList.pt[op2], op2NextPt)))
                {
                    if (or1 == or2)
                    {
                        //Debug.Log($"Split1 {outPtList.pt[op1]} {outPtList.pt[op2]}");
                        //SPLIT REQUIRED
                        //make sure op1.prev and op2.next match positions
                        //by inserting an extra vertex if needed
                        if (op1PrevPt != op2NextPt)
                        {
                            if (PointBetween(op1PrevPt, outPtList.pt[op2], op2NextPt))
                                outPtList.next[op2] = InsertOp(op1PrevPt, op2);
                            else
                                outPtList.prev[op1] = InsertOp(op2NextPt, outPtList.prev[op1]);
                        }

                        //current              to     new
                        //op1.p[opA] >>> op1   ...    opA \   / op1
                        //op2.n[opB] <<< op2   ...    opB /   \ op2
                        int opA = outPtList.prev[op1], opB = outPtList.next[op2];
                        outPtList.next[opA] = opB;
                        outPtList.prev[opB] = opA;
                        outPtList.prev[op1] = op2;
                        outPtList.next[op2] = op1;
                        CompleteSplit(op1, opA, or1);
                    }
                    else
                    {
                        //Debug.Log($"Join1 {outPtList.pt[op1]} {outPtList.pt[op2]}");
                        //JOIN, NOT SPLIT
                        int opA = outPtList.prev[op1], opB = outPtList.next[op2];
                        outPtList.next[opA] = opB;
                        outPtList.prev[opB] = opA;
                        outPtList.prev[op1] = op2;
                        outPtList.next[op2] = op1;
                        //this isn't essential but it's
                        //easier to track ownership when it
                        //always defers to the lower index
                        if (or1 < or2)
                        {
                            outrecList.pts[or1] = op1;
                            outrecList.pts[or2] = -1;
                            outrecList.owner[or2] = or1;
                        }
                        else
                        {
                            result = or2;
                            outrecList.pts[or2] = op1;
                            outrecList.pts[or1] = -1;
                            outrecList.owner[or1] = or2;
                        }
                    }
                    break;
                }
                else if (op1NextPt == op2PrevPt ||
                            ((InternalClipperFunc.CrossProduct(op1NextPt, outPtList.pt[op2], op2PrevPt) == 0) &&
                            CollinearSegsOverlap(op1NextPt, outPtList.pt[op1], outPtList.pt[op2], op2PrevPt)))
                {
                    if (or1 == or2)
                    {
                        //Debug.Log($"Split2 {outPtList.pt[op1]} {outPtList.pt[op2]}");
                        //SPLIT REQUIRED
                        //make sure op2.prev and op1.next match positions
                        //by inserting an extra vertex if needed
                        if (op2PrevPt != op1NextPt)
                        {
                            if (PointBetween(op2PrevPt, outPtList.pt[op1], op1NextPt))
                                outPtList.next[op1] = InsertOp(op2PrevPt, op1);
                            else
                                outPtList.prev[op2] = InsertOp(op1NextPt, outPtList.prev[op2]);
                        }

                        //current              to     new
                        //op2.p[opA] >>> op2   ...    opA \   / op2
                        //op1.n[opB] <<< op1   ...    opB /   \ op1
                        int opA = outPtList.prev[op2], opB = outPtList.next[op1];
                        outPtList.next[opA] = opB;
                        outPtList.prev[opB] = opA;
                        outPtList.prev[op2] = op1;
                        outPtList.next[op1] = op2;
                        CompleteSplit(op1, opA, or1);
                    }
                    else
                    {
                        //Debug.Log($"Join2 {outPtList.pt[op1]} {outPtList.pt[op2]}");
                        //JOIN, NOT SPLIT
                        int opA = outPtList.next[op1], opB = outPtList.prev[op2];
                        outPtList.prev[opA] = opB;
                        outPtList.next[opB] = opA;
                        outPtList.next[op1] = op2;
                        outPtList.prev[op2] = op1;
                        if (or1 < or2)
                        {
                            outrecList.pts[or1] = op1;
                            outrecList.pts[or2] = -1;
                            outrecList.owner[or2] = or1;
                        }
                        else
                        {
                            result = or2;
                            outrecList.pts[or2] = op1;
                            outrecList.pts[or1] = -1;
                            outrecList.owner[or1] = or2;
                        }
                    }
                    break;
                }
                else if (PointBetween(op1NextPt, outPtList.pt[op2], op2PrevPt) &&
                            DistanceFromLineSqrd(op1NextPt, outPtList.pt[op2], op2PrevPt) < 2.01)
                {
                    InsertOp(op1NextPt, outPtList.prev[op2]);
                    continue;
                }
                else if (PointBetween(op2NextPt, outPtList.pt[op1], op1PrevPt) &&
                            DistanceFromLineSqrd(op2NextPt, outPtList.pt[op1], op1PrevPt) < 2.01)
                {
                    InsertOp(op2NextPt, outPtList.prev[op1]);
                    continue;
                }
                else if (PointBetween(op1PrevPt, outPtList.pt[op2], op2NextPt) &&
                            DistanceFromLineSqrd(op1PrevPt, outPtList.pt[op2], op2NextPt) < 2.01)
                {
                    InsertOp(op1PrevPt, op2);
                    continue;
                }
                else if (PointBetween(op2PrevPt, outPtList.pt[op1], op1NextPt) &&
                            DistanceFromLineSqrd(op2PrevPt, outPtList.pt[op1], op1NextPt) < 2.01)
                {
                    InsertOp(op2PrevPt, op1);
                    continue;
                }

                //something odd needs tidying up
                if (CheckDisposeAdjacent(ref op1, op2, or1)) continue;
                else if (CheckDisposeAdjacent(ref op2, op1, or1)) continue;
                else if (op1PrevPt != op2NextPt &&
                    (DistanceSqr(op1PrevPt, op2NextPt) < 2.01))
                {
                    outPtList.pt[outPtList.prev[op1]] = op2NextPt;
                    continue;
                }
                else if (op1NextPt != op2PrevPt &&
                    (DistanceSqr(op1NextPt, op2PrevPt) < 2.01))
                {
                    outPtList.pt[outPtList.prev[op2]] = op1NextPt;
                    continue;
                }
                else
                {
                    //OK, there doesn't seem to be a way to join after all
                    //so just tidy up the polygons
                    outrecList.pts[or1] = op1;
                    if (or2 != or1)
                    {
                        outrecList.pts[or2] = op2;
                        CleanCollinear(or2);
                    }
                    break;
                }
            }
            //Debug.Log($"After Joint: Outrec {result} {PointCount(outrecList.pts[result])} {outrecList.owner[result]}");           
            return result;
        }

        private void UpdateOutrecOwner(int outrec)
        {
            int opCurr = outrecList.pts[outrec];
            for (; ; )
            {
                outPtList.outrec[opCurr] = outrec;
                opCurr = outPtList.next[opCurr];
                if (opCurr == outrecList.pts[outrec]) return;
            }
        }

        private void CompleteSplit(int op1, int op2, int outrec)
        {
            double area1 = Area(op1);
            double area2 = Area(op2);
            if (math.abs(area1) < 1)
            {
                SafeDisposeOutPts(op1);
                op1 = -1;
            }
            else if (math.abs(area2) < 1)
            {
                SafeDisposeOutPts(op2);
                op2 = -1;
            }
            //Debug.Log($"Complete split {area1} {area2} {outPtList.pt[op2]} {PointCount(op2)}");
            if (op1 == -1)
                outrecList.pts[outrec] = op2;
            else if (op2 == -1)
                outrecList.pts[outrec] = op1;
            else
            {

                int newOr = outrecList.AddOutRec(-1, OutRecState.Undefined, -1);

                if (math.abs(area1) >= math.abs(area2))
                {
                    outrecList.pts[outrec] = op1;
                    outrecList.pts[newOr] = op2;
                }
                else
                {
                    outrecList.pts[outrec] = op2;
                    outrecList.pts[newOr] = op1;
                }

                if ((area1 > 0) == (area2 > 0))
                {
                    outrecList.owner[newOr] = outrecList.owner[outrec];
                    outrecList.state[newOr] = outrecList.state[outrec];
                }
                else
                {
                    outrecList.owner[newOr] = outrec;
                    if (outrecList.state[outrec] == OutRecState.Outer)
                        outrecList.state[newOr] = OutRecState.Inner;
                    else
                        outrecList.state[newOr] = OutRecState.Outer;
                }
                UpdateOutrecOwner(newOr);
                CleanCollinear(newOr);
            }
        }

        private void CleanCollinear(int outrec)
        {
            outrec = GetRealOutRec(outrec);
            if (outrec == -1 || outrecList.frontEdge[outrec] != -1 ||
                !ValidateClosedPathEx(outrecList.pts[outrec])) return;
            var startOp = outrecList.pts[outrec];
            var op2 = startOp;
            for (; ; )
            {
                if (outPtList.joiner[op2] != -1) return;
                //NB if preserveCollinear == true, then only remove 180 deg. spikes
                var prevOP2 = outPtList.prev[op2];
                var nextOP2 = outPtList.next[op2];
                if ((InternalClipperFunc.CrossProduct(outPtList.pt[prevOP2], outPtList.pt[op2], outPtList.pt[nextOP2]) == 0) &&
                    ((outPtList.pt[op2] == outPtList.pt[prevOP2]) || (outPtList.pt[op2] == outPtList.pt[nextOP2]) || !PreserveCollinear ||
                    (InternalClipperFunc.DotProduct(outPtList.pt[prevOP2], outPtList.pt[op2], outPtList.pt[nextOP2]) < 0)))
                {
                    if (op2 == outrecList.pts[outrec])
                        outrecList.pts[outrec] = prevOP2;
                    op2 = DisposeOutPt(op2);
                    if (!ValidateClosedPathEx(op2))
                    {
                        outrecList.pts[outrec] = -1;
                        return;
                    }
                    startOp = op2;
                    continue;
                }
                op2 = nextOP2;
                if (op2 == startOp) break;
            }
            FixSelfIntersects(outrecList.pts[outrec]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double AreaTriangle(long2 pt1, long2 pt2, long2 pt3)
        {
            return 0.5 * (pt1.x * (double)(pt2.y - pt3.y) +
                          pt2.x * (double)(pt3.y - pt1.y) +
                          pt3.x * (double)(pt1.y - pt2.y));
        }

        private int DoSplitOp(int outRecOp, int splitOp)
        {
            int prevOp = outPtList.prev[splitOp], nextNextOp = outPtList.next[outPtList.next[splitOp]];
            int result = prevOp;
            InternalClipperFunc.GetIntersectPoint(
                outPtList.pt[prevOp], outPtList.pt[splitOp], outPtList.pt[outPtList.next[splitOp]], outPtList.pt[nextNextOp], out double2 ipD);
            long2 ip = new long2(ipD);

            double area1 = Area(outRecOp);
            double area2 = AreaTriangle(ip, outPtList.pt[splitOp], outPtList.pt[outPtList.next[splitOp]]);

            if (ip == outPtList.pt[prevOp] || ip == outPtList.pt[nextNextOp])
            {
                outPtList.prev[nextNextOp] = prevOp;
                outPtList.next[prevOp] = nextNextOp;
            }
            else
            {
                int newOp2 = outPtList.NewOutPt(ip, outPtList.outrec[prevOp]);
                outPtList.prev[newOp2] = prevOp;
                outPtList.next[newOp2] = nextNextOp;
                outPtList.prev[nextNextOp] = newOp2;
                outPtList.next[prevOp] = newOp2;
            }

            SafeDeleteOutPtJoiners(outPtList.next[splitOp]);
            SafeDeleteOutPtJoiners(splitOp);

            if ((math.abs(area2) >= 1) &&
                ((math.abs(area2) > math.abs(area1)) ||
                ((area2 > 0) == (area1 > 0))))
            {
                int newOutRec = outrecList.AddOutRec(outrecList.owner[outPtList.outrec[prevOp]], outrecList.state[outPtList.outrec[prevOp]], -1);
                outPtList.outrec[splitOp] = newOutRec;
                outPtList.outrec[outPtList.next[splitOp]] = newOutRec;

                int newOp = outPtList.NewOutPt(ip, newOutRec);
                outPtList.prev[newOp] = outPtList.next[splitOp];
                outPtList.next[newOp] = splitOp;
                outrecList.pts[newOutRec] = newOp;
                outPtList.prev[splitOp] = newOp;
                outPtList.next[outPtList.next[splitOp]] = newOp;
            }
            return result;
        }

        private void FixSelfIntersects(int op)
        {
            if (!IsValidClosedPath(op)) return;
            int op2 = op;
            for (; ; )
            {
                //3 edged polygons can't self-intersect
                var op2Next = outPtList.next[op2];
                var op2Prev = outPtList.prev[op2];
                if (op2Prev == outPtList.next[op2Next]) break;
                if (InternalClipperFunc.SegmentsIntersect(outPtList.pt[op2Prev],
                        outPtList.pt[op2], outPtList.pt[op2Next], outPtList.pt[outPtList.next[op2Next]]))
                {
                    if (op2 == op || op2Next == op) op = op2Prev;
                    op2 = DoSplitOp(op, op2);
                    op = op2;
                    continue;
                }
                else
                    op2 = op2Next;

                if (op2 == op) break;
            }
        }

        internal bool BuildPath(int op, bool isOpen, ref Polygon path)
        {
            //try
            {
                int cnt = PointCount(op);
                if (cnt < 2) return false;
                path.AddComponent();
                var lastPt = outPtList.pt[op];
                path.nodes.Add(_invScale * lastPt);
                op = outPtList.prev[op];
                for (int i = 1; i < cnt; i++)
                {
                    if (outPtList.pt[op] != lastPt)
                    {
                        lastPt = outPtList.pt[op];
                        path.nodes.Add(_invScale * lastPt);
                    }
                    op = outPtList.prev[op];
                }
                if (!isOpen)
                {
                    if (outPtList.pt[op] != outPtList.pt[outPtList.next[op]])
                    {
                        lastPt = outPtList.pt[op];
                        path.nodes.Add(_invScale * lastPt);
                    }
                }
            }
            //catch
            //{
            //    return false;
            //}

            return true;
        }
        internal bool BuildPathReverse(int op, bool isOpen, ref Polygon path)
        {
            //try
            {
                int cnt = PointCount(op);
                if (cnt < 2) return false;
                path.AddComponent();
                var lastPt = outPtList.pt[op];
                path.nodes.Add(_invScale * lastPt);
                op = outPtList.next[op];
                for (int i = 1; i < cnt; i++)
                {
                    if (outPtList.pt[op] != lastPt)
                    {
                        lastPt = outPtList.pt[op];
                        path.nodes.Add(_invScale * lastPt);
                    }
                    op = outPtList.next[op];
                }
                if (!isOpen)
                {
                    if (outPtList.pt[op] != outPtList.pt[outPtList.prev[op]])
                    {
                        lastPt = outPtList.pt[op];
                        path.nodes.Add(_invScale * lastPt);
                    }
                }
            }
            //catch
            //{
            //    return false;
            //}

            return true;
        }

        bool BuildPaths(ref Polygon solutionClosed, ref Polygon solutionOpen)
        {
            //try
            //{
            solutionClosed.Clear();
            solutionOpen.Clear();
            solutionClosed.nodes.Capacity = outPtList.pt.Length;
            solutionOpen.nodes.Capacity = outPtList.pt.Length;
            solutionClosed.startIDs.Capacity = outrecList.owner.Length;
            solutionOpen.startIDs.Capacity = outrecList.owner.Length;

            for (int outrec = 0, length = outrecList.owner.Length; outrec < length; outrec++)
            {
                if (outrecList.pts[outrec] == -1) continue;
                if (outrecList.state[outrec] == OutRecState.Open)
                    BuildPath(outrecList.pts[outrec], true, ref solutionOpen);
                else
                    BuildPath(outrecList.pts[outrec], false, ref solutionClosed);
            }
            if (solutionOpen.nodes.Length > 0)
                solutionOpen.ClosePolygon();
            if (solutionClosed.nodes.Length > 0)
                solutionClosed.ClosePolygon();
            //}
            //catch
            //{
            //    return false;
            //}

            return true;
        }
        bool BuildTree(ref PolyTree polytree, ref Polygon solutionOpen)
        {
            polytree.Clear(outrecList.owner.Length, Allocator.Temp);
            solutionOpen.Clear();
            solutionOpen.nodes.Capacity = outPtList.pt.Length;
            solutionOpen.startIDs.Capacity = outrecList.owner.Length;
            var components = polytree.components;
            var exteriorIDs = polytree.exteriorIDs;
            for (int i = 0, length = components.Length; i < length; i++)
                components[i] = new TreeNode(i); //initialize
            //try
            {
                for (int outrec = 0, length = outrecList.owner.Length; outrec < length; outrec++)
                {
                    ////make sure outer/owner paths preceed their inner paths ...
                    //int j = outrecList.owner[outrec];
                    //if (j != -1 && j > outrec)
                    //    SwapOutRecs(outrec, j);

                    if (outrecList.pts[outrec] == -1) continue;
                    int cnt = PointCount(outPtList.next[outrecList.pts[outrec]]);
                    if (outrecList.state[outrec] == OutRecState.Open)
                    {
                        BuildPath(outrecList.pts[outrec], true, ref solutionOpen);
                        continue;
                    }
                    if (cnt < 3) continue;

                    //update ownership ...
                    var owner = outrecList.owner[outrec];
                    while (owner != -1 && outrecList.pts[owner] == -1)
                    {
                        outrecList.owner[outrec] = outrecList.owner[owner];
                        owner = outrecList.owner[outrec];
                    }
                    if (owner != -1 && outrecList.state[owner] == outrecList.state[outrec])
                    {
                        if (IsOuter(outrec)) outrecList.owner[outrec] = -1;
                        else outrecList.owner[outrec] = outrecList.owner[outrecList.owner[outrec]];
                    }

                    if (outrecList.owner[outrec] == -1) //if no owner, definitely an outer polygon
                    {
                        var node = components[outrec];
                        node.orientation = PolyOrientation.CCW;
                        components[outrec] = node;
                        exteriorIDs.Add(outrec);
                    }
                    else
                    {
                        //if owner has no startID, or is not the first Outer Polygon,
                        //then promote current path to Outer Polygon
                        //maybe better approach: if (outrec_list.owner[i] == -1 || !OuterPolygonList.Contains(outrec_list.owner[i])) 
                        if (outrecList.owner[outrec] == -1 || outrecList.owner[outrec] != 0)
                        {
                            var node = components[outrec];
                            node.orientation = PolyOrientation.CCW;
                            components[outrec] = node;
                            exteriorIDs.Add(outrec);
                        }
                        else
                        {
                            var ownerNode = components[outrecList.owner[outrec]];
                            PolyOrientation desiredOrientation;
                            switch (ownerNode.orientation)
                            {
                                case PolyOrientation.CW: desiredOrientation = PolyOrientation.CCW; break;
                                case PolyOrientation.CCW: desiredOrientation = PolyOrientation.CW; break;
                                case PolyOrientation.None: desiredOrientation = PolyOrientation.None; Debug.Log($"Should not happen"); break;
                                default: desiredOrientation = PolyOrientation.None; break;
                            }
                            var node = components[outrec];
                            node.orientation = desiredOrientation;
                            polytree.AddChildComponent(outrecList.owner[outrec], node);
                        }
                    }
                }
            }
            //catch
            //{
            //    return false;
            //}

            return true;
        }
        public void GetPolygonWithHoles(in PolyTree polyTree, int outrec, ref Polygon outPolygon)
        {
            if (polyTree.components[outrec].orientation == PolyOrientation.CCW)
            {
                //Debug.Log("taking Exterior as is");
                BuildPath(outrecList.pts[outrec], false, ref outPolygon);
            }
            else
            {
                //Debug.Log("reversing Exterior");
                BuildPathReverse(outrecList.pts[outrec], false, ref outPolygon);
            }
            TreeNode holeNode;
            int holeID;
            while (polyTree.GetNextComponent(outrec, out holeNode, out holeID))
            {
                if (holeNode.orientation == PolyOrientation.CW)
                {
                    //Debug.Log("taking Hole as is");
                    BuildPath(outrecList.pts[holeID], false, ref outPolygon);
                }
                else
                {
                    //Debug.Log("reversing Hole");
                    BuildPathReverse(outrecList.pts[holeID], false, ref outPolygon);
                }
                outrec = holeID;
            }
            outPolygon.ClosePolygon(); //abuse StartID to store end of last Component
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void SwapOutRecs(int outRec1, int outRec2)
        {
            var owner = outrecList.owner[outRec1];
            var frontEdge = outrecList.frontEdge[outRec1];
            var backEdge = outrecList.backEdge[outRec1];
            var pts = outrecList.pts[outRec1];
            var polypath = outrecList.polypath[outRec1];
            var state = outrecList.state[outRec1];

            outrecList.owner[outRec1] = outrecList.owner[outRec2];
            outrecList.frontEdge[outRec1] = outrecList.frontEdge[outRec2];
            outrecList.backEdge[outRec1] = outrecList.backEdge[outRec2];
            outrecList.pts[outRec1] = outrecList.pts[outRec2];
            outrecList.polypath[outRec1] = outrecList.polypath[outRec2];
            outrecList.state[outRec1] = outrecList.state[outRec2];

            outrecList.owner[outRec2] = owner;
            outrecList.frontEdge[outRec2] = frontEdge;
            outrecList.backEdge[outRec2] = backEdge;
            outrecList.pts[outRec2] = pts;
            outrecList.polypath[outRec2] = polypath;
            outrecList.state[outRec2] = state;
        }

        public int AddJoiner(bool addToList, int _op1, int _op2, int _nextH)
        {
            int current = joinerList.idx.Length;
            if (addToList)
            {
                joinerList.idx.Add(current);
                //Debug.Log($"Real Joiner at pos {current} {outPtList.pt[_op1]}");
            }
            else
            {
                joinerList.idx.Add(-1);
                //Debug.Log($"Trial Joiner at pos {current} {outPtList.pt[_op1]}");
            }

            joinerList.nextH.Add(_nextH);
            joinerList.op1.Add(_op1);
            joinerList.op2.Add(_op2);
            joinerList.next1.Add(outPtList.joiner[_op1]);
            outPtList.joiner[_op1] = current;

            if (_op2 != -1)
            {
                joinerList.next2.Add(outPtList.joiner[_op2]);
                outPtList.joiner[_op2] = current;
            }
            else
                joinerList.next2.Add(-1);
            //return joinerList.idx[current];
            return current;
        }

        public bool Execute(ClipType clipType, FillRule fillRule, ref Polygon solutionClosed, ref Polygon solutionOpen)
        {
            bool success = true;
            solutionClosed.Clear();
            solutionOpen.Clear();
            //try
            {
                ExecuteInternal(clipType, fillRule);
                BuildPaths(ref solutionClosed, ref solutionOpen);
            }
            //catch
            //{
            //    success = false;
            //}

            ClearSolution();
            return success;
        }
        public bool Execute(ClipType clipType, FillRule fillRule, ref Polygon solutionClosed)
        {
            var solutionOpen = new Polygon(0, Allocator.Temp);
            return Execute(clipType, fillRule, ref solutionClosed, ref solutionOpen);
        }
        public bool Execute(ClipType clipType, FillRule fillRule, ref PolyTree polytree, ref Polygon openPaths)
        {
            bool success = true;
            //try
            {
                ExecuteInternal(clipType, fillRule);
                BuildTree(ref polytree, ref openPaths);
            }
            //catch
            //{
            //    Debug.Log($"Error");
            //    success = false;
            //}

            //ClearSolution();
            return success;
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
            Debug.Log($"OutPoint List Size: pt {outPtList.pt.Length} outrec {outPtList.outrec.Length} joiner {outPtList.joiner.Length} next {outPtList.next.Length} prev {outPtList.prev.Length}");
            Debug.Log($"OutRec List Size: pts {outrecList.pts.Length} backEdge {outrecList.backEdge.Length} frontEdge {outrecList.frontEdge.Length} owner {outrecList.owner.Length} state {outrecList.state.Length} polypath {outrecList.polypath.Length}");
            Debug.Log($"Actives List Size: bot {actives.bot.Length} top {actives.top.Length} prevInAEL {actives.prevInAEL.Length} nextInAEL {actives.nextInAEL.Length} prevInSEL {actives.prevInSEL.Length} nextInSEL {actives.nextInSEL.Length} outrec {actives.outrec.Length} vertexTop {actives.vertexTop.Length} windCount {actives.windCount.Length} windCount2 {actives.windCount2.Length} windDx {actives.windDx.Length}");
            Debug.Log($"Joiner List Size: idx {joinerList.idx.Length} op1 {joinerList.op1.Length} op2 {joinerList.op2.Length} next1 {joinerList.next1.Length} next2 {joinerList.next2.Length} nextH {joinerList.nextH.Length}");
            Debug.Log($"Intersect List Size: {intersectList.Length} ");
        }

    } //ClipperBase class

    public class ClipperLibException : Exception
    {
        public ClipperLibException(string description) : base(description) { }
    }
} //namespace