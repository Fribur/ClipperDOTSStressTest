
using System;
using System.Collections.Generic;

namespace Chart3D.Helper.MinHeap
{
    public struct Cell
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float H { get; private set; }
        public float D { get; private set; }
        public float Max { get; private set; }

        public Cell(float x, float y, float h, int distance)
        {
            X = x;
            Y = y;
            H = h;
            D = distance;
            Max = D + H * 1.41421356237f; //*math.sqrt(2);
        } 

    }
}
