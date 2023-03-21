//using System;
//using System.Runtime.CompilerServices;
//using Unity.IL2CPP.CompilerServices;
//using Unity.Mathematics;

//namespace Chart3D.MathExtensions
//{
//    /// <summary>
//    /// A quaternionD type for representing rotations.
//    /// </summary>
//    [Serializable]
//    public partial struct quaternionD : System.IEquatable<quaternionD>, IFormattable
//    {
//        /// <summary>
//        /// The quaternionD component values.
//        /// </summary>
//        public float4 value;

//        //        /// <summary>A quaternionD representing the identity transform.</summary>
//        //        public static readonly quaternionD identity = new quaternionD(0.0f, 0.0f, 0.0f, 1.0f);

//        //        /// <summary>Constructs a quaternionD from four float values.</summary>
//        //        /// <param name="x">The quaternionD x component.</param>
//        //        /// <param name="y">The quaternionD y component.</param>
//        //        /// <param name="z">The quaternionD z component.</param>
//        //        /// <param name="w">The quaternionD w component.</param>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public quaternionD(float x, float y, float z, float w) { value.x = x; value.y = y; value.z = z; value.w = w; }

//        //        /// <summary>Constructs a quaternionD from float4 vector.</summary>
//        //        /// <param name="value">The quaternionD xyzw component values.</param>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public quaternionD(float4 value) { this.value = value; }

//        //        /// <summary>Implicitly converts a float4 vector to a quaternionD.</summary>
//        //        /// <param name="v">The quaternionD xyzw component values.</param>
//        //        /// <returns>The quaternionD constructed from a float4 vector.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static implicit operator quaternionD(float4 v) { return new quaternionD(v); }

//        //        /// <summary>Constructs a unit quaternionD from a float3x3 rotation matrix. The matrix must be orthonormal.</summary>
//        //        /// <param name="m">The float3x3 orthonormal rotation matrix.</param>
//        //        public quaternionD(float3x3 m)
//        //        {
//        //            float3 u = m.c0;
//        //            float3 v = m.c1;
//        //            float3 w = m.c2;

//        //            uint u_sign = (asuint(u.x) & 0x80000000);
//        //            float t = v.y + asfloat(asuint(w.z) ^ u_sign);
//        //            uint4 u_mask = uint4((int)u_sign >> 31);
//        //            uint4 t_mask = uint4(asint(t) >> 31);

//        //            float tr = 1.0f + abs(u.x);

//        //            uint4 sign_flips = uint4(0x00000000, 0x80000000, 0x80000000, 0x80000000) ^ (u_mask & uint4(0x00000000, 0x80000000, 0x00000000, 0x80000000)) ^ (t_mask & uint4(0x80000000, 0x80000000, 0x80000000, 0x00000000));

//        //            value = float4(tr, u.y, w.x, v.z) + asfloat(asuint(float4(t, v.x, u.z, w.y)) ^ sign_flips);   // +---, +++-, ++-+, +-++

//        //            value = asfloat((asuint(value) & ~u_mask) | (asuint(value.zwxy) & u_mask));
//        //            value = asfloat((asuint(value.wzyx) & ~t_mask) | (asuint(value) & t_mask));
//        //            value = normalize(value);
//        //        }

//        //        /// <summary>Constructs a unit quaternionD from an orthonormal float4x4 matrix.</summary>
//        //        /// <param name="m">The float4x4 orthonormal rotation matrix.</param>
//        //        public quaternionD(float4x4 m)
//        //        {
//        //            float4 u = m.c0;
//        //            float4 v = m.c1;
//        //            float4 w = m.c2;

//        //            uint u_sign = (asuint(u.x) & 0x80000000);
//        //            float t = v.y + asfloat(asuint(w.z) ^ u_sign);
//        //            uint4 u_mask = uint4((int)u_sign >> 31);
//        //            uint4 t_mask = uint4(asint(t) >> 31);

//        //            float tr = 1.0f + abs(u.x);

