using Clipper2Lib;
using System.Collections.Generic;
using Unity.Mathematics;
using PolygonMath;
using Unity.Collections;


public static class StaticHelper
{    
    public static void GenerateRandomPath(Random rand, int width, int height, int count, out List<List<Point64>> _subj, out List<List<Point64>> _clip)
    {        
        _subj = new List<List<Point64>>();
        _clip = new List<List<Point64>>();

        _subj.Add(MakeRandomPath(width, height, count, ref rand));
        _clip.Add(MakeRandomPath(width, height, count, ref rand));
    }
    private static Point64 MakeRandomPoint1(int maxWidth, int maxHeight, ref Random rand)
    {
        long x = rand.NextInt(0, maxWidth);
        var y = rand.NextInt(0, maxHeight);
        return new Point64(x, y);
    }

    public static List<Point64> MakeRandomPath(int width, int height, int count, ref Random rand)
    {
        List<Point64> result = new List<Point64>(count);
        for (int i = 0; i < count; ++i)
            result.Add(MakeRandomPoint1(width, height, ref rand));
        return result;
    }
    public static void GenerateRandomPolygon(Random rand, int width, int height, int count, out Polygon _subj, out Polygon _clip)
    {
        _subj = new Polygon(count, Allocator.Persistent);
        _clip = new Polygon(count, Allocator.Persistent);

        _subj.AddComponent();
        for (int i = 0; i < count; ++i)
            _subj.nodes.Add(MakeRandomPoint2(width, height, ref rand));
        _subj.ClosePolygon();

        _clip.AddComponent();
        for (int i = 0; i < count; ++i)
            _clip.nodes.Add(MakeRandomPoint2(width, height, ref rand));
        _clip.ClosePolygon();
    }
    private static double2 MakeRandomPoint2(int maxWidth, int maxHeight, ref Random rand)
    {
        long x = rand.NextInt(0, maxWidth);
        var y = rand.NextInt(0, maxHeight);
        return new double2(x, y);
    }    
}
