using Unity.Collections;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    // OutPt: vertex data structure for clipping solutions
    public struct OutPtLL
    {
        public NativeList<long2> pt;
        public NativeList<int> next;
        public NativeList<int> prev;
        public NativeList<int> outrec;
        public NativeList<int> joiner;
        public bool IsCreated;

        public OutPtLL(int size, Allocator allocator)
        {
            pt = new NativeList<long2>(size, allocator);
            next = new NativeList<int>(size, allocator);
            prev = new NativeList<int>(size, allocator);
            outrec = new NativeList<int>(size, allocator);
            joiner = new NativeList<int>(size, allocator);
            IsCreated = true;
        }
        public int NewOutPt(long2 pt, int _outrec_ID)
        {
            int current = this.pt.Length;
            this.pt.Add(pt);
            outrec.Add(_outrec_ID);
            next.Add(current);
            prev.Add(current);
            joiner.Add(-1);
            return current;
        }        
        public void Dispose()
        {
            if (pt.IsCreated) pt.Dispose();
            if (next.IsCreated) next.Dispose();
            if (prev.IsCreated) prev.Dispose();
            if (outrec.IsCreated) outrec.Dispose();
            if (joiner.IsCreated) joiner.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (pt.IsCreated) pt.Clear();
            if (next.IsCreated) next.Clear();
            if (prev.IsCreated) prev.Clear();
            if (outrec.IsCreated) outrec.Clear();
            if (joiner.IsCreated) joiner.Clear();
        }
    };

} //namespace