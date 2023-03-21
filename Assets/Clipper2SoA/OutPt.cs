using Chart3D.MathExtensions;
using Unity.Collections;
using Unity.Jobs;

namespace Clipper2SoA
{
    // OutPt: vertex data structure for clipping solutions
    public struct OutPtLL
    {
        public NativeList<long2> pt;
        public NativeList<int> next;
        public NativeList<int> prev;
        public NativeList<int> outrec;
        public NativeList<int> horz;
        public bool IsCreated;

        public OutPtLL(int size, Allocator allocator)
        {
            pt = new NativeList<long2>(size, allocator);
            next = new NativeList<int>(size, allocator);
            prev = new NativeList<int>(size, allocator);
            outrec = new NativeList<int>(size, allocator);
            horz = new NativeList<int>(size, allocator);
            IsCreated = true;
        }
        public int NewOutPt(long2 pt, int _outrec_ID)
        {
            int current = this.pt.Length;
            this.pt.Add(pt);
            outrec.Add(_outrec_ID);
            next.Add(current);
            prev.Add(current);
            horz.Add(-1);
            return current;
        }        
        public void Dispose()
        {
            if (pt.IsCreated) pt.Dispose();
            if (next.IsCreated) next.Dispose();
            if (prev.IsCreated) prev.Dispose();
            if (outrec.IsCreated) outrec.Dispose();
            if (horz.IsCreated) horz.Dispose();
            IsCreated = false;
        }
        public void Dispose(JobHandle jobHandle)
        {
            if (pt.IsCreated) pt.Dispose(jobHandle);
            if (next.IsCreated) next.Dispose(jobHandle);
            if (prev.IsCreated) prev.Dispose(jobHandle);
            if (outrec.IsCreated) outrec.Dispose(jobHandle);
            if (horz.IsCreated) horz.Dispose(jobHandle);
            IsCreated = false;
        }
        public void Clear()
        {
            if (pt.IsCreated) pt.Clear();
            if (next.IsCreated) next.Clear();
            if (prev.IsCreated) prev.Clear();
            if (outrec.IsCreated) outrec.Clear();
            if (horz.IsCreated) horz.Clear();
        }
    };

} //namespace