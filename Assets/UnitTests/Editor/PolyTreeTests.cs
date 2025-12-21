using Chart3D.MathExtensions;
using Clipper2AoS;
using Clipper2Lib;
using NUnit.Framework;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;

public class TestPolytree
{
    [Test]
    public void Clipper2AoS_TestPolytree2()
    {
        Paths64 subj = new(), subj_open = new(), clip = new();

        Assert.IsTrue(ClipperFileIO.LoadTestNum("Assets\\Tests\\PolytreeHoleOwner2.txt",
            1, subj, subj_open, clip, out Clipper2Lib.ClipType clipType, out Clipper2Lib.FillRule fillrule,
            out _, out _, out _),
            "Unable to read PolytreeHoleOwner2.txt");

        PolyTree solutionTree = new(16, Allocator.TempJob);
        var openSolutionNodes = new NativeList<int2>(Allocator.TempJob);
        var openSolutionStartIDs = new NativeList<int>(Allocator.TempJob);
        var solutionNodes = new NativeList<int2>(Allocator.TempJob);
        var solutionStartIDs = new NativeList<int>(Allocator.TempJob);
        ClipperL c_L = new(Allocator.TempJob);

        Path64 pointsOfInterestOutside = new()
        {
            new Point64(21887, 10420),
            new Point64(21726, 10825),
            new Point64(21662, 10845),
            new Point64(21617, 10890)
        };

        foreach (Point64 pt in pointsOfInterestOutside)
        {
            foreach (Path64 path in subj)
            {
                Assert.IsTrue(Clipper.PointInPolygon(pt, path) == Clipper2Lib.PointInPolygonResult.IsOutside,
                    "outside point of interest found inside subject");
            }
        }

        Path64 pointsOfInterestInside = new()
        {
            new Point64(21887, 10430),
            new Point64(21843, 10520),
            new Point64(21810, 10686),
            new Point64(21900, 10461)
        };

        foreach (Point64 pt in pointsOfInterestInside)
        {
            int poi_inside_counter = 0;
            foreach (Path64 path in subj)
            {
                if (Clipper.PointInPolygon(pt, path) == Clipper2Lib.PointInPolygonResult.IsInside)
                    poi_inside_counter++;
            }
            Assert.IsTrue(poi_inside_counter == 1,
                string.Format("poi_inside_counter - expected 1 but got {0}", poi_inside_counter));
        }

        PathToNativeHelper.PathsToPolygon(subj, false, out NativeList<int2> subjNodes, out NativeList<int> subjStartIDs, Allocator.TempJob);
        PathToNativeHelper.PathsToPolygon(subj_open, true, out NativeList<int2> subj_openNodes, out NativeList<int> subj_openStartIDs, Allocator.TempJob);
        PathToNativeHelper.PathsToPolygon(clip, false, out NativeList<int2> clipNodes, out NativeList<int> clipStartIDs, Allocator.TempJob);
        var nativeFillRule = PathToNativeHelper.FillRule_ClipperToNative(fillrule);
        var nativeClipType = PathToNativeHelper.ClipType_ClipperToNative(clipType);

        c_L.AddSubject(subjNodes.AsArray(), subjStartIDs.AsArray());
        c_L.AddOpenSubject(subj_openNodes.AsArray(), subj_openStartIDs.AsArray());
        c_L.AddClip(clipNodes.AsArray(), clipStartIDs.AsArray());
        c_L.Execute(nativeClipType, nativeFillRule, ref solutionTree, ref openSolutionNodes, ref openSolutionStartIDs);
        //c_L.TraverseDepthFirst_WritePolytreeToFile(ref solutionTree);

        //double dummy = 0;
        //PolyTreeAccessorDelegates.PolyTree_GetSolution_DepthFirst(ref solutionTree, ref c_L, ref solutionNodes, ref solutionStartIDs, ref dummy, PolyTreeAccessorDelegates.BuildPathInvoke);
        c_L.PolyTree_GetSolution_DepthFirst(ref solutionTree, ref solutionNodes, ref solutionStartIDs);
        double a1 = PolyTreeAccessorExtensions.SignedArea(solutionNodes, solutionStartIDs);
        //double a2 = 0;
        //PolyTreeAccessorDelegates.PolyTree_GetSolution_DepthFirst(ref solutionTree, ref c_L, ref solutionNodes, ref solutionStartIDs, ref a2, PolyTreeAccessorDelegates.AreaInvoke);

        Assert.IsTrue(a1 > 330000,
            string.Format("solution has wrong area - value expected: 331,052; value returned; {0} ", a1));

        //Assert.IsTrue(Math.Abs(a1 - a2) < 0.0001,
        //    string.Format("solution tree has wrong area - value expected: {0}; value returned; {1} ", a1, a2));

        //int dummyInt = 0;
        //Assert.IsTrue(PolyTreeAccessorDelegates.PolyTree_ForAllExteriorNodes(ref solutionTree, ref c_L, new long2(),ref dummyInt, PolyTreeAccessorDelegates.ContainsChildrenInvoke),
        //    "The polytree doesn't properly contain its children");
        Assert.IsTrue(c_L.CheckPolytreeFullyContainsChildren(ref solutionTree),
            "The polytree doesn't properly contain its children");

        foreach (Point64 pt in pointsOfInterestOutside)
        {
            int counter = 0;
            //PolyTreeAccessorDelegates.PolyTree_ForAllExteriorNodes(ref solutionTree, ref c_L, new long2(pt.X, pt.Y), ref counter, PolyTreeAccessorDelegates.ContainsPointInvoke);
            c_L.PolytreeContainsPoint(ref solutionTree, new long2(pt.X, pt.Y), ref counter);
            Assert.IsTrue(counter >= 0, $"Polytree has too many holes: {counter}");
            Assert.IsFalse(counter != 0, "The polytree indicates it contains a point that it should not contain");
        }

        foreach (Point64 pt in pointsOfInterestInside)
        {
            int counter = 0;
            //PolyTreeAccessorDelegates.PolyTree_ForAllExteriorNodes(ref solutionTree, ref c_L, new long2(pt.X, pt.Y), ref counter, PolyTreeAccessorDelegates.ContainsPointInvoke);
            c_L.PolytreeContainsPoint(ref solutionTree, new long2(pt.X, pt.Y), ref counter);
            Assert.IsTrue(counter >= 0, $"Polytree has too many holes: {counter}");
            Assert.IsTrue(counter != 0, "The polytree indicates it does not contain a point that it should contain");
        }

        solutionTree.Dispose();
        solutionNodes.Dispose();
        solutionStartIDs.Dispose();
        openSolutionNodes.Dispose();
        openSolutionStartIDs.Dispose();
        subjNodes.Dispose();
        subjStartIDs.Dispose();
        subj_openNodes.Dispose();
        subj_openStartIDs.Dispose();
        clipNodes.Dispose();
        clipStartIDs.Dispose();
        c_L.Dispose();

    }
    [Test]
    public void Clipper2AoS_TestPolytree3()
    {
        Paths64 subj = new()
    {
    Clipper.MakePath(new int[] {1588700, -8717600,
    1616200, -8474800, 1588700, -8474800 }),
    Clipper.MakePath(new int[] { 13583800,-15601600,
    13582800,-15508500, 13555300,-15508500, 13555500,-15182200,
    13010900,-15185400 }),
    Clipper.MakePath(new int[] { 956700, -3092300, 1152600,
    3147400, 25600, 3151700 }),
    Clipper.MakePath(new int[] {
    22575900,-16604000, 31286800,-12171900,
    31110200,4882800, 30996200,4826300, 30414400,5447400, 30260000,5391500,
    29662200,5805400, 28844500,5337900, 28435000,5789300, 27721400,5026400,
    22876300,5034300, 21977700,4414900, 21148000,4654700, 20917600,4653400,
    19334300,12411000, -2591700,12177200, 53200,3151100, -2564300,12149800,
    7819400,4692400, 10116000,5228600, 6975500,3120100, 7379700,3124700,
    11037900,596200, 12257000,2587800, 12257000,596200, 15227300,2352700,
    18444400,1112100, 19961100,5549400, 20173200,5078600, 20330000,5079300,
    20970200,4544300, 20989600,4563700, 19465500,1112100, 21611600,4182100,
    22925100,1112200, 22952700,1637200, 23059000,1112200, 24908100,4181200,
    27070100,3800600, 27238000,3800700, 28582200,520300, 29367800,1050100,
    29291400,179400, 29133700,360700, 29056700,312600, 29121900,332500,
    29269900,162300, 28941400,213100, 27491300,-3041500, 27588700,-2997800,
    22104900,-16142800, 13010900,-15603000, 13555500,-15182200,
    13555300,-15508500, 13582800,-15508500, 13583100,-15154700,
    1588700,-8822800, 1588700,-8379900, 1588700,-8474800, 1616200,-8474800,
    1003900,-630100, 1253300,-12284500, 12983400,-16239900}),
    Clipper.MakePath(new int[] { 198200, 12149800, 1010600, 12149800, 1011500, 11859600 }),
    Clipper.MakePath(new int[] { 21996700, -7432000, 22096700, -7432000, 22096700, -7332000 })
    };

        ClipperL c_L = new(Allocator.TempJob);
        PolyTree solutionTree = new(16, Allocator.TempJob);
        var openSolutionNodes = new NativeList<int2>(Allocator.TempJob);
        var openSolutionStartIDs = new NativeList<int>(Allocator.TempJob);

        PathToNativeHelper.PathsToPolygon(subj, false, out NativeList<int2> subjNodes, out NativeList<int> subjStartIDs, Allocator.TempJob);
        c_L.AddSubject(subjNodes.AsArray(), subjStartIDs.AsArray());
        c_L.Execute(Clipper2AoS.ClipType.Union, Clipper2AoS.FillRule.NonZero, ref solutionTree, ref openSolutionNodes, ref openSolutionStartIDs);
        //c_L.TraverseDepthFirst_WritePolytreeToFile(ref solutionTree);

        var nodes = solutionTree.nodes;
        //Assert.IsTrue(solutionTree.Count == 1 && solutionTree[0].Count == 2
        //    && solutionTree[0][1].Count == 1, "Incorrect PolyTree nesting.");

        var node0 = nodes[solutionTree.root];
        var node0_1 = nodes[node0.firstChild];
        var node0_1_1 =  nodes[node0_1.firstChild];
        var node0_1_2 = nodes[node0_1_1.nextSibling];
        Assert.IsTrue(node0.childCount == 1 && node0_1.childCount == 2
            && node0_1_2.childCount == 1, "Incorrect PolyTree nesting.");

        solutionTree.Dispose();
        openSolutionNodes.Dispose();
        openSolutionStartIDs.Dispose();
        subjNodes.Dispose();
        subjStartIDs.Dispose();
        c_L.Dispose();
    } // end TESTMETHOD TestPolytree3

