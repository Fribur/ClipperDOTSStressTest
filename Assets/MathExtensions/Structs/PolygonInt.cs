using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Chart3D.MathExtensions
{
    public struct PolygonInt
    {
        public NativeList<int2> nodes;
        public NativeList<int> startIDs;
        public NativeList<PolyOrientation> orientations;
        int2x2 aabb;
        bool aabbSet;
        public bool IsCreated;
        public PolygonInt(int size, Allocator allocator)
        {
            nodes = new NativeList<int2>(size, allocator);
            startIDs = new NativeList<int>(16, allocator);
            orientations = new NativeList<PolyOrientation>(allocator);
            aabb = MathHelper.emptyAABBi2();
            aabbSet = false;
            IsCreated = true;
        }
        public PolygonInt(int NodeSize, int Components, Allocator allocator)
        {
            nodes = new NativeList<int2>(NodeSize, allocator);
            startIDs = new NativeList<int>(Components + 1, allocator);
            orientations = new NativeList<PolyOrientation>(Components, allocator);
            aabb = MathHelper.emptyAABBi2();
            aabbSet = false;
            IsCreated = true;
        }
        public PolygonInt(in NativeArray<int2> nodes, in NativeArray<int> startIDs, Allocator allocator)
        {
            this.nodes = new NativeList<int2>(nodes.Length, allocator);
            aabb = MathHelper.emptyAABBi2();
            aabbSet = true;
            for (int i = 0, length = nodes.Length; i < length; i++)
            {
                this.nodes.Add(nodes[i]);
                aabb = MathHelper.IncludeInAABB(aabb, nodes[i]);
            }

            this.startIDs = new NativeList<int>(startIDs.Length, allocator);
            this.startIDs.AddRange(startIDs);

            orientations = new NativeList<PolyOrientation>(startIDs.Length - 1, allocator);
            for (int i = 0, length = startIDs.Length - 1; i < length; i++)
                orientations.Add(PolyOrientation.None);

            IsCreated = true;
        }
        public PolygonInt(in PolygonInt sourcePoly, Allocator allocator)
        {
            nodes = new NativeList<int2>(sourcePoly.nodes.Length, allocator);
            nodes.AddRange(sourcePoly.nodes.AsArray());

            startIDs = new NativeList<int>(sourcePoly.startIDs.Length, allocator);
            startIDs.AddRange(sourcePoly.startIDs.AsArray());

            orientations = new NativeList<PolyOrientation>(sourcePoly.orientations.Length, allocator);
            orientations.AddRange(sourcePoly.orientations.AsArray());

            aabb = sourcePoly.GetAABBfromPolygon();
            aabbSet = true;
            IsCreated = true;
        }
        public PolygonInt(in Polygon sourcePoly, Allocator allocator)
        {
            nodes = new NativeList<int2>(sourcePoly.nodes.Length, allocator);
            for (int n = 0, nn = sourcePoly.nodes.Length; n < nn; n++)
                nodes.Add((int2)sourcePoly.nodes[n]);

            startIDs = new NativeList<int>(sourcePoly.startIDs.Length, allocator);
            startIDs.AddRange(sourcePoly.startIDs.AsArray());

            orientations = new NativeList<PolyOrientation>(sourcePoly.orientations.Length, allocator);
            orientations.AddRange(sourcePoly.orientations.AsArray());

            aabb = (int2x2)sourcePoly.GetAABBfromPolygon();
            aabbSet = true;
            IsCreated = true;
        }
        public int2x2 GetAABBfromPolygon()
        {
            if (!aabbSet)
            {
                for (int i = 0, end = nodes.Length; i < end; i++)
                    aabb = MathHelper.IncludeInAABB(aabb, nodes[i]);
                aabbSet = true;
            }
            return aabb;
        }
        public void GetAABBsfromPolygon(ref NativeList<int2x2> componentAABBs)
        {

            for (int i = 0, length = startIDs.Length - 1; i < length; i++)
            {
                var aabb = MathHelper.emptyAABBi2();
                int start = startIDs[i];
                int end = startIDs[i + 1];
                for (int j = start; j < end; j++)
                    aabb = MathHelper.IncludeInAABB(aabb, nodes[j]);
                componentAABBs.Add(aabb);
            }
        }
        public void AddComponent(NativeList<int2> points)
        {
            if (points.Length == 0)
                return;
            startIDs.Add(this.nodes.Length);
            orientations.Add(PolyOrientation.None);
            nodes.AddRange(points.AsArray());
        }
        public void AddComponent(in NativeArray<int2> points, int start, int end)
        {
            if (points.Length == 0)
                return;
            startIDs.Add(this.nodes.Length);
            orientations.Add(PolyOrientation.None);
            for (int i = start; i < end; i++)
                nodes.Add(points[i]);
        }
        public void AddComponent(in NativeList<int2> points, int start, int end)
        {
            if (points.Length == 0)
                return;
            startIDs.Add(this.nodes.Length);
            orientations.Add(PolyOrientation.None);
            for (int i = start; i < end; i++)
                nodes.Add(points[i]);
        }
        public void AddComponent()
        {
            startIDs.Add(nodes.Length);
            orientations.Add(PolyOrientation.None);
        }
        public void RemoveLastComponent()
        {
            int startIDstart = startIDs.Length - 2;
            int startIDend = startIDstart + 1;
            int nodeStart = startIDs[startIDstart];
            int nodeEnd = startIDs[startIDend];
            for (int i = nodeEnd; i >= nodeStart; i++)
                nodes.RemoveAt(i);
            startIDs.RemoveAt(startIDend);
            orientations.RemoveAt(orientations.Length-1);
        }
        public void AddComponent(ref PolygonInt polygon, int componentID)
        {
            polygon.GetComponentStartEnd(componentID, out int start, out int end);
            if (end - start == 0)
                return;
            startIDs.Add(nodes.Length);
            orientations.Add(polygon.Orientation(componentID));
            for (int k = start; k < end; k++)
                nodes.Add(polygon.nodes[k]);
            if (!MathHelper.Equals(polygon.nodes[start], polygon.nodes[end - 1]))
                nodes.Add(polygon.nodes[start]); //close the component
        }
        public void ClosePolygon()
        {
            if (startIDs.Length > 0 && startIDs[startIDs.Length - 1] != nodes.Length)
                startIDs.Add(nodes.Length);
        }               
        public void Dispose()
        {
            if (nodes.IsCreated) nodes.Dispose();
            if (startIDs.IsCreated) startIDs.Dispose();
            if (orientations.IsCreated) orientations.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (nodes.IsCreated) nodes.Clear();
            if (startIDs.IsCreated) startIDs.Clear();
            if (orientations.IsCreated) orientations.Clear();
        }
        public void Reverse(int componentID)
        {
            switch (orientations[componentID])
            {
                case PolyOrientation.CW:
                    orientations[componentID] = PolyOrientation.CCW;
                    break;
                case PolyOrientation.CCW:
                    orientations[componentID] = PolyOrientation.CW;
                    break;
                default:
                    orientations[componentID] = PolyOrientation.None;
                    break;
            }
            GetComponentStartEnd(componentID, out int start, out int end);
            int i = start, j = end - 1;
            int2 temp;
            while (i < j)
            {
                temp = nodes[i];
                nodes[i] = nodes[j];
                nodes[j] = temp;
                i++;
                j--;
            }
        }
        public PolyOrientation Orientation(int componentID)
        {
            if (orientations[componentID] == PolyOrientation.None)
            {
                GetComponentStartEnd(componentID, out int start, out int end);
                orientations[componentID] = MathHelper.GetPolyOrientation(MathHelper.SignedArea(nodes, start, end));
                return orientations[componentID];
            }
            else
                return orientations[componentID];
        }
        public void GetComponentStartEnd(int componentID, out int start, out int end)
        {
            start = startIDs[componentID];
            end = startIDs[componentID + 1];
        }
        public void Dispose(JobHandle jobHandle)
        {
            nodes.Dispose(jobHandle);
            startIDs.Dispose(jobHandle);
            orientations.Dispose(jobHandle);
        }
    }
}