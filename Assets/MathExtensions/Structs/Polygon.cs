using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Chart3D.MathExtensions
{
    public struct Polygon
    {
        public NativeList<double2> nodes;
        public NativeList<int> startIDs;
        public NativeList<PolyOrientation> orientations;
        double2x2 aabb;
        bool aabbSet;
        public bool IsCreated;
        public Polygon(int size, Allocator allocator)
        {
            nodes = new NativeList<double2>(size, allocator);
            startIDs = new NativeList<int>(16, allocator);
            orientations = new NativeList<PolyOrientation>(allocator);
            aabb = MathHelper.emptyAABBd2();
            aabbSet = false;
            IsCreated = true;
        }
        public Polygon(int NodeSize, int Components, Allocator allocator)
        {
            nodes = new NativeList<double2>(NodeSize, allocator);
            startIDs = new NativeList<int>(Components + 1, allocator);
            orientations = new NativeList<PolyOrientation>(Components, allocator);
            aabb = MathHelper.emptyAABBd2();
            aabbSet = false;
            IsCreated = true;
        }
        public Polygon(in NativeArray<int2> nodes, in NativeArray<int> startIDs, Allocator allocator)
        {
            this.nodes = new NativeList<double2>(nodes.Length, allocator);
            aabb = MathHelper.emptyAABBd2();
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
        public Polygon(in PolygonInt sourcePoly, float scale, Allocator allocator)
        {
            aabb = sourcePoly.GetAABBfromPolygon();
            aabbSet = true;

            nodes = new NativeList<double2>(sourcePoly.nodes.Length, allocator);
            for (int i = 0, length = sourcePoly.nodes.Length; i < length; i++)
                nodes.Add((double2)sourcePoly.nodes[i] * scale);

            startIDs = new NativeList<int>(sourcePoly.startIDs.Length, allocator);
            startIDs.AddRange(sourcePoly.startIDs.AsArray());

            //to-do: copy orientations from source?
            orientations = new NativeList<PolyOrientation>(sourcePoly.startIDs.Length - 1, allocator);
            for (int i = 0, length = sourcePoly.startIDs.Length - 1; i < length; i++)
                orientations.Add(PolyOrientation.None);

            IsCreated = true;
        }
        public Polygon(in Polygon sourcePoly, Allocator allocator)
        {
            aabb = sourcePoly.GetAABBfromPolygon();
            aabbSet = true;

            nodes = new NativeList<double2>(sourcePoly.nodes.Length, allocator);
            nodes.AddRange(sourcePoly.nodes.AsArray());

            startIDs = new NativeList<int>(sourcePoly.startIDs.Length, allocator);
            startIDs.AddRange(sourcePoly.startIDs.AsArray());

            orientations = new NativeList<PolyOrientation>(sourcePoly.orientations.Length, allocator);
            orientations.AddRange(sourcePoly.orientations.AsArray());
            IsCreated = true;
        }
        public double2x2 GetAABBfromPolygon()
        {
            if (!aabbSet)
            {
                for (int i = 0, end = nodes.Length; i < end; i++)
                    aabb = MathHelper.IncludeInAABB(aabb, nodes[i]);
            }
            return aabb;
        }
        public void GetAABBsfromPolygon(ref NativeList<float2x2> componentAABBs)
        {

            for (int i = 0, length = startIDs.Length - 1; i < length; i++)
            {
                var aabb = MathHelper.emptyAABBd2();
                int start = startIDs[i];
                int end = startIDs[i + 1];
                for (int j = start; j < end; j++)
                    aabb = MathHelper.IncludeInAABB(aabb, nodes[j]);
                componentAABBs.Add((float2x2)aabb);
            }
        }
        public void AddComponent(NativeList<double2> points)
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
        public void AddComponent(in NativeList<double2> points, int start, int end)
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
        public void AddComponent(ref Polygon polygon, int componentID)
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
            double2 temp;
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

        //public static PolyOrientation GetOrientation(in Polygon poly, int componentID)
        //{
        //    int start = poly.startIDs[componentID];
        //    int end = poly.startIDs[componentID + 1];
        //    var Nodes = poly.nodes;
        //    return GetPolyOrientation(MathHelper.SignedArea(in Nodes, start, end));
        //}
        //public static void PrintOrientation(in Polygon poly)
        //{
        //    for (int k = 0, length = poly.startIDs.Length - 1; k < length; k++)
        //    {
        //        PolyOrientation orientation = GetOrientation(poly, k);
        //        Debug.Log($"Component {k}: {orientation} {poly.Orientation(k)}");
        //    }
        //}
        public void GetComponentStartEnd(int componentID, out int start, out int end)
        {
            start = startIDs[componentID];
            end = startIDs[componentID + 1];
        }
    }

}