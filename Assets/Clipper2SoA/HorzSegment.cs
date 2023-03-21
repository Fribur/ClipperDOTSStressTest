namespace Clipper2SoA
{
    public struct HorzSegment
    {
        public int leftOp;
        public int rightOp;
        public bool leftToRight;
        public HorzSegment(int op)
        {
            leftOp = op;
            rightOp = -1;
            leftToRight = true;
        }
    };    

} //namespace