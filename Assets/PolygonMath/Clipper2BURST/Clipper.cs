using Clipper2Lib;
using Unity.Mathematics;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    public static class ClipperFunc
    {
        public static Rect64 MaxInvalidRect64()
        {
            return new Rect64(long.MaxValue, long.MaxValue, long.MinValue, long.MinValue);
        }

        public static RectD MaxInvalidRectD()
        {
            return new RectD(double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue);
        }
        public static double Sqr(double value)
        {
          return value * value;
        }
        public static bool PointsNearEqual(double2 pt1, double2 pt2, double distanceSqrd)
        {
          return Sqr(pt1.x - pt2.x) + Sqr(pt1.y - pt2.y) < distanceSqrd;
        }
    }
} 