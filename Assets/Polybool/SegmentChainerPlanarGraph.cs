//using System;
//using System.Collections.Generic;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;

//namespace Polybool
//{
//    internal static class SegmentChainerPlanar
//    {
//        private struct HalfEdge
//        {
//            public int from;
//            public int to;
//            public int twin;
//            public double angle;   // atan2(dy, dx)
//            public bool used;
//        }

//        internal static Polygon SegmentChainer(NativeList<Segment> segments, bool inverted)
//        {
//            // 1) Build vertices and half-edges
//            BuildGraph(segments,
//                out NativeList<long2> vertices,
//                out NativeList<HalfEdge> edges,
//                out UnsafeList<UnsafeList<int>> adj);

//            var polygon = new Polygon(segments.Length, 8, false, Allocator.Temp);

//            // 2) Traverse faces
//            for (int e = 0; e < edges.Length; e++)
//            {
//                if (edges[e].used)
//                    continue;

//                var face = TraceFace(e, edges, adj, vertices);
//                if (face.Length < 3)
//                    continue;

//                polygon.AddComponent(face, 0, face.Length);
//            }

//            if (polygon.nodes.Length > 0)
//                polygon.ClosePolygon();

//            // 3) Fix orientation (reuse existing logic)
//            FixOrientation(polygon);
//            return polygon;
//        }

//        // -----------------------------
//        // Graph construction
//        // -----------------------------
//        private static void BuildGraph(
//            NativeList<Segment> segments,
//            out NativeList<long2> vertices,
//            out NativeList<HalfEdge> edges,
//            out UnsafeList<UnsafeList<int>> adj)
//        {
//            var tmpVerticies = new NativeList<long2>(segments.Length * 2, Allocator.Temp);
//            var tmpEdges = new NativeList<HalfEdge>(segments.Length * 2, Allocator.Temp);

//            var vtxMap = new Dictionary<long2, int>();

//            int GetVertex(long2 p)
//            {
//                if (!vtxMap.TryGetValue(p, out int id))
//                {
//                    id = tmpVerticies.Length;
//                    tmpVerticies.Add(p);
//                    vtxMap[p] = id;
//                }
//                return id;
//            }

//            // Build half-edges
//            foreach (var seg in segments)
//            {
//                if (seg.start.CompareTo(seg.end) == 0)
//                    continue;

//                long2 p0 = seg.Eval(seg.start).ToLong2();
//                long2 p1 = seg.Eval(seg.end).ToLong2();

//                int v0 = GetVertex(p0);
//                int v1 = GetVertex(p1);

//                double a01 = Math.Atan2(p1.y - p0.y, p1.x - p0.x);
//                double a10 = Math.Atan2(p0.y - p1.y, p0.x - p1.x);

//                int e0 = tmpEdges.Length;
//                int e1 = e0 + 1;

//                tmpEdges.Add(new HalfEdge { from = v0, to = v1, twin = e1, angle = a01, used = false });
//                tmpEdges.Add(new HalfEdge { from = v1, to = v0, twin = e0, angle = a10, used = false });
//            }

//            // Build adjacency lists
//            adj = new UnsafeList<UnsafeList<int>>(tmpVerticies.Length, Allocator.Temp);
//            for (int i = 0; i < adj.Length; i++)
//                adj[i] = new UnsafeList<int>();

//            for (int i = 0; i < tmpEdges.Length; i++)
//                adj[tmpEdges[i].from].Add(i);

//            // Sort adjacency by angle CCW
//            for (int i = 0; i < adj.Length; i++)
//            {
//                adj[i].Sort((a, b) => tmpEdges[a].angle.CompareTo(tmpEdges[b].angle));
//            }
//            edges = tmpEdges;
//            vertices = tmpVerticies;
//        }

//        // -----------------------------
//        // Face traversal
//        // -----------------------------
//        private static UnsafeList<long2> TraceFace(
//            int startEdge,
//            NativeList<HalfEdge> edges,
//            UnsafeList<UnsafeList<int>> adj,
//            NativeList<long2> vertices)
//        {
//            var result = new UnsafeList<long2>(16, Allocator.Temp);
//            int e = startEdge;

//            while (true)
//            {
//                var egde = edges[e];
//                if (egde.used)
//                    break;

//                egde.used = true;
//                edges[e] = egde;
//                var egdeTwin = edges[egde.twin];
//                egdeTwin.used = true;
//                edges[egde.twin] = egdeTwin;

//                int vFrom = edges[e].from;
//                int vTo = edges[e].to;

//                result.Add(vertices[vFrom]);

//                // At vTo, find next edge (previous in CCW order)
//                var list = adj[vTo];
//                int twin = edges[e].twin;
//                int idx = list.IndexOf(twin);
//                if (idx < 0)
//                    break; // should never happen

//                int nextIdx = (idx - 1 + list.Length) % list.Length;
//                e = list[nextIdx];

//                if (e == startEdge)
//                    break;
//            }

//            return result;
//        }

//        // -----------------------------
//        // Orientation fixing
//        // -----------------------------
//        private static void FixOrientation(Polygon polygon)
//        {
//            if (polygon.startIDs.Length <= 1)
//                return;

//            var startIDs = polygon.startIDs;
//            var nodes = polygon.nodes;

//            Span<double> areas = stackalloc double[startIDs.Length - 1];

//            int maxRegion = 0;
//            double maxArea = 0;

//            for (int i = 0; i < startIDs.Length - 1; i++)
//            {
//                int s = startIDs[i];
//                int e = startIDs[i + 1];
//                double a = nodes.SignedArea(s, e);
//                areas[i] = a;
//                double abs = Math.Abs(a);
//                if (abs > maxArea)
//                {
//                    maxArea = abs;
//                    maxRegion = i;
//                }
//            }

//            if (areas[maxRegion] < 0)
//                polygon.Reverse(maxRegion);

//            int outerStart = startIDs[maxRegion];
//            int outerEnd = startIDs[maxRegion + 1];

//            for (int i = 0; i < startIDs.Length - 1; i++)
//            {
//                if (i == maxRegion)
//                    continue;

//                bool inside = polygon.PnInPolyFranklin(nodes[startIDs[i]], outerStart, outerEnd, false);
//                if (inside)
//                {
//                    if (areas[i] > 0)
//                        polygon.Reverse(i);
//                }
//                else
//                {
//                    if (areas[i] < 0)
//                        polygon.Reverse(i);
//                }
//            }
//        }
//    }
//}
