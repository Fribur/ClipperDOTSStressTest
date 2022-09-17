using Unity.Collections;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    public struct PolyTree
    {
        public NativeArray<TreeNode> components;
        public NativeList<int> exteriorIDs;
        public bool GetNextComponent(int parentID, out int nextID)
        {
            nextID = components[parentID].childID;
            if (nextID != -1) //first try to go deep (to next child)
            {
                //Debug.Log("Go to child");
                return true;
            }
            nextID = components[parentID].rightID; //if that fails, try to go sideways (to next sibling)
            if (nextID != -1)
            {
                //Debug.Log("Go right");
                return true;
            }
            //if that fails, try to go to parent, check if right sibling exists, if not repeat until either parent is root (-1) or rightID exists
            do
            {
                //Debug.Log("Going up");
                nextID = components[parentID].rightID;
                parentID = components[parentID].parentID;
            } while (nextID == -1 && parentID != -1);
            if (nextID != -1) //return the next sibling that was found
            {
                //Debug.Log("Go Up-->right");
                return true;
            }
            if (parentID != -1) //last resort, return parent
            {
                //Debug.Log("Go Up-->parent");
                return true;
            }
            return false;
        }

        public void AddChildComponent(int parentID, TreeNode node)
        {
            //new node can just be added
            node.parentID = parentID;
            components[node.ID] = node;

            //now find out if node is direct child of parent, or right of an existing child

            int leftID = -1, childID;
            childID = components[parentID].childID;
            while (childID != -1)//has already child, so add new node to the right of the last one
            {
                leftID = childID;
                childID = components[childID].rightID;
            }
            if (leftID != -1) //modify left node to point right to new node
            {
                var leftnode = components[leftID];
                leftnode.rightID = node.ID;
                components[leftID] = leftnode;
            }
            else //modify parent node to point to first child 
            {
                var parentNode = components[parentID];
                parentNode.childID = node.ID;
                components[parentID] = parentNode;
            }
        }

        public bool IsCreated;
        public PolyTree(int outerIDsize, Allocator allocator)
        {
            components = new NativeArray<TreeNode>(outerIDsize, allocator);
            exteriorIDs = new NativeList<int>(outerIDsize, allocator);
            IsCreated = true;
        }
        public void Clear(int outerIDsize, Allocator allocator)
        {
            components.Dispose();
            components = new NativeArray<TreeNode>(outerIDsize, allocator);
            exteriorIDs.Clear();
        }
        public void Dispose()
        {
            if (components.IsCreated) components.Dispose();
            if (exteriorIDs.IsCreated) exteriorIDs.Dispose();
            IsCreated = false;
        }
    };
}