using System.Collections.Generic;

namespace Chart3D.Helper.MinHeap
{
    public struct HeapItemMinFirst : IComparer<HeapItem>
    {
        public int Compare(HeapItem a, HeapItem b)
        {
            if (a.Cost == b.Cost)
            {
                return 0;
            }
            else
            {
                if (a.Cost > b.Cost) 
                    return 1; // this will sort ascending 
                else
                    return -1;  //this will sort ascending
            }
        }
    }
    public struct HeapItemMaxFirst : IComparer<HeapItem>
    {
        public int Compare(HeapItem a, HeapItem b)
        {
            if (a.Cost == b.Cost)
            {
                return 0;
            }
            else
            {
                if (a.Cost > b.Cost)
                    return -1; // this will sort descending 
                else
                    return 1;  //this will sort descending
            }
        }
    }

}