    [Test]
    public void Clipper2Lib_TestPolytree2()
    {
        Paths64 subject = new(), subjectOpen = new(), clip = new();

        Assert.IsTrue(ClipperFileIO.LoadTestNum("Assets\\Tests\\PolytreeHoleOwner2.txt",
            1, subject, subjectOpen, clip, out Clipper2Lib.ClipType cliptype, out Clipper2Lib.FillRule fillrule,
            out _, out _, out _),
            "Unable to read PolytreeHoleOwner2.txt");

        PolyTree64 solutionTree = new();
        Paths64 solution_open = new();
        Clipper64 clipper = new();

        Path64 pointsOfInterestOutside = new()
    {
    new Point64(21887, 10420),
    new Point64(21726, 10825),
    new Point64(21662, 10845),
    new Point64(21617, 10890)
    };

        foreach (Point64 pt in pointsOfInterestOutside)
        {
            foreach (Path64 path in subject)
            {
                Assert.IsTrue(Clipper.PointInPolygon(pt, path) == Clipper2Lib.PointInPolygonResult.IsOutside,
                    "outside point of interest found inside subject");
            }
        }

        Path64 pointsOfInterestInside = new()
    {
    new Point64(21887, 10430),
    new Point64(21843, 10520),
    new Point64(21810, 10686),
    new Point64(21900, 10461)
    };

        foreach (Point64 pt in pointsOfInterestInside)
        {
            int poi_inside_counter = 0;
            foreach (Path64 path in subject)
            {
                if (Clipper.PointInPolygon(pt, path) == Clipper2Lib.PointInPolygonResult.IsInside)
                    poi_inside_counter++;
            }
            Assert.IsTrue(poi_inside_counter == 1,
                string.Format("poi_inside_counter - expected 1 but got {0}", poi_inside_counter));
        }

        clipper.AddSubject(subject);
        clipper.AddOpenSubject(subjectOpen);
        clipper.AddClip(clip);
        clipper.Execute(cliptype, fillrule, solutionTree, solution_open);
        //WritePolytreeToFile(solutionTree);

        Paths64 solutionPaths = Clipper.PolyTreeToPaths64(solutionTree);
        double a1 = Clipper.Area(solutionPaths), a2 = solutionTree.Area();

        Assert.IsTrue(a1 > 330000,
            string.Format("solution has wrong area - value expected: 331,052; value returned; {0} ", a1));

        Assert.IsTrue(Math.Abs(a1 - a2) < 0.0001,
            string.Format("solution tree has wrong area - value expected: {0}; value returned; {1} ", a1, a2));

        Assert.IsTrue(CheckPolytreeFullyContainsChildren(solutionTree),"The polytree doesn't properly contain its children");

        foreach (Point64 pt in pointsOfInterestOutside)
            Assert.IsFalse(PolytreeContainsPoint(solutionTree, pt),
                "The polytree indicates it contains a point that it should not contain");

        foreach (Point64 pt in pointsOfInterestInside)
            Assert.IsTrue(PolytreeContainsPoint(solutionTree, pt),
                "The polytree indicates it does not contain a point that it should contain");
    }

