using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Polybool
{  
    /// <summary> Use this to store t of the equation a(t) = a0 + t * (a1 - a0) as a rational number.</summary>
    [DebuggerDisplay("{num} / {den}")]
    public readonly struct Rational : IEquatable<Rational>, IComparable<Rational>
    {
        public readonly long num; // numerator of t
        public readonly long den; // denominator of t (must be > 0)
        /// <summary> Use this to store the t fraction of the equation a(t) = a0 + t * (a1 - a0) as a rational number.</summary>
        public Rational(long num, long den)
        {
            if (den < 0)
            {
                num = -num;
                den = -den;
            }
            this.num = num;
            this.den = den;
        }
        public Rational(long value)
        {
            num = value;
            den = 1;
        }
        public static Rational Zero => new Rational(0, 1);
        public static Rational One => new Rational(1, 1);
        public static Rational Half(Rational r) => new Rational(r.num, r.den * 2);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Rational Reduced()
        {
            if (num == 0)
                return Zero;

            long g = PointUtils.Gcd(Math.Abs(num), den);
            return g == 1 ? this : new Rational(num / g, den / g);
        }        
        public long ToInt64Exact()
        {
            //Debug.Assert(IsInteger);
            return num / den;
        }
        public bool IsOne()
        {
            return num == den;
        }
        public bool IsZero()
        {
            return num == 0;
        }

        public static PointRational EvalRational(long2 p0, long dx, long dy, Rational t)
        {
            return new PointRational(
                new Rational(p0.x) + t * new Rational(dx),
                new Rational(p0.y) + t * new Rational(dy)
            );
        }        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> check if rational is larger than 0 and smaller than 1 </summary>
        public bool InRangeStrict()
        {
            return num > 0 && num < den;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary> Check if rational is between (including) 0 and 1 </summary>
        public bool InRangeInclusive()
        {
            return num >= 0 && num <= den;
        }

        /// <summary> check if rational t is larger than rational min and smaller than max </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRangeStrict(Rational t, Rational min, Rational max)
        {
            return t.CompareTo(min) > 0 && t.CompareTo(max) < 0;
        }

        /// <summary> Check if rational is between (including) rational min and max </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InRangeInclusive(Rational t, Rational min, Rational max)
        {
            return t.CompareTo(min) >= 0 && t.CompareTo(max) <= 0;
        }

        
        /// <summary>
        /// p is the point to be projected. s0 is the start point of the line segment, 
        /// sdx and sdy are from the s0S1 vector (s1 - s0), and sLengthSquared is 
        /// squared length of the segment
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rational ProjectPointOntoSegmentLine(long2 p, long2 s0, long sdx, long sdy, long sLengthSquared)
        {
            // dx = p.x - b0.x
            long s0p_x = p.x - s0.x;    
            long s0p_y = p.y - s0.y;    

            long num = s0p_x * sdx + s0p_y * sdy;
            return new Rational(num, sLengthSquared);
        }
        public static Rational ProjectPointOntoLine(PointRational p, long2 s0, long sdx, long sdy, long sLengthSquared)
        {
            // s0p_x = p.x - s0.x
            Rational s0p_x = p.x - new Rational(s0.x); // x of vector s0 -> p
            Rational s0p_y = p.y - new Rational(s0.y); // y of vector s0 -> p

            // dot = (p - b0) · d
            Rational dot = s0p_x * new Rational(sdx) + s0p_y * new Rational(sdy);

            // t = dot / |d|^2
            return dot / new Rational(sLengthSquared);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rational operator -(Rational a, Rational b)
        {
            return new Rational(
                a.num * b.den - b.num * a.den,
                a.den * b.den
            );
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static Rational operator -(Rational a, Rational b)
        //{
        //    long g = PointUtils.Gcd(a.den, b.den);

        //    long ad = a.num * (b.den / g);
        //    long bc = b.num * (a.den / g);
        //    long den = (a.den / g) * b.den;

        //    return new Rational(ad - bc, den);
        //}


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rational operator *(Rational a, Rational b)
        {
            // (a.num / a.den) * (b.num / b.den)
            // = (a.num * b.num) / (a.den * b.den)
            return new Rational(
                a.num * b.num,
                a.den * b.den
            );
        }

        //public static Rational operator *(Rational a, Rational b)
        //{
        //    long g1 = PointUtils.Gcd(Math.Abs(a.num), b.den);
        //    long g2 = PointUtils.Gcd(Math.Abs(b.num), a.den);

        //    long num = (a.num / g1) * (b.num / g2);
        //    long den = (a.den / g2) * (b.den / g1);

        //    return new Rational(num, den);
        //}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rational operator /(Rational a, Rational b)
        {
            return new Rational(
                a.num * b.den,
                a.den * b.num
            );
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static Rational operator /(Rational a, Rational b)
        //{
        //    if (b.num == 0)
        //        throw new DivideByZeroException();

        //    long g1 = PointUtils.Gcd(Math.Abs(a.num), Math.Abs(b.num));
        //    long g2 = PointUtils.Gcd(a.den, b.den);

        //    long num = (a.num / g1) * (b.den / g2);
        //    long den = (a.den / g2) * (b.num / g1);

        //    return new Rational(num, den);
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rational operator +(Rational a, Rational b)
        {
            // a/b + c/d = (ad + bc) / bd
            return new Rational(
                a.num * b.den + b.num * a.den,
                a.den * b.den
            );
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static Rational operator +(Rational a, Rational b)
        //{
        //    // Reduce cross terms before multiply:
        //    long g = PointUtils.Gcd(a.den, b.den);

        //    long ad = a.num * (b.den / g);
        //    long bc = b.num * (a.den / g);
        //    long den = (a.den / g) * b.den;

        //    return new Rational(ad + bc, den);
        //}

        public static Rational operator -(Rational a, long b) =>new Rational(a.num - b * a.den, a.den);
        public static Rational operator +(Rational a, long b) =>new Rational(a.num + b * a.den, a.den);
        public static Rational operator *(Rational a, long b) => new Rational(a.num * b, a.den);
        public static Rational operator *(long a, Rational b) => new Rational(b.num * a, b.den);

        public static Rational Min(Rational a, Rational b) => a.CompareTo(b) <= 0 ? a : b;

        public static Rational Max(Rational a, Rational b) => a.CompareTo(b) >= 0 ? a : b;
        public int CompareTo(Rational other)
        {
            return (num * other.den).CompareTo(other.num * den);
        }
        public override bool Equals(object obj)
        {
            return obj is Rational other && Equals(other);
        }
        public bool Equals(Rational other)
        {
            return num == other.num && den == other.den;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare(in Rational a, in Rational b)
        {
            // a.num * b.den ? b.num * a.den
            var left = a.num * b.den;
            var right = b.num * a.den;
            return left.CompareTo(right);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Compare128(in Rational a, in Rational b)
        {
            // a.num * b.den ? b.num * a.den
            var left = Math128.Mul128(a.num, b.den);
            var right = Math128.Mul128(b.num, a.den);
            return left.CompareTo(right);
        }
        public static bool operator <=(Rational a, Rational b)
        => Compare(a, b) <= 0;

        public static bool operator >=(Rational a, Rational b)
            => Compare(a, b) >= 0;

        public static bool operator <(Rational a, Rational b)
            => Compare(a, b) < 0;

        public static bool operator >(Rational a, Rational b)
            => Compare(a, b) > 0;
        public static bool operator ==(Rational e1, Rational e2)
        {
            return e1.num == e2.num && e1.den == e2.den;
        }
        public static bool operator !=(Rational e1, Rational e2)
        {
            return !(e1 == e2);
        }
        public override string ToString()
        {
            return $"{num} / {den}";
        }
        public override int GetHashCode()
        {
            //return HashCode.Combine(num, den);
            int hashCode = 2055808453;
            hashCode = hashCode * -1521134295 + num.GetHashCode();
            hashCode = hashCode * -1521134295 + den.GetHashCode();
            return hashCode;
        }

        public bool IsInteger => den == 1 || num % den == 0;
    }
}
