﻿/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Date      :  19 July 2023                                                    *
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

namespace Clipper2AoS
{
    //Vertex: a pre-clipping data structure. It is used to separate polygons
    //into ascending and descending 'bounds' (or sides) that start at local
    //minima and ascend to a local maxima, before descending again.

    [Flags]
    public enum PointInPolygonResult { IsOn = 0, IsInside = 1, IsOutside = 2 };
    public enum VertexFlags { None = 0, OpenStart = 1, OpenEnd = 2, LocalMax = 4, LocalMin = 8 };
    public struct ClipperL
    {
        ClipType _cliptype;
        FillRule _fillrule;
        int _activesID;
        int _selID;
        //input lists
        NativeList<LocalMinima> _minimaList;
        NativeList<Vertex> _vertexList;
        //solution lists
        NativeList<Active> _activesList;
        NativeList<IntersectNode> _intersectList;
        NativeList<OutRec> _outrecList;
        NativeList<OutPt> _outPtList;
        MinHeap<long> _scanlineList;
        NativeList<HorzSegment> _horzSegList;
        NativeList<HorzJoin> _horzJoinList;
        NativeList<int> splits;
        NativeList<int> nextSplit;
        int _currentLocMin;
        long _currentBotY;
        bool _isSortedMinimaList;
        bool _hasOpenPaths;
        internal bool _using_polytree;
        internal bool _succeeded;
        //private readonly double _scale;
        //private readonly double _invScale;
        public bool PreserveCollinear { get; set; }
        public bool ReverseSolution { get; set; }


