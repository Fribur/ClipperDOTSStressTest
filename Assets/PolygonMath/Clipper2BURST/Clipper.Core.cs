using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    public struct long2
    {
        public long x;
        public long y;
        public long2(long2 pt)
        {
            x = pt.x;
            y = pt.y;
        }

        public long2(long x, long y)
        {
            this.x = x;
            this.y = y;
        }

        public long2(double x, double y)
        {
            this.x = (long)math.round(x);
            this.y = (long)math.round(y);
        }

        public long2(double2 pt)
        {
            x = (long)math.round(pt.x);
            y = (long)math.round(pt.y);
        }
        public long2(long2 pt, double scale)
        {
            x = (long)math.round(pt.x * scale);
            y = (long)math.round(pt.y * scale);
        }

        public long2(double2 pt, double scale)
        {
            x = (long)math.round(pt.x * scale);
            y = (long)math.round(pt.y * scale);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(long2 lhs, long2 rhs) { return lhs.x == rhs.x && lhs.y == rhs.y; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(long2 lhs, long2 rhs) { return lhs.x != rhs.x || lhs.y != rhs.y; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator +(long2 lhs, long2 rhs) { return new long2(lhs.x + rhs.x, lhs.y + rhs.y); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator -(long2 lhs, long2 rhs) { return new long2(lhs.x - rhs.x, lhs.y - rhs.y); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double2 operator *(double lhs, long2 rhs) { return new double2(lhs * rhs.x, lhs * rhs.y); }

        public override bool Equals(object obj)
        {
            if (obj is long2 p)
                return this == p;
            else
                return false;
        }

        public override string ToString()
        {
            return $"({x},{y})";
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 29 + (int)x;
            hash = hash * 29 + (int)y;
            return hash;
        }
    }
    public struct Rect64
    {
        public long left;
        public long top;
        public long right;
        public long bottom;

        public Rect64(long l, long t, long r, long b)
        {
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


    }

    //Note: all clipping operations except for Difference are commutative.
    public enum ClipType
    {
        None,
        Intersection,
        Union,
        Difference,
        Xor
    };

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

    public static class InternalClipperFunc
    {
        public const double floatingPointTolerance = 1E-12;
        public const double defaultMinimumEdgeLength = 0.1;
        internal static bool IsAlmostZero(double value)
        {
            return (math.abs(value) <= floatingPointTolerance);
        }

        public static double CrossProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double)(pt2.x - pt1.x) * (pt3.y - pt2.y) -
                    (double)(pt2.y - pt1.y) * (pt3.x - pt2.x));
        }
        public static double DotProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double)(pt2.x - pt1.x) * (pt3.x - pt2.x) +
                    (double)(pt2.y - pt1.y) * (pt3.y - pt2.y));
        }

        public static double DotProduct(double2 vec1, double2 vec2)
        {
            return (vec1.x * vec2.x + vec1.y * vec2.y);
        }

        public static bool GetIntersectPoint(long2 ln1a, long2 ln1b, long2 ln2a, long2 ln2b, out double2 ip)
        {
            ip = new double2();
            double m1, b1, m2, b2;
            if (ln1b.x == ln1a.x)
            {
                if (ln2b.x == ln2a.x) return false;
                m2 = (double)(ln2b.y - ln2a.y) / (ln2b.x - ln2a.x);
                b2 = ln2a.y - m2 * ln2a.x;
                ip.x = ln1a.x;
                ip.y = m2 * ln1a.x + b2;
            }
            else if (ln2b.x == ln2a.x)
            {
                m1 = (double)(ln1b.y - ln1a.y) / (ln1b.x - ln1a.x);
                b1 = ln1a.y - m1 * ln1a.x;
                ip.x = ln2a.x;
                ip.y = m1 * ln2a.x + b1;
            }
            else
            {
                m1 = (double)(ln1b.y - ln1a.y) / (ln1b.x - ln1a.x);
                b1 = ln1a.y - m1 * ln1a.x;
                m2 = (double)(ln2b.y - ln2a.y) / (ln2b.x - ln2a.x);
                b2 = ln2a.y - m2 * ln2a.x;
                if (Math.Abs(m1 - m2) > floatingPointTolerance)
                {
                    ip.x = (b2 - b1) / (m1 - m2);
                    ip.y = m1 * ip.x + b1;
                }
                else
                {
                    ip.x = (ln1a.x + ln1b.x) * 0.5;
                    ip.y = (ln1a.y + ln1b.y) * 0.5;
                }
            }

            return true;
        }
        public static bool SegmentsIntersect(long2 seg1a, long2 seg1b, long2 seg2a, long2 seg2b)
        {
            double dx1 = seg1a.x - seg1b.x;
            double dy1 = seg1a.y - seg1b.y;
            double dx2 = seg2a.x - seg2b.x;
            double dy2 = seg2a.y - seg2b.y;
            return (((dy1 * (seg2a.x - seg1a.x) -
                dx1 * (seg2a.y - seg1a.y)) * (dy1 * (seg2b.x - seg1a.x) -
                dx1 * (seg2b.y - seg1a.y)) < 0) &&
                ((dy2 * (seg1a.x - seg2a.x) -
                dx2 * (seg1a.y - seg2a.y)) * (dy2 * (seg1b.x - seg2a.x) -
                dx2 * (seg1b.y - seg2a.y)) < 0));
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
            if (outptLL.next[startOp] == startOp || outptLL.next[startOp] == outptLL.prev[startOp])
                return PointInPolygonResult.IsOutside;

            int curr = startOp, prev = outptLL.prev[curr];
            while (outptLL.pt[prev].y == pt.y)
            {
                if (prev == startOp) return PointInPolygonResult.IsOutside;
                prev = outptLL.prev[prev];
            }

            bool is_above = outptLL.pt[prev].y < pt.y;
            outptLL.next[outptLL.prev[startOp]] = -1; // temporary !!!
            int val = 0;
            do
            {
                if (is_above)
                {
                    while (curr != -1 && outptLL.pt[curr].y < pt.y) curr = outptLL.next[curr];
                    if (curr == -1) break;
                }
                else
                {
                    while (curr != -1 && outptLL.pt[curr].y > pt.y) curr = outptLL.next[curr];
                    if (curr == -1) break;
                }
                prev = outptLL.prev[curr];

                if (outptLL.pt[curr].y == pt.y)
                {
                    if (outptLL.pt[curr].x == pt.x || (outptLL.pt[curr].y == outptLL.pt[prev].y &&
                      ((pt.x < outptLL.pt[prev].x) != (pt.x < outptLL.pt[curr].x))))
                    {
                        outptLL.next[outptLL.prev[startOp]] = startOp; // reestablish the link
                        return PointInPolygonResult.IsOn;
                    }
                    curr = outptLL.next[curr];
                    continue;
                }

                if (pt.x < outptLL.pt[curr].x && pt.x < outptLL.pt[prev].x)
                {
                    // we're only interested in edges crossing on the left
                }
                else if (pt.x > outptLL.pt[prev].x && pt.x > outptLL.pt[curr].x)
                    val = 1 - val; // toggle val
                else
                {
                    double d = InternalClipperFunc.CrossProduct(outptLL.pt[prev], outptLL.pt[curr], pt);
                    if (d == 0)
                    {
                        outptLL.next[outptLL.prev[startOp]] = startOp; // reestablish the link
                        return PointInPolygonResult.IsOn;
                    }
                    if ((d < 0) == is_above) val = 1 - val;
                }
                is_above = !is_above;
                curr = outptLL.next[curr];

            } while (curr != -1);

            outptLL.next[outptLL.prev[startOp]] = startOp;
            return val == 0 ?
              PointInPolygonResult.IsOutside :
              PointInPolygonResult.IsInside;
        }


    } //InternalClipperFuncs
} //namespace