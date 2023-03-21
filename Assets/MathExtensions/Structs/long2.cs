using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Chart3D.MathExtensions
{
    public struct long2
    {
        public long x;
        public long y;
        public long2(long2 pt)
        {
            x = pt.x;
            y = pt.y;
        }

        public long2(long x, long y)
        {
            this.x = x;
            this.y = y;
        }

        public long2(double x, double y)
        {
            this.x = (long)math.round(x);
            this.y = (long)math.round(y);
        }

        public long2(double2 pt)
        {
            x = (long)math.round(pt.x);
            y = (long)math.round(pt.y);
        }
        public long2(int2 pt)
        {
            x = pt.x;
            y = pt.y;
        }
        public long2(long2 pt, double scale)
        {
            x = (long)math.round(pt.x * scale);
            y = (long)math.round(pt.y * scale);
        }

        public long2(double2 pt, double scale)
        {
            x = (long)math.round(pt.x * scale);
            y = (long)math.round(pt.y * scale);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(long2 lhs, long2 rhs) { return lhs.x == rhs.x && lhs.y == rhs.y; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(long2 lhs, long2 rhs) { return lhs.x != rhs.x || lhs.y != rhs.y; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator +(long2 lhs, long2 rhs) { return new long2(lhs.x + rhs.x, lhs.y + rhs.y); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator -(long2 lhs, long2 rhs) { return new long2(lhs.x - rhs.x, lhs.y - rhs.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 operator *(double lhs, long2 rhs) { return new double2(lhs * rhs.x, lhs * rhs.y); }
        public static implicit operator int2(long2 value) => new int2((int)value.x, (int)value.y);
        public override bool Equals(object obj)
        {
            if (obj != null && obj is long2 p)
                return this == p;
            else
                return false;
        }

        public override string ToString()
        {
            return $"({x},{y})";
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 29 + (int)x;
            hash = hash * 29 + (int)y;
            return hash;
        }
    }

}