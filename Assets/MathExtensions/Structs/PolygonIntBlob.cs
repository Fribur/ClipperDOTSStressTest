using Unity.Entities;
using Unity.Mathematics;

namespace Chart3D.MathExtensions
{
    public struct PolygonIntBlob
    {
        public BlobArray<int2> nodes;
        public BlobArray<int> startIDs;
    }    
}