//        //            uint4 sign_flips = uint4(0x00000000, 0x80000000, 0x80000000, 0x80000000) ^ (u_mask & uint4(0x00000000, 0x80000000, 0x00000000, 0x80000000)) ^ (t_mask & uint4(0x80000000, 0x80000000, 0x80000000, 0x00000000));

//        //            value = float4(tr, u.y, w.x, v.z) + asfloat(asuint(float4(t, v.x, u.z, w.y)) ^ sign_flips);   // +---, +++-, ++-+, +-++

//        //            value = asfloat((asuint(value) & ~u_mask) | (asuint(value.zwxy) & u_mask));
//        //            value = asfloat((asuint(value.wzyx) & ~t_mask) | (asuint(value) & t_mask));

//        //            value = normalize(value);
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD representing a rotation around a unit axis by an angle in radians.
//        //        /// The rotation direction is clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="axis">The axis of rotation.</param>
//        //        /// <param name="angle">The angle of rotation in radians.</param>
//        //        /// <returns>The quaternionD representing a rotation around an axis.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD AxisAngle(float3 axis, float angle)
//        //        {
//        //            float sina, cosa;
//        //            math.sincos(0.5f * angle, out sina, out cosa);
//        //            return quaternionD(float4(axis * sina, cosa));
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the x-axis, then the y-axis and finally the z-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in x-y-z order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerXYZ(float3 xyz)
//        //        {
//        //            // return mul(rotateZ(xyz.z), mul(rotateY(xyz.y), rotateX(xyz.x)));
//        //            float3 s, c;
//        //            sincos(0.5f * xyz, out s, out c);
//        //            return quaternionD(
//        //                // s.x * c.y * c.z - s.y * s.z * c.x,
//        //                // s.y * c.x * c.z + s.x * s.z * c.y,
//        //                // s.z * c.x * c.y - s.x * s.y * c.z,
//        //                // c.x * c.y * c.z + s.y * s.z * s.x
//        //                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(-1.0f, 1.0f, -1.0f, 1.0f)
//        //                );
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the x-axis, then the z-axis and finally the y-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in x-z-y order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerXZY(float3 xyz)
//        //        {
//        //            // return mul(rotateY(xyz.y), mul(rotateZ(xyz.z), rotateX(xyz.x)));
//        //            float3 s, c;
//        //            sincos(0.5f * xyz, out s, out c);
//        //            return quaternionD(
//        //                // s.x * c.y * c.z + s.y * s.z * c.x,
//        //                // s.y * c.x * c.z + s.x * s.z * c.y,
//        //                // s.z * c.x * c.y - s.x * s.y * c.z,
//        //                // c.x * c.y * c.z - s.y * s.z * s.x
//        //                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(1.0f, 1.0f, -1.0f, -1.0f)
//        //                );
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the y-axis, then the x-axis and finally the z-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in y-x-z order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerYXZ(float3 xyz)
//        //        {
//        //            // return mul(rotateZ(xyz.z), mul(rotateX(xyz.x), rotateY(xyz.y)));
//        //            float3 s, c;
//        //            sincos(0.5f * xyz, out s, out c);
//        //            return quaternionD(
//        //                // s.x * c.y * c.z - s.y * s.z * c.x,
//        //                // s.y * c.x * c.z + s.x * s.z * c.y,
//        //                // s.z * c.x * c.y + s.x * s.y * c.z,
//        //                // c.x * c.y * c.z - s.y * s.z * s.x
//        //                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(-1.0f, 1.0f, 1.0f, -1.0f)
//        //                );
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the y-axis, then the z-axis and finally the x-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in y-z-x order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerYZX(float3 xyz)
//        //        {
//        //            // return mul(rotateX(xyz.x), mul(rotateZ(xyz.z), rotateY(xyz.y)));
//        //            float3 s, c;
//        //            sincos(0.5f * xyz, out s, out c);
//        //            return quaternionD(
//        //                // s.x * c.y * c.z - s.y * s.z * c.x,
//        //                // s.y * c.x * c.z - s.x * s.z * c.y,
//        //                // s.z * c.x * c.y + s.x * s.y * c.z,
//        //                // c.x * c.y * c.z + s.y * s.z * s.x
//        //                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(-1.0f, -1.0f, 1.0f, 1.0f)
//        //                );
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the z-axis, then the x-axis and finally the y-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// This is the default order rotation order in Unity.
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in z-x-y order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerZXY(float3 xyz)
//        //        {
//        //            // return mul(rotateY(xyz.y), mul(rotateX(xyz.x), rotateZ(xyz.z)));
//        //            float3 s, c;
//        //            sincos(0.5f * xyz, out s, out c);
//        //            return quaternionD(
//        //                // s.x * c.y * c.z + s.y * s.z * c.x,
//        //                // s.y * c.x * c.z - s.x * s.z * c.y,
//        //                // s.z * c.x * c.y - s.x * s.y * c.z,
//        //                // c.x * c.y * c.z + s.y * s.z * s.x
//        //                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(1.0f, -1.0f, -1.0f, 1.0f)
//        //                );
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the z-axis, then the y-axis and finally the x-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in z-y-x order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerZYX(float3 xyz)
//        //        {
//        //            // return mul(rotateX(xyz.x), mul(rotateY(xyz.y), rotateZ(xyz.z)));
//        //            float3 s, c;
//        //            sincos(0.5f * xyz, out s, out c);
//        //            return quaternionD(
//        //                // s.x * c.y * c.z + s.y * s.z * c.x,
//        //                // s.y * c.x * c.z - s.x * s.z * c.y,
//        //                // s.z * c.x * c.y + s.x * s.y * c.z,
//        //                // c.x * c.y * c.z - s.y * s.x * s.z
//        //                float4(s.xyz, c.x) * c.yxxy * c.zzyz + s.yxxy * s.zzyz * float4(c.xyz, s.x) * float4(1.0f, -1.0f, 1.0f, -1.0f)
//        //                );
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the x-axis, then the y-axis and finally the z-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in x-y-z order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerXYZ(float x, float y, float z) { return EulerXYZ(float3(x, y, z)); }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the x-axis, then the z-axis and finally the y-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in x-z-y order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerXZY(float x, float y, float z) { return EulerXZY(float3(x, y, z)); }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the y-axis, then the x-axis and finally the z-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in y-x-z order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerYXZ(float x, float y, float z) { return EulerYXZ(float3(x, y, z)); }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the y-axis, then the z-axis and finally the x-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in y-z-x order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerYZX(float x, float y, float z) { return EulerYZX(float3(x, y, z)); }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the z-axis, then the x-axis and finally the y-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// This is the default order rotation order in Unity.
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in z-x-y order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerZXY(float x, float y, float z) { return EulerZXY(float3(x, y, z)); }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing a rotation around the z-axis, then the y-axis and finally the x-axis.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in z-y-x order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD EulerZYX(float x, float y, float z) { return EulerZYX(float3(x, y, z)); }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing 3 rotations around the principal axes in a given order.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// When the rotation order is known at compile time, it is recommended for performance reasons to use specific
//        //        /// Euler rotation constructors such as EulerZXY(...).
//        //        /// </summary>
//        //        /// <param name="xyz">A float3 vector containing the rotation angles around the x-, y- and z-axis measures in radians.</param>
//        //        /// <param name="order">The order in which the rotations are applied.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in the specified order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD Euler(float3 xyz, RotationOrder order = RotationOrder.ZXY)
//        //        {
//        //            switch (order)
//        //            {
//        //                case RotationOrder.XYZ:
//        //                    return EulerXYZ(xyz);
//        //                case RotationOrder.XZY:
//        //                    return EulerXZY(xyz);
//        //                case RotationOrder.YXZ:
//        //                    return EulerYXZ(xyz);
//        //                case RotationOrder.YZX:
//        //                    return EulerYZX(xyz);
//        //                case RotationOrder.ZXY:
//        //                    return EulerZXY(xyz);
//        //                case RotationOrder.ZYX:
//        //                    return EulerZYX(xyz);
//        //                default:
//        //                    return quaternionD.identity;
//        //            }
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD constructed by first performing 3 rotations around the principal axes in a given order.
//        //        /// All rotation angles are in radians and clockwise when looking along the rotation axis towards the origin.
//        //        /// When the rotation order is known at compile time, it is recommended for performance reasons to use specific
//        //        /// Euler rotation constructors such as EulerZXY(...).
//        //        /// </summary>
//        //        /// <param name="x">The rotation angle around the x-axis in radians.</param>
//        //        /// <param name="y">The rotation angle around the y-axis in radians.</param>
//        //        /// <param name="z">The rotation angle around the z-axis in radians.</param>
//        //        /// <param name="order">The order in which the rotations are applied.</param>
//        //        /// <returns>The quaternionD representing the Euler angle rotation in the specified order.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD Euler(float x, float y, float z, RotationOrder order = RotationOrder.Default)
//        //        {
//        //            return Euler(float3(x, y, z), order);
//        //        }

