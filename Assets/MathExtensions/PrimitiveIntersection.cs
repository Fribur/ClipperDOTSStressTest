using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Chart3D.MathExtensions;

namespace Chart3D.PolygonMath
{

    public static class PrimitiveIntersection
    {       

        /// <summary>
        /// Returns a positive value if the points a, b, and c occur in counterclockwise order (c lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(c lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Orient2DFast(double2 p, double2 q, double2 r)
        {
            return (p.x - r.x) * (q.y - r.y) - (p.y - r.y) * (q.x - r.x);
        }
        /// <summary>
        /// Returns a positive value if the points a, b, and c occur in counterclockwise order (c lies to the left of the directed line defined by points a and b).
        /// Returns a negative value if they occur in clockwise order(c lies to the right of the directed line ab).
        /// Returns zero if they are collinear.
        /// </summary>  
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Orient2DFast(float2 p, float2 q, float2 r)
        {
            return (p.x - r.x) * (q.y - r.y) - (p.y - r.y) * (q.x - r.x);
        }
        
        /// <summary>
        /// PNPOLY, WR Franklin, keeping track of whether the number of edges crossed are even or odd. 0 means even and 1 means odd
        /// </summary>
        public static bool PnInPolyFranklin(double2 v, in NativeArray<double2> nodes, int start, int end, bool isInside)
        {
            for (int i = start, j = end - 1; i < end; j = i++) //from (0, prev) until (end, prev)
            {
                var Pi= nodes[i];
                var Pj= nodes[j];
                if (((Pi.y > v.y) != (Pj.y > v.y)) && (v.x < (Pj.x - Pi.x) * (v.y - Pi.y) / (Pj.y - Pi.y) + Pi.x))
                    isInside = !isInside;
            }
            return isInside;
        }
        /// <summary>
        /// PNPOLY, WR Franklin, keeping track of whether the number of edges crossed are even or odd. 0 means even and 1 means odd
        /// </summary>
        public static bool PnInPolyFranklin(int2 v, in NativeArray<int2> nodes, int start, int end, bool isInside)
        {
            for (int i = start, j = end - 1; i < end; j = i++) //from (0, prev) until (end, prev)
            {
                var Pi = (double2)nodes[i];
                var Pj = (double2)nodes[j];
                if (((Pi.y > v.y) != (Pj.y > v.y)) && (v.x < (Pj.x - Pi.x) * (v.y - Pi.y) / (Pj.y - Pi.y) + Pi.x))
                    isInside = !isInside;
            }
            return isInside;
        }
        /// <summary>
        /// Check for a given point if it is inside a Polygon. 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="nodes"> Polygon having multiple components. First one is outer component. </param>
        /// <param name="startIDs">Must contain StartIDs of all components. Last startID  MUST be end of last component</param>
        /// <returns></returns>
        public static bool PnInPolyFranklin(int2 point, in NativeArray<int2> nodes, in NativeArray<int> startIDs)
        {
            bool c = false;  //keeping track of whether the number of edges crossed are even or odd. 0 means even and 1 means odd
            for (int i = 0, length = startIDs.Length - 1; i < length; i++)
                c = PnInPolyFranklin(point, nodes, startIDs[i], startIDs[i + 1], c);
            return c;
        }

