using System.Collections.Generic;


namespace Chart3D.Helper.MinHeap
{
    public struct DoubleComparer : IComparer<double>
    {
        int IComparer<double>.Compare(double a, double b) => a.CompareTo(b);
    }
    public struct DoubleComparerMaxFirst : IComparer<double>
    {
        public int Compare(double a, double b)
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
