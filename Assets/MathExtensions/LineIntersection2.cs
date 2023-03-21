using Unity.Mathematics;
using Chart3D.MathExtensions;
using System;

namespace Chart3D.MathExtensions
{
    public static class Epsilon
    {
        public const double Eps = 0.000000001f;
    }
    public static class LineIntersection2
    {
        public static bool LinesIntersect(double2 a1, double2 a2, double2 b1, double2 b2, out IntersectionPoint intersectionPoint)
        {
            intersectionPoint = default;
            double2 va = a2 - a1;
            double2 vb = b2 - b1;
            double kross = MyMath.cross(va, vb);

            if (math.abs(kross) < Epsilon.Eps)
                return false;
            double2 e = a1 - b1;

            double a = MyMath.cross(vb, e) / kross;
            double b = MyMath.cross(va, e) / kross;

            intersectionPoint = new IntersectionPoint(CalcAlongUsingValue(a), CalcAlongUsingValue(b), new double2(a1.x + a * va.x, a1.y + a * va.y));
            return true;
        }

        private static int CalcAlongUsingValue(double value)
        {
            if (value <= -Epsilon.Eps)
                return -2;
            else if (value < Epsilon.Eps)
                return -1;
            else if (value - 1 <= -Epsilon.Eps)
                return 0;
            else if (value - 1 < Epsilon.Eps)
                return 1;
            else
                return 2;
        }
    }
}