        //Return an integer 1, 0, or -1 depending on whether the point P is within (1), outside (0), or on (-1) the polygon poly, respectively.
        //for purpose of Map Loading: 1=true, 0 = false, -1=false
        public static bool PnInPolyHao(float2 p, ref NativeArray<int2> poly)
        {
            int j, k = 0;
            float f, u1, v1, u2, v2;
            for (int i = 0, end = poly.Length; i < end; i++)
            {
                j = (i + 1) % end;
                v1 = poly[i].y - p.y;
                v2 = poly[j].y - p.y;
                if ((v1 < 0 && v2 < 0) || (v1 > 0 && v2 > 0)) //case 11 or 26
                    continue;
                u1 = poly[i].x - p.x;
                u2 = poly[j].x - p.x;
                if (v2 > 0 && v1 <= 0) //Case 3,9,16,21,13, or 24
                {
                    f = u1 * v2 - u2 * v1;
                    if (f > 0) //Case 3 or 9
                        k = k + 1; //Hanlde Case 3 or 9
                    else if (f == 0) //case 16 or 21. The rest are case 13 or 24
                        return false; //Handle case 16 or 21, return -1
                }
                else if (v1 > 0 && v2 <= 0) //case 4, 10, 19, 20, 12, or 25
                {
                    f = u1 * v2 - u2 * v1;
                    if (f < 0) //case 4 or 10
                        k = k + 1; //handle case 4 or 10
                    else if (f == 0) //case 19 or 20. The rest are case 12 or 25
                        return false; //handle case 19 or 20, return -1
                }
                else if (v2 == 0 && v1 < 0) // case 7, 14, or 17
                {
                    f = u1 * v2 - u2 * v1;
                    if (f == 0)
                        return false; //case 17. The rest are Case 7 or 14 , return -1
                }
                else if (v1 == 0 && v2 < 0) //case 8, 15, or 18
                {
                    f = u1 * v2 - u2 * v1;
                    if (f == 0)
                        return false; //Case 18. The rest are Case 8 or 15, return -1
                }
                else if (v1 == 0 && v2 == 0) //case 1, 2, 5, 6, 22, or 23
                {
                    if (u2 <= 0 && u1 >= 0)//case 1
                        return false; //handle case 1, return -1
                    else if (u1 <= 0 && u2 >= 0) //case 2. The rest are case 5,6,22, or 23
                        return false; //handle case 2, return -1
                }
            }
            if (k % 2 == 0)
                return false;
            else
                return true;
        }

        /// <summary>
        /// Circle  vs rectable collision, rectangle does not have to be axis aligned.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RectangleOverlapsCircle(float2x2 aabb, float2 circleCenter, float radius)
        {
            float2 A = aabb.c0;
            float2 B = new float2(aabb.c0.x, aabb.c1.y);
            float2 C = aabb.c1;
            float2 D = new float2(aabb.c1.x, aabb.c0.y);

            float2 closestAB = GetClosestPointOnLineSegment(A, B, circleCenter);
            float2 closestBC = GetClosestPointOnLineSegment(B, C, circleCenter);
            float2 closestCD = GetClosestPointOnLineSegment(C, D, circleCenter);
            float2 closestDA = GetClosestPointOnLineSegment(D, A, circleCenter);

            return math.distance(closestAB, circleCenter) < radius ||
                   math.distance(closestBC, circleCenter) < radius ||
                   math.distance(closestCD, circleCenter) < radius ||
                   math.distance(closestDA, circleCenter) < radius;
        }

        //public static bool AABBOverlapsCircle(int2x2 aabb, float2 circleCenter, float radius)
        //{
        //    float2 aabbCenter = GetAABBCentroid(aabb);
        //    float2 distance = math.abs(circleCenter - aabbCenter);
        //    var halfWidthHeight = (aabb.c1 - aabb.c0) / 2;
        //    if (distance.x > (halfWidthHeight.x + radius)) { return false; }
        //    if (distance.y > (halfWidthHeight.y + radius)) { return false; }
        //    if (distance.x <= halfWidthHeight.x) { return true; }
        //    if (distance.y <= halfWidthHeight.y) { return true; }

        //    var cDist_sq = math.pow((distance.x - halfWidthHeight.x),2) + math.pow((distance.y - halfWidthHeight.y),2);

        //    return (cDist_sq <= math.pow(radius, 2));
        //}
        public static bool AABBOverlapsCircle(int2x2 aabb, float2 circleCenter, float radius)
        {
            float2 aabbCenter = MathHelper.GetAABBCentroid(aabb);
            var halfWidthHeight = (aabb.c1 - aabb.c0) / 2;
            float2 distance = math.abs(circleCenter - aabbCenter);
            float2 u = math.max(distance - halfWidthHeight, 0);
            return math.dot(u, u) < math.pow(radius, 2);
        }