//        //        /// <summary>Returns a quaternionD that rotates around the x-axis by a given number of radians.</summary>
//        //        /// <param name="angle">The clockwise rotation angle when looking along the x-axis towards the origin in radians.</param>
//        //        /// <returns>The quaternionD representing a rotation around the x-axis.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD RotateX(float angle)
//        //        {
//        //            float sina, cosa;
//        //            math.sincos(0.5f * angle, out sina, out cosa);
//        //            return quaternionD(sina, 0.0f, 0.0f, cosa);
//        //        }

//        //        /// <summary>Returns a quaternionD that rotates around the y-axis by a given number of radians.</summary>
//        //        /// <param name="angle">The clockwise rotation angle when looking along the y-axis towards the origin in radians.</param>
//        //        /// <returns>The quaternionD representing a rotation around the y-axis.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD RotateY(float angle)
//        //        {
//        //            float sina, cosa;
//        //            math.sincos(0.5f * angle, out sina, out cosa);
//        //            return quaternionD(0.0f, sina, 0.0f, cosa);
//        //        }

//        //        /// <summary>Returns a quaternionD that rotates around the z-axis by a given number of radians.</summary>
//        //        /// <param name="angle">The clockwise rotation angle when looking along the z-axis towards the origin in radians.</param>
//        //        /// <returns>The quaternionD representing a rotation around the z-axis.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD RotateZ(float angle)
//        //        {
//        //            float sina, cosa;
//        //            math.sincos(0.5f * angle, out sina, out cosa);
//        //            return quaternionD(0.0f, 0.0f, sina, cosa);
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD view rotation given a unit length forward vector and a unit length up vector.
//        //        /// The two input vectors are assumed to be unit length and not collinear.
//        //        /// If these assumptions are not met use float3x3.LookRotationSafe instead.
//        //        /// </summary>
//        //        /// <param name="forward">The view forward direction.</param>
//        //        /// <param name="up">The view up direction.</param>
//        //        /// <returns>The quaternionD view rotation.</returns>
//        //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        //        public static quaternionD LookRotation(float3 forward, float3 up)
//        //        {
//        //            float3 t = normalize(cross(up, forward));
//        //            return quaternionD(float3x3(t, cross(forward, t), forward));
//        //        }