    [Test]
    public void Clipper2Lib_TestPolytree3()
    {
        Paths64 subject = new()
    {
    Clipper.MakePath(new int[] {1588700, -8717600,
    1616200, -8474800, 1588700, -8474800 }),
    Clipper.MakePath(new int[] { 13583800,-15601600,
    13582800,-15508500, 13555300,-15508500, 13555500,-15182200,
    13010900,-15185400 }),
    Clipper.MakePath(new int[] { 956700, -3092300, 1152600,
    3147400, 25600, 3151700 }),
    Clipper.MakePath(new int[] {
    22575900,-16604000, 31286800,-12171900,
    31110200,4882800, 30996200,4826300, 30414400,5447400, 30260000,5391500,
    29662200,5805400, 28844500,5337900, 28435000,5789300, 27721400,5026400,
    22876300,5034300, 21977700,4414900, 21148000,4654700, 20917600,4653400,
    19334300,12411000, -2591700,12177200, 53200,3151100, -2564300,12149800,
    7819400,4692400, 10116000,5228600, 6975500,3120100, 7379700,3124700,
    11037900,596200, 12257000,2587800, 12257000,596200, 15227300,2352700,
    18444400,1112100, 19961100,5549400, 20173200,5078600, 20330000,5079300,
    20970200,4544300, 20989600,4563700, 19465500,1112100, 21611600,4182100,
    22925100,1112200, 22952700,1637200, 23059000,1112200, 24908100,4181200,
    27070100,3800600, 27238000,3800700, 28582200,520300, 29367800,1050100,
    29291400,179400, 29133700,360700, 29056700,312600, 29121900,332500,
    29269900,162300, 28941400,213100, 27491300,-3041500, 27588700,-2997800,
    22104900,-16142800, 13010900,-15603000, 13555500,-15182200,
    13555300,-15508500, 13582800,-15508500, 13583100,-15154700,
    1588700,-8822800, 1588700,-8379900, 1588700,-8474800, 1616200,-8474800,
    1003900,-630100, 1253300,-12284500, 12983400,-16239900}),
    Clipper.MakePath(new int[] { 198200, 12149800, 1010600, 12149800, 1011500, 11859600 }),
    Clipper.MakePath(new int[] { 21996700, -7432000, 22096700, -7432000, 22096700, -7332000 })
    };
        PolyTree64 solutionTree = new();

        Clipper64 clipper = new();
        clipper.AddSubject(subject);
        clipper.Execute(Clipper2Lib.ClipType.Union, Clipper2Lib.FillRule.NonZero, solutionTree);
        //WritePolytreeToFile(solutionTree);

        Assert.IsTrue(solutionTree.Count == 1 && solutionTree[0].Count == 2
            && solutionTree[0][1].Count == 1, "Incorrect PolyTree nesting.");


    } // end TESTMETHOD TestPolytree3
    private static void PolyPathContainsPoint(PolyPath64 pp, Point64 pt, ref int counter)
    {
        if (Clipper.PointInPolygon(pt, pp.Polygon!) != Clipper2Lib.PointInPolygonResult.IsOutside)
        {
            if (pp.IsHole) --counter; else ++counter;
        }
        for (int i = 0; i < pp.Count; i++)
        {
            PolyPath64 child = (PolyPath64)pp[i];
            PolyPathContainsPoint(child, pt, ref counter);
        }
    }    

