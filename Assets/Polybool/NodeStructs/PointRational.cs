using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Polybool
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct PointRational
    {
        public readonly Rational x;
        public readonly Rational y;

        public PointRational(Rational x, Rational y)
        {
            this.x = x;
            this.y = y;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long2 ToLong2()
        {
            //Debug.Assert(x.IsInteger, "X is not integer");
            //Debug.Assert(y.IsInteger, "Y is not integer");
            return new long2( x.ToInt64Exact(),y.ToInt64Exact());
        }
        public override string ToString()
        {
            return $"{x} {y}";
        }
        public string DebuggerDisplay
        {
            get
            {
                if (x.IsInteger && y.IsInteger)
                    return $"{x.ToInt64Exact()},{y.ToInt64Exact()}";

                return $"({x},{y})";
            }
        }
    }
}
