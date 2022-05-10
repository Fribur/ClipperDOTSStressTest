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
using System.Collections.Generic;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    public struct LocalMinima
    {
        public readonly int vertex_ID;
        public readonly long2 vertex;
        public readonly PathType polytype;
        public readonly bool isOpen;

        public LocalMinima(int vertexID, long2 vertex, PathType polytype, bool isOpen = false)
        {
            this.vertex_ID = vertexID;
            this.vertex = vertex;
            this.polytype = polytype;
            this.isOpen = isOpen;
        }
    };
    struct LocMinSorter : IComparer<LocalMinima>
    {
        public int Compare(LocalMinima locMin1, LocalMinima locMin2)
        {
            return locMin2.vertex.y.CompareTo(locMin1.vertex.y);
        }
    }

} //namespace