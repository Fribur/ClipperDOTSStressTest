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
    public struct TreeNode
    {
        public int ID;
        public int parentID;
        public int rightID;
        public int childID;
        public PolyOrientation orientation;
        public TreeNode(int ID)
        {
            this.ID = ID;
            this.parentID = -1;
            this.rightID = -1;
            this.childID = -1;
            this.orientation = PolyOrientation.None;
        }
    };

} //namespace