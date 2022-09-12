using Clipper2Lib;
using System.IO;
using Unity.Collections;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    // OutRec: path data structure for clipping solutions
    public struct OutRecLL
    {
        public NativeList<int> owner;
        public NativeList<int> frontEdge;
        public NativeList<int> backEdge;
        public NativeList<int> pts;
        public NativeList<int> polypath;
        //public NativeList<Rect64> bounds;
        public NativeList<bool> isOpen;
        public bool IsCreated;

        public OutRecLL(int size, Allocator allocator)
        {
            owner = new NativeList<int>(size, allocator);
            frontEdge = new NativeList<int>(size, allocator);
            backEdge = new NativeList<int>(size, allocator);
            pts = new NativeList<int>(size, allocator);
            polypath = new NativeList<int>(size, allocator);
            //bounds = new NativeList<Rect64>(size, allocator);
            isOpen = new NativeList<bool>(size, allocator);
            IsCreated = true;
        }
        public int AddOutRec(int owner_ID, bool _isOpen, int _pts)
        {
            int current = owner.Length;
            owner.Add(owner_ID);
            frontEdge.Add(-1);
            backEdge.Add(-1);
            pts.Add(_pts);
            polypath.Add(-1);
            //bounds.Add(new Rect64());
            isOpen.Add(_isOpen);
            return current;
        }
        public void Dispose()
        {
            if (owner.IsCreated) owner.Dispose();
            if (frontEdge.IsCreated) frontEdge.Dispose();
            if (backEdge.IsCreated) backEdge.Dispose();
            if (pts.IsCreated) pts.Dispose();
            if (polypath.IsCreated) polypath.Dispose();
            //if (bounds.IsCreated) bounds.Dispose();
            if (isOpen.IsCreated) isOpen.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (owner.IsCreated) owner.Clear();
            if (frontEdge.IsCreated) frontEdge.Clear();
            if (backEdge.IsCreated) backEdge.Clear();
            if (pts.IsCreated) pts.Clear();
            if (polypath.IsCreated) polypath.Clear();
            //if (bounds.IsCreated) bounds.Dispose();
            if (isOpen.IsCreated) isOpen.Clear();
        }
    };

} //namespace