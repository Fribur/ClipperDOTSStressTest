using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Polybool
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct Segment : IEquatable<Segment>
    {
        public readonly long2 p0;                  // ORIGINAL exact endpoints (do not modify after construction)
        public readonly long2 p1;                  // ORIGINAL exact endpoints (do not modify after construction)
        public readonly long2 pd => p1 - p0;

        public Rational start;              // Parametric represetation of start point: p(start) = p0 + start * (p1 - p0)
        public Rational end;                // Parametric represetation of end point: p(end) = p0 + end * (p1 - p0)

        public int windingTopToBottom;      //store here winding of egde crossing vertial ray from top to bottom
        public int windingLeftToRight;      //store here winding of egde crossing horizontal ray from left to right
        ushort _boolField;
        public bool fillAbove
        {
            get { return Utils.GetBit(_boolField, 0); }
            set { _boolField = Utils.SetBit(_boolField, 0, value); }
        }
        public bool fillBelow
        {
            get { return Utils.GetBit(_boolField, 1); }
            set { _boolField = Utils.SetBit(_boolField, 1, value); }
        }
        public bool fillOtherAbove
        {
            get { return Utils.GetBit(_boolField, 2); }
            set { _boolField = Utils.SetBit(_boolField, 2, value); }
        }
        public bool fillOtherBelow
        {
            get { return Utils.GetBit(_boolField, 3); }
            set { _boolField = Utils.SetBit(_boolField, 3, value); }
        }
        public bool myFillSet
        {
            get { return Utils.GetBit(_boolField, 4); }
            set { _boolField = Utils.SetBit(_boolField, 4, value); }
        }
        public bool otherFillSet
        {
            get { return Utils.GetBit(_boolField, 5); }
            set { _boolField = Utils.SetBit(_boolField, 5, value); }
        }
        public bool closed
        {
            get { return Utils.GetBit(_boolField, 6); }
            set { _boolField = Utils.SetBit(_boolField, 6, value); }
        }
        public bool inResults
        {
            get { return Utils.GetBit(_boolField, 7); }
            set { _boolField = Utils.SetBit(_boolField, 7, value); }
        }
        public bool isPrimary
        {
            get { return Utils.GetBit(_boolField, 8); }
            set { _boolField = Utils.SetBit(_boolField, 8, value); }
        }  

        public Segment(long2 segStart, long2 segEnd, Rational intervalStart, Rational intervalEnd, bool isPrimary, bool closed)
        {
            p0 = segStart;
            p1 = segEnd;
            start = intervalStart;
            end = intervalEnd;
            _boolField = 0;
            windingTopToBottom = 0;
            windingLeftToRight = 0;
            myFillSet = false;
            otherFillSet = false;
            fillAbove = false;
            fillBelow = false;
            fillOtherAbove = false;
            fillOtherBelow = false;
            this.closed = closed;
            this.isPrimary = isPrimary;
        }
        public Segment(Segment segment, bool fillAbove, bool fillBelow)
        {
            p0 = segment.p0;
            p1 = segment.p1;
            start = segment.start;
            end = segment.end;
            _boolField = segment._boolField;
            windingTopToBottom = segment.windingTopToBottom;
            windingLeftToRight = segment.windingLeftToRight;
            myFillSet = true;
            this.fillAbove = fillAbove;
            this.fillBelow = fillBelow;
            otherFillSet = true;
            fillOtherAbove = false;
            fillOtherBelow = false;
            closed = segment.closed;
            isPrimary = true;
        }
        public Segment(Segment segment,  Rational intervalStart)
        {
            p0 = segment.p0;
            p1 = segment.p1;
            start = intervalStart;
            end = segment.end;
            _boolField = segment._boolField;
            windingTopToBottom = segment.windingTopToBottom;
            windingLeftToRight = segment.windingLeftToRight;
            myFillSet = segment.myFillSet;
            fillAbove = segment.fillAbove;
            fillBelow = segment.fillBelow;
            otherFillSet = false;	//do NOT copy otherFill to the right segment or the combine phase will fail!!!
            fillOtherAbove = false; //do NOT copy otherFill to the right segment or the combine phase will fail!!!
            fillOtherBelow = false; //do NOT copy otherFill to the right segment or the combine phase will fail!!!
            closed = segment.closed;
            isPrimary = segment.isPrimary;
        }
        public void Split(Rational ip, out Segment right)
        {            
            right = new Segment(this, ip);  //generate right Segment            
            end = ip;                       //update Endpoint of left segment
        }

        /// <summary>
        /// Very crucial method to express a point along the segment line as a rational (x/y) and 
        /// NOT as long2 or double2. Convert to long2 or double2 ONLY right at the end in segment chainer. Otherwise 
        /// topology of segments toward eachother WILL break!
        /// </summary>
        public PointRational Eval(Rational t)
        {
            // p(t) = p0 + t * (p1 - p0)
            // Everything stays exact in Rational space
            // this is very importent, segment chainer WILL break if t Rational is converted
            // into double prior to multiplying it with dx! Introduces rounding error, topology
            // and segment chainer will break 
            long dx = p1.x - p0.x;
            long dy = p1.y - p0.y;

            return new PointRational(new Rational(p0.x) + t * dx, new Rational(p0.y) + t * dy);
        }

        public static IntersectionResultType SegmentLineIntersectSegmentLine(ref Segment segA, ref Segment segB, out Rational tA1, out Rational tB1, out Rational tA2, out Rational tB2)
        {
            tA1 = tA2 = tB1 = tB2 = default;

            var a0 = segA.p0;
            var a1 = segA.p1;
            var aMin = segA.start;
            var aMax = segA.end;

            var b0 = segB.p0;
            var b1 = segB.p1;
            var bMin = segB.start;
            var bMax = segB.end;

            long adx = a1.x - a0.x;
            long ady = a1.y - a0.y;
            long bdx = b1.x - b0.x;
            long bdy = b1.y - b0.y;

            //use of 128 bit version of CrossProduct and IsCollinear here ensure we stay exact when
            // calculating intersection point. We need to 100% ensure both segments.Eval to the exact same intersection point,
            // or segment chainer will fail
            long det = PointUtils128.CrossProduct128(adx, ady, bdx, bdy).ToInt64Saturating();
            //long det = PointUtils.CrossProduct(adx, ady, bdx, bdy);

            // =========================================================
            // PARALLEL / COLLINEAR
            // =========================================================
            if (det == 0)
            {
                if (!PointUtils128.IsCollinear128(a0, b0, a1))
                //if (!PointUtils.IsCollinear(a0, b0, a1))
                    return IntersectionResultType.Nothing;

                // ---- Project B endpoints onto A ----
                long aLen2 = adx * adx + ady * ady;

                Rational b0onA = Rational.ProjectPointOntoSegmentLine(b0, a0, adx, ady, aLen2);
                Rational b1onA = Rational.ProjectPointOntoSegmentLine(b1, a0, adx, ady, aLen2);

                // ---- Overlap interval in A parameter space ----
                Rational overlapMinA = Rational.Max(Rational.Min(b0onA, b1onA), aMin);
                Rational overlapMaxA = Rational.Min(Rational.Max(b0onA, b1onA), aMax);

                if (overlapMinA.CompareTo(overlapMaxA) > 0)
                    return IntersectionResultType.Nothing;

                // ---- Map overlap endpoints to B using exact evaluation ----
                long bLen2 = bdx * bdx + bdy * bdy;

                PointRational pMin = Rational.EvalRational(a0, adx, ady, overlapMinA);
                PointRational pMax = Rational.EvalRational(a0, adx, ady, overlapMaxA);

                Rational overlapMinB = Rational.ProjectPointOntoLine(pMin, b0, bdx, bdy, bLen2);
                Rational overlapMaxB = Rational.ProjectPointOntoLine(pMax, b0, bdx, bdy, bLen2);

                if (!Rational.InRangeInclusive(overlapMinA, aMin, aMax) || !Rational.InRangeInclusive(overlapMaxA, aMin, aMax))
                    return IntersectionResultType.Nothing;
                if (!Rational.InRangeInclusive(overlapMinB, bMin, bMax) || !Rational.InRangeInclusive(overlapMaxB, bMin, bMax))
                    return IntersectionResultType.Nothing;

                if (overlapMinA.CompareTo(overlapMaxA) == 0)
                {
                    tA1 = overlapMinA.Reduced();
                    tB1 = overlapMinB.Reduced();
                    //DebugHelper.AssertIntersectionExact(segA, segB, tA1, tB1);
                    return IntersectionResultType.One;
                }

                tA1 = overlapMinA.Reduced();
                tA2 = overlapMaxA.Reduced();
                tB1 = overlapMinB.Reduced();
                tB2 = overlapMaxB.Reduced();
                //DebugHelper.AssertIntersectionExact(segA, segB, tA1, tB1);
                //DebugHelper.AssertIntersectionExact(segA, segB, tA2, tB2);
                return IntersectionResultType.Two;
            }

            // =========================================================
            // SINGLE INTERSECTION POINT
            // =========================================================
            long dx = a0.x - b0.x;
            long dy = a0.y - b0.y;

            long numA = PointUtils128.CrossProduct128(bdx, bdy, dx, dy).ToInt64Saturating();
            long numB = PointUtils128.CrossProduct128(adx, ady, dx, dy).ToInt64Saturating();
            //long numA = PointUtils.CrossProduct(bdx, bdy, dx, dy);
            //long numB = PointUtils.CrossProduct(adx, ady, dx, dy);

            Rational tA = new Rational(numA, det);
            Rational tB = new Rational(numB, det);

            if (!Rational.InRangeInclusive(tA, aMin, aMax) || !Rational.InRangeInclusive(tB, bMin, bMax))
                return IntersectionResultType.Nothing;

            tA1 = tA.Reduced();
            tB1 = tB.Reduced();
            //DebugHelper.AssertIntersectionExact(segA, segB, tA1, tB1);
            return IntersectionResultType.One;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> Returns -1 if parametric point1 is smaller than parametric point2</summary>
        public static int Compare(in long2 pa, in long2 dpa, in Rational tA, in long2 pb, in long2 dpb, in Rational tB)
        {
            var compX = CompareCoord(pa.x, dpa.x, tA, pb.x, dpb.x, tB);
            if (compX == 0)
                return CompareCoord(pa.y, dpa.y, tA, pb.y, dpb.y, tB);
            return compX;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareCoord(long p0a, long da, in Rational tA, long p0b, long db, in Rational tB)
        {
            // denominators must be positive for strict ordering
            long denA = tA.den;
            long denB = tB.den;

            var LA = p0a * denA + da * tA.num;
            var LB = p0b * denB + db * tB.num;

            // Compare LA * tB.den  vs  LB * tA.den
            // IMPORTANT: multiply AFTER sign normalization

            // Fast sign check before multiplication
            int sLA = Math.Sign(LA);
            int sLB = Math.Sign(LB);

            if (sLA != sLB)
                return sLA < sLB ? -1 : 1;

            // Same sign → safe to compare scaled magnitudes
            var left = LA * denB;
            var right = LB * denA;

            return left.CompareTo(right);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> Returns -1 if parametric point1 is smaller than parametric point2</summary>
        public static int Compare128(in long2 pa, in long2 dpa, in Rational tA, in long2 pb, in long2 dpb, in Rational tB)
        {
            var compX = CompareCoord128(pa.x, dpa.x, tA, pb.x, dpb.x, tB);
            if (compX == 0)
                return CompareCoord128(pa.y, dpa.y, tA, pb.y, dpb.y, tB);
            return compX;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareCoord128(long p0a, long da, in Rational tA, long p0b, long db, in Rational tB)
        {
            // denominators must be positive for strict ordering
            long denA = tA.den;
            long denB = tB.den;

            // LA = p0a * tA.den + da * tA.num
            var LA = Math128.Add128(Math128.Mul128(p0a, denA), Math128.Mul128(da, tA.num));

            // LB = p0b * tB.den + db * tB.num
            var LB = Math128.Add128(Math128.Mul128(p0b, denB), Math128.Mul128(db, tB.num));

            // Compare LA * tB.den  vs  LB * tA.den
            // IMPORTANT: multiply AFTER sign normalization

            // Fast sign check before multiplication
            int sLA = Math128.Sign128(LA);
            int sLB = Math128.Sign128(LB);

            if (sLA != sLB)
                return sLA < sLB ? -1 : 1;

            // Same sign → safe to compare scaled magnitudes
            var left = Math128.Mul128x64(LA, denB);
            var right = Math128.Mul128x64(LB, denA);

            return left.CompareTo(right);
        }

        public override bool Equals(object obj)
        {
            return obj is Segment other && Equals(other);
        }
        public bool Equals(Segment other)
        {
            return p0 == other.p0 && p1 == other.p1 &&
                start == other.start && end == other.end &&
                isPrimary == other.isPrimary &&
                windingTopToBottom == other.windingTopToBottom &&
                windingLeftToRight == other.windingLeftToRight &&
                _boolField == other._boolField;
        }

        public static bool operator ==(Segment e1, Segment e2)
        {
            return e1.p0 == e2.p0 && e1.p1 == e2.p1 &&
                e1.start == e2.start && e1.end == e2.end &&
                e1.isPrimary == e2.isPrimary &&
                e1.windingTopToBottom == e2.windingTopToBottom &&
                e1.windingLeftToRight == e2.windingLeftToRight &&
                e1._boolField == e2._boolField;
        }
        public static bool operator !=(Segment e1, Segment e2)
        {
            return !(e1 == e2);
        }
        public override int GetHashCode()
        {
            //return HashCode.Combine(p0, p1, _boolField);
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + p0.GetHashCode();
            hashCode = hashCode * -1521134295 + p1.GetHashCode();
            hashCode = hashCode * -1521134295 + start.GetHashCode();
            hashCode = hashCode * -1521134295 + end.GetHashCode();
            hashCode = hashCode * -1521134295 + _boolField.GetHashCode();
            return hashCode;
        }
        private string DebuggerDisplay
        {
            get
            {
                //return $"{p0.x},{p0.y} → {p1.x},{p1.y}";                 //original start and end points
                return $"{Eval(start).ToLong2()} → {Eval(end).ToLong2()}"; //current active interval
            }
        }
    }
}