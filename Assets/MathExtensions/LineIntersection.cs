using Unity.Mathematics;
using System.Runtime.CompilerServices;

namespace Chart3D.MathExtensions
{
    public static class LineIntersection
    {
        static double crossProduct(double2 a, double2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        /// <summary>
        /// Finds the dot product of two vectors.
        /// </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The dot product</returns>
        static double dotProduct(double2 a, double2 b)
        {
            return (a.x * b.x) + (a.y * b.y);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="a1">point of first line</param>
        /// <param name="a2">point of first line</param>
        /// <param name="b1">point of second line</param>
        /// <param name="b2">point of second line</param>
        /// <param name="noEndpointTouch">whether to skip single touchpoints (meaning connected segments) as intersections</param>
        /// <param name="intersection"></param>
        /// <returns>If the lines intersect, the point of intersection.If they overlap, the two end points 
        /// of the overlapping segment. Otherwise, null.</returns>
        public static bool intersection(double2 a1, double2 a2, double2 b1, double2 b2)
        {
            double2 va = a2 - a1;
            double2 vb = b2 - b1;

            // The rest is pretty much a straight port of the algorithm.
            double2 e = b1 - a1;
            double kross = crossProduct(va, vb);
            double sqrKross = kross * kross;
            double sqrLenA = dotProduct(va, va);

            if (sqrKross > 0)
            {
                double s = crossProduct(e, vb) / kross;

                if (s < 0 || s > 1)
                {
                    // not on line segment a
                    return false;
                }

                double t = crossProduct(e, va) / kross;

                if (t < 0 || t > 1)
                {
                    // not on line segment b
                    return false;
                }

                if (s == 0 || s == 1)
                {
                    // on an endpoint of line segment a
                    return true;
                }
                if (t == 0 || t == 1)
                {
                    // on an endpoint of line segment b
                    return true;
                }
                return true;
            }
            
            kross = crossProduct(e, va);
            sqrKross = kross * kross;

            if (sqrKross > 0 /* EPS * sqLenB * sqLenE */)
            {
                // Lines are just parallel, not the same. No overlap.
                return false;
            }
            double sa = dotProduct(va, e) / sqrLenA;
            double sb = sa + dotProduct(va, vb) / sqrLenA;
            double smin = math.min(sa, sb);
            double smax = math.max(sa, sb);


            // this is, essentially, the FindIntersection acting on floats from
            // Schneider & Eberly, just inlined into this function.
            if (smin <= 1 && smax >= 0)
            {
                // overlap on an end point
                if (smin == 1)
                    return true;

                if (smax == 0)
                    return true;

                // There's overlap on a segment -- two points of intersection. Return both.
                return true;
            }
            return false;
        }

    public static bool doIntersect(double2 p1, double2 p2, double2 q1, double2 q2)
        {
            // Find the four orientations needed for general and
            // special cases
            var o1 = Orient2D(p1, q1, p2);
            var o2 = Orient2D(p1, q1, q2);
            var o3 = Orient2D(p2, q2, p1);
            var o4 = Orient2D(p2, q2, q1);

            // General case
            if (o1 != o2 && o3 != o4)
                return true;

            // Special Cases
            // p1, q1 and p2 are collinear and p2 lies on segment p1q1
            if (o1 == 0 && onSegment(p1, p2, q1)) return true;

            // p1, q1 and q2 are collinear and q2 lies on segment p1q1
            if (o2 == 0 && onSegment(p1, q2, q1)) return true;

            // p2, q2 and p1 are collinear and p1 lies on segment p2q2
            if (o3 == 0 && onSegment(p2, p1, q2)) return true;

            // p2, q2 and q1 are collinear and q1 lies on segment p2q2
            if (o4 == 0 && onSegment(p2, q1, q2)) return true;

            return false; // Doesn't fall in any of the above cases
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Orient2D(double2 p0, double2 p1, double2 p2)
        {
            return (p0.x - p2.x) * (p1.y - p2.y) - (p0.y - p2.y) * (p1.x - p2.x);
        }
        // Given three collinear points p, q, r, the function checks if
        // point q lies on line segment 'pr'
        static bool onSegment(double2 p, double2 q, double2 r)
        {
            if (q.x <= math.max(p.x, r.x) && q.x >= math.min(p.x, r.x) &&
                q.y <= math.max(p.y, r.y) && q.y >= math.min(p.y, r.y))
                return true;

            return false;
        }
        // compute twice the signed area of the triange [P,Q,R]
        static double TriangleArea2x(double2 P, double2 Q, double2 R)
        {
            return (Q.x - P.x) * (R.y - P.y) - (Q.y - P.y) * (R.x - P.x);
        }
    }
}