//        //        /// <summary>
//        //        /// Returns a quaternionD view rotation given a forward vector and an up vector.
//        //        /// The two input vectors are not assumed to be unit length.
//        //        /// If the magnitude of either of the vectors is so extreme that the calculation cannot be carried out reliably or the vectors are collinear,
//        //        /// the identity will be returned instead.
//        //        /// </summary>
//        //        /// <param name="forward">The view forward direction.</param>
//        //        /// <param name="up">The view up direction.</param>
//        //        /// <returns>The quaternionD view rotation or the identity quaternionD.</returns>
//        //        public static quaternionD LookRotationSafe(float3 forward, float3 up)
//        //        {
//        //            float forwardLengthSq = dot(forward, forward);
//        //            float upLengthSq = dot(up, up);

//        //            forward *= rsqrt(forwardLengthSq);
//        //            up *= rsqrt(upLengthSq);

//        //            float3 t = cross(up, forward);
//        //            float tLengthSq = dot(t, t);
//        //            t *= rsqrt(tLengthSq);

//        //            float mn = min(min(forwardLengthSq, upLengthSq), tLengthSq);
//        //            float mx = max(max(forwardLengthSq, upLengthSq), tLengthSq);

//        //            bool accept = mn > 1e-35f && mx < 1e35f && isfinite(forwardLengthSq) && isfinite(upLengthSq) && isfinite(tLengthSq);
//        //            return quaternionD(select(float4(0.0f, 0.0f, 0.0f, 1.0f), quaternionD(float3x3(t, cross(forward, t), forward)).value, accept));
//        //        }