    private static bool PolyPathFullyContainsChildren(PolyPath64 pp)
    {
        foreach (PolyPath64 child in pp.Cast<PolyPath64>())
        {
            foreach (Point64 pt in child.Polygon!)
                if (Clipper.PointInPolygon(pt, pp.Polygon!) == Clipper2Lib.PointInPolygonResult.IsOutside)
                    return false;
            if (child.Count > 0 && !PolyPathFullyContainsChildren(child))
                return false;
        }
        return true;
    }

    private static bool PolytreeContainsPoint(PolyTree64 polytree, Point64 pt)
    {
        int counter = 0;
        for (int i = 0; i < polytree.Count; i++)
        {
            PolyPath64 child = polytree[i];
            PolyPathContainsPoint(child, pt, ref counter);
        }
        Assert.IsTrue(counter >= 0, $"Polytree has too many holes: {counter}");
        return counter != 0;
    }
    private static bool CheckPolytreeFullyContainsChildren(PolyTree64 polytree)
    {
        for (int i = 0; i < polytree.Count; i++)
        {
            PolyPath64 child = polytree[i];
            if (child.Count > 0 && !PolyPathFullyContainsChildren(child))
                return false;
        }
        return true;
    }
    

    //private static bool WritePolytreeToFile(PolyTree64 polytree)
    //{
    //    StreamWriter writer = new StreamWriter("polytree_correct.txt", false);
    //    PolyPathBase parent;
    //    PolyPathBase parentParent;
    //    PolyPathBase parentParentParent;
    //    for (int i = 0; i < polytree.Count; i++)
    //    {
    //        PolyPath64 node = (PolyPath64)polytree[i];
    //        var level = node.Level;
    //        switch (level)
    //        {
    //            case 0:
    //                //writer.WriteLine($"0000\n");
    //                break;
    //            case 1:
    //                writer.WriteLine($"{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            case 2:
    //                parent = node._parent;
    //                writer.WriteLine($"{parent.outrecIdx:D3}__{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            case 3:
    //                parent = node._parent;
    //                parentParent = parent._parent;
    //                parentParentParent = parentParent._parent;
    //                writer.WriteLine($"{parentParent.outrecIdx:D3}__{parent.outrecIdx:D3}__{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            case 4:
    //                parent = node._parent;
    //                parentParent = parent._parent;
    //                parentParentParent = parentParent._parent;
    //                writer.WriteLine($"{parentParentParent.outrecIdx:D3}__{parentParent.outrecIdx:D3}__{parent.outrecIdx:D3}__{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            default:
    //                writer.WriteLine($"{node.outrecIdx:D3} (level: {level} ({node.Polygon.Count} nodes)");
    //                break;
    //        }
    //        WritePolytreeToFileInner(node, writer);
    //    }
    //    writer.Close();
    //    return true;
    //}
    //private static void WritePolytreeToFileInner(PolyPath64 pp, StreamWriter writer)
    //{
    //    PolyPathBase parent;
    //    PolyPathBase parentParent;
    //    PolyPathBase parentParentParent;
    //    foreach (PolyPath64 node in pp.Cast<PolyPath64>())
    //    {
    //        var level = node.Level;
    //        switch (level)
    //        {
    //            case 0:
    //                //writer.WriteLine($"0000\n");
    //                break;
    //            case 1:
    //                writer.WriteLine($"{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            case 2:
    //                parent = node._parent;
    //                writer.WriteLine($"{parent.outrecIdx:D3}__{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            case 3:
    //                parent = node._parent;
    //                parentParent = parent._parent;
    //                parentParentParent = parentParent._parent;
    //                writer.WriteLine($"{parentParent.outrecIdx:D3}__{parent.outrecIdx:D3}__{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            case 4:
    //                parent = node._parent;
    //                parentParent = parent._parent;
    //                parentParentParent = parentParent._parent;
    //                writer.WriteLine($"{parentParentParent.outrecIdx:D3}__{parentParent.outrecIdx:D3}__{parent.outrecIdx:D3}__{node.outrecIdx:D3} ({node.Polygon.Count} nodes)");
    //                break;
    //            default:
    //                writer.WriteLine($"{node.outrecIdx:D3} (level: {level} ({node.Polygon.Count} nodes)");
    //                break;
    //        }
    //        WritePolytreeToFileInner(node, writer);
    //    }
    //}

} // end TestClass
