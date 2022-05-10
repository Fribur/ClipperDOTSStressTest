/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - also known as Clipper2                            *
* Date      :  8 May 2022                                                      *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This is the main polygon clipping module                        *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/
using Unity.Mathematics;

namespace PolygonMath.Clipping.Clipper2LibBURST
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