using System.Collections.Generic;


namespace Chart3D.Helper.MinHeap
{
    struct FloatComparer : IComparer<float>
    {
        int IComparer<float>.Compare(float a, float b) => a.CompareTo(b);
    }
    struct FloatComparerMaxFirst : IComparer<float>
    {
        public int Compare(float a, float b)
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
