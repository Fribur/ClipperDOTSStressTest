using Unity.Collections;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    public struct VertexLL
    {
        public NativeList<long2> pt;
        public NativeList<int> next;
        public NativeList<int> prev;
        public NativeList<VertexFlags> flags;
        public bool IsCreated;
        public VertexLL(int size, Allocator allocator)
        {
            pt = new NativeList<long2>(size, allocator);
            flags = new NativeList<VertexFlags>(size, allocator);
            for (int i = 0, length = flags.Length; i < length; i++)
                flags[i] = VertexFlags.None;
            prev = new NativeList<int>(size, allocator);
            next = new NativeList<int>(size, allocator);
            IsCreated = true;
        }
        public int AddVertex(long2 vertex, VertexFlags flag, bool firstVertex, int? firstVertexID=0)
        {
            int current = pt.Length;
            pt.Add(vertex);
            flags.Add(flag);
            
            if (!firstVertex)
            {
                next.Add((int)firstVertexID); //extend NextList, set Next of Tail to Head
                prev.Add(current - 1); //extend PrevList, set point current index to Prev index
                next[current - 1] = current;
                prev[(int)firstVertexID] = current; //set Prev of Head to Tail
            }
            else
            {
                prev.Add(current);
                next.Add(current);
            }
            return current;
        }

        public void Dispose()
        {
            if (pt.IsCreated) pt.Dispose();
            if (flags.IsCreated) flags.Dispose();
            if (next.IsCreated) next.Dispose();
            if (prev.IsCreated) prev.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (pt.IsCreated) pt.Clear();
            if (flags.IsCreated) flags.Clear();
            if (next.IsCreated) next.Clear();
            if (prev.IsCreated) prev.Clear();
        }
    };

} //namespace