using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using Chart3D.MathExtensions;

namespace Chart3D.PolygonMath
{    
    public struct TwoTransposedAabbs
    {
        public int2 Lx, Hx;    // Lower and upper bounds along the X axis.
        public int2 Ly, Hy;    // Lower and upper bounds along the Y axis.
        public void SetAabb(int index, int2x2 aabb)
        {
            Lx[index] = aabb.c0.x;
            Hx[index] = aabb.c1.x;

            Ly[index] = aabb.c0.y;
            Hy[index] = aabb.c1.y;
        }
    }    
}

