using Unity.Collections;
using Unity.Mathematics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace PolygonMath
{    
    public static class GeoHelper
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
    }
}