//        /// <summary>Returns true if the quaternionD is equal to a given quaternionD, false otherwise.</summary>
//        /// <param name="x">The quaternionD to compare with.</param>
//        /// <returns>True if the quaternionD is equal to the input, false otherwise.</returns>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public bool Equals(quaternionD x) { return value.x == x.value.x && value.y == x.value.y && value.z == x.value.z && value.w == x.value.w; }

//        /// <summary>Returns whether true if the quaternionD is equal to a given quaternionD, false otherwise.</summary>
//        /// <param name="x">The object to compare with.</param>
//        /// <returns>True if the quaternionD is equal to the input, false otherwise.</returns>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public override bool Equals(object x) { return x is quaternionD converted && Equals(converted); }

//        /// <summary>Returns a hash code for the quaternionD.</summary>
//        /// <returns>The hash code of the quaternionD.</returns>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public override int GetHashCode() { return (int)math.hash(this); }

//        /// <summary>Returns a string representation of the quaternionD.</summary>
//        /// <returns>The string representation of the quaternionD.</returns>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public override string ToString()
//        {
//            return string.Format("quaternionD({0}f, {1}f, {2}f, {3}f)", value.x, value.y, value.z, value.w);
//        }

//        /// <summary>Returns a string representation of the quaternionD using a specified format and culture-specific format information.</summary>
//        /// <param name="format">The format string.</param>
//        /// <param name="formatProvider">The format provider to use during string formatting.</param>
//        /// <returns>The formatted string representation of the quaternionD.</returns>
//        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//        public string ToString(string format, IFormatProvider formatProvider)
//        {
//            return string.Format("quaternionD({0}f, {1}f, {2}f, {3}f)", value.x.ToString(format, formatProvider), value.y.ToString(format, formatProvider), value.z.ToString(format, formatProvider), value.w.ToString(format, formatProvider));
//        }
//    }

//    //    public static partial class math
//    //    {
//    //        /// <summary>Returns a quaternionD constructed from four float values.</summary>
//    //        /// <param name="x">The x component of the quaternionD.</param>
//    //        /// <param name="y">The y component of the quaternionD.</param>
//    //        /// <param name="z">The z component of the quaternionD.</param>
//    //        /// <param name="w">The w component of the quaternionD.</param>
//    //        /// <returns>The quaternionD constructed from individual components.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD quaternionD(float x, float y, float z, float w) { return new quaternionD(x, y, z, w); }

//    //        /// <summary>Returns a quaternionD constructed from a float4 vector.</summary>
//    //        /// <param name="value">The float4 containing the components of the quaternionD.</param>
//    //        /// <returns>The quaternionD constructed from a float4.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD quaternionD(float4 value) { return new quaternionD(value); }

