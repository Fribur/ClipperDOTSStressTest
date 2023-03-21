using Chart3D.MathExtensions;
using System;
using System.Collections.Generic;

namespace Clipper2SoA
{
    public struct LocalMinima : IEquatable<LocalMinima>
    {
        public readonly int vertex_ID;
        public readonly long2 vertex;
        public readonly PathType polytype;
        public readonly bool isOpen;

        public LocalMinima(int vertexID, long2 vertex, PathType polytype, bool isOpen = false)
        {
            this.vertex_ID = vertexID;
            this.vertex = vertex;
            this.polytype = polytype;
            this.isOpen = isOpen;
        }
        public bool Equals(LocalMinima other)
        {
            return this == other;
        }
        public static bool operator ==(LocalMinima lm1, LocalMinima lm2)
        {
            return lm1.vertex_ID == lm2.vertex_ID &&
                lm1.vertex == lm2.vertex &&
                lm1.polytype == lm2.polytype &&
                lm1.isOpen == lm2.isOpen;
        }

        public static bool operator !=(LocalMinima lm1, LocalMinima lm2)
        {
            return !(lm1 == lm2);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is LocalMinima minima && this == minima;
        }
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 29 + vertex_ID;
            hash = hash * 29 + vertex.GetHashCode();
            hash = hash * 29 + (int)polytype;
            //hash = hash * 29 + (int)isOpen;
            return hash;
        }
    };
    struct LocMinSorter : IComparer<LocalMinima>
    {
        public int Compare(LocalMinima locMin1, LocalMinima locMin2)
        {
            return locMin2.vertex.y.CompareTo(locMin1.vertex.y);
        }
    }

} //namespace