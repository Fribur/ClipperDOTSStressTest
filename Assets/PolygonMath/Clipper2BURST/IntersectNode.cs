namespace PolygonMath.Clipping.Clipper2LibBURST
{
    // IntersectNode: a structure representing 2 intersecting edges.
    // Intersections must be sorted so they are processed from the largest
    // Y coordinates to the smallest while keeping edges adjacent.

    public struct IntersectNode
    {
        public long2 pt;
        public int edge1;
        public int edge2;
        public IntersectNode(long2 _pt, int _edge1, int _edge2)
        {
            pt = _pt;
            edge1 = _edge1;
            edge2 = _edge2;
        }
    };

} //namespace