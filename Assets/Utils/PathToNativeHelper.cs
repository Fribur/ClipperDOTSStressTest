using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Clipper2Lib
{
    public static class PathToNativeHelper
    {

        public static Clipper2AoS.ClipType ClipType_ClipperToNative(Clipper2Lib.ClipType clipType)
        {
            switch (clipType)
            {
                case ClipType.NoClip: return Clipper2AoS.ClipType.NoClip;
                case ClipType.Intersection: return Clipper2AoS.ClipType.Intersection;
                case ClipType.Union: return Clipper2AoS.ClipType.Union;
                case ClipType.Difference: return Clipper2AoS.ClipType.Difference;
                case ClipType.Xor: return Clipper2AoS.ClipType.Xor;
                default: return Clipper2AoS.ClipType.NoClip;
            }
        }
        public static Clipper2AoS.FillRule FillRule_ClipperToNative(Clipper2Lib.FillRule clipType)
        {
            switch (clipType)
            {
                case FillRule.NonZero: return Clipper2AoS.FillRule.NonZero;
                case FillRule.Positive: return Clipper2AoS.FillRule.Positive;
                case FillRule.Negative: return Clipper2AoS.FillRule.Negative;
                case FillRule.EvenOdd: return Clipper2AoS.FillRule.EvenOdd;
                default: return Clipper2AoS.FillRule.NonZero;
            }
        }

        public static Paths64 PolygonToPaths(NativeList<int2> nodes, NativeList<int> startIDs)
        {
            var paths = new Paths64();
            for (int i = 0, length = startIDs.Length - 1; i < length; i++)
            {
                int start = startIDs[i];
                int end = startIDs[i + 1];
                var path = new Path64(end - start);
                for (int j = start; j < end; j++)
                    path.Add(new Point64(nodes[j].x, nodes[j].y));
                paths.Add(path);
            }
            return paths;
        }
        public static void AddPathToPolygon(List<Point64> path, bool isOpen, NativeList<int2> nodes, NativeList<int> startIDs)
        {
            startIDs.Add(nodes.Length);
            var end = path.Count;
            for (int k = 0; k < end; k++)
                nodes.Add(new int2((int)path[k].X, (int)path[k].Y));
            if (!isOpen && path[0] != path[end - 1])
                nodes.Add(new int2((int)path[0].X, (int)path[0].Y));
        }
        public static void PathsToPolygon(Paths64 paths, bool isOpen, out NativeList<int2> nodes, out NativeList<int> startIDs,  Allocator allocator)
        {
            nodes = new NativeList<int2>(256, allocator);
            startIDs = new NativeList<int>(4, allocator);
            for (int i = 0, length = paths.Count; i < length; i++)
            {
                var path = paths[i];
                AddPathToPolygon(path, isOpen, nodes, startIDs);
            }
            startIDs.Add(nodes.Length);//close Polygon
        }
    }
}
