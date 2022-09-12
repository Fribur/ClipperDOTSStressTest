using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PolygonMath
{
    public struct Polygon
    {
        public NativeList<double2> nodes;
        public NativeList<int> startIDs;
        NativeList<PolyOrientation> orientations;
        public bool IsCreated;
        public Polygon(int size, Allocator allocator)
        {
            nodes = new NativeList<double2>(size, allocator);
            startIDs = new NativeList<int>(allocator);
            orientations = new NativeList<PolyOrientation>(allocator);
            IsCreated = true;
        }
        public Polygon(int NodeSize, int Components, Allocator allocator)
        {
            nodes = new NativeList<double2>(NodeSize, allocator);
            startIDs = new NativeList<int>(Components + 1, allocator);
            orientations = new NativeList<PolyOrientation>(Components, allocator);
            IsCreated = true;
        }
        public Polygon(in NativeArray<int2> nodes, in NativeArray<int> startIDs, Allocator allocator)
        {
            this.nodes = new NativeList<double2>(nodes.Length, allocator);
            for (int i = 0, length = nodes.Length; i < length; i++)
                this.nodes.Add(nodes[i]);

            this.startIDs = new NativeList<int>(startIDs.Length, allocator);
            this.startIDs.AddRange(startIDs);

            orientations = new NativeList<PolyOrientation>(startIDs.Length - 1, allocator);
            for (int i = 0, length = startIDs.Length - 1; i < length; i++)
                orientations.Add(PolyOrientation.None);

            IsCreated = true;
        }
        public Polygon(in Polygon sourcePoly, Allocator allocator)
        {
            this.nodes = new NativeList<double2>(sourcePoly.nodes.Length, allocator);
            this.nodes.AddRange(sourcePoly.nodes);

            this.startIDs = new NativeList<int>(sourcePoly.startIDs.Length, allocator);
            this.startIDs.AddRange(sourcePoly.startIDs);

            this.orientations = new NativeList<PolyOrientation>(sourcePoly.orientations.Length, allocator);
            this.orientations.AddRange(sourcePoly.orientations);
            IsCreated = true;
        }
        public void AddComponent(NativeList<double2> points)
        {
            if (points.Length == 0)
                return;
            startIDs.Add(this.nodes.Length);
            orientations.Add(PolyOrientation.None);
            nodes.AddRange(points);
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
            if (!GeoHelper.Equals(polygon.nodes[start], polygon.nodes[end - 1]))
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
                orientations[componentID] = PolygonHelper.GetOrientation(PolygonHelper.SignedArea(nodes, start, end));
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
    }
}