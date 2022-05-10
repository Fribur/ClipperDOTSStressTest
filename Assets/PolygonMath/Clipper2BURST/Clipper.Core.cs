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
using System;
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
            x = (long) math.round(pt.x);
            y = (long) math.round(pt.y);
        }
        public long2(long2 pt, double scale)
        {
            x = (long)math.round(pt.x * scale);
            y = (long)math.round(pt.y * scale);
        }

        public long2(double2 pt, double scale)
        {
            x = (long) math.round(pt.x * scale);
            y = (long) math.round(pt.y * scale);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(long2 lhs, long2 rhs) { return lhs.x == rhs.x && lhs.y == rhs.y;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(long2 lhs, long2 rhs) { return lhs.x != rhs.x || lhs.y != rhs.y; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long2 operator +(long2 lhs, long2 rhs)  { return new long2(lhs.x + rhs.x, lhs.y + rhs.y); }

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

        public override int GetHashCode() { return 0; }
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

    public enum OutRecState
    {
        Undefined,
        Open,
        Outer,
        Inner
    };

    public static class InternalClipperFunc
    {
        public const double floatingPointTolerance = 1E-15;
        public const double defaultMinimumEdgeLength = 0.1;

        public static double CrossProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double) (pt2.x - pt1.x) * (pt3.y - pt2.y) -
                    (double) (pt2.y - pt1.y) * (pt3.x - pt2.x));
        }
        public static double DotProduct(long2 pt1, long2 pt2, long2 pt3)
        {
            //typecast to double to avoid potential int overflow
            return ((double) (pt2.x - pt1.x) * (pt3.x - pt2.x) +
                    (double) (pt2.y - pt1.y) * (pt3.y - pt2.y));
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
    } //InternalClipperFuncs
} //namespace