using Chart3D.MathExtensions;
using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Clipper2SoA
{    
    public struct Rect64
    {
        public long left;
        public long top;
        public long right;
        public long bottom;

        public Rect64(long l, long t, long r, long b)
        {
            //if (r < l || b < t)
            //    Debug.LogError("Invalid Rect64 assignment");
            left = l;
            top = t;
            right = r;
            bottom = b;
        }

        public Rect64(Rect64 rec)
        {
            left = rec.left;
            top = rec.top;
            right = rec.right;
            bottom = rec.bottom;
        }

        public long Width
        {
            get => right - left;
            set => right = left + value;
        }

        public long Height
        {
            get => bottom - top;
            set => bottom = top + value;
        }

        public bool IsEmpty()
        {
            return bottom <= top || right <= left;
        }
        public bool Contains(long2 pt)
        {
            return pt.x > left && pt.x < right &&
              pt.y > top && pt.y < bottom;
        }

        public bool Contains(Rect64 rec)
        {
            return rec.left >= left && rec.right <= right &&
              rec.top >= top && rec.bottom <= bottom;
        }
        public bool Intersects(Rect64 rec)
        {
            return (Math.Max(left, rec.left) <= Math.Min(right, rec.right)) &&
              (Math.Max(top, rec.top) <= Math.Min(bottom, rec.bottom));
        }       

    }

    //Note: all clipping operations except for Difference are commutative.
    //public enum ClipType
    //{
    //    None,
    //    Intersection,
    //    Union,
    //    Difference,
    //    Xor
    //};

    public enum PathType
    {
        Subject,
        Clip
    };

    //By far the most widely used filling rules for polygons are EvenOdd
    //and NonZero, sometimes called Alternate and Winding respectively.
    //https://en.wikipedia.org/wiki/Nonzero-rule
    public enum FillRule
    {
        EvenOdd,
        NonZero,
        Positive,
        Negative
    };

    //PointInPolygon
    public enum PipResult
    {
        Inside,
        Outside,
        OnEdge
    };

    public static class InternalClipper
    {
        internal const long MaxInt64 = 9223372036854775807;
        internal const long MaxCoord = MaxInt64 / 4;
        internal const double max_coord = MaxCoord;
        internal const double min_coord = -MaxCoord;
        internal const long Invalid64 = MaxInt64;

        internal const double defaultArcTolerance = 0.25;
        public const double floatingPointTolerance = 1E-12;
        public const double defaultMinimumEdgeLength = 0.1;

        internal static void CheckPrecision(int precision)
        {
            if (precision < -8 || precision > 8)
                Debug.LogError("Precision is out of range.");
        }
        internal static bool IsAlmostZero(double value)
        {
            return (math.abs(value) <= floatingPointTolerance);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double CrossProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double)(pt2.x - pt1.x) * (pt3.y - pt2.y) -
                    (double)(pt2.y - pt1.y) * (pt3.x - pt2.x));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double DotProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double)(pt2.x - pt1.x) * (pt3.x - pt2.x) +
                    (double)(pt2.y - pt1.y) * (pt3.y - pt2.y));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long CheckCastInt64(double val)
        {
            if ((val >= max_coord) || (val <= min_coord)) return Invalid64;
            return (long)math.round(val);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetIntersectPt(long2 ln1a,
            long2 ln1b, long2 ln2a, long2 ln2b, out long2 ip)
        {
            double dy1 = (ln1b.y - ln1a.y);
            double dx1 = (ln1b.x - ln1a.x);
            double dy2 = (ln2b.y - ln2a.y);
            double dx2 = (ln2b.x - ln2a.x);
            double cp = dy1 * dx2 - dy2 * dx1;
            if (cp == 0.0)
            {
                ip = new long2();
                return false;
            }
            double qx = dx1 * ln1a.y - dy1 * ln1a.x;
            double qy = dx2 * ln2a.y - dy2 * ln2a.x;
            ip = new long2(
              CheckCastInt64((dx1 * qy - dx2 * qx) / cp),
              CheckCastInt64((dy1 * qy - dy2 * qx) / cp));
            return (ip.x != Invalid64 && ip.y != Invalid64);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GetIntersectPoint(long2 ln1a,
            long2 ln1b, long2 ln2a, long2 ln2b, out double2 ip)
        {
            double dy1 = (ln1b.y - ln1a.y);
            double dx1 = (ln1b.x - ln1a.x);
            double dy2 = (ln2b.y - ln2a.y);
            double dx2 = (ln2b.x - ln2a.x);
            double q1 = dy1 * ln1a.x - dx1 * ln1a.y;
            double q2 = dy2 * ln2a.x - dx2 * ln2a.y;
            double cross_prod = dy1 * dx2 - dy2 * dx1;
            if (cross_prod == 0.0)
            {
                ip = new double2();
                return false;
            }
            ip = new double2(
              (dx2 * q1 - dx1 * q2) / cross_prod,
              (dy2 * q1 - dy1 * q2) / cross_prod);
            return true;
        }
        internal static bool SegsIntersect(long2 seg1a,
            long2 seg1b, long2 seg2a, long2 seg2b, bool inclusive = false)
        {
            if (inclusive)
            {
                double res1 = CrossProduct(seg1a, seg2a, seg2b);
                double res2 = CrossProduct(seg1b, seg2a, seg2b);
                if (res1 * res2 > 0) return false;
                double res3 = CrossProduct(seg2a, seg1a, seg1b);
                double res4 = CrossProduct(seg2b, seg1a, seg1b);
                if (res3 * res4 > 0) return false;
                // ensure NOT collinear
                return (res1 != 0 || res2 != 0 || res3 != 0 || res4 != 0);
            }
            else
            {
                return (CrossProduct(seg1a, seg2a, seg2b) *
                  CrossProduct(seg1b, seg2a, seg2b) < 0) &&
                  (CrossProduct(seg2a, seg1a, seg1b) *
                  CrossProduct(seg2b, seg1a, seg1b) < 0);
            }
        }
        public static long2 GetClosestPtOnSegment(long2 offPt,
            long2 seg1, long2 seg2)
        {
            if (seg1.x == seg2.x && seg1.y == seg2.y) return seg1;
            double dx = (seg2.x - seg1.x);
            double dy = (seg2.y - seg1.y);
            double q = ((offPt.x - seg1.x) * dx +
              (offPt.y - seg1.y) * dy) / ((dx * dx) + (dy * dy));
            if (q < 0) q = 0; else if (q > 1) q = 1;
            return new long2(
              seg1.x + math.round(q * dx), seg1.y + math.round(q * dy));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PointCount(in OutPtLL outPtList, int op)
        {
            int p = op;
            int cnt = 0;
            do
            {
                cnt++;
                p = outPtList.next[p];
            } while (p != op);
            return cnt;
        }

        public static PointInPolygonResult PointInPolygon(long2 pt, ref OutPtLL outptLL, int startOp)
        {
            int originalStartID = startOp;
            int nextOp;
            if ((nextOp = outptLL.next[startOp]) == startOp || outptLL.next[nextOp] == startOp)//same as "if (len < 3)"
                return PointInPolygonResult.IsOutside;

            while ((nextOp = outptLL.next[startOp]) != startOp && outptLL.pt[startOp].y == pt.y) startOp = nextOp;
            if (outptLL.next[startOp] == startOp) return PointInPolygonResult.IsOutside;

            double d;
            bool isAbove = outptLL.pt[startOp].y < pt.y, startingAbove = isAbove;
            int val = 0, i = outptLL.next[startOp], end = startOp;
            int prevOp;
            while (true)
            {
                if (i == end)
                {
                    if (end == originalStartID || startOp == originalStartID) break;
                    end = startOp;
                    i = originalStartID;
                }

                if (isAbove)
                {
                    while (i != end && outptLL.pt[i].y < pt.y) i = outptLL.next[i];
                    if (i == end) continue;
                }
                else
                {
                    while (i != end && outptLL.pt[i].y > pt.y) i = outptLL.next[i];
                    if (i == end) continue;
                }

                long2 curr = outptLL.pt[i];
                prevOp = outptLL.prev[i];
                if(i == prevOp) prevOp = outptLL.prev[originalStartID];
                var prev = outptLL.pt[prevOp];


                if (curr.y == pt.y)
                {
                    if (curr.x == pt.x || (curr.y == prev.y &&
                      ((pt.x < prev.x) != (pt.x < curr.x))))
                        return PointInPolygonResult.IsOn;
                    i = outptLL.next[i];
                    if (i == startOp) break;
                    continue;
                }

                if (pt.x < curr.x && pt.x < prev.x)
                {
                    // we're only interested in edges crossing on the left
                }
                else if (pt.x > prev.x && pt.x > curr.x)
                {
                    val = 1 - val; // toggle val
                }
                else
                {
                    d = CrossProduct(prev, curr, pt);
                    if (d == 0) return PointInPolygonResult.IsOn;
                    if ((d < 0) == isAbove) val = 1 - val;
                }
                isAbove = !isAbove;
                i = outptLL.next[i];
            }

            prevOp = outptLL.prev[originalStartID];
            if (isAbove != startingAbove)
            {
                if (i == prevOp) i = originalStartID;
                if (i == originalStartID)
                    d = CrossProduct(outptLL.pt[prevOp], outptLL.pt[originalStartID], pt);
                else
                    d = CrossProduct(outptLL.pt[prevOp], outptLL.pt[i], pt);
                if (d == 0) return PointInPolygonResult.IsOn;
                if ((d < 0) == isAbove) val = 1 - val;
            }

            if (val == 0)
                return PointInPolygonResult.IsOutside;
            return PointInPolygonResult.IsInside;
        }



    } //InternalClipperFuncs
} //namespace