        /// <summary>
        /// Find for a given point the closest point on a line segments
        /// </summary>
        public static float2 GetClosestPointOnLineSegment(float2 pointA, float2 pointB, float2 P)
        {
            float2 AP = P - pointA;         //Vector from A to P   
            float2 edgeA = pointB - pointA; //Vector from A to B
            float dot = math.dot(edgeA, AP);
            float edgeLengthSquared = math.lengthsq(edgeA);
            dot = math.max(dot, 0.0f);              //Check if P projection is over vectorAB 
            dot = math.min(dot, edgeLengthSquared); //Check if P projection is over vectorAB 
            float invEdgeLengthSquared = 1.0f / edgeLengthSquared;
            float frac = dot * invEdgeLengthSquared; //The normalized "distance" from a to your closest point
            return pointA + edgeA * frac;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AABBOverlapsAABB(int2x2 a, int2x2 b)
        {
            return (a.c0.x <= b.c1.x) && (a.c1.x >= b.c0.x) &&
                (a.c0.y <= b.c1.y) && (a.c1.y >= b.c0.y);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointInCircle(double2 point, double2 circleCenter, float radius)
        {
            return math.distance(point, circleCenter) < radius;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TriangleVertexInCircle(double2x3 triangle, double2 circleCenter, float radius)
        {
            var aInCircle = math.distance(triangle.c0, circleCenter) < radius;
            var bInCircle = math.distance(triangle.c1, circleCenter) < radius;
            var cInCircle = math.distance(triangle.c2, circleCenter) < radius;
            return aInCircle || bInCircle || cInCircle;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TriangleCircleOverlap(float2x3 triangle, float2 circleCenter, float radius)
        {
            if (math.distance(triangle.c0, circleCenter) < radius) //TEST 1a: Vertex within circle
                return true;
            if (math.distance(triangle.c1, circleCenter) < radius) //TEST 1b: Vertex within circle
                return true;
            if (math.distance(triangle.c2, circleCenter) < radius) //TEST 1v: Vertex within circle
                return true;
            if (math.distance(circleCenter, GetClosestPointOnLineSegment(triangle.c0, triangle.c1, circleCenter)) <= radius)//TEST 2a: Edge 1 intersects circle
                return true;
            if (math.distance(circleCenter, GetClosestPointOnLineSegment(triangle.c1, triangle.c2, circleCenter)) <= radius)//TEST 2b: Edge 2 intersects circle
                return true;
            if (math.distance(circleCenter, GetClosestPointOnLineSegment(triangle.c2, triangle.c0, circleCenter)) <= radius)//TEST 2c: Edge 3 intersects circle
                return true;
            if (math.distance(circleCenter, MathHelper.GetTriangleCentroid(triangle)) <= radius) //TEST 3: Circle centre within triangle
                return true;
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TriangleCircleOverlapSIMD(float2x3 triangle, float2 circleCenter, float radius)
        {
            double4 distance;
            distance.w = math.distance(triangle.c0, circleCenter); //TEST 1a: Vertex within circle
            distance.x = math.distance(triangle.c1, circleCenter); //TEST 1b: Vertex within circle
            distance.y = math.distance(triangle.c2, circleCenter); //TEST 1v: Vertex within circle
            distance.z = math.distance(GetClosestPointOnLineSegment(triangle.c0, triangle.c1, circleCenter), circleCenter);//TEST 2a: Edge 1 intersects circle
            if (math.any(distance <= radius))
                return true;
            distance.w = math.distance(GetClosestPointOnLineSegment(triangle.c1, triangle.c2, circleCenter), circleCenter);//TEST 2b: Edge 2 intersects circle
            distance.x = math.distance(GetClosestPointOnLineSegment(triangle.c2, triangle.c0, circleCenter), circleCenter);//TEST 2c: Edge 3 intersects circle
            distance.y = math.distance(MathHelper.GetTriangleCentroid(triangle), circleCenter); //TEST 3: Circle centre within triangle
            distance.z = distance.w;
            if (math.any(distance <= radius))
                return true;
            return false;
        }
        /// <summary>
        /// DOT product test (method 3 from: http://totologic.blogspot.com/2014/01/accurate-point-in-triangle-test.html
        /// works for CCW and CW  tringles, see here http://www.sunshine2k.de/coding/java/PointInTriangle/PointInTriangle.html
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointInTriangle(double2 p, double2x3 t)
        {
            double side1 = (t.c2.x - p.x) * (t.c0.y - p.y) - (t.c0.x - p.x) * (t.c2.y - p.y);//Orient2D
            double side2 = (t.c0.x - p.x) * (t.c1.y - p.y) - (t.c1.x - p.x) * (t.c0.y - p.y);//Orient2D
            double side3 = (t.c1.x - p.x) * (t.c2.y - p.y) - (t.c2.x - p.x) * (t.c1.y - p.y);//Orient2D
            return (side1 > 0 && side2 > 0 && side3 > 0) || (side1 < 0 && side2 < 0 && side3 < 0); //first test works for CCW-triangles, second for CW-triangles;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool4 PointInTriangleSIMD(double4 ax, double4 ay, double4 bx, double4 by, double4 cx, double4 cy, double4 px, double4 py)
        {
            double4 side1 = (cx - px) * (ay - py) - (ax - px) * (cy - py);//Orient2D
            double4 side2 = (ax - px) * (by - py) - (bx - px) * (ay - py);//Orient2D
            double4 side3 = (bx - px) * (cy - py) - (cx - px) * (by - py);//Orient2D
            bool4 CCW = side1 > 0 & side2 > 0 & side3 > 0;
            if (math.any(CCW))
                return CCW;
            else
                return side1 < 0 & side2 < 0 & side3 < 0;
            //return (side1 > 0 & side2 > 0 & side3 > 0) | (side1 < 0 & side2 < 0 & side3 < 0); //first test works for CCW-triangles, second for CW-triangles;
        }
        public static double3 IntersectPlanePoint(double3 origin, double3 direction, double3 planePoint)
        {
            var diff = origin - planePoint;
            var prod1 = math.dot(diff, math.up());
            var prod2 = math.dot(direction, math.up());
            var prod3 = prod1 / prod2;
            return origin - direction * prod3;
        }

        public static bool IntersectSpherePoint(double3 origin, double3 direction, double3 sphereCenter, double sphereRadius, out double3 spherePoint)
        {
            double t0, t1;  //solutions for t if the ray intersects 
            spherePoint = default;
            //// geometric solution
            //Vec3f L = center - origin; 
            //float tca = L.dotProduct(dir); 
            //// if (tca < 0) return false;
            //float d2 = L.dotProduct(L) - tca * tca; 
            //if (d2 > radius2) return false; 
            //float thc = sqrt(radius2 - d2); 
            //t0 = tca - thc; 
            //t1 = tca + thc; 

            // analytic solution
            double3 L = origin - sphereCenter;
            double a = math.dot(direction, direction);
            double b = 2 * math.dot(direction, L);
            double c = math.dot(L, L) - math.pow(sphereRadius, 2);
            if (!solveQuadratic(a, b, c, out t0, out t1))
                return false;
            if (t0 > t1)
            {
                double tmp = t0;
                t0 = t1;
                t1 = tmp;
            }

            if (t0 < 0)
            {
                if (t1 < 0)
                    return false;  //both t0 and t1 are negative 
                t0 = t1;  //if t0 is negative, let's use t1 instead 
            }

            //t = t0; 
            spherePoint = origin + direction * t0;
            return true;
        }
        public static bool solveQuadratic(double a, double b, double c, out double x0, out double x1)
        {
            double discr = b * b - 4 * a * c;
            x0 = default;
            x1 = default;
            if (discr < 0)
                return false;
            else if (discr == 0)
                x0 = x1 = -0.5 * b / a;
            else
            {
                double q = (b > 0) ? -0.5 * (b + math.sqrt(discr)) : -0.5 * (b - math.sqrt(discr));
                x0 = q / a;
                x1 = c / q;
            }
            if (x0 > x1)
            {
                double tmp = x0;
                x0 = x1;
                x1 = tmp;
            }
            return true;
        }
        public static bool2 PointInsideAABB1vs2(in TwoTransposedAabbs aabbT, int2 x, int2 y)
        {
            bool2 lc = x >= aabbT.Lx & x <= aabbT.Hx;
            bool2 hc = y >= aabbT.Ly & y <= aabbT.Hy;
            bool2 c = lc & hc;
            return c;
        }
        public static bool4 Overlap1Vs4(ref FourTransposedAabbs aabbInput, ref FourTransposedAabbs aabbNode)
        {
            bool4 lc = (aabbInput.Lx <= aabbNode.Hx) & (aabbInput.Ly <= aabbNode.Hy);
            bool4 hc = (aabbInput.Hx >= aabbNode.Lx) & (aabbInput.Hy >= aabbNode.Ly);
            bool4 c = lc & hc;
            return c;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PointInsideAABB(int2 p, in int2x2 aabb)
        {
            bool lc = p.x >= aabb.c0.x & p.x <= aabb.c1.x;
            bool hc = p.y >= aabb.c0.y & p.y <= aabb.c1.y;
            bool c = lc & hc;
            return c;
        }
        public static float MinDistanceOfPointToAABB(float2 point, in int2x2 aabb)
        {
            float2 A = aabb.c0;
            float2 B = new float2(aabb.c0.x, aabb.c1.y);
            float2 C = aabb.c1;
            float2 D = new float2(aabb.c1.x, aabb.c0.y);

            float2 closestAB = GetClosestPointOnLineSegment(A, B, point);
            float2 closestBC = GetClosestPointOnLineSegment(B, C, point);
            float2 closestCD = GetClosestPointOnLineSegment(C, D, point);
            float2 closestDA = GetClosestPointOnLineSegment(D, A, point);

            float ABdistance = math.distance(closestAB, point);
            float BCdistance = math.distance(closestBC, point);
            float CDdistance = math.distance(closestCD, point);
            float DAdistance = math.distance(closestDA, point);

            float minDistance = math.min(ABdistance, math.min(BCdistance, math.min(CDdistance, DAdistance)));
            return minDistance;
        }

        public static bool MapInsideRadius(float2 point, float radius, ref NativeArray<int2> s57Nodes)
        {
            for (int i = 0, end = s57Nodes.Length; i < end - 1; i++)
            {
                float2 closest = GetClosestPointOnLineSegment(s57Nodes[i], s57Nodes[i + 1], point);
                if (math.distance(closest, point) < radius)//one of the polygonsegments is crossing radius around player
                    return true;
            }
            return false;
        }

        public static bool MapIntersectsCircle(float2 point, float radius, ref NativeArray<int2> s57Nodes, out float minDistance)
        {
            minDistance = float.MaxValue;
            for (int i = 0, end = s57Nodes.Length; i < end - 1; i++)
            {
                float2 closest = GetClosestPointOnLineSegment(s57Nodes[i], s57Nodes[i + 1], point);
                float distance = math.distance(closest, point);
                if (distance < radius && distance < minDistance)//one of the polygonsegments is crossing radius around player
                    minDistance = distance;
            }
            return minDistance < float.MaxValue;
        }
    }    
    //public static void GetPolygonFromArrays(ref NativeList<int2> vertices, ref NativeList<int> startIDs, ref Polygon polygon, int coordinateMultiplicationFactor)
    //{
    //    for (int i = 0, length = startIDs.Length; i < length - 1; i++)
    //    {
    //        int start = startIDs[i];
    //        int end = startIDs[i + 1];
    //        polygon.AddComponent();
    //        for (int j = start; j < end; j++)
    //            polygon.nodes.Add((double2)vertices[j]/ coordinateMultiplicationFactor);
    //    }
    //    polygon.ClosePolygon();
    //}
}

