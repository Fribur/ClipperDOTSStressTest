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