//    //        /// <summary>Returns a unit quaternionD constructed from a float3x3 rotation matrix. The matrix must be orthonormal.</summary>
//    //        /// <param name="m">The float3x3 rotation matrix.</param>
//    //        /// <returns>The quaternionD constructed from a float3x3 matrix.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD quaternionD(float3x3 m) { return new quaternionD(m); }

//    //        /// <summary>Returns a unit quaternionD constructed from a float4x4 matrix. The matrix must be orthonormal.</summary>
//    //        /// <param name="m">The float4x4 matrix (must be orthonormal).</param>
//    //        /// <returns>The quaternionD constructed from a float4x4 matrix.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD quaternionD(float4x4 m) { return new quaternionD(m); }

//    //        /// <summary>Returns the conjugate of a quaternionD value.</summary>
//    //        /// <param name="q">The quaternionD to conjugate.</param>
//    //        /// <returns>The conjugate of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD conjugate(quaternionD q)
//    //        {
//    //            return quaternionD(q.value * float4(-1.0f, -1.0f, -1.0f, 1.0f));
//    //        }

//    //        /// <summary>Returns the inverse of a quaternionD value.</summary>
//    //        /// <param name="q">The quaternionD to invert.</param>
//    //        /// <returns>The quaternionD inverse of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD inverse(quaternionD q)
//    //        {
//    //            float4 x = q.value;
//    //            return quaternionD(rcp(dot(x, x)) * x * float4(-1.0f, -1.0f, -1.0f, 1.0f));
//    //        }

//    //        /// <summary>Returns the dot product of two quaternionDs.</summary>
//    //        /// <param name="a">The first quaternionD.</param>
//    //        /// <param name="b">The second quaternionD.</param>
//    //        /// <returns>The dot product of two quaternionDs.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static float dot(quaternionD a, quaternionD b)
//    //        {
//    //            return dot(a.value, b.value);
//    //        }

//    //        /// <summary>Returns the length of a quaternionD.</summary>
//    //        /// <param name="q">The input quaternionD.</param>
//    //        /// <returns>The length of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static float length(quaternionD q)
//    //        {
//    //            return sqrt(dot(q.value, q.value));
//    //        }

//    //        /// <summary>Returns the squared length of a quaternionD.</summary>
//    //        /// <param name="q">The input quaternionD.</param>
//    //        /// <returns>The length squared of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static float lengthsq(quaternionD q)
//    //        {
//    //            return dot(q.value, q.value);
//    //        }

//    //        /// <summary>Returns a normalized version of a quaternionD q by scaling it by 1 / length(q).</summary>
//    //        /// <param name="q">The quaternionD to normalize.</param>
//    //        /// <returns>The normalized quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD normalize(quaternionD q)
//    //        {
//    //            float4 x = q.value;
//    //            return quaternionD(rsqrt(dot(x, x)) * x);
//    //        }

//    //        /// <summary>
//    //        /// Returns a safe normalized version of the q by scaling it by 1 / length(q).
//    //        /// Returns the identity when 1 / length(q) does not produce a finite number.
//    //        /// </summary>
//    //        /// <param name="q">The quaternionD to normalize.</param>
//    //        /// <returns>The normalized quaternionD or the identity quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD normalizesafe(quaternionD q)
//    //        {
//    //            float4 x = q.value;
//    //            float len = math.dot(x, x);
//    //            return quaternionD(math.select(Mathematics.quaternionD.identity.value, x * math.rsqrt(len), len > FLT_MIN_NORMAL));
//    //        }

//    //        /// <summary>
//    //        /// Returns a safe normalized version of the q by scaling it by 1 / length(q).
//    //        /// Returns the given default value when 1 / length(q) does not produce a finite number.
//    //        /// </summary>
//    //        /// <param name="q">The quaternionD to normalize.</param>
//    //        /// <param name="defaultvalue">The default value.</param>
//    //        /// <returns>The normalized quaternionD or the default value.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD normalizesafe(quaternionD q, quaternionD defaultvalue)
//    //        {
//    //            float4 x = q.value;
//    //            float len = math.dot(x, x);
//    //            return quaternionD(math.select(defaultvalue.value, x * math.rsqrt(len), len > FLT_MIN_NORMAL));
//    //        }

