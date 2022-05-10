/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - also known as Clipper2                            *
* Date      :  8 May 2022                                                      *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This is the main polygon clipping module                        *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/
namespace PolygonMath.Clipping.Clipper2LibBURST
{
    //IntersectNode: a structure representing 2 intersecting edges.
    //Intersections must be sorted so they are processed from the largest
    //Y coordinates to the smallest while keeping edges adjacent.

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