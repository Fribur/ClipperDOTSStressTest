using System.Collections.Generic;


namespace Chart3D.Helper.MinHeap
{
    public struct LongComparer : IComparer<long>
    {
        int IComparer<long>.Compare(long a, long b) => a.CompareTo(b);
    }
    public struct LongComparerMaxFirst : IComparer<long>
    {
        public int Compare(long a, long b)
        {
            if (a == b)
            {
                return 0;
            }
            else
            {
                if (a > b)
                    return -1; //this will sort descending 
                else
                    return 1; //this will sort descending 
            }
        }
    }
}