//    //        /// <summary>Returns the natural exponent of a quaternionD. Assumes w is zero.</summary>
//    //        /// <param name="q">The quaternionD with w component equal to zero.</param>
//    //        /// <returns>The natural exponent of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD unitexp(quaternionD q)
//    //        {
//    //            float v_rcp_len = rsqrt(dot(q.value.xyz, q.value.xyz));
//    //            float v_len = rcp(v_rcp_len);
//    //            float sin_v_len, cos_v_len;
//    //            sincos(v_len, out sin_v_len, out cos_v_len);
//    //            return quaternionD(float4(q.value.xyz * v_rcp_len * sin_v_len, cos_v_len));
//    //        }

//    //        /// <summary>Returns the natural exponent of a quaternionD.</summary>
//    //        /// <param name="q">The quaternionD.</param>
//    //        /// <returns>The natural exponent of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD exp(quaternionD q)
//    //        {
//    //            float v_rcp_len = rsqrt(dot(q.value.xyz, q.value.xyz));
//    //            float v_len = rcp(v_rcp_len);
//    //            float sin_v_len, cos_v_len;
//    //            sincos(v_len, out sin_v_len, out cos_v_len);
//    //            return quaternionD(float4(q.value.xyz * v_rcp_len * sin_v_len, cos_v_len) * exp(q.value.w));
//    //        }

//    //        /// <summary>Returns the natural logarithm of a unit length quaternionD.</summary>
//    //        /// <param name="q">The unit length quaternionD.</param>
//    //        /// <returns>The natural logarithm of the unit length quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD unitlog(quaternionD q)
//    //        {
//    //            float w = clamp(q.value.w, -1.0f, 1.0f);
//    //            float s = acos(w) * rsqrt(1.0f - w * w);
//    //            return quaternionD(float4(q.value.xyz * s, 0.0f));
//    //        }

//    //        /// <summary>Returns the natural logarithm of a quaternionD.</summary>
//    //        /// <param name="q">The quaternionD.</param>
//    //        /// <returns>The natural logarithm of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD log(quaternionD q)
//    //        {
//    //            float v_len_sq = dot(q.value.xyz, q.value.xyz);
//    //            float q_len_sq = v_len_sq + q.value.w * q.value.w;

//    //            float s = acos(clamp(q.value.w * rsqrt(q_len_sq), -1.0f, 1.0f)) * rsqrt(v_len_sq);
//    //            return quaternionD(float4(q.value.xyz * s, 0.5f * log(q_len_sq)));
//    //        }

//    //        /// <summary>Returns the result of transforming the quaternionD b by the quaternionD a.</summary>
//    //        /// <param name="a">The quaternionD on the left.</param>
//    //        /// <param name="b">The quaternionD on the right.</param>
//    //        /// <returns>The result of transforming quaternionD b by the quaternionD a.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD mul(quaternionD a, quaternionD b)
//    //        {
//    //            return quaternionD(a.value.wwww * b.value + (a.value.xyzx * b.value.wwwx + a.value.yzxy * b.value.zxyy) * float4(1.0f, 1.0f, 1.0f, -1.0f) - a.value.zxyz * b.value.yzxz);
//    //        }

//    //        /// <summary>Returns the result of transforming a vector by a quaternionD.</summary>
//    //        /// <param name="q">The quaternionD transformation.</param>
//    //        /// <param name="v">The vector to transform.</param>
//    //        /// <returns>The transformation of vector v by quaternionD q.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static float3 mul(quaternionD q, float3 v)
//    //        {
//    //            float3 t = 2 * cross(q.value.xyz, v);
//    //            return v + q.value.w * t + cross(q.value.xyz, t);
//    //        }

