
using Unity.Mathematics;

namespace Chart3D.MathExtensions
{
    public struct IntersectionPoint
    {
        public double2 Pt;
        public int alongA;
        public int alongB;

        public IntersectionPoint(int alongA, int alongB, double2 pt)
        {
            this.alongA = alongA;
            this.alongB = alongB;
            Pt = pt;
        }
    }
}