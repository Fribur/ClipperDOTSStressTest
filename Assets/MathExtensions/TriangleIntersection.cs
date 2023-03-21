using Chart3D.PolygonMath;
using Unity.Mathematics;

namespace Chart3D.MathExtensions
{
    public static class TriangleIntersection
    {
        /// <summary>
        /// Two dimensional Triangle-Triangle Overlap Test
        /// </summary>
        //public static bool Overlap2D(int2x3 t1, int2x3 t2)
        public static bool Overlap2D(float2x3 t1, float2x3 t2)
        {
            float2 p1 = t1.c0;
            float2 q1 = t1.c1;
            float2 r1 = t1.c2;

            float2 p2 = t2.c0;
            float2 q2 = t2.c1;
            float2 r2 = t2.c2;

            if (PrimitiveIntersection.Orient2DFast(p1, q1, r1) < 0.0f)
            {
                if (PrimitiveIntersection.Orient2DFast(p2, q2, r2) < 0.0f)
                    return DetectIntersection2D(p1, r1, q1,p2, r2, q2);
                else
                    return DetectIntersection2D(p1, r1, q1, p2, q2, r2);
            }
            else
            {
                if (PrimitiveIntersection.Orient2DFast(p2, q2, r2) < 0.0f)
                    return DetectIntersection2D(p1, q1, r1, p2, r2, q2);
                else
                    return DetectIntersection2D(p1, q1, r1, p2, q2, r2);
            }
        }
        private static bool DetectIntersection2D(float2 p1, float2 q1, float2 r1, float2 p2, float2 q2, float2 r2)
        {
            if (PrimitiveIntersection.Orient2DFast(p2, q2, p1) >= 0.0f)
            {
                if (PrimitiveIntersection.Orient2DFast(q2, r2, p1) >= 0.0f)
                {
                    if (PrimitiveIntersection.Orient2DFast(r2, p2, p1) >= 0.0f)
                        return true;
                    else
                        return IntersectionEdge(p1, q1, r1, p2, q2, r2);
                }
                else
                {
                    if (PrimitiveIntersection.Orient2DFast(r2, p2, p1) >= 0.0f)
                        return IntersectionEdge(p1, q1, r1, r2, p2, q2);
                    else
                        return IntersectionVertex(p1, q1, r1,p2, q2, r2);
                }
            }
            else
            {
                if (PrimitiveIntersection.Orient2DFast(q2, r2, p1) >= 0.0f)
                {
                    if (PrimitiveIntersection.Orient2DFast(r2, p2, p1) >= 0.0f)
                        return IntersectionEdge(p1, q1, r1,q2, r2, p2);
                    else
                        return IntersectionVertex(p1, q1, r1,q2, r2, p2);
                }
                else
                    return IntersectionVertex(p1, q1, r1,r2, p2, q2);
            }
        }
        private static bool IntersectionVertex(float2 p1, float2 q1, float2 r1, float2 p2, float2 q2, float2 r2)
        {
            if (PrimitiveIntersection.Orient2DFast(r2, p2, q1) >= 0.0f)
            {
                if (PrimitiveIntersection.Orient2DFast(r2, q2, q1) <= 0.0f)
                {
                    if (PrimitiveIntersection.Orient2DFast(p1, p2, q1) > 0.0f)
                    {
                        return PrimitiveIntersection.Orient2DFast(p1, q2, q1) <= 0.0f;
                    }
                    else
                    {
                        if (PrimitiveIntersection.Orient2DFast(p1, p2, r1) >= 0.0f)
                        {
                            return PrimitiveIntersection.Orient2DFast(q1, r1, p2) >= 0.0f;
                        }
                    }
                }
                else if (PrimitiveIntersection.Orient2DFast(p1, q2, q1) <= 0.0f)
                {
                    if (PrimitiveIntersection.Orient2DFast(r2, q2, r1) <= 0.0f)
                    {
                        return PrimitiveIntersection.Orient2DFast(q1, r1, q2) >= 0.0f;
                    }
                }
            }
            else if (PrimitiveIntersection.Orient2DFast(r2, p2, r1) >= 0.0f)
            {
                if (PrimitiveIntersection.Orient2DFast(q1, r1, r2) >= 0.0f)
                {
                    return PrimitiveIntersection.Orient2DFast(p1, p2, r1) >= 0.0f;
                }
                else if (PrimitiveIntersection.Orient2DFast(q1, r1, q2) >= 0.0f)
                {
                    return PrimitiveIntersection.Orient2DFast(r2, r1, q2) >= 0.0f;
                }
            }

            return false;
        }
        private static bool IntersectionEdge(float2 p1, float2 q1, float2 r1, float2 p2, float2 q2, float2 r2)
        {
            if (PrimitiveIntersection.Orient2DFast(r2, p2, q1) >= 0.0f)
            {
                if (PrimitiveIntersection.Orient2DFast(p1, p2, q1) >= 0.0f)
                    return PrimitiveIntersection.Orient2DFast(p1, q1, r2) >= 0.0f;
                else
                {
                    if (PrimitiveIntersection.Orient2DFast(q1, r1, p2) >= 0.0f)
                        return PrimitiveIntersection.Orient2DFast(r1, p1, p2) >= 0.0f;
                }
            }
            else
            {
                if (PrimitiveIntersection.Orient2DFast(r2, p2, r1) >= 0.0f)
                {
                    if (PrimitiveIntersection.Orient2DFast(p1, p2, r1) >= 0.0f)
                    {
                        if (PrimitiveIntersection.Orient2DFast(p1, r1, r2) >= 0.0f)
                            return true;
                        else
                            return PrimitiveIntersection.Orient2DFast(q1, r1, r2) >= 0.0f;
                    }
                }
            }
            return false;
        }
    }
}