        public ClipperL(Allocator allocator, int roundingDecimalPrecision = 2)
        {
            _cliptype = ClipType.None;
            _fillrule = FillRule.EvenOdd;
            _activesID = -1;
            _selID = -1;
            //input lists
            _vertexList = new NativeList<Vertex>(128, allocator);
            _minimaList = new NativeList<LocalMinima>(1024, allocator);
            //solution lists
            _activesList = new NativeList<Active>(256, allocator);
            _intersectList = new NativeList<IntersectNode>(1024, allocator);
            _outrecList = new NativeList<OutRec>(16, allocator);
            _outPtList = new NativeList<OutPt>(1024, allocator);
            _scanlineList = new MinHeap<long>(64, allocator, Comparison.Max);
            _horzSegList = new NativeList<HorzSegment>(64, allocator);
            _horzJoinList = new NativeList<HorzJoin>(64, allocator);
            splits = new NativeList<int>(16, allocator);
            nextSplit = new NativeList<int>(16, allocator);
            _currentLocMin = 0;
            _currentBotY = long.MaxValue;
            _isSortedMinimaList = false;
            _hasOpenPaths = false;
            _using_polytree = false;
            _succeeded = false;
            PreserveCollinear = true;
            ReverseSolution = false;
            //_scale = math.pow(10, roundingDecimalPrecision);
            //_invScale = 1 / _scale;
        }
        public void Dispose()
        {
            //input lists
            if (_vertexList.IsCreated) _vertexList.Dispose();
            if (_minimaList.IsCreated) _minimaList.Dispose();
            //solution lists
            if (_activesList.IsCreated) _activesList.Dispose();
            if (_intersectList.IsCreated) _intersectList.Dispose();
            if (_outrecList.IsCreated) _outrecList.Dispose();
            if (_outPtList.IsCreated) _outPtList.Dispose();
            if (_scanlineList.IsCreated) _scanlineList.Dispose();
            if (_horzSegList.IsCreated) _horzSegList.Dispose();
            if (_horzJoinList.IsCreated) _horzJoinList.Dispose();
            if (splits.IsCreated) splits.Dispose();
            if (nextSplit.IsCreated) nextSplit.Dispose();
        }
        public void Dispose(JobHandle jobHandle)
        {
            //input lists
            if (_vertexList.IsCreated) _vertexList.Dispose(jobHandle);
            if (_minimaList.IsCreated) _minimaList.Dispose(jobHandle);
            //solution lists
            if (_activesList.IsCreated) _activesList.Dispose(jobHandle);
            if (_intersectList.IsCreated) _intersectList.Dispose(jobHandle);
            if (_outrecList.IsCreated) _outrecList.Dispose(jobHandle);
            if (_outPtList.IsCreated) _outPtList.Dispose(jobHandle);
            if (_scanlineList.IsCreated) _scanlineList.Dispose(jobHandle);
            if (_horzSegList.IsCreated) _horzSegList.Dispose(jobHandle);
            if (_horzJoinList.IsCreated) _horzJoinList.Dispose(jobHandle);
            if (splits.IsCreated) splits.Dispose(jobHandle);
            if (nextSplit.IsCreated) nextSplit.Dispose(jobHandle);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOdd(int val)
        {
            return (val & 1) != 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHotEdge(ref Active ae)
        {
            return ae.outrec != -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsOpen(ref Active ae)
        {
            return ae.localMin.isOpen;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsOpenEnd(ref Active ae)
        {
            return ae.localMin.isOpen && IsOpenEnd(ref _vertexList.ElementAt(ae.vertexTop));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsOpenEnd(ref Vertex v)
        {
            return (v.flags & (VertexFlags.OpenStart | VertexFlags.OpenEnd)) != VertexFlags.None;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetPrevHotEdge(ref Active ae)
        {
            int prevID = ae.prevInAEL;
            if (prevID != -1)
            {
                ref var prev = ref _activesList.ElementAt(prevID);
                while (prevID != -1 && (IsOpen(ref prev) || !IsHotEdge(ref prev)))
                {
                    prevID = prev.prevInAEL;
                    if (prevID != -1)
                        prev = ref _activesList.ElementAt(prevID);
                }
            }
            return prevID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFront(ref Active ae, int aeID)
        {
            return aeID == _outrecList.ElementAt(ae.outrec).frontEdge;
        }

        /*******************************************************************************
        *  Dx:                             0(90deg)                                    *
        *                                  |                                           *
        *               +inf (180deg) <--- o --. -inf (0deg)                          *
        *******************************************************************************/
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double GetDx(long2 pt1, long2 pt2)
        {
            double dy = pt2.y - pt1.y;
            if (dy != 0)
                return (pt2.x - pt1.x) / dy;
            if (pt2.x > pt1.x)
                return double.NegativeInfinity;
            return double.PositiveInfinity;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long TopX(ref Active ae, long currentY)
        {
            if ((currentY == ae.top.y) || (ae.top.x == ae.bot.x)) return ae.top.x;
            if (currentY == ae.bot.y) return ae.bot.x;
            return ae.bot.x + (long)math.round(ae.dx * (currentY - ae.bot.y));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHorizontal(ref Active ae)
        {
            return ae.top.y == ae.bot.y;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHeadingRightHorz(ref Active ae)
        {
            return double.IsNegativeInfinity(ae.dx);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHeadingLeftHorz(ref Active ae)
        {
            return double.IsPositiveInfinity(ae.dx);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SwapActivesIDs(ref int ae1, ref int ae2)
        {
            (ae2, ae1) = (ae1, ae2);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SwapActives(ref Active ae1, ref Active ae2)
        {
            (ae2, ae1) = (ae1, ae2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PathType GetPolyType(ref Active ae)
        {
            return ae.localMin.polytype;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsSamePolyType(ref Active ae, ref Active ae2)
        {
            return GetPolyType(ref ae) == GetPolyType(ref ae2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDx(ref Active ae)
        {
            ae.dx = GetDx(ae.bot, ae.top);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NextVertex(ref Active ae)
        {
            ref var vertexTop = ref _vertexList.ElementAt(ae.vertexTop);
            if (ae.windDx > 0)
                return vertexTop.next;
            return vertexTop.prev;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long2 PrevPrevVertex(ref Active ae)
        {
            ref var vertexTop = ref _vertexList.ElementAt(ae.vertexTop);
            if (ae.windDx > 0)
                return _vertexList[_vertexList.ElementAt(vertexTop.prev).prev].pt;
            return _vertexList[_vertexList.ElementAt(vertexTop.next).next].pt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsMaxima(ref Vertex vertex)
        {
            return (vertex.flags & VertexFlags.LocalMax) != VertexFlags.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsMaxima(ref Active ae)
        {
            return IsMaxima(ref _vertexList.ElementAt(ae.vertexTop));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetMaximaPair(ref Active ae)
        {
            int ae2ID;
            ae2ID = ae.nextInAEL;
            while (ae2ID != -1)
            {
                ref var ae2 = ref _activesList.ElementAt(ae2ID);
                if (ae2.vertexTop == ae.vertexTop) return ae2ID; // Found!
                ae2ID = ae2.nextInAEL;
            }
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCurrYMaximaVertex_Open(ref Active ae)
        {
            int resultID = ae.vertexTop;
            ref var result = ref _vertexList.ElementAt(resultID);
            if (ae.windDx > 0)
            {
                ref var resultNext = ref _vertexList.ElementAt(result.next);
                while (resultNext.pt.y == result.pt.y &&
                  ((result.flags & (VertexFlags.OpenEnd |
                  VertexFlags.LocalMax)) == VertexFlags.None))
                {
                    resultID = result.next;
                    result = ref _vertexList.ElementAt(resultID);
                    resultNext = ref _vertexList.ElementAt(result.next);
                }
            }
            else
            {
                ref var resultPrev = ref _vertexList.ElementAt(result.prev);
                while (resultPrev.pt.y == result.pt.y &&
                  ((result.flags & (VertexFlags.OpenEnd |
                  VertexFlags.LocalMax)) == VertexFlags.None))
                {
                    resultID = result.prev;
                    result = ref _vertexList.ElementAt(resultID);
                    resultPrev = ref _vertexList.ElementAt(result.prev);
                }
            }
            if (!IsMaxima(ref result)) resultID = -1; // not a maxima
            return resultID;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetCurrYMaximaVertex(ref Active ae)
        {
            int resultID = ae.vertexTop;
            ref var result = ref _vertexList.ElementAt(resultID);
            if (ae.windDx > 0)
            {
                ref var resultNext = ref _vertexList.ElementAt(result.next);
                while (resultNext.pt.y == result.pt.y)
                {
                    resultID = result.next;
                    result = ref _vertexList.ElementAt(resultID);
                    resultNext = ref _vertexList.ElementAt(result.next);
                }
            }
            else
            {
                ref var resultPrev = ref _vertexList.ElementAt(result.prev);
                while (resultPrev.pt.y == result.pt.y)
                {
                    resultID = result.prev;
                    result = ref _vertexList.ElementAt(resultID);
                    resultPrev = ref _vertexList.ElementAt(result.prev);
                }
            }
            if (!IsMaxima(ref result)) resultID = -1; // not a maxima
            return resultID;
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
        private static void SetSides(ref OutRec outrec, int startEdge, int endEdge)
        {
            outrec.frontEdge = startEdge;
            outrec.backEdge = endEdge;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SwapOutrecs(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID)
        {
            int or1ID = ae1.outrec; // at least one edge has 
            int or2ID = ae2.outrec; // an assigned outrec

            if (or1ID == or2ID) //would also be true if both are null
            {
                ref var or1 = ref _outrecList.ElementAt(or1ID);
                int aeID = or1.frontEdge;
                or1.frontEdge = or1.backEdge;
                or1.backEdge = aeID;
                return;
            }

            if (or1ID != -1)
            {
                ref var or1 = ref _outrecList.ElementAt(or1ID);
                if (ae1ID == or1.frontEdge)
                    or1.frontEdge = ae2ID;
                else
                    or1.backEdge = ae2ID;
            }

            if (or2ID != -1)
            {
                ref var or2 = ref _outrecList.ElementAt(or2ID);
                if (ae2ID == or2.frontEdge)
                    or2.frontEdge = ae1ID;
                else
                    or2.backEdge = ae1ID;
            }

            ae1.outrec = or2ID;
            ae2.outrec = or1ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetOwner(ref OutRec outrec, int outrecID, ref OutRec newOwner, int newOwnerID)
        {
            //precondition1: new_owner is never null
            if (newOwner.owner != -1)
            {
                ref var newOwnerOwner = ref _outrecList.ElementAt(newOwner.owner);
                while (newOwner.owner != -1 && newOwnerOwner.pts == -1)
                {
                    newOwner.owner = newOwnerOwner.owner;
                    if (newOwner.owner != -1)
                        newOwnerOwner = ref _outrecList.ElementAt(newOwner.owner);
                }
            }

            //make sure that outrec isn't an owner of newOwner
            var tmpID = newOwnerID;
            while (tmpID != -1 && tmpID != outrecID)
            {
                ref var tmp = ref _outrecList.ElementAt(tmpID);
                tmpID = tmp.owner;
            }
            if (tmpID != -1)
                newOwner.owner = outrec.owner;
            outrec.owner = newOwnerID;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double Area(int opID)
        {
            // https://en.wikipedia.org/wiki/Shoelace_formula
            double area = 0.0;
            int op2ID = opID;
            do
            {
                ref var op2 = ref _outPtList.ElementAt(op2ID);
                var op2Pt = op2.pt;
                var op2PrevPt = _outPtList.ElementAt(op2.prev).pt;
                area += (double)(op2PrevPt.y + op2Pt.y) *
                  (op2PrevPt.x - op2Pt.x);
                op2ID = op2.next;
            } while (op2ID != opID);
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
        private int GetRealOutRec(int outRecID)
        {
            OutRec outRec;
            while (outRecID != -1 && (outRec = _outrecList[outRecID]).pts == -1)
                outRecID = outRec.owner;
            return outRecID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UncoupleOutRec(ref Active ae)
        {
            if (ae.outrec == -1) return;
            ref var outrec = ref _outrecList.ElementAt(ae.outrec);
            _activesList.ElementAt(outrec.frontEdge).outrec = -1;
            _activesList.ElementAt(outrec.backEdge).outrec = -1;
            outrec.frontEdge = -1;
            outrec.backEdge = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool OutrecIsAscending(ref Active hotEdge, int hotEdgeID)
        {
            return hotEdgeID == _outrecList.ElementAt(hotEdge.outrec).frontEdge;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SwapFrontBackSides(ref OutRec outrec)
        {
            // while this proc. is needed for open paths
            // it's almost never needed for closed paths
            int ae2ID = outrec.frontEdge;
            outrec.frontEdge = outrec.backEdge;
            outrec.backEdge = ae2ID;
            outrec.pts = _outPtList.ElementAt(outrec.pts).next;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EdgesAdjacentInAEL(IntersectNode inode)
        {
            ref var inodeEdge1 = ref _activesList.ElementAt(inode.edge1);
            return (inodeEdge1.nextInAEL == inode.edge2) || (inodeEdge1.prevInAEL == inode.edge2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ClearSolutionOnly()
        {
            //while (_actives != null) DeleteFromAEL(_actives);
            if (_activesList.IsCreated) _activesList.Clear();//deviation from Clipper2
            _activesID = -1;
            _selID = -1;
            if (_intersectList.IsCreated) _intersectList.Clear();
            if (_outrecList.IsCreated) _outrecList.Clear();
            if (_outPtList.IsCreated) _outPtList.Clear();//deviation from Clipper2
            if (_scanlineList.IsCreated) _scanlineList.Clear();
            if (_horzSegList.IsCreated) _horzSegList.Clear();
            if (_horzJoinList.IsCreated) _horzJoinList.Clear();
            if (splits.IsCreated) splits.Clear();//deviation from Clipper2
            if (nextSplit.IsCreated) nextSplit.Clear();//deviation from Clipper2
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ClearSolutionOnly();
            if (_minimaList.IsCreated) _minimaList.Clear();
            if (_vertexList.IsCreated) _vertexList.Clear();
            _currentLocMin = 0;
            _isSortedMinimaList = false;
            _hasOpenPaths = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            if (!_isSortedMinimaList)
            {
                _minimaList.Sort(new LocMinSorter(_vertexList));
                _isSortedMinimaList = true;
            }

            _scanlineList._stack.Capacity = _minimaList.Length;
            for (int i = _minimaList.Length - 1; i >= 0; i--)
                InsertScanline(_vertexList[_minimaList[i].vertex].pt.y);

            _currentBotY = 0;
            _currentLocMin = 0;
            if (_activesList.IsCreated) _activesList.Clear();//deviation from Clipper2
            _activesID = -1;
            _selID = -1;
            _succeeded = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void InsertScanline(long y)
        {
            var index = _scanlineList._stack.BinarySearch(y);
            if (index >= 0)
                return;
            _scanlineList.Push(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool PopScanline(out long y)
        {
            if (_scanlineList.IsEmpty)
            {
                y = 0;
                return false;
            }

            y = _scanlineList.Pop();
            while (!_scanlineList.IsEmpty && y == _scanlineList.Peek())
                _scanlineList.Pop();  // Pop duplicates.
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasLocMinAtY(long y)
        {
            return (_currentLocMin < _minimaList.Length && _vertexList[_minimaList[_currentLocMin].vertex].pt.y == y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private LocalMinima PopLocalMinima()
        {
            return _minimaList[_currentLocMin++];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddLocMin(int vertID, PathType polytype, bool isOpen)
        {
            ref var vert = ref _vertexList.ElementAt(vertID);
            // make sure the vertex is added only once ...
            if ((vert.flags & VertexFlags.LocalMin) != VertexFlags.None) return;
            vert.flags |= VertexFlags.LocalMin;

            LocalMinima lm = new LocalMinima(vertID, polytype, isOpen);
            _minimaList.Add(lm);
        }
        void EnsureVertexListCapacity(int additionalVertextCount)
        {
            _vertexList.Capacity = _vertexList.Length + additionalVertextCount;
        }

        void AddPathsToVertexList(NativeArray<int2> nodes, NativeArray<int> startIDs, PathType polytype, bool isOpen)
        {
            for (int componentID = 0, pathCnt = startIDs.Length - 1; componentID < pathCnt; componentID++) //for each component of Poly
            {
                int start = startIDs[componentID];
                int end = startIDs[componentID + 1];
                AddPathToVertexList(nodes, start, end, polytype, isOpen);
            }
        }
        void AddPathToVertexList(NativeArray<int2> nodes, int start, int end, PathType polytype, bool isOpen)
        {
            int v0ID = -1, prev_vID = -1, curr_vID;
            for (int i = start; i < end; i++)
            {
                //var pt = new long2(nodes[i], _scale); //only needed when input data is float or double
                var pt = new long2(nodes[i]);
                if (v0ID == -1)
                {
                    v0ID = _vertexList.AddVertex(pt, VertexFlags.None, true);
                    prev_vID = v0ID;
                }
                else if (_vertexList[prev_vID].pt != pt) // ie skips duplicates
                    prev_vID = _vertexList.AddVertex(pt, VertexFlags.None, false, v0ID);
            }
            ref var v0 = ref _vertexList.ElementAt(v0ID);
            ref var prev_v = ref _vertexList.ElementAt(prev_vID);
            if (prev_vID == -1 || prev_v.prev == -1) return;
            //the following eliminates the end point (identical with start) for closed polygons from the linked list
            if (!isOpen && prev_v.pt == v0.pt) prev_v = ref _vertexList.ElementAt(prev_vID = prev_v.prev);
            prev_v.next = v0ID; //link tail to head
            v0.prev = prev_vID; //link head to tail
            if (!isOpen && prev_v.next == prev_vID) return;

            // OK, we have a valid path
            bool going_up, going_up0;
            ref var curr_v = ref _vertexList.ElementAt(curr_vID = v0.next);
            if (isOpen)
            {
                while (curr_vID != v0ID && curr_v.pt.y == v0.pt.y)
                    curr_v = ref _vertexList.ElementAt(curr_vID = curr_v.next);
                going_up = curr_v.pt.y <= v0.pt.y;
                if (going_up)
                {
                    v0.flags = VertexFlags.OpenStart;
                    AddLocMin(v0ID, polytype, true);
                }
                else
                    v0.flags = VertexFlags.OpenStart | VertexFlags.LocalMax;
            }
            else // closed path
            {
                prev_v = ref _vertexList.ElementAt(prev_vID = v0.prev);
                while (prev_vID != v0ID && prev_v.pt.y == v0.pt.y)
                    prev_v = ref _vertexList.ElementAt(prev_vID = prev_v.prev);
                if (prev_vID == v0ID)
                    return; // only open paths can be completely flat
                going_up = prev_v.pt.y > v0.pt.y;
            }

            going_up0 = going_up;
            prev_vID = v0ID;
            curr_vID = v0.next;
            while (curr_vID != v0ID)
            {
                curr_v = ref _vertexList.ElementAt(curr_vID);
                prev_v = ref _vertexList.ElementAt(prev_vID);
                if (curr_v.pt.y > prev_v.pt.y && going_up)
                {
                    prev_v.flags |= VertexFlags.LocalMax;
                    going_up = false;
                }
                else if (curr_v.pt.y < prev_v.pt.y && !going_up)
                {
                    going_up = true;
                    AddLocMin(prev_vID, polytype, isOpen);
                }
                prev_vID = curr_vID;
                curr_vID = curr_v.next;
            }

            prev_v = ref _vertexList.ElementAt(prev_vID);
            if (isOpen)
            {
                prev_v.flags |= VertexFlags.OpenEnd;
                if (going_up)
                    prev_v.flags |= VertexFlags.LocalMax;
                else
                    AddLocMin(prev_vID, polytype, isOpen);
            }
            else if (going_up != going_up0)
            {
                if (going_up0) AddLocMin(prev_vID, polytype, false);
                else prev_v.flags |= VertexFlags.LocalMax;
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
            _hasOpenPaths = isOpen;
            _isSortedMinimaList = false;
            EnsureVertexListCapacity(end - start);
            AddPathToVertexList(nodes, start, end, polytype, isOpen);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPaths(ref PolygonInt path, PathType polytype, bool isOpen = false)
        {
            if (isOpen) _hasOpenPaths = true;
            _isSortedMinimaList = false;
            EnsureVertexListCapacity(path.nodes.Length);
            AddPathsToVertexList(path.nodes.AsArray(), path.startIDs.AsArray(), polytype, isOpen);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPaths(NativeArray<int2> nodes, NativeArray<int> startIDs, PathType polytype, bool isOpen = false)
        {
            if (isOpen) _hasOpenPaths = true;
            _isSortedMinimaList = false;
            EnsureVertexListCapacity(nodes.Length);
            AddPathsToVertexList(nodes, startIDs, polytype, isOpen);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsContributingClosed(ref Active ae)
        {
            switch (_fillrule)
            {
                case FillRule.Positive:
                    if (ae.windCount != 1) return false;
                    break;
                case FillRule.Negative:
                    if (ae.windCount != -1) return false;
                    break;
                case FillRule.NonZero:
                    if (math.abs(ae.windCount) != 1) return false;
                    break;
            }

            switch (_cliptype)
            {
                case ClipType.Intersection:
                    return _fillrule switch
                    {
                        FillRule.Positive => ae.windCount2 > 0,
                        FillRule.Negative => ae.windCount2 < 0,
                        _ => ae.windCount2 != 0,
                    };

                case ClipType.Union:
                    return _fillrule switch
                    {
                        FillRule.Positive => ae.windCount2 <= 0,
                        FillRule.Negative => ae.windCount2 >= 0,
                        _ => ae.windCount2 == 0,
                    };

                case ClipType.Difference:
                    bool result = _fillrule switch
                    {
                        FillRule.Positive => (ae.windCount2 <= 0),
                        FillRule.Negative => (ae.windCount2 >= 0),
                        _ => (ae.windCount2 == 0),
                    };
                    return (GetPolyType(ref ae) == PathType.Subject) ? result : !result;

                case ClipType.Xor:
                    return true; // XOr is always contributing unless open

                default:
                    return false;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsContributingOpen(ref Active ae)
        {
            bool isInClip, isInSubj;
            switch (_fillrule)
            {
                case FillRule.Positive:
                    isInSubj = ae.windCount > 0;
                    isInClip = ae.windCount2 > 0;
                    break;
                case FillRule.Negative:
                    isInSubj = ae.windCount < 0;
                    isInClip = ae.windCount2 < 0;
                    break;
                default:
                    isInSubj = ae.windCount != 0;
                    isInClip = ae.windCount2 != 0;
                    break;
            }

            bool result = _cliptype switch
            {
                ClipType.Intersection => isInClip,
                ClipType.Union => !isInSubj && !isInClip,
                _ => !isInClip
            };
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetWindCountForClosedPathEdge(ref Active ae, int aeID)
        {
            // Wind counts refer to polygon regions not edges, so here an edge's WindCnt
            // indicates the higher of the wind counts for the two regions touching the
            // edge. (nb: Adjacent regions can only ever have their wind counts differ by
            // one. Also, open paths have no meaningful wind directions or counts.)

            int ae2ID = ae.prevInAEL;
            // find the nearest closed path edge of the same PolyType in AEL (heading left)
            PathType pt = GetPolyType(ref ae);
            if (ae2ID != -1)
            {
                ref var ae2 = ref _activesList.ElementAt(ae2ID);
                while (ae2ID != -1 && (GetPolyType(ref ae2) != pt || IsOpen(ref ae2)))
                {
                    ae2ID = ae2.prevInAEL;
                    if (ae2ID != -1)
                        ae2 = ref _activesList.ElementAt(ae2ID);
                }
            }

            if (ae2ID == -1)
            {
                ae.windCount = ae.windDx;
                ae2ID = _activesID;
            }
            else
            {
                ref var ae2 = ref _activesList.ElementAt(ae2ID);
                if (_fillrule == FillRule.EvenOdd)
                {
                    ae.windCount = ae.windDx;
                    ae.windCount2 = ae2.windCount2;
                    ae2ID = ae2.nextInAEL;
                }
                else
                {
                    // NonZero, positive, or negative filling here ...
                    // when e2's WindCnt is in the SAME direction as its WindDx,
                    // then polygon will fill on the right of 'e2' (and 'e' will be inside)
                    // nb: neither e2.WindCnt nor e2.WindDx should ever be 0.
                    if (ae2.windCount * ae2.windDx < 0)
                    {
                        // opposite directions so 'ae' is outside 'ae2' ...
                        if (math.abs(ae2.windCount) > 1)
                        {
                            // outside prev poly but still inside another.
                            if (ae2.windDx * ae.windDx < 0)
                                // reversing direction so use the same WC
                                ae.windCount = ae2.windCount;
                            else
                                // otherwise keep 'reducing' the WC by 1 (i.e. towards 0) ...
                                ae.windCount = ae2.windCount + ae.windDx;
                        }
                        else
                            // now outside all polys of same polytype so set own WC ...
                            ae.windCount = (IsOpen(ref ae) ? 1 : ae.windDx);
                    }
                    else
                    {
                        //'ae' must be inside 'ae2'
                        if (ae2.windDx * ae.windDx < 0)
                            // reversing direction so use the same WC
                            ae.windCount = ae2.windCount;
                        else
                            // otherwise keep 'increasing' the WC by 1 (i.e. away from 0) ...
                            ae.windCount = ae2.windCount + ae.windDx;
                    }

                    ae.windCount2 = ae2.windCount2;
                    ae2ID = ae2.nextInAEL; // i.e. get ready to calc WindCnt2
                }
            }


            // update windCount2 ...            
            if (_fillrule == FillRule.EvenOdd)
            {
                while (ae2ID != aeID)
                {
                    ref var ae2 = ref _activesList.ElementAt(ae2ID);
                    if (GetPolyType(ref ae2) != pt && !IsOpen(ref ae2))
                        ae.windCount2 = (ae.windCount2 == 0 ? 1 : 0);
                    ae2ID = ae2.nextInAEL;
                }
            }
            else
            {
                while (ae2ID != aeID)
                {
                    ref var ae2 = ref _activesList.ElementAt(ae2ID);
                    if (GetPolyType(ref ae2) != pt && !IsOpen(ref ae2))
                        ae.windCount2 += ae2.windDx;
                    ae2ID = ae2.nextInAEL;
                }
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetWindCountForOpenPathEdge(ref Active ae, int aeID)
        {
            int ae2ID = _activesID;
            if (_fillrule == FillRule.EvenOdd)
            {
                int cnt1 = 0, cnt2 = 0;
                while (ae2ID != aeID)
                {
                    ref var ae2 = ref _activesList.ElementAt(ae2ID);
                    if (GetPolyType(ref ae2) == PathType.Clip)
                        cnt2++;
                    else if (!IsOpen(ref ae2))
                        cnt1++;
                    ae2ID = ae2.nextInAEL;
                }

                ae.windCount = (IsOdd(cnt1) ? 1 : 0);
                ae.windCount2 = (IsOdd(cnt2) ? 1 : 0);
            }
            else
            {
                while (ae2ID != aeID)
                {
                    ref var ae2 = ref _activesList.ElementAt(ae2ID);
                    if (GetPolyType(ref ae2) == PathType.Clip)
                        ae.windCount2 += ae2.windDx;
                    else if (!IsOpen(ref ae2))
                        ae.windCount += ae2.windDx;
                    ae2ID = ae2.nextInAEL;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidAelOrder(ref Active resident, ref Active newcomer)
        {
            if (newcomer.curX != resident.curX)
                return newcomer.curX > resident.curX;

            // get the turning direction  a1.top, a2.bot, a2.top
            double d = InternalClipper.CrossProduct(resident.top, newcomer.bot, newcomer.top);
            if (d != 0) return (d < 0);

            // edges must be collinear to get here

            // for starting open paths, place them according to
            // the direction they're about to turn
            if (!IsMaxima(ref resident) && (resident.top.y > newcomer.top.y))
            {
                return InternalClipper.CrossProduct(newcomer.bot,
                  resident.top, _vertexList.ElementAt(NextVertex(ref resident)).pt) <= 0;
            }

            if (!IsMaxima(ref newcomer) && (newcomer.top.y > resident.top.y))
            {
                return InternalClipper.CrossProduct(newcomer.bot,
                  newcomer.top, _vertexList.ElementAt(NextVertex(ref newcomer)).pt) >= 0;
            }

            long y = newcomer.bot.y;
            bool newcomerIsLeft = newcomer.isLeftBound;

            if (resident.bot.y != y || _vertexList[resident.localMin.vertex].pt.y != y)
                return newcomer.isLeftBound;
            // resident must also have just been inserted
            if (resident.isLeftBound != newcomerIsLeft)
                return newcomerIsLeft;
            if (InternalClipper.CrossProduct(PrevPrevVertex(ref resident),
                  resident.bot, resident.top) == 0) return true;
            // compare turning direction of the alternate bound
            return (InternalClipper.CrossProduct(PrevPrevVertex(ref resident),
              newcomer.bot, PrevPrevVertex(ref newcomer)) > 0) == newcomerIsLeft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertLeftEdge(ref Active ae, int aeID)
        {
            int ae2ID;

            if (_activesID == -1)
            {
                ae.prevInAEL = -1;
                ae.nextInAEL = -1;
                _activesID = aeID;
            }
            else
            {
                ref var _actives = ref _activesList.ElementAt(_activesID);
                if (!IsValidAelOrder(ref _actives, ref ae))
                {
                    ae.prevInAEL = -1;
                    ae.nextInAEL = _activesID;
                    _actives.prevInAEL = aeID;
                    _activesID = aeID;
                }
                else
                {
                    ae2ID = _activesID;
                    ref var ae2 = ref _activesList.ElementAt(ae2ID);
                    while (ae2.nextInAEL != -1 && IsValidAelOrder(ref _activesList.ElementAt(ae2.nextInAEL), ref ae))
                        ae2 = ref _activesList.ElementAt(ae2ID = ae2.nextInAEL);
                    //don't separate joined edges
                    if (ae2.joinWith == JoinWith.Right) ae2 = ref _activesList.ElementAt(ae2ID = ae2.nextInAEL);
                    ae.nextInAEL = ae2.nextInAEL;
                    if (ae2.nextInAEL != -1) _activesList.ElementAt(ae2.nextInAEL).prevInAEL = aeID;
                    ae.prevInAEL = ae2ID;
                    ae2.nextInAEL = aeID;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertRightEdge(ref Active ae, int aeID, ref Active ae2, int ae2ID)
        {
            ae2.nextInAEL = ae.nextInAEL;
            if (ae.nextInAEL != -1) _activesList.ElementAt(ae.nextInAEL).prevInAEL = ae2ID;
            ae2.prevInAEL = aeID;
            ae.nextInAEL = ae2ID;
        }
        private void InsertLocalMinimaIntoAEL(long botY)
        {
            LocalMinima localMinima;
            Vertex localMinimaVertex;
            int leftBoundID, rightBoundID;
            Active temp;
            // Add any local minima (if any) at BotY ...
            // NB horizontal local minima edges should contain locMin.vertex.prev
            while (HasLocMinAtY(botY))
            {
                localMinima = PopLocalMinima();
                localMinimaVertex = _vertexList[localMinima.vertex]; //cache to minimize cache misses
                if ((localMinimaVertex.flags & VertexFlags.OpenStart) != VertexFlags.None)
                {
                    leftBoundID = -1;
                }
                else
                {
                    temp = new Active
                    {
                        bot = localMinimaVertex.pt,
                        curX = localMinimaVertex.pt.x,
                        windDx = -1,
                        vertexTop = localMinimaVertex.prev,
                        top = _vertexList[localMinimaVertex.prev].pt,
                        outrec = -1,
                        localMin = localMinima
                    };
                    leftBoundID = _activesList.Length;
                    SetDx(ref temp);
                    _activesList.Add(temp); //deviation from Clipper2, only to enable disposal                                        
                }

                if ((localMinimaVertex.flags & VertexFlags.OpenEnd) != VertexFlags.None)
                {
                    rightBoundID = -1;
                }
                else
                {
                    temp = new Active
                    {
                        bot = localMinimaVertex.pt,
                        curX = localMinimaVertex.pt.x,
                        windDx = 1,
                        vertexTop = localMinimaVertex.next, // i.e. ascending
                        top = _vertexList[localMinimaVertex.next].pt,
                        outrec = -1,
                        localMin = localMinima
                    };
                    rightBoundID = _activesList.Length;
                    SetDx(ref temp);
                    _activesList.Add(temp); //deviation from Clipper2, only to enable disposal
                }

                // Currently LeftB is just the descending bound and RightB is the ascending.
                // Now if the LeftB isn't on the left of RightB then we need swap them.
                if (leftBoundID != -1 && rightBoundID != -1)
                {
                    ref var leftBoundTmp = ref _activesList.ElementAt(leftBoundID);
                    ref var rightBoundTmp = ref _activesList.ElementAt(rightBoundID);
                    if (IsHorizontal(ref leftBoundTmp))
                    {
                        if (IsHeadingRightHorz(ref leftBoundTmp)) SwapActivesIDs(ref leftBoundID, ref rightBoundID);
                    }
                    else if (IsHorizontal(ref rightBoundTmp))
                    {
                        if (IsHeadingLeftHorz(ref rightBoundTmp)) SwapActivesIDs(ref leftBoundID, ref rightBoundID);
                    }
                    else if (leftBoundTmp.dx < rightBoundTmp.dx)
                        SwapActivesIDs(ref leftBoundID, ref rightBoundID);
                    //so when leftBound has windDx == 1, the polygon will be oriented
                    //counter-clockwise in Cartesian coords (clockwise with inverted Y).
                }
                else if (leftBoundID == -1)
                {
                    leftBoundID = rightBoundID;
                    rightBoundID = -1;
                }

                bool contributing;
                ref var leftBound = ref _activesList.ElementAt(leftBoundID);
                leftBound.isLeftBound = true;
                InsertLeftEdge(ref leftBound, leftBoundID);

                if (IsOpen(ref leftBound))
                {
                    SetWindCountForOpenPathEdge(ref leftBound, leftBoundID);
                    contributing = IsContributingOpen(ref leftBound);
                }
                else
                {
                    SetWindCountForClosedPathEdge(ref leftBound, leftBoundID);
                    contributing = IsContributingClosed(ref leftBound);
                }

                if (rightBoundID != -1)
                {
                    ref var rightBound = ref _activesList.ElementAt(rightBoundID);
                    rightBound.windCount = leftBound.windCount;
                    rightBound.windCount2 = leftBound.windCount2;
                    InsertRightEdge(ref leftBound, leftBoundID, ref rightBound, rightBoundID); ///////

                    if (contributing)
                    {
                        AddLocalMinPoly(ref leftBound, leftBoundID, ref rightBound, rightBoundID, leftBound.bot, true);
                        if (!IsHorizontal(ref leftBound))
                            CheckJoinLeft(ref leftBound, leftBoundID, leftBound.bot);
                    }

                    if (rightBound.nextInAEL != -1)
                    {
                        ref var rightBoundNextInAEL = ref _activesList.ElementAt(rightBound.nextInAEL);
                        while (rightBound.nextInAEL != -1 &&
                                IsValidAelOrder(ref rightBoundNextInAEL, ref rightBound))
                        {
                            IntersectEdges(ref rightBound, rightBoundID, ref rightBoundNextInAEL, rightBound.nextInAEL, rightBound.bot);
                            SwapPositionsInAEL(ref rightBound, rightBoundID, ref rightBoundNextInAEL, rightBound.nextInAEL);
                            if (rightBound.nextInAEL != -1)
                                rightBoundNextInAEL = ref _activesList.ElementAt(rightBound.nextInAEL);
                        }
                    }

                    if (IsHorizontal(ref rightBound))
                        PushHorz(ref rightBound, rightBoundID);
                    else
                    {
                        CheckJoinRight(ref rightBound, rightBoundID, rightBound.bot);
                        InsertScanline(rightBound.top.y);
                    }
                }
                else if (contributing)
                    StartOpenPath(ref leftBound, leftBoundID, leftBound.bot);

                if (IsHorizontal(ref leftBound))
                    PushHorz(ref leftBound, leftBoundID);
                else
                    InsertScanline(leftBound.top.y);
            } // while (HasLocMinAtY())
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushHorz(ref Active ae, int aeID)
        {
            ae.nextInSEL = _selID;
            _selID = aeID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PopHorz(out int aeID)
        {
            aeID = _selID;
            if (_selID == -1) return false;
            _selID = _activesList.ElementAt(_selID).nextInSEL;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddLocalMinPoly(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID, long2 pt, bool isNew = false)
        {
            int outrecID = NewOutRec();
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            ae1.outrec = outrecID;
            ae2.outrec = outrecID;

            if (IsOpen(ref ae1))
            {
                outrec.owner = -1;
                outrec.isOpen = true;
                if (ae1.windDx > 0)
                    SetSides(ref outrec, ae1ID, ae2ID);
                else
                    SetSides(ref outrec, ae2ID, ae1ID);
            }
            else
            {
                outrec.isOpen = false;
                int prevHotEdgeID = GetPrevHotEdge(ref ae1);
                // e.windDx is the winding direction of the **input** paths
                // and unrelated to the winding direction of output polygons.
                // Output orientation is determined by e.outrec.frontE which is
                // the ascending edge (see AddLocalMinPoly).
                if (prevHotEdgeID != -1)
                {
                    ref var prevHotEdge = ref _activesList.ElementAt(prevHotEdgeID);
                    if (_using_polytree)
                        SetOwner(ref outrec, outrecID, ref _outrecList.ElementAt(prevHotEdge.outrec), prevHotEdge.outrec);
                    outrec.owner = prevHotEdge.outrec;
                    if (OutrecIsAscending(ref prevHotEdge, prevHotEdgeID) == isNew)
                        SetSides(ref outrec, ae2ID, ae1ID);
                    else
                        SetSides(ref outrec, ae1ID, ae2ID);
                }
                else
                {
                    outrec.owner = -1;
                    if (isNew)
                        SetSides(ref outrec, ae1ID, ae2ID);
                    else
                        SetSides(ref outrec, ae2ID, ae1ID);
                }
            }

            int opID = NewOutPt(pt, outrecID);
            outrec.pts = opID;
            return opID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddLocalMaxPoly(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID, long2 pt)
        {
            if (IsJoined(ref ae1)) Split(ref ae1, ae1ID, pt);
            if (IsJoined(ref ae2)) Split(ref ae2, ae2ID, pt);

            if (IsFront(ref ae1, ae1ID) == IsFront(ref ae2, ae2ID))
            {
                if (IsOpenEnd(ref ae1))
                    SwapFrontBackSides(ref _outrecList.ElementAt(ae1.outrec));
                else if (IsOpenEnd(ref ae2))
                    SwapFrontBackSides(ref _outrecList.ElementAt(ae2.outrec));
                else
                {
                    _succeeded = false;
                    return -1;
                }
            }

            int result = AddOutPt(ref ae1, ae1ID, pt);
            if (ae1.outrec == ae2.outrec)
            {
                ref var outrec = ref _outrecList.ElementAt(ae1.outrec);
                outrec.pts = result;

                if (_using_polytree)
                {
                    int eID = GetPrevHotEdge(ref ae1);
                    if (eID == -1)
                        outrec.owner = -1;
                    else
                    {
                        ref var e = ref _activesList.ElementAt(eID);
                        SetOwner(ref outrec, ae1.outrec, ref _outrecList.ElementAt(e.outrec), e.outrec);
                    }
                    // nb: outRec.owner here is likely NOT the real
                    // owner but this will be fixed in DeepCheckOwner()
                }
                UncoupleOutRec(ref ae1);
            }
            // and to preserve the winding orientation of outrec ...
            else if (IsOpen(ref ae1))
            {
                if (ae1.windDx < 0)
                    JoinOutrecPaths(ref ae1, ae1ID, ref ae2, ae2ID);
                else
                    JoinOutrecPaths(ref ae2, ae2ID, ref ae1, ae1ID);
            }
            else if (_outrecList.ElementAt(ae1.outrec).idx < _outrecList.ElementAt(ae2.outrec).idx) //replace with ae2.outrec < ae2.outrec (position in list is identical to idx)
                JoinOutrecPaths(ref ae1, ae1ID, ref ae2, ae2ID);
            else
                JoinOutrecPaths(ref ae2, ae2ID, ref ae1, ae1ID);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void JoinOutrecPaths(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID)
        {
            // join ae2 outrec path onto ae1 outrec path and then delete ae2 outrec path
            // pointers. (NB Only very rarely do the joining ends share the same coords.)
            ref var ae1Outrec = ref _outrecList.ElementAt(ae1.outrec);
            ref var ae2Outrec = ref _outrecList.ElementAt(ae2.outrec);
            int p1StartID = ae1Outrec.pts;
            int p2StartID = ae2Outrec.pts;
            ref var p1Start = ref _outPtList.ElementAt(p1StartID);
            ref var p2Start = ref _outPtList.ElementAt(p2StartID);
            int p1EndID = p1Start.next;
            int p2EndID = p2Start.next;
            ref var p1End = ref _outPtList.ElementAt(p1EndID);
            ref var p2End = ref _outPtList.ElementAt(p2EndID);
            if (IsFront(ref ae1, ae1ID))
            {
                p2End.prev = p1StartID;
                p1Start.next = p2EndID;
                p2Start.next = p1EndID;
                p1End.prev = p2StartID;
                ae1Outrec.pts = p2StartID;
                // nb: if IsOpen(e1) then e1 & e2 must be a 'maximaPair'
                ae1Outrec.frontEdge = ae2Outrec.frontEdge;
                if (ae1Outrec.frontEdge != -1)
                    _activesList.ElementAt(ae1Outrec.frontEdge).outrec = ae1.outrec;
            }
            else
            {
                p1End.prev = p2StartID;
                p2Start.next = p1EndID;
                p1Start.next = p2EndID;
                p2End.prev = p1StartID;

                ae1Outrec.backEdge = ae2Outrec.backEdge;
                if (ae1Outrec.backEdge != -1)
                    _activesList.ElementAt(ae1Outrec.backEdge).outrec = ae1.outrec;
            }

            // after joining, the ae2.OutRec must contains no vertices ...
            ae2Outrec.frontEdge = -1;
            ae2Outrec.backEdge = -1;
            ae2Outrec.pts = -1;
            SetOwner(ref ae2Outrec, ae2.outrec, ref ae1Outrec, ae1.outrec);

            if (IsOpenEnd(ref ae1))
            {
                ae2Outrec.pts = ae1Outrec.pts;
                ae1Outrec.pts = -1;
            }

            // and ae1 and ae2 are maxima and are about to be dropped from the Actives list.
            ae1.outrec = -1;
            ae2.outrec = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int AddOutPt(ref Active ae, int aeID, long2 pt)
        {
            // Outrec.OutPts: a circular doubly-linked-list of POutPt where ...
            // opFront[.Prev]* ~~~> opBack & opBack == opFront.Next
            int outrecID = ae.outrec;
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            bool toFront = IsFront(ref ae, aeID);
            int opFrontID = outrec.pts;
            ref var opFront = ref _outPtList.ElementAt(opFrontID);
            int opBackID = opFront.next;
            ref var opBack = ref _outPtList.ElementAt(opBackID);

            if (toFront && (pt == opFront.pt)) return opFrontID;
            else if (!toFront && (pt == opBack.pt)) return opBackID;

            int newOpID = NewOutPt(pt, outrecID);
            opFront = ref _outPtList.ElementAt(opFrontID);//fetch again due to invalidated references
            opBack = ref _outPtList.ElementAt(opBackID);//fetch again due to invalidated references
            ref var newOp = ref _outPtList.ElementAt(newOpID);
            opBack.prev = newOpID;
            newOp.prev = opFrontID;
            newOp.next = opBackID;
            opFront.next = newOpID;
            if (toFront) outrec.pts = newOpID;
            return newOpID;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NewOutRec()
        {
            int currentID = _outrecList.Length;
            OutRec result = new OutRec
            {
                idx = currentID,
                splitStart = -1,
                polypath = -1,
                owner = -1,
                frontEdge = -1,
                backEdge = -1,
                pts = -1,
            };
            _outrecList.Add(result);
            return currentID;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int NewOutPt(long2 pt, int outrecID)
        {
            int currentID = _outPtList.Length;
            OutPt result = new OutPt { pt = pt, outrec = outrecID };
            result.next = currentID;
            result.prev = currentID;
            result.horz = -1;
            _outPtList.Add(result);
            return currentID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int StartOpenPath(ref Active ae, int aeID, long2 pt)
        {
            int outrecID = NewOutRec();
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            outrec.isOpen = true;
            if (ae.windDx > 0)
            {
                outrec.frontEdge = aeID;
                outrec.backEdge = -1;
            }
            else
            {
                outrec.frontEdge = -1;
                outrec.backEdge = aeID;
            }

            ae.outrec = outrecID;
            int op = NewOutPt(pt, outrecID);
            outrec.pts = op;
            return op;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateEdgeIntoAEL(ref Active ae, int aeID)
        {
            ae.bot = ae.top;
            ae.vertexTop = NextVertex(ref ae);
            ae.top = _vertexList.ElementAt(ae.vertexTop).pt;
            ae.curX = ae.bot.x;
            SetDx(ref ae);

            if (IsJoined(ref ae)) Split(ref ae, aeID, ae.bot);

            if (IsHorizontal(ref ae)) return;
            InsertScanline(ae.top.y);

            CheckJoinLeft(ref ae, aeID, ae.bot);
            CheckJoinRight(ref ae, aeID, ae.bot, true); // (#500)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int FindEdgeWithMatchingLocMin(ref Active e)
        {
            int resultID = e.nextInAEL;
            while (resultID != -1)
            {
                ref var result = ref _activesList.ElementAt(resultID);
                if (result.localMin == e.localMin) return resultID;
                if (!IsHorizontal(ref result) && e.bot != result.bot) resultID = -1;
                else resultID = result.nextInAEL;
            }
            resultID = e.prevInAEL;
            while (resultID != -1)
            {
                ref var result = ref _activesList.ElementAt(resultID);
                if (result.localMin == e.localMin) return resultID;
                if (!IsHorizontal(ref result) && e.bot != result.bot) return -1;
                resultID = result.prevInAEL;
            }
            return resultID;
        }

        private int IntersectEdges(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID, long2 pt)
        {
            int resultOp = -1;

            // MANAGE OPEN PATH INTERSECTIONS SEPARATELY ...
            if (_hasOpenPaths && (IsOpen(ref ae1) || IsOpen(ref ae2)))
            {
                if (IsOpen(ref ae1) && IsOpen(ref ae2)) return -1;
                // the following line avoids duplicating quite a bit of code
                if (IsOpen(ref ae2)) //swap ae1 and ae2 just locally within this function
                {
                    ae1 = ref _activesList.ElementAt(ae2ID);
                    ae2 = ref _activesList.ElementAt(ae1ID);
                }
                if (IsJoined(ref ae2)) Split(ref ae2, ae2ID, pt); // needed for safety

                if (_cliptype == ClipType.Union)
                {
                    if (!IsHotEdge(ref ae2)) return -1;
                }
                else if (ae2.localMin.polytype == PathType.Subject)
                    return -1;

                switch (_fillrule)
                {
                    case FillRule.Positive:
                        if (ae2.windCount != 1) return -1; break;
                    case FillRule.Negative:
                        if (ae2.windCount != -1) return -1; break;
                    default:
                        if (math.abs(ae2.windCount) != 1) return -1; break;
                }

                ref var ae1LocalMinVertex = ref _vertexList.ElementAt(ae1.localMin.vertex);
                // toggle contribution ...
                if (IsHotEdge(ref ae1))
                {
                    resultOp = AddOutPt(ref ae1, ae1ID, pt);
                    if (IsFront(ref ae1, ae1ID))
                        _outrecList.ElementAt(ae1.outrec).frontEdge = -1;
                    else
                        _outrecList.ElementAt(ae1.outrec).backEdge = -1;
                    ae1.outrec = -1;
                }

                // horizontal edges can pass under open paths at a LocMins
                else if (pt == ae1LocalMinVertex.pt &&
                  !IsOpenEnd(ref ae1LocalMinVertex))
                {
                    // find the other side of the LocMin and
                    // if it's 'hot' join up with it ...
                    int ae3ID = FindEdgeWithMatchingLocMin(ref ae1);
                    if (ae3ID != -1)
                    {
                        ref var ae3 = ref _activesList.ElementAt(ae3ID);
                        if (IsHotEdge(ref ae3))
                        {
                            ae1.outrec = ae3.outrec;
                            ref var ae3Outrec = ref _outrecList.ElementAt(ae3.outrec);
                            if (ae1.windDx > 0)
                                SetSides(ref ae3Outrec, ae1ID, ae3ID);
                            else
                                SetSides(ref ae3Outrec, ae3ID, ae1ID);
                            return ae3Outrec.pts;
                        }
                    }

                    resultOp = StartOpenPath(ref ae1, ae1ID, pt);
                }
                else
                    resultOp = StartOpenPath(ref ae1, ae1ID, pt);

                return resultOp;
            }

            // MANAGING CLOSED PATHS FROM HERE ON
            if (IsJoined(ref ae1)) Split(ref ae1, ae1ID, pt);
            if (IsJoined(ref ae2)) Split(ref ae2, ae2ID, pt);

            // UPDATE WINDING COUNTS...

            int oldE1WindCount, oldE2WindCount;
            if (ae1.localMin.polytype == ae2.localMin.polytype)
            {
                if (_fillrule == FillRule.EvenOdd)
                {
                    oldE1WindCount = ae1.windCount;
                    ae1.windCount = ae2.windCount;
                    ae2.windCount = oldE1WindCount;
                }
                else
                {
                    if (ae1.windCount + ae2.windDx == 0)
                        ae1.windCount = -ae1.windCount;
                    else
                        ae1.windCount += ae2.windDx;
                    if (ae2.windCount - ae1.windDx == 0)
                        ae2.windCount = -ae2.windCount;
                    else
                        ae2.windCount -= ae1.windDx;
                }
            }
            else
            {
                if (_fillrule != FillRule.EvenOdd)
                    ae1.windCount2 += ae2.windDx;
                else
                    ae1.windCount2 = (ae1.windCount2 == 0 ? 1 : 0);
                if (_fillrule != FillRule.EvenOdd)
                    ae2.windCount2 -= ae1.windDx;
                else
                    ae2.windCount2 = (ae2.windCount2 == 0 ? 1 : 0);
            }

            switch (_fillrule)
            {
                case FillRule.Positive:
                    oldE1WindCount = ae1.windCount;
                    oldE2WindCount = ae2.windCount;
                    break;
                case FillRule.Negative:
                    oldE1WindCount = -ae1.windCount;
                    oldE2WindCount = -ae2.windCount;
                    break;
                default:
                    oldE1WindCount = math.abs(ae1.windCount);
                    oldE2WindCount = math.abs(ae2.windCount);
                    break;
            }

            bool e1WindCountIs0or1 = oldE1WindCount == 0 || oldE1WindCount == 1;
            bool e2WindCountIs0or1 = oldE2WindCount == 0 || oldE2WindCount == 1;

            if ((!IsHotEdge(ref ae1) && !e1WindCountIs0or1) || (!IsHotEdge(ref ae2) && !e2WindCountIs0or1)) return -1;

            // NOW PROCESS THE INTERSECTION ...

            // if both edges are 'hot' ...
            if (IsHotEdge(ref ae1) && IsHotEdge(ref ae2))
            {
                if ((oldE1WindCount != 0 && oldE1WindCount != 1) || (oldE2WindCount != 0 && oldE2WindCount != 1) ||
                    (ae1.localMin.polytype != ae2.localMin.polytype && _cliptype != ClipType.Xor))
                {
                    resultOp = AddLocalMaxPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                }
                else if (IsFront(ref ae1, ae1ID) || (ae1.outrec == ae2.outrec))
                {
                    // this 'else if' condition isn't strictly needed but
                    // it's sensible to split polygons that ony touch at
                    // a common vertex (not at common edges).
                    resultOp = AddLocalMaxPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                    AddLocalMinPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                }
                else
                {
                    // can't treat as maxima & minima
                    resultOp = AddOutPt(ref ae1, ae1ID, pt);

                    AddOutPt(ref ae2, ae2ID, pt);
                    SwapOutrecs(ref ae1, ae1ID, ref ae2, ae2ID);
                }
            }

            // if one or other edge is 'hot' ...
            else if (IsHotEdge(ref ae1))
            {
                resultOp = AddOutPt(ref ae1, ae1ID, pt);
                SwapOutrecs(ref ae1, ae1ID, ref ae2, ae2ID);
            }
            else if (IsHotEdge(ref ae2))
            {
                resultOp = AddOutPt(ref ae2, ae2ID, pt);
                SwapOutrecs(ref ae1, ae1ID, ref ae2, ae2ID);
            }

            // neither edge is 'hot'
            else
            {
                long e1Wc2, e2Wc2;
                switch (_fillrule)
                {
                    case FillRule.Positive:
                        e1Wc2 = ae1.windCount2;
                        e2Wc2 = ae2.windCount2;
                        break;
                    case FillRule.Negative:
                        e1Wc2 = -ae1.windCount2;
                        e2Wc2 = -ae2.windCount2;
                        break;
                    default:
                        e1Wc2 = Math.Abs(ae1.windCount2);
                        e2Wc2 = Math.Abs(ae2.windCount2);
                        break;
                }

                if (!IsSamePolyType(ref ae1, ref ae2))
                {
                    resultOp = AddLocalMinPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                }
                else if (oldE1WindCount == 1 && oldE2WindCount == 1)
                {
                    resultOp = -1;
                    switch (_cliptype)
                    {
                        case ClipType.Union:
                            if (e1Wc2 > 0 && e2Wc2 > 0) return -1;
                            resultOp = AddLocalMinPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                            break;

                        case ClipType.Difference:
                            if (((GetPolyType(ref ae1) == PathType.Clip) && (e1Wc2 > 0) && (e2Wc2 > 0)) ||
                                ((GetPolyType(ref ae1) == PathType.Subject) && (e1Wc2 <= 0) && (e2Wc2 <= 0)))
                            {
                                resultOp = AddLocalMinPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                            }

                            break;

                        case ClipType.Xor:
                            resultOp = AddLocalMinPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                            break;

                        default: // ClipType.Intersection:
                            if (e1Wc2 <= 0 || e2Wc2 <= 0) return -1;
                            resultOp = AddLocalMinPoly(ref ae1, ae1ID, ref ae2, ae2ID, pt);
                            break;
                    }
                }
            }

            return resultOp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeleteFromAEL(ref Active ae, int aeID)
        {
            int prevID = ae.prevInAEL;
            int nextID = ae.nextInAEL;
            if (prevID == -1 && nextID == -1 && (aeID != _activesID)) return; // already deleted
            if (prevID != -1)
                _activesList.ElementAt(prevID).nextInAEL = nextID;
            else
                _activesID = nextID;
            if (nextID != -1) _activesList.ElementAt(nextID).prevInAEL = prevID;
            //delete &ae;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdjustCurrXAndCopyToSEL(long topY)
        {
            int aeID = _activesID;
            _selID = aeID;
            while (aeID != -1)
            {
                ref var ae = ref _activesList.ElementAt(aeID);
                ae.prevInSEL = ae.prevInAEL;
                ae.nextInSEL = ae.nextInAEL;
                ae.jump = ae.nextInSEL;
                if (ae.joinWith == JoinWith.Left)
                    ae.curX = _activesList.ElementAt(ae.prevInAEL).curX; // this also avoids complications
                else
                    ae.curX = TopX(ref ae, topY);
                // NB don't update ae.curr.Y yet (see AddNewIntersectNode)
                aeID = ae.nextInAEL;
            }
        }

        void ExecuteInternal(ClipType ct, FillRule fillRule)
        {
            if (ct == ClipType.None) return;
            _fillrule = fillRule;
            _cliptype = ct;
            Reset();
            if (!PopScanline(out long y)) return;
            while (_succeeded)
            {
                InsertLocalMinimaIntoAEL(y);
                int aeID;
                while (PopHorz(out aeID)) DoHorizontal(aeID);
                if (_horzSegList.Length > 0)
                {
                    ConvertHorzSegsToJoins();
                    _horzSegList.Clear();
                }
                _currentBotY = y; // bottom of scanbeam
                if (!PopScanline(out y))
                    break; // y new top of scanbeam
                DoIntersections(y);
                DoTopOfScanbeam(y);
                while (PopHorz(out aeID)) DoHorizontal(aeID);
            }
            if (_succeeded) ProcessHorzJoins();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoIntersections(long topY)
        {
            if (BuildIntersectList(topY))
            {
                ProcessIntersectList();
                _intersectList.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNewIntersectNode(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID, long topY)
        {
            if (!InternalClipper.GetIntersectPt(
              ae1.bot, ae1.top, ae2.bot, ae2.top, out long2 ip))
                ip = new long2(ae1.curX, topY);
            //Debug.Log($"{ip.x:D9} {ip.y:D9} (SoA new intersect node)");
            if (ip.y > _currentBotY || ip.y < topY)
            {
                double absDx1 = math.abs(ae1.dx);
                double absDx2 = math.abs(ae2.dx);
                if (absDx1 > 100 && absDx2 > 100)
                {
                    if (absDx1 > absDx2)
                        ip = InternalClipper.GetClosestPtOnSegment(ip, ae1.bot, ae1.top);
                    else
                        ip = InternalClipper.GetClosestPtOnSegment(ip, ae2.bot, ae2.top);
                }
                else if (absDx1 > 100)
                    ip = InternalClipper.GetClosestPtOnSegment(ip, ae1.bot, ae1.top);
                else if (absDx2 > 100)
                    ip = InternalClipper.GetClosestPtOnSegment(ip, ae2.bot, ae2.top);
                else
                {
                    if (ip.y < topY) ip.y = topY;
                    else ip.y = _currentBotY;
                    if (absDx1 < absDx2) ip.x = TopX(ref ae1, ip.y);
                    else ip.x = TopX(ref ae2, ip.y);
                }
            }
            IntersectNode node = new IntersectNode(ip, ae1ID, ae2ID);
            _intersectList.Add(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ExtractFromSEL(ref Active ae)
        {
            int resultID = ae.nextInSEL;
            if (resultID != -1)
                _activesList.ElementAt(resultID).prevInSEL = ae.prevInSEL;
            _activesList.ElementAt(ae.prevInSEL).nextInSEL = resultID;
            return resultID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Insert1Before2InSEL(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID)
        {
            ae1.prevInSEL = ae2.prevInSEL;
            if (ae1.prevInSEL != -1)
                _activesList.ElementAt(ae1.prevInSEL).nextInSEL = ae1ID;
            ae1.nextInSEL = ae2ID;
            ae2.prevInSEL = ae1ID;
        }
        private bool BuildIntersectList(long topY)
        {
            if (_activesID == -1 || _activesList.ElementAt(_activesID).nextInAEL == -1) return false;

            // Calculate edge positions at the top of the current scanbeam, and from this
            // we will determine the intersections required to reach these new positions.
            AdjustCurrXAndCopyToSEL(topY);

            // Find all edge intersections in the current scanbeam using a stable merge
            // sort that ensures only adjacent edges are intersecting. Intersect info is
            // stored in FIntersectList ready to be processed in ProcessIntersectList.
            // Re merge sorts see https://stackoverflow.com/a/46319131/359538

            int leftID = _selID, rightID = -1, lEndID = -1, rEndID = -1, currBaseID = -1, prevBaseID = -1, tmpID = -1;

            ref var left = ref _activesList.ElementAt(leftID);
            while (left.jump != -1)
            {
                prevBaseID = -1;
                while (leftID != -1 && (left = ref _activesList.ElementAt(leftID)).jump != -1)
                {
                    currBaseID = leftID;
                    rightID = left.jump;
                    ref var right = ref _activesList.ElementAt(rightID);
                    lEndID = rightID;
                    rEndID = right.jump;
                    left.jump = rEndID;
                    while (leftID != lEndID && rightID != rEndID)
                    {
                        left = ref _activesList.ElementAt(leftID); //while redundant for first iteration, leftID could change during loop, so need to fetch it again
                        right = ref _activesList.ElementAt(rightID); //while redundant for first iteration, leftID could change during loop, so need to fetch it again
                        if (right.curX < left.curX)
                        {
                            tmpID = right.prevInSEL;
                            ref var tmp = ref _activesList.ElementAt(tmpID);
                            for (; ; )
                            {
                                AddNewIntersectNode(ref tmp, tmpID, ref right, rightID, topY);
                                if (tmpID == leftID) break;
                                tmpID = tmp.prevInSEL!;
                                tmp = ref _activesList.ElementAt(tmpID);
                            }

                            tmpID = rightID;
                            tmp = ref _activesList.ElementAt(tmpID);
                            rightID = ExtractFromSEL(ref tmp);
                            lEndID = rightID;
                            Insert1Before2InSEL(ref tmp, tmpID, ref left, leftID);
                            if (leftID == currBaseID)
                            {
                                currBaseID = tmpID;
                                _activesList.ElementAt(currBaseID).jump = rEndID;
                                if (prevBaseID == -1) _selID = currBaseID;
                                else _activesList.ElementAt(prevBaseID).jump = currBaseID;
                            }
                        }
                        else leftID = left.nextInSEL;
                    }

                    prevBaseID = currBaseID;
                    leftID = rEndID;
                }
                leftID = _selID;
                left = ref _activesList.ElementAt(leftID);
            }

            return _intersectList.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessIntersectList()
        {
            // We now have a list of intersections required so that edges will be
            // correctly positioned at the top of the scanbeam. However, it's important
            // that edge intersections are processed from the bottom up, but it's also
            // crucial that intersections only occur between adjacent edges.

            // First we do a quicksort so intersections proceed in a bottom up order ...
            _intersectList.Sort(new IntersectListSort());

            // Now as we process these intersections, we must sometimes adjust the order
            // to ensure that intersecting edges are always adjacent ...
            for (int i = 0; i < _intersectList.Length; ++i)
            {
                if (!EdgesAdjacentInAEL(_intersectList[i]))
                {
                    int j = i + 1;
                    while (!EdgesAdjacentInAEL(_intersectList[j])) j++;
                    // swap
                    (_intersectList[j], _intersectList[i]) =
                      (_intersectList[i], _intersectList[j]);
                }

                IntersectNode node = _intersectList[i];
                ref var nodeEdge1 = ref _activesList.ElementAt(node.edge1);
                ref var nodeEdge2 = ref _activesList.ElementAt(node.edge2);
                IntersectEdges(ref nodeEdge1, node.edge1, ref nodeEdge2, node.edge2, node.pt);
                SwapPositionsInAEL(ref nodeEdge1, node.edge1, ref nodeEdge2, node.edge2);
                nodeEdge1.curX = node.pt.x;
                nodeEdge2.curX = node.pt.x;
                CheckJoinLeft(ref nodeEdge2, node.edge2, node.pt, true);
                CheckJoinRight(ref nodeEdge1, node.edge1, node.pt, true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SwapPositionsInAEL(ref Active ae1, int ae1ID, ref Active ae2, int ae2ID)
        {
            // preconditon: ae1 must be immediately to the left of ae2
            int nextID = ae2.nextInAEL;
            if (nextID != -1) _activesList.ElementAt(nextID).prevInAEL = ae1ID;
            int prevID = ae1.prevInAEL;
            if (prevID != -1) _activesList.ElementAt(prevID).nextInAEL = ae2ID;
            ae2.prevInAEL = prevID;
            ae2.nextInAEL = ae1ID;
            ae1.prevInAEL = ae2ID;
            ae1.nextInAEL = nextID;
            if (ae2.prevInAEL == -1) _activesID = ae2ID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ResetHorzDirection(ref Active horz, int vertexMax,
                out long leftX, out long rightX)
        {
            if (horz.bot.x == horz.top.x)
            {
                // the horizontal edge is going nowhere ...
                leftX = horz.curX;
                rightX = horz.curX;
                int aeID = horz.nextInAEL;
                if (aeID != -1)
                {
                    ref var ae = ref _activesList.ElementAt(aeID);
                    while (aeID != -1 && ae.vertexTop != vertexMax)
                    {
                        aeID = ae.nextInAEL;
                        if (aeID != -1)
                            ae = ref _activesList.ElementAt(aeID);
                    }
                }
                return aeID != -1;
            }

            if (horz.curX < horz.top.x)
            {
                leftX = horz.curX;
                rightX = horz.top.x;
                return true;
            }
            leftX = horz.top.x;
            rightX = horz.curX;
            return false; // right to left
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HorzIsSpike(ref Active horz)
        {
            long2 nextPt = _vertexList.ElementAt(NextVertex(ref horz)).pt;
            return (horz.bot.x < horz.top.x) != (horz.top.x < nextPt.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TrimHorz(ref Active horzEdge, bool preserveCollinear)
        {
            bool wasTrimmed = false;
            long2 pt = _vertexList.ElementAt(NextVertex(ref horzEdge)).pt;

            while (pt.y == horzEdge.top.y)
            {
                // always trim 180 deg. spikes (in closed paths)
                // but otherwise break if preserveCollinear = true
                if (preserveCollinear &&
                (pt.x < horzEdge.top.x) != (horzEdge.bot.x < horzEdge.top.x))
                    break;

                horzEdge.vertexTop = NextVertex(ref horzEdge);
                horzEdge.top = pt;
                wasTrimmed = true;
                if (IsMaxima(ref horzEdge)) break;
                pt = _vertexList.ElementAt(NextVertex(ref horzEdge)).pt;
            }
            if (wasTrimmed) SetDx(ref horzEdge); // +/-infinity
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToHorzSegList(ref OutPt op, int opID)
        {
            if (_outrecList.ElementAt(op.outrec).isOpen) return;
            _horzSegList.Add(new HorzSegment(opID));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLastOp(ref Active hotEdge, int hotEdgeID)
        {
            ref var outrec = ref _outrecList.ElementAt(hotEdge.outrec);
            return (hotEdgeID == outrec.frontEdge) ?
              outrec.pts : _outPtList.ElementAt(outrec.pts).next;
        }

        private void DoHorizontal(int horzID)
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
            ref var horz = ref _activesList.ElementAt(horzID);
            long2 pt;
            bool horzIsOpen = IsOpen(ref horz);
            long Y = horz.bot.y;

            int vertex_max = horzIsOpen ?
              GetCurrYMaximaVertex_Open(ref horz) :
              GetCurrYMaximaVertex(ref horz);

            // remove 180 deg.spikes and also simplify
            // consecutive horizontals when PreserveCollinear = true
            if (vertex_max != -1 &&
              !horzIsOpen && vertex_max != horz.vertexTop)
                TrimHorz(ref horz, PreserveCollinear);

            bool isLeftToRight =
              ResetHorzDirection(ref horz, vertex_max, out long leftX, out long rightX);

            if (IsHotEdge(ref horz))
            {
                int opID = AddOutPt(ref horz, horzID, new long2(horz.curX, Y));
                ref var op = ref _outPtList.ElementAt(opID);
                AddToHorzSegList(ref op, opID);
            }
            int currOutrec = horz.outrec;

            for (; ; )
            {
                // loops through consec. horizontal edges (if open)
                int aeID = isLeftToRight ? horz.nextInAEL : horz.prevInAEL;
                while (aeID != -1)
                {
                    ref var ae = ref _activesList.ElementAt(aeID);
                    if (ae.vertexTop == vertex_max)
                    {
                        // do this first!!
                        if (IsHotEdge(ref horz) && IsJoined(ref ae)) Split(ref ae, aeID, ae.top);

                        if (IsHotEdge(ref horz))
                        {
                            while (horz.vertexTop != vertex_max)
                            {
                                AddOutPt(ref horz, horzID, horz.top);
                                UpdateEdgeIntoAEL(ref horz, horzID);
                            }
                            if (isLeftToRight)
                                AddLocalMaxPoly(ref horz, horzID, ref ae, aeID, horz.top);
                            else
                                AddLocalMaxPoly(ref ae, aeID, ref horz, horzID, horz.top);
                        }
                        DeleteFromAEL(ref ae, aeID);
                        DeleteFromAEL(ref horz, horzID);
                        return;
                    }

                    // if horzEdge is a maxima, keep going until we reach
                    // its maxima pair, otherwise check for break conditions
                    if (vertex_max != horz.vertexTop || IsOpenEnd(ref horz))
                    {
                        // otherwise stop when 'ae' is beyond the end of the horizontal line
                        if ((isLeftToRight && ae.curX > rightX) ||
                            (!isLeftToRight && ae.curX < leftX)) break;

                        if (ae.curX == horz.top.x && !IsHorizontal(ref ae))
                        {
                            pt = _vertexList.ElementAt(NextVertex(ref horz)).pt;

                            // to maximize the possibility of putting open edges into
                            // solutions, we'll only break if it's past HorzEdge's end
                            if (IsOpen(ref ae) && !IsSamePolyType(ref ae, ref horz) && !IsHotEdge(ref ae))
                            {
                                if ((isLeftToRight && (TopX(ref ae, pt.y) > pt.x)) ||
                                  (!isLeftToRight && (TopX(ref ae, pt.y) < pt.x))) break;
                            }
                            // otherwise for edges at horzEdge's end, only stop when horzEdge's
                            // outslope is greater than e's slope when heading right or when
                            // horzEdge's outslope is less than e's slope when heading left.
                            else if ((isLeftToRight && (TopX(ref ae, pt.y) >= pt.x)) ||
                                (!isLeftToRight && (TopX(ref ae, pt.y) <= pt.x))) break;
                        }
                    }

                    pt = new long2(ae.curX, Y);

                    if (isLeftToRight)
                    {
                        IntersectEdges(ref horz, horzID, ref ae, aeID, pt);
                        SwapPositionsInAEL(ref horz, horzID, ref ae, aeID);
                        horz.curX = ae.curX;
                        aeID = horz.nextInAEL;
                    }
                    else
                    {
                        IntersectEdges(ref ae, aeID, ref horz, horzID, pt);
                        SwapPositionsInAEL(ref ae, aeID, ref horz, horzID);
                        horz.curX = ae.curX;
                        aeID = horz.prevInAEL;
                    }

                    if (IsHotEdge(ref horz) && (horz.outrec != currOutrec))
                    {
                        currOutrec = horz.outrec;
                        var opID = GetLastOp(ref horz, horzID);
                        AddToHorzSegList(ref _outPtList.ElementAt(opID), opID);
                    }

                } // we've reached the end of this horizontal

                // check if we've finished looping
                // through consecutive horizontals
                if (horzIsOpen && IsOpenEnd(ref horz)) // ie open at top
                {
                    if (IsHotEdge(ref horz))
                    {
                        AddOutPt(ref horz, horzID, horz.top);
                        if (IsFront(ref horz, horzID))
                            _outrecList.ElementAt(horz.outrec).frontEdge = -1;
                        else
                            _outrecList.ElementAt(horz.outrec).backEdge = -1;
                        horz.outrec = -1;
                    }
                    DeleteFromAEL(ref horz, horzID);
                    return;
                }
                else if (_vertexList.ElementAt(NextVertex(ref horz)).pt.y != horz.top.y)
                    break;

                //still more horizontals in bound to process ...
                if (IsHotEdge(ref horz))
                    AddOutPt(ref horz, horzID, horz.top);
                UpdateEdgeIntoAEL(ref horz, horzID);

                if (PreserveCollinear && !horzIsOpen && HorzIsSpike(ref horz))
                    TrimHorz(ref horz, true);

                isLeftToRight = ResetHorzDirection(ref horz,
                  vertex_max, out leftX, out rightX);

            } // end for loop and end of (possible consecutive) horizontals

            if (IsHotEdge(ref horz))
            {
                var opID = AddOutPt(ref horz, horzID, horz.top);
                ref var op = ref _outPtList.ElementAt(opID);
                AddToHorzSegList(ref op, opID);
            }

            UpdateEdgeIntoAEL(ref horz, horzID); // this is the end of an intermediate horiz.
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoTopOfScanbeam(long y)
        {
            _selID = -1; // sel_ is reused to flag horizontals (see PushHorz below)
            int aeID = _activesID;
            while (aeID != -1)
            {
                ref var ae = ref _activesList.ElementAt(aeID);
                // NB 'ae' will never be horizontal here
                if (ae.top.y == y)
                {
                    ae.curX = ae.top.x;
                    if (IsMaxima(ref ae))
                    {
                        aeID = DoMaxima(ref ae, aeID); // TOP OF BOUND (MAXIMA)                        
                        continue;
                    }

                    // INTERMEDIATE VERTEX ...
                    if (IsHotEdge(ref ae))
                        AddOutPt(ref ae, aeID, ae.top);
                    UpdateEdgeIntoAEL(ref ae, aeID);
                    if (IsHorizontal(ref ae))
                        PushHorz(ref ae, aeID); // horizontals are processed later
                }
                else // i.e. not the top of the edge
                    ae.curX = TopX(ref ae, y);

                aeID = ae.nextInAEL;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DoMaxima(ref Active ae, int aeID)
        {
            int prevEID;
            int nextEID, maxPairID;
            prevEID = ae.prevInAEL;
            nextEID = ae.nextInAEL;

            if (IsOpenEnd(ref ae))
            {
                if (IsHotEdge(ref ae)) AddOutPt(ref ae, aeID, ae.top);
                if (!IsHorizontal(ref ae))
                {
                    if (IsHotEdge(ref ae))
                    {
                        ref var aeOutrec = ref _outrecList.ElementAt(ae.outrec);
                        if (IsFront(ref ae, aeID))
                            aeOutrec.frontEdge = -1;
                        else
                            aeOutrec.backEdge = -1;
                        ae.outrec = -1;
                    }
                    DeleteFromAEL(ref ae, aeID);
                }
                return nextEID;
            }

            maxPairID = GetMaximaPair(ref ae);
            if (maxPairID == -1) return nextEID; // eMaxPair is horizontal

            ref var maxPair = ref _activesList.ElementAt(maxPairID);
            if (IsJoined(ref ae)) Split(ref ae, aeID, ae.top);
            if (IsJoined(ref maxPair)) Split(ref maxPair, maxPairID, maxPair.top);

            // only non-horizontal maxima here.
            // process any edges between maxima pair ...
            while (nextEID != maxPairID)
            {
                ref var nextE = ref _activesList.ElementAt(nextEID);
                IntersectEdges(ref ae, aeID, ref nextE, nextEID, ae.top);
                SwapPositionsInAEL(ref ae, aeID, ref nextE, nextEID);
                nextEID = ae.nextInAEL;
            }

            if (IsOpen(ref ae))
            {
                if (IsHotEdge(ref ae))
                    AddLocalMaxPoly(ref ae, aeID, ref maxPair, maxPairID, ae.top);
                DeleteFromAEL(ref maxPair, maxPairID);
                DeleteFromAEL(ref ae, aeID);
                return prevEID != -1 ? _activesList.ElementAt(prevEID).nextInAEL : _activesID;
            }

            // here ae.nextInAel == ENext == EMaxPair ...
            if (IsHotEdge(ref ae))
                AddLocalMaxPoly(ref ae, aeID, ref maxPair, maxPairID, ae.top);

            DeleteFromAEL(ref ae, aeID);
            DeleteFromAEL(ref maxPair, maxPairID);
            return prevEID != -1 ? _activesList.ElementAt(prevEID).nextInAEL : _activesID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsJoined(ref Active e)
        {
            return e.joinWith != JoinWith.None;
        }
        private void Split(ref Active e, int eID, long2 currPt)
        {
            if (e.joinWith == JoinWith.Right)
            {
                int nextInAELID;
                ref var nextInAEL = ref _activesList.ElementAt(nextInAELID = e.nextInAEL);
                e.joinWith = JoinWith.None;
                nextInAEL.joinWith = JoinWith.None;
                AddLocalMinPoly(ref e, eID, ref nextInAEL, nextInAELID, currPt, true);
            }
            else
            {
                int prevInAELID;
                ref var prevInAEL = ref _activesList.ElementAt(prevInAELID = e.prevInAEL);
                e.joinWith = JoinWith.None;
                prevInAEL.joinWith = JoinWith.None;
                AddLocalMinPoly(ref prevInAEL, prevInAELID, ref e, eID, currPt, true);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckJoinLeft(ref Active e, int eID, long2 pt, bool checkCurrX = false)
        {
            int prevID = e.prevInAEL;
            if (prevID == -1)
                return;

            ref Active prev = ref _activesList.ElementAt(prevID);
            if (IsOpen(ref e) || IsOpen(ref prev) ||
              !IsHotEdge(ref e) || !IsHotEdge(ref prev)) return;
            if ((pt.y < e.top.y + 2 || pt.y < prev.top.y + 2) &&    //avoid trivial joins
              ((e.bot.y > pt.y) || (prev.bot.y > pt.y))) return;    // (#490)

            if (checkCurrX)
            {
                if (ClipperFunc.PerpendicDistFromLineSqrd(pt, prev.bot, prev.top) > 0.25) return;
            }
            else if (e.curX != prev.curX) return;
            if (InternalClipper.CrossProduct(e.top, pt, prev.top) != 0) return;

            ref var eOutrec = ref _outrecList.ElementAt(e.outrec);
            ref var prevOutrec = ref _outrecList.ElementAt(prev.outrec);
            if (eOutrec.idx == prevOutrec.idx)
                AddLocalMaxPoly(ref prev, prevID, ref e, eID, pt);
            else if (eOutrec.idx < prevOutrec.idx)
                JoinOutrecPaths(ref e, eID, ref prev, prevID);
            else
                JoinOutrecPaths(ref prev, prevID, ref e, eID);
            prev.joinWith = JoinWith.Right;
            e.joinWith = JoinWith.Left;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckJoinRight(ref Active e, int eID, long2 pt, bool checkCurrX = false)
        {
            int nextID = e.nextInAEL;
            if (nextID == -1)
                return;

            ref Active next = ref _activesList.ElementAt(nextID);
            if (IsOpen(ref e) || !IsHotEdge(ref e) || IsJoined(ref e) ||
              IsOpen(ref next) || !IsHotEdge(ref next)) return;
            if ((pt.y < e.top.y + 2 || pt.y < next.top.y + 2) &&    //avoid trivial joins
              ((e.bot.y > pt.y) || (next.bot.y > pt.y))) return;    // (#490)

            if (checkCurrX)
            {
                if (ClipperFunc.PerpendicDistFromLineSqrd(pt, next.bot, next.top) > 0.25) return;
            }
            else if (e.curX != next.curX) return;
            if (InternalClipper.CrossProduct(e.top, pt, next.top) != 0)
                return;

            ref var eOutrec = ref _outrecList.ElementAt(e.outrec);
            ref var nextOutrec = ref _outrecList.ElementAt(next.outrec);
            if (eOutrec.idx == nextOutrec.idx)
                AddLocalMaxPoly(ref e, eID, ref next, nextID, pt);
            else if (eOutrec.idx < nextOutrec.idx)
                JoinOutrecPaths(ref e, eID, ref next, nextID);
            else
                JoinOutrecPaths(ref next, nextID, ref e, eID);
            e.joinWith = JoinWith.Right;
            next.joinWith = JoinWith.Left;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixOutRecPts(ref OutRec outrec, int outrecID)
        {
            int opID = outrec.pts;
            do
            {
                ref var op = ref _outPtList.ElementAt(opID);
                op.outrec = outrecID;
                opID = op.next;
            } while (opID != outrec.pts);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SetHorzSegHeadingForward(ref HorzSegment hs, ref OutPt opP, int opPID, ref OutPt opN, int opNID)
        {
            if (opP.pt.x == opN.pt.x) return false;
            if (opP.pt.x < opN.pt.x)
            {
                hs.leftOp = opPID;
                hs.rightOp = opNID;
                hs.leftToRight = true;
            }
            else
            {
                hs.leftOp = opNID;
                hs.rightOp = opPID;
                hs.leftToRight = false;
            }
            return true;
        }

        private bool UpdateHorzSegment(ref HorzSegment hs, int hsID)
        {
            int opID = hs.leftOp;
            ref var op = ref _outPtList.ElementAt(opID);
            int outrecID = GetRealOutRec(op.outrec);
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            bool outrecHasEdges = outrec.frontEdge != -1;
            long curr_y = op.pt.y;
            int opPID = opID, opNID = opID;
            ref OutPt opP = ref op, opN = ref op;
            ref var opPprev = ref _outPtList.ElementAt(opP.prev);
            ref var opNnext = ref _outPtList.ElementAt(opN.next);
            if (outrecHasEdges)
            {
                int opAID = outrec.pts;
                int opZID = _outPtList.ElementAt(opAID).next;
                while (opPID != opZID && opPprev.pt.y == curr_y)
                {
                    opPID = opP.prev;
                    opP = ref _outPtList.ElementAt(opPID);
                    opPprev = ref _outPtList.ElementAt(opP.prev);
                }
                while (opNID != opAID && opNnext.pt.y == curr_y)
                {
                    opNID = opN.next;
                    opN = ref _outPtList.ElementAt(opNID);
                    opNnext = ref _outPtList.ElementAt(opN.next);
                }
            }
            else
            {
                while (opP.prev != opNID && opPprev.pt.y == curr_y)
                {
                    opPID = opP.prev;
                    opP = ref _outPtList.ElementAt(opPID);
                    opPprev = ref _outPtList.ElementAt(opP.prev);
                }
                while (opN.next != opPID && opNnext.pt.y == curr_y)
                {
                    opNID = opN.next;
                    opN = ref _outPtList.ElementAt(opNID);
                    opNnext = ref _outPtList.ElementAt(opN.next);
                }
            }
            bool result =
              SetHorzSegHeadingForward(ref hs, ref opP, opPID, ref opN, opNID) &&
                _outPtList.ElementAt(hs.leftOp).horz == -1;

            if (result)
                _outPtList.ElementAt(hs.leftOp).horz = hsID;
            else
                hs.rightOp = -1; // (for sorting)
            return result;
        }


        /// <summary> method will invalidate passed in references, so do not </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DuplicateOp(int opID, bool insert_after)
        {
            ref var op = ref _outPtList.ElementAt(opID);
            int resultID = NewOutPt(op.pt, op.outrec);
            op = ref _outPtList.ElementAt(opID);//fetch again due to invalidated references
            ref var result = ref _outPtList.ElementAt(resultID);
            if (insert_after)
            {
                result.next = op.next;
                _outPtList.ElementAt(result.next).prev = resultID;
                result.prev = opID;
                op.next = resultID;
            }
            else
            {
                result.prev = op.prev;
                _outPtList.ElementAt(result.prev).next = resultID;
                result.next = opID;
                op.prev = resultID;
            }
            return resultID;
        }

        private void ConvertHorzSegsToJoins()
        {
            int k = 0;
            for (int i = 0, length = _horzSegList.Length; i < length; i++)
                if (UpdateHorzSegment(ref _horzSegList.ElementAt(i), i)) k++;
            if (k < 2) return;
            _horzSegList.Sort(new HorzSegSorter(_outPtList));

            for (int i = 0; i < k - 1; i++)
            {
                ref var hs1 = ref _horzSegList.ElementAt(i);
                ref var hs1LeftOp = ref _outPtList.ElementAt(hs1.leftOp);
                ref var hs1RightOp = ref _outPtList.ElementAt(hs1.rightOp);
                // for each HorzSegment, find others that overlap
                for (int j = i + 1; j < k; j++)
                {
                    ref var hs2 = ref _horzSegList.ElementAt(j);
                    ref var hs2LeftOp = ref _outPtList.ElementAt(hs2.leftOp);
                    ref var hs2RightOp = ref _outPtList.ElementAt(hs2.rightOp);
                    if ((hs2LeftOp.pt.x >= hs1RightOp.pt.x) ||
                       (hs2.leftToRight == hs1.leftToRight) ||
                       (hs2RightOp.pt.x <= hs1LeftOp.pt.x)) continue;
                    long curr_y = hs1LeftOp.pt.y;
                    if (hs1.leftToRight)
                    {
                        ref var hs1LeftOpNext = ref _outPtList.ElementAt(hs1LeftOp.next);
                        while (hs1LeftOpNext.pt.y == curr_y &&
                          hs1LeftOpNext.pt.x <= hs2LeftOp.pt.x)
                        {
                            hs1.leftOp = hs1LeftOp.next;
                            hs1LeftOp = ref _outPtList.ElementAt(hs1.leftOp);
                            hs1LeftOpNext = ref _outPtList.ElementAt(hs1LeftOp.next);
                        }

                        ref var hs2LeftOpPrev = ref _outPtList.ElementAt(hs2LeftOp.prev);
                        while (hs2LeftOpPrev.pt.y == curr_y &&
                          hs2LeftOpPrev.pt.x <= hs1LeftOp.pt.x)
                        {
                            hs2.leftOp = hs2LeftOp.prev;
                            hs2LeftOp = ref _outPtList.ElementAt(hs2.leftOp);
                            hs2LeftOpPrev = ref _outPtList.ElementAt(hs2LeftOp.prev);
                        }
                        HorzJoin join = new HorzJoin(
                          DuplicateOp(hs1.leftOp, true),
                          DuplicateOp(hs2.leftOp, false));
                        _horzJoinList.Add(join);
                    }
                    else
                    {
                        ref var hs1LeftOpPrev = ref _outPtList.ElementAt(hs1LeftOp.prev);
                        while (hs1LeftOpPrev.pt.y == curr_y &&
                          hs1LeftOpPrev.pt.x <= hs2LeftOp.pt.x)
                        {
                            hs1.leftOp = hs1LeftOp.prev;
                            hs1LeftOp = ref _outPtList.ElementAt(hs1.leftOp);
                            hs1LeftOpPrev = ref _outPtList.ElementAt(hs1LeftOp.prev);
                        }

                        ref var hs2LeftOpNext = ref _outPtList.ElementAt(hs2LeftOp.next);
                        while (hs2LeftOpNext.pt.y == curr_y &&
                          hs2LeftOpNext.pt.x <= hs1LeftOp.pt.x)
                        {
                            hs2.leftOp = hs2LeftOp.next;
                            hs2LeftOp = ref _outPtList.ElementAt(hs2.leftOp);
                            hs2LeftOpNext = ref _outPtList.ElementAt(hs2LeftOp.next);
                        }
                        HorzJoin join = new HorzJoin(
                          DuplicateOp(hs2.leftOp, true),
                          DuplicateOp(hs1.leftOp, false));
                        _horzJoinList.Add(join);
                    }
                    //adding to _outPtList invalidates references, so get them again
                    hs1 = ref _horzSegList.ElementAt(i);
                    hs1LeftOp = ref _outPtList.ElementAt(hs1.leftOp); //fetch again due to invalidated references
                    hs1RightOp = ref _outPtList.ElementAt(hs1.rightOp); //fetch again due to invalidated references
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Rect64 GetBounds(int opID)
        {
            ref var op = ref _outPtList.ElementAt(opID);
            Rect64 result = new Rect64(op.pt.x, op.pt.y, op.pt.x, op.pt.y);
            int op2ID = op.next;
            while (op2ID != opID)
            {
                ref var op2 = ref _outPtList.ElementAt(op2ID);
                if (op2.pt.x < result.left) result.left = op2.pt.x;
                else if (op2.pt.x > result.right) result.right = op2.pt.x;
                if (op2.pt.y < result.top) result.top = op2.pt.y;
                else if (op2.pt.y > result.bottom) result.bottom = op2.pt.y;
                op2ID = op2.next;
            }
            return result;
        }
        private PointInPolygonResult PointInOpPolygon(long2 pt, int opID)
        {
            ref var op = ref _outPtList.ElementAt(opID);
            if (opID == op.next || op.prev == op.next)
                return PointInPolygonResult.IsOutside;

            int op2ID = opID;
            do
            {
                if (op.pt.y != pt.y) break;
                op = ref _outPtList.ElementAt(opID = op.next);
            } while (opID != op2ID);
            if (op.pt.y == pt.y) // not a proper polygon
                return PointInPolygonResult.IsOutside;

            // must be above or below to get here
            bool isAbove = op.pt.y < pt.y, startingAbove = isAbove;
            int val = 0;

            op2ID = op.next;
            ref var op2 = ref _outPtList.ElementAt(op2ID);
            ref var op2Prev = ref _outPtList.ElementAt(op2.prev);
            while (op2ID != opID)
            {
                if (isAbove)
                    while (op2ID != opID && op2.pt.y < pt.y) op2 = ref _outPtList.ElementAt(op2ID = op2.next);
                else
                    while (op2ID != opID && op2.pt.y > pt.y) op2 = ref _outPtList.ElementAt(op2ID = op2.next);
                if (op2ID == opID) break;

                // must have touched or crossed the pt.Y horizonal
                // and this must happen an even number of times

                op2Prev = ref _outPtList.ElementAt(op2.prev);
                if (op2.pt.y == pt.y) // touching the horizontal
                {
                    if (op2.pt.x == pt.x || (op2.pt.y == op2Prev.pt.y &&
                      (pt.x < op2Prev.pt.x) != (pt.x < op2.pt.x)))
                        return PointInPolygonResult.IsOn;
                    op2 = ref _outPtList.ElementAt(op2ID = op2.next);
                    if (op2ID == opID) break;
                    continue;
                }

                if (op2.pt.x <= pt.x || op2Prev.pt.x <= pt.x)
                {
                    if ((op2Prev.pt.x < pt.x && op2.pt.x < pt.x))
                        val = 1 - val; // toggle val
                    else
                    {
                        double d = InternalClipper.CrossProduct(op2Prev.pt, op2.pt, pt);
                        if (d == 0) return PointInPolygonResult.IsOn;
                        if ((d < 0) == isAbove) val = 1 - val;
                    }
                }
                isAbove = !isAbove;
                op2 = ref _outPtList.ElementAt(op2ID = op2.next);
            }

            if (isAbove != startingAbove)
            {
                op2Prev = ref _outPtList.ElementAt(op2.prev);
                double d = InternalClipper.CrossProduct(op2Prev.pt, op2.pt, pt);
                if (d == 0) return PointInPolygonResult.IsOn;
                if ((d < 0) == isAbove) val = 1 - val;
            }

            if (val == 0) return PointInPolygonResult.IsOutside;
            else return PointInPolygonResult.IsInside;
        }

        private bool Path1InsidePath2(int op1ID, int op2ID)
        {
            // we need to make some accommodation for rounding errors
            // so we won't jump if the first vertex is found outside
            int outside_cnt = 0;
            int opID = op1ID;
            ref var op = ref _outPtList.ElementAt(opID);
            do
            {
                PointInPolygonResult result = PointInOpPolygon(op.pt, op2ID);
                if (result == PointInPolygonResult.IsOutside) ++outside_cnt;
                else if (result == PointInPolygonResult.IsInside) --outside_cnt;
                op = ref _outPtList.ElementAt(opID = op.next);
            } while (opID != op1ID && math.abs(outside_cnt) < 2);
            if (math.abs(outside_cnt) > 1) return (outside_cnt < 0);
            // since path1's location is still equivocal, check its midpoint            
            long2 mp = GetBounds(opID).MidPoint();
            return PointInOpPolygon(mp, op2ID) == PointInPolygonResult.IsInside;
        }

        private void ProcessHorzJoins()
        {
            for (int i = 0, length = _horzJoinList.Length; i < length; i++)
            {
                HorzJoin j = _horzJoinList[i];

                ref var jOp1 = ref _outPtList.ElementAt(j.op1);
                ref var jOp2 = ref _outPtList.ElementAt(j.op2);

                int or1ID = GetRealOutRec(jOp1.outrec);
                int or2ID = GetRealOutRec(jOp2.outrec);
                ref var or1 = ref _outrecList.ElementAt(or1ID);
                ref var or2 = ref _outrecList.ElementAt(or2ID);

                int op1bID = jOp1.next;
                int op2bID = jOp2.prev;
                ref var op1b = ref _outPtList.ElementAt(op1bID);
                ref var op2b = ref _outPtList.ElementAt(op2bID);

                jOp1.next = j.op2;
                jOp2.prev = j.op1;
                op1b.prev = op2bID;
                op2b.next = op1bID;

                if (or1ID == or2ID)  // 'join' is really a split
                {
                    or2ID = NewOutRec();
                    or1 = ref _outrecList.ElementAt(or1ID);//fetch again due to invalidated references
                    or2 = ref _outrecList.ElementAt(or2ID);
                    or2.pts = op1bID;

                    FixOutRecPts(ref or2, or2ID);
                    ref var or1Pts = ref _outPtList.ElementAt(or1.pts);
                    if (or1Pts.outrec == or2ID)
                    {
                        or1.pts = j.op1;
                        or1Pts.outrec = or1ID;
                    }

                    if (_using_polytree)  //#498, #520, #584, D#576
                    {
                        ref var or2Pts = ref _outPtList.ElementAt(or2.pts);
                        if (Path1InsidePath2(or1.pts, or2.pts))
                        {
                            or2.owner = or1.owner;
                            SetOwner(ref or1, or1ID, ref or2, or2ID);
                        }
                        else
                        {
                            SetOwner(ref or2, or2ID, ref or1, or1ID);
                            AddSplit(ref or1, or2ID);
                        }
                    }
                    else
                        or2.owner = or1ID;
                }
                else
                {
                    or2.pts = -1;
                    if (_using_polytree)
                        SetOwner(ref or2, or2ID, ref or1, or1ID);
                    else
                        or2.owner = or1ID;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PtsReallyClose(long2 pt1, long2 pt2)
        {
            return (math.abs(pt1.x - pt2.x) < 2) && (math.abs(pt1.y - pt2.y) < 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsVerySmallTriangle(int opID)
        {
            ref var op = ref _outPtList.ElementAt(opID);
            ref var opNext = ref _outPtList.ElementAt(op.next);
            ref var opPrev = ref _outPtList.ElementAt(op.prev);
            return opNext.next == op.prev &&
              (PtsReallyClose(opPrev.pt, opNext.pt) ||
                  PtsReallyClose(op.pt, opNext.pt) ||
                  PtsReallyClose(op.pt, opPrev.pt));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidClosedPath(int opID)
        {
            if (opID == -1)
                return false;
            ref var op = ref _outPtList.ElementAt(opID);
            return op.next != opID &&
              (op.next != op.prev || !IsVerySmallTriangle(opID));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int DisposeOutPt(int opID)
        {
            ref var op = ref _outPtList.ElementAt(opID);
            int resultID = (op.next == opID ? -1 : op.next);
            _outPtList.ElementAt(op.prev).next = op.next;
            _outPtList.ElementAt(op.next).prev = op.prev;
            // op == null;
            return resultID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CleanCollinear(int outrecID)
        {
            outrecID = GetRealOutRec(outrecID);
            if (outrecID == -1)
                return;
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            if (outrec.isOpen)
                return;

            if (!IsValidClosedPath(outrec.pts))
            {
                outrec.pts = -1;
                return;
            }

            int startOpID = outrec.pts;
            int op2ID = startOpID;
            for (; ; )
            {
                ref var op2 = ref _outPtList.ElementAt(op2ID);
                ref var op2Next = ref _outPtList.ElementAt(op2.next);
                ref var op2Prev = ref _outPtList.ElementAt(op2.prev);
                // NB if preserveCollinear == true, then only remove 180 deg. spikes
                if ((InternalClipper.CrossProduct(op2Prev.pt, op2.pt, op2Next.pt) == 0) &&
                  ((op2.pt == op2Prev.pt) || (op2.pt == op2Next.pt) || !PreserveCollinear ||
                  (InternalClipper.DotProduct(op2Prev.pt, op2.pt, op2Next.pt) < 0)))
                {
                    if (op2ID == outrec.pts)
                        outrec.pts = op2.prev;
                    op2ID = DisposeOutPt(op2ID);
                    op2 = ref _outPtList.ElementAt(op2ID);
                    if (!IsValidClosedPath(op2ID))
                    {
                        outrec.pts = -1;
                        return;
                    }
                    startOpID = op2ID;
                    continue;
                }
                op2ID = op2.next;
                if (op2ID == startOpID) break;
            }
            FixSelfIntersects(outrecID);
        }

        /// <summary> method will invalidate passed in references, so do not </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DoSplitOp(int outrecID, int splitOpID)
        {
            // splitOp.prev <=> splitOp &&
            // splitOp.next <=> splitOp.next.next are intersecting
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            ref var splitOp = ref _outPtList.ElementAt(splitOpID);
            int prevOpID = splitOp.prev;
            ref var prevOp = ref _outPtList.ElementAt(prevOpID);
            ref var splitOpNext = ref _outPtList.ElementAt(splitOp.next);
            ref var nextNextOp = ref _outPtList.ElementAt(splitOpNext.next);
            outrec.pts = prevOpID;
            int resultID = prevOpID;

            InternalClipper.GetIntersectPoint(
                prevOp.pt, splitOp.pt, splitOpNext.pt, nextNextOp.pt, out long2 ip);

            //Debug.Log($"SoA: {tmp.x:D9} {tmp.y:D9}");

            double area1 = Area(prevOpID);
            double absArea1 = math.abs(area1);

            if (absArea1 < 2)
            {
                outrec.pts = -1;
                return;
            }

            double area2 = AreaTriangle(ip, splitOp.pt, splitOpNext.pt);
            double absArea2 = math.abs(area2);

            // de-link splitOp and splitOp.next from the path
            // while inserting the intersection point
            if (ip == prevOp.pt || ip == nextNextOp.pt)
            {
                nextNextOp.prev = prevOpID;
                prevOp.next = splitOpNext.next;
            }
            else
            {
                int newOp2ID = NewOutPt(ip, outrecID);
                splitOp = ref _outPtList.ElementAt(splitOpID); //fetch again due to invalidated references
                splitOpNext = ref _outPtList.ElementAt(splitOp.next); //fetch again due to invalidated references
                nextNextOp = ref _outPtList.ElementAt(splitOpNext.next); //fetch again due to invalidated references
                ref var newOp2 = ref _outPtList.ElementAt(newOp2ID);
                prevOpID = splitOp.prev;
                prevOp = ref _outPtList.ElementAt(prevOpID);

                newOp2.prev = prevOpID;
                newOp2.next = splitOpNext.next;

                nextNextOp.prev = newOp2ID;
                prevOp.next = newOp2ID;
            }

            // nb: area1 is the path's area *before* splitting, whereas area2 is
            // the area of the triangle containing splitOp & splitOp.next.
            // So the only way for these areas to have the same sign is if
            // the split triangle is larger than the path containing prevOp or
            // if there's more than one self=intersection.
            if (absArea2 > 1 &&
                (absArea2 > absArea1 ||
                 ((area2 > 0) == (area1 > 0))))
            {
                int newOutRecID = NewOutRec();
                outrec = ref _outrecList.ElementAt(outrecID); //fetch again due to invalidated references
                ref var newOutRec = ref _outrecList.ElementAt(newOutRecID);
                newOutRec.owner = outrec.owner;
                splitOp.outrec = newOutRecID;
                splitOpNext.outrec = newOutRecID;

                int newOpID = NewOutPt(ip, newOutRecID);
                splitOp = ref _outPtList.ElementAt(splitOpID);//fetch again due to invalidated references
                splitOpNext = ref _outPtList.ElementAt(splitOp.next);//fetch again due to invalidated references
                ref var newOp = ref _outPtList.ElementAt(newOpID);

                newOp.prev = splitOp.next;
                newOp.next = splitOpID;
                newOutRec.pts = newOpID;
                splitOp.prev = newOpID;
                splitOpNext.next = newOpID;

                if (_using_polytree)
                {
                    if (Path1InsidePath2(prevOpID, newOpID))
                    {
                        AddSplit(ref newOutRec, outrecID);
                    }
                    else
                    {
                        AddSplit(ref outrec, newOutRecID);
                    }
                }
            }
            //else { splitOp = null; splitOp.next = null; }
        }

        /// <summary> method will invalidate passed in references, so do not </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FixSelfIntersects(int outrecID)
        {
            var outrec = _outrecList[outrecID];
            int op2ID = outrec.pts;
            for (; ; )
            {
                ref var op2 = ref _outPtList.ElementAt(op2ID);
                ref var op2Next = ref _outPtList.ElementAt(op2.next);
                ref var op2NextNext = ref _outPtList.ElementAt(op2Next.next);
                ref var op2Prev = ref _outPtList.ElementAt(op2.prev);
                // triangles can't self-intersect
                if (op2.prev == op2Next.next) break;
                if (InternalClipper.SegsIntersect(op2Prev.pt,
                        op2.pt, op2Next.pt, op2NextNext.pt))
                {
                    DoSplitOp(outrecID, op2ID);
                    if ((outrec = _outrecList[outrecID]).pts == -1) return;
                    op2ID = outrec.pts;
                    continue;
                }
                else
                    op2ID = op2.next;
                if (op2ID == outrec.pts) break;
            }
        }

        internal bool PathIsOK(int opID, bool isOpen)
        {
            OutPt op;
            if (opID == -1 || (op = _outPtList[opID]).next == opID || (!isOpen && op.next == op.prev)) return false;

            if (_outPtList[op.next].next == op.prev && IsVerySmallTriangle(opID)) return false; //identical to if (path.Count == 3)
            else return true;
        }
        internal bool BuildPath(int opID, bool reverse, bool isOpen, ref PolygonInt path)
        {
            if (opID == -1)
                return false;
            ref var op = ref _outPtList.ElementAt(opID);
            if (op.next == opID || (!isOpen && op.next == op.prev)) return false;

            path.AddComponent();
            long2 lastPt;
            int op2ID;
            if (reverse)
            {
                lastPt = op.pt;
                op2ID = op.prev;
            }
            else
            {
                opID = op.next;
                op = ref _outPtList.ElementAt(opID);
                lastPt = op.pt;
                op2ID = op.next;
            }
            var firstPt = lastPt;
            path.nodes.Add((int2)lastPt);
            //path.nodes.Add((int2)(_invScale * lastPt));//only needed when input Polygon was float or double

            int pathCount = 0;
            while (op2ID != opID)
            {
                ref var op2 = ref _outPtList.ElementAt(op2ID);
                if (op2.pt != lastPt)
                {
                    lastPt = op2.pt;
                    path.nodes.Add((int2)lastPt);
                    //path.nodes.Add((int2)(_invScale * lastPt));//only needed when input Polygon was float or double
                }
                if (reverse)
                    op2ID = op2.prev;
                else
                    op2ID = op2.next;
                pathCount++;
            }
            if (!isOpen) //verify that polygon is closed
            {
                if (firstPt != lastPt)
                    path.nodes.Add((int2)firstPt);
                //path.nodes.Add((int2)(_invScale * firstPt));//only needed when input Polygon was float or double
            }
            if (pathCount == 3 && IsVerySmallTriangle(op2ID))
            {
                path.ClosePolygon();
                path.RemoveLastComponent();
                return false;
            }
            else return true;
        }
        bool BuildPaths(ref PolygonInt solutionClosed, ref PolygonInt solutionOpen)
        {
            solutionClosed.Clear();
            solutionOpen.Clear();
            solutionClosed.nodes.Capacity = _outPtList.Length;
            solutionOpen.nodes.Capacity = _outPtList.Length;
            solutionClosed.startIDs.Capacity = _outrecList.Length;
            solutionOpen.startIDs.Capacity = _outrecList.Length;

            // _outrecList.Count is not static here because
            // CleanCollinear can indirectly add additional OutRec
            for (int outrecID = 0; outrecID < _outrecList.Length; outrecID++)
            {
                ref var outrec = ref _outrecList.ElementAt(outrecID);
                //if(outrec.idx != outrecID)
                //    Debug.Log("Unexpected);
                if (outrec.pts == -1) continue;

                if (outrec.isOpen)
                    BuildPath(outrec.pts, ReverseSolution, true, ref solutionOpen);
                else
                {
                    CleanCollinear(outrecID);
                    outrec = ref _outrecList.ElementAt(outrecID);
                    // closed paths should always return a Positive orientation
                    // except when ReverseSolution == true
                    BuildPath(outrec.pts, ReverseSolution, false, ref solutionClosed);
                }

            }
            if (solutionOpen.nodes.Length > 0)
                solutionOpen.startIDs.Add(solutionOpen.nodes.Length);
            if (solutionClosed.nodes.Length > 0)
                solutionClosed.ClosePolygon();

            return true;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckBounds(int outrecID)
        {
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            if (outrec.pts == -1) return false;
            if (!outrec.bounds.IsEmpty()) return true;
            CleanCollinear(outrecID);
            outrec = ref _outrecList.ElementAt(outrecID);
            if (outrec.pts == -1) return false;

            if (!PathIsOK(outrec.pts, false))
                return false;
            outrec.bounds = GetBounds(outrec.pts);
            return true;
        }
        private bool CheckSplitOwner(int outrecID, int splitStart)
        {
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            int nextSplitID = splitStart;
            do
            {
                var splitID = GetRealOutRec(nextSplitID);
                if (splitID == -1 || splitID == outrecID || splitID == outrec.owner) continue;
                ref var split = ref _outrecList.ElementAt(splitID);
                if (split.splitStart != -1 && CheckSplitOwner(outrecID, split.splitStart)) return true;
                if (CheckBounds(splitID) && split.bounds.Contains(outrec.bounds) &&
                    Path1InsidePath2(outrec.pts, split.pts))
                {
                    outrec.owner = splitID; //found in split
                    return true;
                }
            } while ((nextSplitID = nextSplit[nextSplitID]) != -1);
            return false;
        }
        private void RecursiveCheckOwners(int outrecID, ref PolyTree polytree)
        {
            // pre-condition: outrec will have valid bounds
            // post-condition: if a valid path, outrec will have a polypath

            ref var outrec = ref _outrecList.ElementAt(outrecID);
            if (outrec.polypath != -1 || outrec.bounds.IsEmpty()) return;

            while (outrec.owner != -1)
            {
                ref var outrecOwner = ref _outrecList.ElementAt(outrec.owner);
                if (outrecOwner.splitStart != -1 &&
                    CheckSplitOwner(outrecID, outrecOwner.splitStart)) break;
                else if (outrecOwner.pts != -1 && CheckBounds(outrec.owner) &&
                  Path1InsidePath2(outrec.pts, outrecOwner.pts)) break;
                outrec.owner = outrecOwner.owner;
            }

            if (outrec.owner != -1)
            {
                if (_outrecList.ElementAt(outrec.owner).polypath == -1)
                    RecursiveCheckOwners(outrec.owner, ref polytree);
                outrec.polypath = outrec.owner;
                polytree.AddChildComponent(outrec.owner, outrecID);
            }
            else //if no owner, definitely an outer polygon
            {
                var exteriorIDs = polytree.exteriorIDs;
                outrec.polypath = outrec.idx;
                exteriorIDs.Add(outrecID);
            }
        }
        void BuildTree(ref PolyTree polytree, ref PolygonInt solutionOpen)
        {
            polytree.Clear();
            solutionOpen.Clear();
            solutionOpen.nodes.Capacity = _outPtList.Length;
            solutionOpen.startIDs.Capacity = _outrecList.Length;
            var components = polytree.components;
            for (int i = 0, length = _outrecList.Length; i < length; i++)
                components.Add(new TreeNode(i)); //initialize

            // _outrecList.Count is not static here because
            // CheckBounds below can indirectly add additional
            // OutRec (via FixOutRecPts & CleanCollinear)
            for (int outrecID = 0; outrecID < _outrecList.Length; outrecID++)
            {
                ref var outrec = ref _outrecList.ElementAt(outrecID);

                if (outrec.pts == -1) continue;

                if (outrec.isOpen)
                {
                    BuildPath(outrec.pts, ReverseSolution, true, ref solutionOpen);
                    continue;
                }
                if (CheckBounds(outrecID))
                {
                    if (_outrecList.Length > components.Length)
                    {
                        Debug.Log("Adding new Tree Nodes");
                        for (int i = components.Length - 1, length = _outrecList.Length; i < length; i++)
                            components.Add(new TreeNode(i)); //initialize
                    }
                    RecursiveCheckOwners(outrecID, ref polytree);
                }
            }
        }
        //private bool DeepCheckOwnerOld(ref OutRec outrec, int outrecID, int ownerID)
        //{
        //    ref var outrecOwner = ref _outrecList.ElementAt(outrec.owner);
        //    if (outrecOwner.bounds.IsEmpty()) outrecOwner.bounds = GetBounds(ref _outPtList.ElementAt(outrecOwner.pts), outrecOwner.pts);

        //    bool isInsideOwnerBounds = outrecOwner.bounds.Contains(outrec.bounds);

        //    // while looking for the correct owner, check the owner's
        //    // splits **before** checking the owner itself because
        //    // splits can occur internally, and checking the owner
        //    // first would miss the inner split's true ownership
        //    var asplitID = outrecOwner.splitStart;
        //    if (asplitID != -1)
        //    {
        //        do
        //        {
        //            int splitID = GetRealOutRec(asplitID);
        //            if (splitID == -1 || splitID <= ownerID || splitID == outrecID)
        //            {
        //                asplitID = nextSplit[asplitID];
        //                continue;
        //            }

        //            if (_outrecList.ElementAt(splitID).splitStart != -1 && DeepCheckOwnerOld(ref outrec, outrecID, splitID)) return true;

        //            ref var split = ref _outrecList.ElementAt(splitID);
        //            if (split.bounds.IsEmpty()) split.bounds = GetBounds(ref _outPtList.ElementAt(split.pts), split.pts);

        //            if (split.bounds.Contains(outrec.bounds) && Path1InsidePath2(outrec.pts, split.pts))
        //            {
        //                outrec.owner = splitID;
        //                return true;
        //            }
        //            asplitID = nextSplit[asplitID];
        //        } while (asplitID != -1);
        //    }

        //    // only continue when not inside recursion
        //    if (ownerID != outrec.owner) return false;

        //    outrecOwner = ref _outrecList.ElementAt(outrec.owner);
        //    for (; ; )
        //    {                
        //        if (isInsideOwnerBounds && Path1InsidePath2(outrec.pts, outrecOwner.pts))
        //            return true;

        //        outrec.owner = outrecOwner.owner;
        //        if (outrec.owner == -1) return false;
        //        outrecOwner = ref _outrecList.ElementAt(outrec.owner);
        //        isInsideOwnerBounds = outrecOwner.bounds.Contains(outrec.bounds);
        //    }
        //}
        //bool BuildTreeOld(ref PolyTree polytree, ref PolygonInt solutionOpen)
        //{
        //    polytree.Clear();
        //    solutionOpen.Clear();
        //    solutionOpen.nodes.Capacity = _outPtList.Length;
        //    solutionOpen.startIDs.Capacity = _outrecList.Length;
        //    var components = polytree.components;
        //    var exteriorIDs = polytree.exteriorIDs;
        //    for (int i = 0, length = _outrecList.Length; i < length; i++)
        //        components.Add(new TreeNode(i)); //initialize

        //    for (int outrecID = 0; outrecID < _outrecList.Length; outrecID++)
        //    {
        //        ref var outrec = ref _outrecList.ElementAt(outrecID);
        //        if (outrec.pts == -1) continue;

        //        if (outrec.isOpen)
        //        {
        //            BuildPath(outrec.pts, ReverseSolution, true, ref solutionOpen);
        //            continue;
        //        }
        //        if (!IsValidClosedPath(outrec.pts))
        //            continue;
        //        if (outrec.bounds.IsEmpty()) outrec.bounds = GetBounds(ref _outPtList.ElementAt(outrec.pts), outrec.pts);

        //        outrec.owner = GetRealOutRec(outrec.owner);
        //        if (outrec.owner != -1)
        //            DeepCheckOwnerOld(ref outrec, outrecID, outrec.owner);

        //        if (outrec.owner == -1) //if no owner, definitely an outer polygon
        //        {
        //            //Debug.Log($"Outrec {outrec}: outer with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[outrec])} nodes");
        //            exteriorIDs.Add(outrecID);
        //        }
        //        else
        //        {
        //            //var node = components[outrecID];
        //            //polytree.AddChildComponent(outrec.owner, node);
        //            polytree.AddChildComponent(outrec.owner, outrecID);
        //            //Debug.Log($"Outrec {outrec}: child of {outrecList.owner[outrec]} {node.orientation} with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[outrec])} nodes.");
        //        }
        //    }
        //    return true;
        //}
        public void GetPolygonWithHoles(in PolyTree polyTree, int outrecID, ref PolygonInt outPolygon)
        {
            ref var outrec = ref _outrecList.ElementAt(outrecID);
            //Debug.Log($"taking Exterior {outrec} with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[outrec])} nodes as is ");
            BuildPath(outrec.pts, false, false, ref outPolygon);

            int holeID;
            int nextID = outrecID;
            while (polyTree.GetNextComponent(nextID, out holeID))
            {
                ref var hole = ref _outrecList.ElementAt(holeID);
                if (hole.owner != outrecID)
                {
                    nextID = holeID;
                    //Debug.Log($"Hole {hole} is an island, tesselate those islands separately!"); //TO-DO: implement (e.g. returning a list with island ID's)
                    continue;
                }
                //if (hole.pts == -1) continue;

                //Debug.Log($"taking Hole {holeID} with {InternalClipperFunc.PointCount(outPtList, outrecList.pts[holeID])} nodes as is");
                BuildPath(hole.pts, false, false, ref outPolygon);

                nextID = holeID;
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
            polytree.Clear();
            openPaths.Clear();
            _using_polytree = true;
            ExecuteInternal(clipType, fillRule);
            BuildTree(ref polytree, ref openPaths);
            //BuildTreeOld(ref polytree, ref openPaths);

            //ClearSolution();
            return _succeeded;
        }
        public int AddSplit(ref OutRec _owningOutRec, int _splitOutRecID)
        {
            int curID = splits.Length;
            splits.Add(_splitOutRecID); //_splitOutRec is stored at index curID
            nextSplit.Add(-1);
            if (_owningOutRec.splitStart != -1)
            {
                //first, search the last index where splits of _owningOutRec are stored
                int splitsEnd, tmp = _owningOutRec.splitStart;
                do
                {
                    splitsEnd = tmp;
                    tmp = nextSplit[tmp];
                } while (tmp != -1);
                nextSplit[splitsEnd] = curID; //then point "next" of that end to the newly added _splitOutRec (stored at curID) 
            }
            else
            {
                _owningOutRec.splitStart = curID; //point Start of the _owningOutRec splitlist to the newly added _splitOutRec (stored at curID)
            }
            return curID;
        }

        //public int AddSplit(ref OutRec _owningOutRec, int _splitOutRecID)
        //{
        //    int curID = splits.Length;
        //    SplitCandidate split = new SplitCandidate { outrec = _splitOutRecID, nextSplit = -1 };
        //    splits.Add(split); //_splitOutRec is stored at index curID
        //    if (_owningOutRec.splitStart != -1)
        //    {
        //        //first, search the last index where splits of _owningOutRec are stored
        //        int splitsEnd, tmp = _owningOutRec.splitStart;
        //        do
        //        {
        //            splitsEnd = tmp;
        //            tmp = splits[tmp].nextSplit;
        //        } while (tmp != -1);
        //        var lastSplit = splits[splitsEnd];
        //        lastSplit.nextSplit = curID; //then point "next" of that end to the newly added _splitOutRec (stored at curID) 
        //    }
        //    else
        //    {
        //        _owningOutRec.splitStart = curID; //point Start of the _owningOutRec splitlist to the newly added _splitOutRec (stored at curID)
        //    }
        //    return curID;
        //}
        //public void PrintSize()
        //{
        //    Debug.Log($"Vertices: {_vertexList.Length}");
        //    Debug.Log($"Minimalist: {_minimaList.Length} ");
        //    Debug.Log($"OutPoints: {_outPtList.Length} ");
        //    Debug.Log($"OutRecs: {_outrecList.Length} ");
        //    Debug.Log($"Actives: {_activesList.Length} ");
        //    Debug.Log($"Intersects: {_intersectList.Length} ");
        //    Debug.Log($"splits: {splits.Length} ");
        //}

    } //ClipperBase class

    public class ClipperLibException : Exception
    {
        public ClipperLibException(string description) : base(description) { }
    }
} //namespace