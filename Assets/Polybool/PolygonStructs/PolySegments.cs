using Unity.Collections;

namespace Polybool
{
    public struct PolySegments
    {        
        public NativeList<Segment> segments;
        public bool inverted;
    }
}