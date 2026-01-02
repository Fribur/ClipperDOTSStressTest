using System;
using System.Runtime.CompilerServices;


namespace Polybool
{
    public static class PointUtils
    {
        internal const float epsilon1Float = 1e-6f;   // at 1, next representable float step (ULP) is +- 2^(0 - 23) = 1.19e-7
        internal const double epsilon1_abs = 1e-12;       // at 1, next representable double step (ULP) is +- 2^(0 - 52) = 2.22045e-16
        internal const double epsilon1_rel = 1e-16;       // at 1, next representable double step (ULP) is +- 2^(0 - 52) = 2.22045e-16

        internal const float epsilon100Float_abs = 1e-5f; // at 100, next representable float step (ULP) is +- 2^(6 - 23) = 7.62939e-06		
        internal const double epsilon100_rel = 1e-10;     // at 100, next representable double step (ULP) is +- 2^(6 - 52) = 1.42109E-14


        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrossProduct(long ax, long ay, long bx, long by)
        { 
            // establishes determinant of matrix formed by vectors a and b
            // |ax bx|
            // |ay by|
            return ax * by -  bx * ay; 
        }

        /// <summary>
        /// Returns a positive value if the points a, b, and p occur in counterclockwise order (CCW, p lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(CW, p lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// Result also happens to be twice the signed area of the triangle
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long Orient2DFast(long2 a, long2 b, long2 p)
        {
            // cross product of vector (a - p) * (b - p) = J. Shewchuk with origin at p
            // cross product of vector (b - a) * (p - a) = most textbock convention with origin at a
            // result is identical but J. Shewchuk way is more symmetrical and has often slightly better numerical behavior when combined with adaptive precision predicates
            long pa_x = a.x - p.x;  // x of vector p -> a
            long pa_y = a.y - p.y;  // y of vector p -> a
            long pb_x = b.x - p.x;  // x of vector p -> b
            long pb_y = b.y - p.y;  // y of vector p -> b

            return CrossProduct(pa_x, pa_y, pb_x, pb_y);
        }
        internal static long Orient2DFast(long ax, long ay, long bx, long by, long px, long py)
        {
            // cross product of vector (a - p) * (b - p) = J. Shewchuk with origin at p
            // cross product of vector (b - a) * (p - a) = most textbock convention with origin at a
            // result is identical but J. Shewchuk way is more symmetrical and has often slightly better numerical behavior when combined with adaptive precision predicates
            long pa_x = ax - px;  // x of vector p -> a
            long pa_y = ay - py;  // y of vector p -> a
            long pb_x = bx - px;  // x of vector p -> b
            long pb_y = by - py;  // y of vector p -> b

            return CrossProduct(pa_x, pa_y, pb_x, pb_y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Orient2DParamPoint(long2 a, long2 b, Rational p, ref Segment pSeg)
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
            // orient(a, b, p(t))
            // = orient(a, b, p0 + t * (p1 - p0))
            // = orient(a, b, p0) + t * orient(a, b, (p1 - p0))

            long2 p0 = pSeg.p0;
            long2 p1 = pSeg.p1;

            long ab_x = b.x - a.x;      // x of vector a -> b
            long ab_y = b.y - a.y;      // y of vector a -> b

            long ap0_x = p0.x - a.x;    // x of vector a -> p0
            long ap0_y = p0.y - a.y;    // y of vector a -> p0

            long p0p1_x = p1.x - p0.x;  // x of vector p0 -> p1
            long p0p1_y = p1.y - p0.y;  // x of vector p0 -> p1

            // orient(a, b, p0)
            var baseTerm = CrossProduct(ab_x, ab_y, ap0_x, ap0_y);

            // orient(a, b, direction)
            var dirTerm = CrossProduct(ab_x, ab_y, p0p1_x, p0p1_y);

            // Combine as rational value:
            // result = baseTerm * d + dirTerm * n
            checked
            {
                long value = baseTerm * den + dirTerm * num;
                return Math.Sign(value);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsCollinear(long2 pt1, long2 sharedPt, long2 pt2)
        {
            long a = sharedPt.x - pt1.x;
            long b = pt2.y - sharedPt.y;
            long c = sharedPt.y - pt1.y;
            long d = pt2.x - sharedPt.x;
            // When checking for collinearity with very large coordinate values
            // then ProductsAreEqual is more accurate than using CrossProduct.
            return ProductsAreEqual(a, b, c, d);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ProductsAreEqual(long a, long b, long c, long d)
        {
            var mul_ab = a * b;
            var mul_cd = c * d;
            return mul_ab.CompareTo(mul_cd) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetWindingTowardsBottom(long2 a, long2 b)
        {
            return Math.Sign(b.x - a.x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetWindingTowardsRight(long2 a, long2 b)
        {
            return Math.Sign(b.y - a.y);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool PtsReallyClose(long2 pt1, long2 pt2)
        {
            return (Math.Abs(pt1.x - pt2.x) < 2) && (Math.Abs(pt1.y - pt2.y) < 2);
        }

        /// <summary> Determine Greatest Common Divisor  = largest positive integer that divides two or more numbers without leaving a remainder </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Gcd(long a, long b)
        {
            // GCD is always non-negative
            if (a < 0) a = -a;
            if (b < 0) b = -b;

            // Handle degenerate cases explicitly
            if (a == 0) return b;
            if (b == 0) return a;

            while (b != 0)
            {
                long r = a % b;
                a = b;
                b = r;
            }

            return a;
        }
    }
}