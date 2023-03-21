﻿using Chart3D.MathExtensions;
using Unity.Collections.LowLevel.Unsafe;

namespace Clipper2AoS
{
    // IntersectNode: a structure representing 2 intersecting edges.
    // Intersections must be sorted so they are processed from the largest
    // Y coordinates to the smallest while keeping edges adjacent.

    public struct IntersectNode
    {
        public long2 pt;
        public int edge1;
        public int edge2;
        public IntersectNode(long2 pt, int edge1ID, int edge2ID)
        {
            this.pt = pt;
            this.edge1 = edge1ID;
            this.edge2 = edge2ID;
        }
    };

} //namespace