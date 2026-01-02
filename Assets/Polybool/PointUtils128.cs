using System;
using System.Runtime.CompilerServices;

namespace Polybool
{
    public static class PointUtils128
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]      
        internal static int Orient2DParamPoint128(long2 a, long2 b, Rational p, ref Segment pSeg)
        {            
            long num = p.num;
            long den = p.den;
            // Enforce rational normalization
            // (this is REQUIRED for correctness)
            // this is done during Rational construction, se we do not have to do this here in the hot path
            //if (den < 0)
            //{
            //    num = -num;
            //    den = -den;
            //}
            long2 p0 = pSeg.p0;
            long2 p1 = pSeg.p1;

            var ab_x = Math128.Sub128(b.x, a.x); // x of vector a -> b
            var ab_y = Math128.Sub128(b.y, a.y); // y of vector a -> b
            
            var ap0_x = Math128.Sub128(p0.x, a.x); // x of vector a -> p0
            var ap0_y = Math128.Sub128(p0.y, a.y); // y of vector a -> p0

            var p0p1_x = Math128.Sub128(p1.x, p0.x); // x of vector p0 -> p1
            var p0p1_y = Math128.Sub128(p1.y, p0.y); // x of vector p0 -> p1

            // orient(a, b, p0)
            var baseTerm = CrossProduct128(ab_x, ab_y, ap0_x, ap0_y);

            // orient(a, b, direction)
            var dirTerm = CrossProduct128(ab_x, ab_y, p0p1_x, p0p1_y);

            // ---- 3. Zero short-circuits (very important) ----
            int baseSign = Math128.Sign128(baseTerm);
            int dirSign = Math128.Sign128(dirTerm);

            if (dirSign == 0)
                return baseSign;

            if (baseSign == 0)
                return Math.Sign(num) * dirSign;

            //// get sign. version A
            //var lhs = Math128.Mul128x64(baseTerm, den);
            //var rhs = Math128.Mul128x64(dirTerm, num);
            //return Math128.Sign128(Math128.Add128(lhs, rhs));

            // get sign. version B
            // ---- 4. Compare |baseTerm| * den  vs  |dirTerm| * num ----

            var lhs = Math128.Mul128x64(Math128.Abs128(baseTerm), den);
            var rhs = Math128.Mul128x64(Math128.Abs128(dirTerm), Math.Abs(num));

            int cmp = lhs.CompareTo(rhs);

            if (cmp > 0)
                return baseSign;

            if (cmp < 0)
                return Math.Sign(num) * dirSign;

            // Exact zero
            return 0;

        }

        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128Like CrossProduct128(long ax, long ay, long bx, long by)
        {
            // establishes determinant of matrix formed by vectors a and b
            // |ax bx|
            // |ay by|
            return Math128.Sub128(Math128.Mul128(ax, by), Math128.Mul128(bx, ay));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Int128Like CrossProduct128(Int128Like ax, Int128Like ay, Int128Like bx, Int128Like by)
        {
            // establishes determinant of matrix formed by vectors a and b
            // |ax bx|
            // |ay by|
            return Math128.Sub128(Math128.Mul128(ax, by), Math128.Mul128(bx, ay));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCollinear128(long2 pt1, long2 sharedPt, long2 pt2)
        {
            long a = sharedPt.x - pt1.x;
            long b = pt2.y - sharedPt.y;
            long c = sharedPt.y - pt1.y;
            long d = pt2.x - sharedPt.x;
            // When checking for collinearity with very large coordinate values
            // then ProductsAreEqual is more accurate than using CrossProduct.
            return ProductsAreEqual128(a, b, c, d);
        }
        // returns true if (and only if) a * b == c * d
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ProductsAreEqual128(long a, long b, long c, long d)
        {
            var mul_ab = Math128.Mul128(a, b);
            var mul_cd = Math128.Mul128(c, d);
            return mul_ab.CompareTo(mul_cd) == 0;
        }  
    }
}