using Unity.Mathematics;

namespace Chart3D.PolygonMath.Clipping.Clipper2LibBURST
{
  public static class ClipperFunc
  {
    public static double Sqr(double value)
    {
      return value * value;
    }
    public static bool PointsNearEqual(double2 pt1, double2 pt2, double distanceSqrd)
    {
      return Sqr(pt1.x - pt2.x) + Sqr(pt1.y - pt2.y) < distanceSqrd;
    }
  }
} //namespace