using Unity.Mathematics;
using Unity.Collections;
using Clipper2Lib;
using Chart3D.MathExtensions;
using Unity.Entities;
//using UnityEngine;

public static class StaticHelper
{
    public static readonly int DisplayWidth = 800;
    public static readonly int DisplayHeight = 600;
    public static readonly int edgeCount = 450;
    public static readonly int numberOfPolygons = 10;
    public static void GenerateRandomPath(Random rand, int width, int height, int count, out Paths64 _subj, out Paths64 _clip)
    {        
        _subj = new Paths64();
        _clip = new Paths64();

        _subj.Add(MakeRandomPath(width, height, count, ref rand));
        _clip.Add(MakeRandomPath(width, height, count, ref rand));
    }
    private static Point64 MakeRandomPoint1(int maxWidth, int maxHeight, ref Random rand)
    {
        long x = rand.NextInt(0, maxWidth);
        var y = rand.NextInt(0, maxHeight);
        return new Point64(x, y);
    }

    public static Path64 MakeRandomPath(int width, int height, int count, ref Random rand)
    {
        var result = new Path64(count);
        for (int i = 0; i < count; ++i)
            result.Add(MakeRandomPoint1(width, height, ref rand));
        return result;
    }
    public static Paths64 GetPaths64(DynamicBuffer<int2> nodes, DynamicBuffer<int> startIDs)
    {
        var _paths=new Paths64();
        for (int i = 0, length= startIDs.Length-1; i < length; i++)
        {
            var start = startIDs[i];
            var end = startIDs[i+1];
            var tempPath = new Path64(end-start);
            for (int k = start; k < end; k++)
            {
                var node = nodes[k];
                tempPath.Add(new Point64(node.x, node.y));
            }
            _paths.Add(tempPath);
        }
        return _paths;
    }
    public static PolygonInt GetPolygonInt(DynamicBuffer<int2> nodes, DynamicBuffer<int> startIDs, Allocator allocator)
    {
        return new PolygonInt(nodes.AsNativeArray(), startIDs.AsNativeArray(), allocator);        
    }
    public static void GenerateRandomPolygon(Random rand, int width, int height, int count, out PolygonInt _subj, out PolygonInt _clip)
    {
        _subj = new PolygonInt(count, Allocator.Persistent);
        _clip = new PolygonInt(count, Allocator.Persistent);

        _subj.AddComponent();
        for (int i = 0; i < count; ++i)
            _subj.nodes.Add(MakeRandomPoint2(width, height, ref rand));
        _subj.ClosePolygon();

        _clip.AddComponent();
        for (int i = 0; i < count; ++i)
            _clip.nodes.Add(MakeRandomPoint2(width, height, ref rand));
        _clip.ClosePolygon();
    }
    public static void GenerateRandomNodes(Random rand, ref DynamicBuffer<int2> nodes, int width, int height, int count)
    {
        for (int i = 0; i < count; ++i)
            nodes.Add(MakeRandomPoint2(width, height, ref rand));
    }
    private static int2 MakeRandomPoint2(int maxWidth, int maxHeight, ref Random rand)
    {
        int x = rand.NextInt(0, maxWidth);
        int y = rand.NextInt(0, maxHeight);
        return new int2(x, y);
    }    
}
