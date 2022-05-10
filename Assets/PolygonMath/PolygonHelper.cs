
using Unity.Collections;
using Unity.Mathematics;


namespace PolygonMath
{
    public enum PolyOrientation : byte
    {
        CW = 0,
        CCW = 1,
        None = 2,
    }
    public static class PolygonHelper
    {
        public static PolyOrientation GetOrientation(double signedArea)
        {
            if (signedArea < 0)
                return PolyOrientation.CW;
            else if (signedArea > 0)
                return PolyOrientation.CCW;
            else
                return PolyOrientation.None;                    
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
    }
}