//    //        /// <summary>Returns the result of rotating a vector by a unit quaternionD.</summary>
//    //        /// <param name="q">The quaternionD rotation.</param>
//    //        /// <param name="v">The vector to rotate.</param>
//    //        /// <returns>The rotation of vector v by quaternionD q.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static float3 rotate(quaternionD q, float3 v)
//    //        {
//    //            float3 t = 2 * cross(q.value.xyz, v);
//    //            return v + q.value.w * t + cross(q.value.xyz, t);
//    //        }

//    //        /// <summary>Returns the result of a normalized linear interpolation between two quaternionDs q1 and a2 using an interpolation parameter t.</summary>
//    //        /// <remarks>
//    //        /// Prefer to use this over slerp() when you know the distance between q1 and q2 is small. This can be much
//    //        /// higher performance due to avoiding trigonometric function evaluations that occur in slerp().
//    //        /// </remarks>
//    //        /// <param name="q1">The first quaternionD.</param>
//    //        /// <param name="q2">The second quaternionD.</param>
//    //        /// <param name="t">The interpolation parameter.</param>
//    //        /// <returns>The normalized linear interpolation of two quaternionDs.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD nlerp(quaternionD q1, quaternionD q2, float t)
//    //        {
//    //            float dt = dot(q1, q2);
//    //            if (dt < 0.0f)
//    //            {
//    //                q2.value = -q2.value;
//    //            }

//    //            return normalize(quaternionD(lerp(q1.value, q2.value, t)));
//    //        }

//    //        /// <summary>Returns the result of a spherical interpolation between two quaternionDs q1 and a2 using an interpolation parameter t.</summary>
//    //        /// <param name="q1">The first quaternionD.</param>
//    //        /// <param name="q2">The second quaternionD.</param>
//    //        /// <param name="t">The interpolation parameter.</param>
//    //        /// <returns>The spherical linear interpolation of two quaternionDs.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static quaternionD slerp(quaternionD q1, quaternionD q2, float t)
//    //        {
//    //            float dt = dot(q1, q2);
//    //            if (dt < 0.0f)
//    //            {
//    //                dt = -dt;
//    //                q2.value = -q2.value;
//    //            }

//    //            if (dt < 0.9995f)
//    //            {
//    //                float angle = acos(dt);
//    //                float s = rsqrt(1.0f - dt * dt);    // 1.0f / sin(angle)
//    //                float w1 = math.sin(angle * (1.0f - t)) * s;
//    //                float w2 = sin(angle * t) * s;
//    //                return quaternionD(q1.value * w1 + q2.value * w2);
//    //            }
//    //            else
//    //            {
//    //                // if the angle is small, use linear interpolation
//    //                return nlerp(q1, q2, t);
//    //            }
//    //        }

//    //        /// <summary>Returns a uint hash code of a quaternionD.</summary>
//    //        /// <param name="q">The quaternionD to hash.</param>
//    //        /// <returns>The hash code for the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static uint hash(quaternionD q)
//    //        {
//    //            return hash(q.value);
//    //        }

//    //        /// <summary>
//    //        /// Returns a uint4 vector hash code of a quaternionD.
//    //        /// When multiple elements are to be hashes together, it can more efficient to calculate and combine wide hash
//    //        /// that are only reduced to a narrow uint hash at the very end instead of at every step.
//    //        /// </summary>
//    //        /// <param name="q">The quaternionD to hash.</param>
//    //        /// <returns>The uint4 vector hash code of the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static uint4 hashwide(quaternionD q)
//    //        {
//    //            return hashwide(q.value);
//    //        }


//    //        /// <summary>
//    //        /// Transforms the forward vector by a quaternionD.
//    //        /// </summary>
//    //        /// <param name="q">The quaternionD transformation.</param>
//    //        /// <returns>The forward vector transformed by the input quaternionD.</returns>
//    //        [MethodImpl(MethodImplOptions.AggressiveInlining)]
//    //        public static float3 forward(quaternionD q) { return mul(q, float3(0, 0, 1)); }  // for compatibility
////}
//}
