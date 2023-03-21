using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Chart3D.MathExtensions
{
    public struct quaternionD : IEquatable<quaternionD>
    {
        /// <summary>
        /// The quaternion component values.
        /// </summary>
        public double4 value;
        public static readonly quaternionD identity = new quaternionD(0.0f, 0.0f, 0.0f, 1.0f);
        public quaternionD(double x, double y, double z, double w) { value.x = x; value.y = y; value.z = z; value.w = w; }


        /// <summary>Constructs a quaternion from float4 vector.</summary>
        /// <param name="value">The quaternion xyzw component values.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public quaternionD(double4 value) { this.value = value; }
        public static implicit operator quaternionD(double4 v) { return new quaternionD(v); }
        public static implicit operator quaternionD(quaternion v) { return new quaternionD(v.value); }
        public static explicit operator quaternion(quaternionD v) { return new quaternion((float4)v.value); }
      


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double3 ToEulerAngles()
        {
            double3 angles;
            // roll (x-axis rotation)
            double sinr_cosp = 2 * (value.w * value.x + value.y * value.z);
            double cosr_cosp = 1 - 2 * (value.x * value.x + value.y * value.y);
            angles.x = math.atan2(sinr_cosp, cosr_cosp);

            // pitch (y-axis rotation)
            double sinp = 2 * (value.w * value.y - value.z * value.x);
            if (math.abs(sinp) >= 1)
                angles.y = math.PI * 0.5f * math.sign(sinp); // use 90 degrees if out of range
            else
                angles.y = math.asin(sinp);

            // yaw (z-axis rotation)
            double siny_cosp = 2 * (value.w * value.z + value.x * value.y);
            double cosy_cosp = 1 - 2 * (value.y * value.y + value.z * value.z);
            angles.z = math.atan2(siny_cosp, cosy_cosp);

            return angles;
        }
        public bool Equals(quaternionD other)
        {
            return math.all(value == other.value);
        }
        public static bool operator == (quaternionD a, quaternionD b)
        {

            return a.Equals(b);
        }
        public static bool operator !=(quaternionD a, quaternionD b)
        {

            return !a.Equals(b);
        }
        public override bool Equals(object obj) => obj is quaternionD other && Equals(other);
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
        public override string ToString()
        {
            return value.ToString();
        }
    }
    public static class MyMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD mul(quaternionD a, quaternionD b)
        {
            return new quaternionD(a.value.wwww * b.value + (a.value.xyzx * b.value.wwwx + a.value.yzxy * b.value.zxyy) * new double4(1.0d, 1.0d, 1.0d, -1.0d) - a.value.zxyz * b.value.yzxz);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 mul(quaternionD q, double3 v)
        {
            double3 t = 2 * math.cross(q.value.xyz, v);
            return v + q.value.w * t + math.cross(q.value.xyz, t);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD inverse(quaternionD q)
        {
            double4 x = q.value;
            return new quaternionD(math.rcp(math.dot(x, x)) * x * new double4(-1.0d, -1.0d, -1.0d, 1.0d));
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static quaternionD FromToRotation(double3 from, double3 to)
        //{
        //    var axis = math.cross(from, to);
        //    var rotationAngle = math.atan2(math.length(axis), math.dot(from, to));
        //    return AxisAngle(math.normalize(axis), -rotationAngle); //-angle = inverse rotation
        //}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD FromToRotation(double3 from, double3 to)
        {
            from *= 100;
            to *= 100;

            //https://people.eecs.berkeley.edu/~wkahan/Triangle.pdf
            var a = math.length(from);
            var b = math.length(to);
            var c = math.length(from - to);
            double mu = 0;
            if (b >= c && c >= 0)
                mu = c - (a - b);
            else if (c >= b && b >= 0)
                mu = b - (a - c);
            else
                Debug.Log("invalid triangle");
            var rotationAngle = 2 * math.atan(math.sqrt(((a - b) + c) * mu / ((a + (b + c)) * ((a - c) + b))));

            var axis = math.cross(from, to);
            //var rotationAngle = math.atan2(math.length(axis), math.dot(from, to));            
            return AxisAngle(math.normalize(axis), -rotationAngle); //-angle = inverse rotation
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD AxisAngle(double3 axis, double angle)
        {
            double sina, cosa;
            sincos(0.5d * angle, out sina, out cosa);
            return new double4(axis * sina, cosa);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 RotateAroundPivot(double3 position, double3 pivot, double3 axis, double delta)
        {
            return mul(AxisAngle(axis, delta), position - pivot) + pivot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 RotateAroundPivot(double3 position, double3 pivot, quaternionD rotation)
        {
            return mul(rotation, position - pivot) + pivot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD RotateX(double angle)
        {
            double sina, cosa;
            sincos(0.5f * angle, out sina, out cosa);
            return new quaternionD(sina, 0.0f, 0.0f, cosa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD RotateY(double angle)
        {
            double sina, cosa;
            sincos(0.5f * angle, out sina, out cosa);
            return new quaternionD(0.0f, sina, 0.0f, cosa);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD RotateZ(double angle)
        {
            double sina, cosa;
            sincos(0.5f * angle, out sina, out cosa);
            return new quaternionD(0.0f, 0.0f, sina, cosa);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 RotateAroundPivot(float3 position, float3 pivot, float3 axis, float delta)
        {
            return math.mul(quaternion.AxisAngle(axis, delta), position - pivot) + pivot;
        }
        public static float3 ToEulerAngles(this quaternion q)
        {
            float3 angles;
            float4 val = q.value;
            // roll (x-axis rotation)
            float sinr_cosp = 2 * (val.w * val.x + val.y * val.z);
            float cosr_cosp = 1 - 2 * (val.x * val.x + val.y * val.y);
            angles.x = math.atan2(sinr_cosp, cosr_cosp);

            // pitch (y-axis rotation)
            float sinp = 2 * (val.w * val.y - val.z * val.x);
            if (math.abs(sinp) >= 1)
                angles.y = math.PI * 0.5f * math.sign(sinp); // use 90 degrees if out of range
            else
                angles.y = math.asin(sinp);

            // yaw (z-axis rotation)
            float siny_cosp = 2 * (val.w * val.z + val.x * val.y);
            float cosy_cosp = 1 - 2 * (val.y * val.y + val.z * val.z);
            angles.z = math.atan2(siny_cosp, cosy_cosp);

            return angles;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void sincos(double x, out double s, out double c) { s = math.sin(x); c = math.cos(x); }


        /// <summary>Finds the magnitude of the cross product of two vectors (if we pretend they're in three dimensions) </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <returns>The magnitude of the cross product</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double cross(double2 a, double2 b)
        {
            return (a.x * b.y) - (a.y * b.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Angle(double3 from, double3 to)
        {
            var axis = math.cross(from, to);
            return math.atan2(math.length(axis), math.dot(from, to));
        }

        /// <summary> returns angle from -PI to PI </summary>
        public static double Angle(double2 from, double2 to)
        {
            //swapping from and to returns clockwise angle in y-up (right handed) coordinate system,
            //see see https://stackoverflow.com/questions/14066933/direct-way-of-computing-clockwise-angle-between-2-vectors
            var determinat = cross(to, from);   //# determinant. change order to get other direction
            var angle = math.atan2(determinat, math.dot(to, from)); //# atan2(y, x) or atan2(sin, cos)
            if (angle < 0) { angle += MathHelper.twoPI; } //Projection.Wrap2PI(Projection.twoPI + boatHeading) % (Projection.twoPI);
            return angle;
        }
        /// <summary>Returns the result of a spherical interpolation between two quaternions q1 and a2 using an interpolation parameter t.</summary>
        /// <param name="q1">The first quaternion.</param>
        /// <param name="q2">The second quaternion.</param>
        /// <param name="t">The interpolation parameter.</param>
        /// <returns>The spherical linear interpolation of two quaternions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD slerp(quaternionD q1, quaternionD q2, double t)
        {
            double dt = math.dot(q1.value, q2.value);
            if (dt < 0.0f)
            {
                dt = -dt;
                q2.value = -q2.value;
            }

            if (dt < 0.9995f)
            {
                double angle = math.acos(dt);
                double s = math.rsqrt(1.0f - dt * dt);    // 1.0f / sin(angle)
                double w1 = math.sin(angle * (1.0f - t)) * s;
                double w2 = math.sin(angle * t) * s;
                return new quaternionD(q1.value * w1 + q2.value * w2);
            }
            else
            {
                // if the angle is small, use linear interpolation
                return nlerp(q1, q2, t);
            }
        }
        /// <summary>Returns the result of a normalized linear interpolation between two quaternions q1 and a2 using an interpolation parameter t.</summary>
        /// <remarks>
        /// Prefer to use this over slerp() when you know the distance between q1 and q2 is small. This can be much
        /// higher performance due to avoiding trigonometric function evaluations that occur in slerp().
        /// </remarks>
        /// <param name="q1">The first quaternion.</param>
        /// <param name="q2">The second quaternion.</param>
        /// <param name="t">The interpolation parameter.</param>
        /// <returns>The normalized linear interpolation of two quaternions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD nlerp(quaternionD q1, quaternionD q2, double t)
        {
            double dt = math.dot(q1.value, q2.value);
            if (dt < 0.0f)
            {
                q2.value = -q2.value;
            }
            return normalize(new quaternionD(math.lerp(q1.value, q2.value, t)));
        }
        /// <summary>Returns a normalized version of a quaternion q by scaling it by 1 / length(q).</summary>
        /// <param name="q">The quaternion to normalize.</param>
        /// <returns>The normalized quaternion.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternionD normalize(quaternionD q)
        {
            double4 x = q.value;
            return new quaternionD(math.rsqrt(math.dot(x, x)) * x);
        }
        public static void CheckOrthogonality(ref quaternionD quat)
        {
            var temp1 = math.length(quat.value);
            if (temp1 < 0.9999999999998 || temp1 > 1.0000000000001)
                RestoreQuaternionOrthogonality(ref quat, temp1);
        }

        public static void RestoreQuaternionOrthogonality(ref quaternionD quat, double length)
        {
            var value = quat.value;
            value.x = value.x / length;
            value.y = value.y / length;
            value.z = value.z / length;
            value.w = value.w / length;
            quat.value = value;
            Debug.Log("Retore orthogonality");
        }
        public static bool isValid(quaternionD rotation)
        {
            var val = rotation.value;
            return !double.IsNaN(val.x) && !double.IsNaN(val.y) && !double.IsNaN(val.z) && !double.IsNaN(val.w);
        }

    }
}
