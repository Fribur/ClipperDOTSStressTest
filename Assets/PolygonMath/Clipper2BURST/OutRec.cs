/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - also known as Clipper2                            *
* Date      :  8 May 2022                                                      *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This is the main polygon clipping module                        *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/
using Unity.Collections;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    public struct OutRecLL
    {
        public NativeList<int> owner;
        public NativeList<int> frontEdge;
        public NativeList<int> backEdge;
        public NativeList<int> pts;
        public NativeList<int> polypath;
        public NativeList<OutRecState> state;
        public bool IsCreated;

        public OutRecLL(int size, Allocator allocator)
        {
            owner = new NativeList<int>(size, allocator);
            frontEdge = new NativeList<int>(size, allocator);
            backEdge = new NativeList<int>(size, allocator);
            pts = new NativeList<int>(size, allocator);
            polypath = new NativeList<int>(size, allocator);
            state = new NativeList<OutRecState>(size, allocator);
            IsCreated = true;
        }
        public int AddOutRec(int owner_ID, OutRecState _state, int _pts)
        {
            int current = owner.Length;
            owner.Add(owner_ID);
            state.Add(_state);
            pts.Add(_pts);
            polypath.Add(-1);
            frontEdge.Add(-1);
            backEdge.Add(-1);
            return current;
        }
        
        public void Dispose()
        {
            if (owner.IsCreated) owner.Dispose();
            if (frontEdge.IsCreated) frontEdge.Dispose();
            if (backEdge.IsCreated) backEdge.Dispose();
            if (pts.IsCreated) pts.Dispose();
            if (polypath.IsCreated) polypath.Dispose();
            if (state.IsCreated) state.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (owner.IsCreated) owner.Clear();
            if (frontEdge.IsCreated) frontEdge.Clear();
            if (backEdge.IsCreated) backEdge.Clear();
            if (pts.IsCreated) pts.Clear();
            if (polypath.IsCreated) polypath.Clear();
            if (state.IsCreated) state.Clear();
        }
    };

} //namespace