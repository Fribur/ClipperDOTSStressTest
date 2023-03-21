using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;


namespace Chart3D.MathExtensions
{
    public static class MathHelper
    {
        const double absTol = math.EPSILON_DBL;
        const double relTol = math.EPSILON_DBL;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(double a, double b)
        {
            return (math.abs(a - b) <= math.max(absTol, relTol * math.max(math.abs(a), math.abs(b))));
        }
        /// <summary>
        /// https://realtimecollisiondetection.net/blog/?p=89
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(double2 a, double2 b)
        {
            return math.all((math.abs(a - b) <= math.max(absTol, relTol * math.max(math.abs(a), math.abs(b)))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals(double4 a, double4 b)
        {
            return math.all((math.abs(a - b) <= math.max(absTol, relTol * math.max(math.abs(a), math.abs(b)))));
        }
        public const double threePI = 3d * math.PI_DBL;
        public const double twoPI = 2d * math.PI_DBL;
        public const double halfPI = 0.5d * math.PI_DBL;
        public readonly static quaternion rotX90deg = quaternion.RotateX(math.radians(-90));
        public static int2x2 GetAABBfromTriangle(int2x3 coordiantes)
        {
            var min = math.min(math.min(coordiantes.c0, coordiantes.c1), coordiantes.c2);
            var max = math.max(math.max(coordiantes.c0, coordiantes.c1), coordiantes.c2);
            return new int2x2(min, max);
        }
        public static int2x2 IncludeInAABB(int2x2 aabb, int2 point)
        {
            int2x2 result;
            result.c0 = math.min(aabb.c0, point);
            result.c1 = math.max(aabb.c1, point);
            return result;
        }
        public static double2x2 IncludeInAABB(double2x2 aabb, double2 point)
        {
            double2x2 result;
            result.c0 = math.min(aabb.c0, point);
            result.c1 = math.max(aabb.c1, point);
            return result;
        }
        public static float3x2 IncludeInAABB(float3x2 aabb, float3 point)
        {
            float3x2 result;
            result.c0 = math.min(aabb.c0, point);
            result.c1 = math.max(aabb.c1, point);
            return result;
        }
        public static int2x2 AABBunion(int2x2 a, int2x2 b)
        {
            int2x2 result;
            result.c0 = math.min(a.c0, b.c0);
            result.c1 = math.max(a.c1, b.c1);
            return result;
        }
        public static int widestAABBDimension(int2x2 aabb)
        {
            var width = aabb.c1 - aabb.c0;
            return width.y > width.x ? 1 : 0;
        }
        public static int2 GetAABBCentroid(int2x2 aabb)
        {
            return (int2)(((double2)aabb.c0 + (double2)aabb.c1) * 0.5); //casting to double2 prevents overflow. 
        }
        public static float GetPointOffsetInAABB(int2x2 aabb, int2 point, int axis)
        {
            float2 offset = point - aabb.c0;
            if (axis == 0)
            {
                if (aabb.c1.x > aabb.c0.x)
                    offset.x /= aabb.c1.x - aabb.c0.x;
                return offset.x;
            }
            else
            {
                if (aabb.c1.y > aabb.c0.y)
                    offset.y /= aabb.c1.y - aabb.c0.y;
                return offset.y;
            }
        }
        public static double2 GetTriangleCentroid(double2x3 triangle)
        {
            return (triangle.c0 + triangle.c1 + triangle.c2) / 3;
        }
        public static float3x2 emptyAABBf3()
        {
            return new float3x2 { c0 = float.MaxValue, c1 = float.MinValue };
        }
        public static double2x2 emptyAABBd2()
        {
            return new double2x2 { c0 = double.MaxValue, c1 = double.MinValue };
        }
        public static int2x2 emptyAABBi2()
        {
            return new int2x2 { c0 = int.MaxValue, c1 = int.MinValue };
        }
        public static float SurfaceArea(int2x2 aabb)
        {
            var diff = aabb.c1 - aabb.c0;
            return diff.x * diff.y;
        }
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in NativeList<double2> data, int start, int end)
        {
            double area = default;
            for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
                area += (data[prev].x - data[i].x) * (data[i].y + data[prev].y);
            return area * 0.5;
        }
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in NativeList<int2> data, int start, int end)
        {
            double area = default;
            for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
                area += ((double)data[prev].x - (double)data[i].x) * ((double)data[i].y + (double)data[prev].y);
            return area * 0.5;
        }
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in NativeArray<int2> data, int start, int end)
        {
            double area = default;
            for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
                area += ((double)data[prev].x - (double)data[i].x) * ((double)data[i].y + (double)data[prev].y);
            return area * 0.5;
        }


        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in NativeSlice<int2> data)
        {
            int start = 0;
            int length = data.Length;
            double area = default;
            for (int i = start, prev = length - 1; i < length; prev = i++) //from (0, prev) until (end, prev)
                area += (double)(data[prev].x - data[i].x) * (double)(data[i].y + data[prev].y);
            return area * 0.5;
        }

        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(ref System.Collections.ObjectModel.Collection<float2> data, int start, int length)
        {
            double area = default;
            for (int i = start, prev = length - 1; i < length; prev = i++) //from (0, prev) until (end, prev)
                area += (double)(data[prev].x - data[i].x) * (double)(data[i].y + data[prev].y);
            return area * 0.5;
        }
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in Polygon poly)
        {
            double area = 0;
            for (int k = 0, length = poly.startIDs.Length - 1; k < length; k++)
            {
                int start = poly.startIDs[k];
                int end = poly.startIDs[k + 1];
                var Nodes = poly.nodes;
                area += SignedArea(in Nodes, start, end);
                //Debug.Log($"Area: {area}");
            }
            return area;
        }
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in PolygonInt poly)
        {
            double area = 0;
            for (int k = 0, length = poly.startIDs.Length - 1; k < length; k++)
            {
                int start = poly.startIDs[k];
                int end = poly.startIDs[k + 1];
                var Nodes = poly.nodes;
                area += SignedArea(in Nodes, start, end);
                //Debug.Log($"Area: {area}");
            }
            return area;
        }
        /// <summary>
        /// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
        /// </summary>
        public static double SignedArea(in NativeArray<int2> nodes, NativeArray<int>startIDs)
        {
            double area = 0;
            for (int k = 0, length = startIDs.Length - 1; k < length; k++)
            {
                int start = startIDs[k];
                int end = startIDs[k + 1];
                area += SignedArea(in nodes, start, end);
                //Debug.Log($"Area: {area}");
            }
            return area;
        }
        public static PolyOrientation GetPolyOrientation(double signedArea)
        {
            if (signedArea < 0)
                return PolyOrientation.CW;
            else if (signedArea > 0)
                return PolyOrientation.CCW;
            else
                return PolyOrientation.None;
        }
        public static double LerpAngle(double a, double b, double t)
        {
            double num = WrapAroundLimit(b - a, MathHelper.twoPI);
            if (num > math.PI_DBL)
                num -= MathHelper.twoPI;

            return a + num * math.clamp(t, 0, 1);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Wrap2PI(double angle_rad)
        {
            return WrapAroundLimit(angle_rad, MathHelper.twoPI);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double WrapAroundHalfLimit(double val, double lim)
        {
            var halfLim = lim * 0.5;
            while (val <= -halfLim) val += lim;   // inefficient, but clear
            while (val > halfLim) val -= lim;     // inefficient, but clear
            return val;
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public static double WrapAroundLimit(double val, double lim)
        //{
        //    while (val <= -lim) val += lim;   // inefficient, but clear
        //    while (val > lim) val -= lim;     // inefficient, but clear
        //    return val;
        //}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double WrapAroundLimit(double val, double lim)
        {
            return math.clamp(val - math.floor(val / lim) * lim, 0f, lim);
        }
        //static double Area(List<PointD> path)
        //{
        //    double a = 0.0;
        //    int cnt = path.Count, j = cnt - 1;
        //    if (cnt < 3) return 0.0;
        //    for (int i = 0; i < cnt; i++)
        //    {
        //        double d = (path[j].x + path[i].x);
        //        a += d * (path[j].y - path[i].y);
        //        j = i;
        //    }
        //    return -a * 0.5;
        //}
        public static int2x2 GetAABBfromNodes(in NativeList<int2> nodes)
        {
            int2x2 boundingBox = emptyAABBi2();
            for (int i = 0, end = nodes.Length; i < end; i++)
                boundingBox = IncludeInAABB(boundingBox, nodes[i]);
            return boundingBox;
        }
    }
}
