using System.Diagnostics;

namespace Polybool
{
    internal class DebugHelper
    {
        public static void AssertIntersectionExact(in Segment segA, in Segment segB, Rational tA, Rational tB)
        {
            var ipA = segA.Eval(tA);
            var ipB = segB.Eval(tB);
            Debug.Assert(PointEquals(ipA, ipB), $"INTERSECTION PARAMETER MISMATCH\n A={segA} tA={tA} ipA={ipA}\n B={segB} tB={tB} ipB={ipB}");
        }
        static bool PointEquals(PointRational a, PointRational b)
        {
            return RationalEquals(a.x, b.x) &&
                   RationalEquals(a.y, b.y);
        }
        static bool RationalEquals(Rational a, Rational b)
        {
            var lhs = Math128.Mul128(a.num, b.den);
            var rhs = Math128.Mul128(b.num, a.den);
            return lhs.CompareTo(rhs) == 0;
        }
    }
}
