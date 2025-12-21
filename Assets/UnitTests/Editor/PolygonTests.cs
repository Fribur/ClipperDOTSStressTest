using Clipper2Lib;
using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Mathematics;
using Clipper2AoS;

public class PolygonTests
{    
    [Test]
    public void Clipper2Lib_TestClosedPaths()
    {
        int testNum = 0;
        while (true)
        {
            testNum++;
            Clipper64 c64 = new();
            Paths64 subj = new(), subj_open = new(), clip = new();
            Paths64 solution = new(), solution_open = new();

            if (!ClipperFileIO.LoadTestNum("Assets\\Tests\\Polygons.txt",
              testNum, subj, subj_open, clip, out Clipper2Lib.ClipType clipType, out Clipper2Lib.FillRule fillrule,
              out long storedArea, out int storedCount, out _))
            {
                Assert.IsTrue(testNum > 180, string.Format("Loading test polygon {0} failed.", testNum));
                break;
            }

            c64.AddSubject(subj);
            c64.AddOpenSubject(subj_open);
            c64.AddClip(clip);
            c64.Execute(clipType, fillrule, solution, solution_open);
            int measuredCount = solution.Count;
            long measuredArea = (long)Clipper.Area(solution);
            int countDiff = storedCount > 0 ? Math.Abs(storedCount - measuredCount) : 0;
            long areaDiff = storedArea > 0 ? Math.Abs(storedArea - measuredArea) : 0;
            double areaDiffRatio = storedArea <= 0 ? 0 : (double)areaDiff / storedArea;

            // check polygon counts
            if (storedCount > 0)
            {
                if (IsInList(testNum, new int[] { 140, 150, 165, 166, 172, 173, 176, 177, 179 }))
                {
                    Assert.IsTrue(countDiff <= 9);
                }
                else if (testNum >= 120)
                {
                    Assert.IsTrue(countDiff <= 6);
                }
                else if (IsInList(testNum, new int[] { 27, 121, 126 }))
                    Assert.IsTrue(countDiff <= 2);
                else if (IsInList(testNum, new int[] { 23, 37, 43, 45, 87, 102, 111, 118, 119 }))
                    Assert.IsTrue(countDiff <= 1);
                else
                    Assert.IsTrue(countDiff == 0);
            }

            // check polygon areas
            if (storedArea > 0)
            {
                if (IsInList(testNum, new int[] { 19, 22, 23, 24 }))
                    Assert.IsTrue(areaDiffRatio <= 0.5);
                else if (testNum == 193)
                    Assert.IsTrue(areaDiffRatio <= 0.25);
                else if (testNum == 63)
                    Assert.IsTrue(areaDiffRatio <= 0.1);
                else if (testNum == 16)
                    Assert.IsTrue(areaDiffRatio <= 0.075);
                else if (IsInList(testNum, new int[] { 15, 26 }))
                    Assert.IsTrue(areaDiffRatio <= 0.05);
                else if (IsInList(testNum, new int[] { 52, 53, 54, 59, 60, 64, 117, 118, 119, 184 }))
                    Assert.IsTrue(areaDiffRatio <= 0.02);
                else
                    Assert.IsTrue(areaDiffRatio <= 0.01);
            }

        } //bottom of num loop
    }
    [Test]
    public void Clipper2AoS_TestClosedPaths()
    {
        var openSolutionNodes = new NativeList<int2>(Allocator.TempJob);
        var openSolutionStartIDs = new NativeList<int>(Allocator.TempJob);
        var solutionNodes = new NativeList<int2>(Allocator.TempJob);
        var solutionStartIDs = new NativeList<int>(Allocator.TempJob);
        int testNum = 0;
        while (true)
        {
            testNum++;
            ClipperL c_L = new(Allocator.TempJob);
            Paths64 subj = new(), subj_open = new(), clip = new();

            if (!ClipperFileIO.LoadTestNum("Assets\\Tests\\Polygons.txt",
              testNum, subj, subj_open, clip, out Clipper2Lib.ClipType clipType, out Clipper2Lib.FillRule fillrule,
              out long storedArea, out int storedCount, out _))
            {
                Assert.IsTrue(testNum > 180, string.Format("Loading test polygon {0} failed.", testNum));
                break;
            }
            PathToNativeHelper.PathsToPolygon(subj, false, out NativeList<int2> subjNodes, out NativeList<int> subjStartIDs, Allocator.TempJob);
            PathToNativeHelper.PathsToPolygon(subj_open, true, out NativeList<int2> subj_openNodes, out NativeList<int> subj_openStartIDs, Allocator.TempJob);
            PathToNativeHelper.PathsToPolygon(clip, false, out NativeList<int2> clipNodes, out NativeList<int> clipStartIDs, Allocator.TempJob);
            var nativeFillRule = PathToNativeHelper.FillRule_ClipperToNative(fillrule);
            var nativeClipType = PathToNativeHelper.ClipType_ClipperToNative(clipType);

            c_L.AddSubject(subjNodes.AsArray(), subjStartIDs.AsArray());
            c_L.AddOpenSubject(subj_openNodes.AsArray(), subj_openStartIDs.AsArray());
            c_L.AddClip(clipNodes.AsArray(), clipStartIDs.AsArray());
            c_L.Execute(nativeClipType, nativeFillRule, ref solutionNodes, ref solutionStartIDs, ref openSolutionNodes, ref openSolutionStartIDs);
            int measuredCount = solutionStartIDs.Length - 1;
            long measuredArea = (long)PolyTreeAccessorExtensions.SignedArea(solutionNodes, solutionStartIDs);
            int countDiff = storedCount > 0 ? Math.Abs(storedCount - measuredCount) : 0;
            long areaDiff = storedArea > 0 ? Math.Abs(storedArea - measuredArea) : 0;
            double areaDiffRatio = storedArea <= 0 ? 0 : (double)areaDiff / storedArea;

            // check polygon counts
            if (storedCount > 0)
            {
                if (IsInList(testNum, new int[] { 140, 150, 165, 166, 172, 173, 176, 177, 179 }))
                {
                    Assert.IsTrue(countDiff <= 9, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
                }
                else if (testNum >= 120)
                {
                    Assert.IsTrue(countDiff <= 6, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
                }
                else if (IsInList(testNum, new int[] { 27, 121, 126 }))
                    Assert.IsTrue(countDiff <= 2, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
                else if (IsInList(testNum, new int[] { 23, 37, 43, 45, 87, 102, 111, 118, 119 }))
                    Assert.IsTrue(countDiff <= 1, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
                else
                    Assert.IsTrue(countDiff == 0, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
            }

            // check polygon areas
            if (storedArea > 0)
            {
                if (IsInList(testNum, new int[] { 19, 22, 23, 24 }))
                    Assert.IsTrue(areaDiffRatio <= 0.5, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
                else if (testNum == 193)
                    Assert.IsTrue(areaDiffRatio <= 0.25, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
                else if (testNum == 63)
                    Assert.IsTrue(areaDiffRatio <= 0.1, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
                else if (testNum == 16)
                    Assert.IsTrue(areaDiffRatio <= 0.075, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
                else if (IsInList(testNum, new int[] { 15, 26 }))
                    Assert.IsTrue(areaDiffRatio <= 0.05, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
                else if (IsInList(testNum, new int[] { 52, 53, 54, 59, 60, 64, 117, 118, 119, 184 }))
                    Assert.IsTrue(areaDiffRatio <= 0.02, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
                else
                    Assert.IsTrue(areaDiffRatio <= 0.01, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            }
            solutionNodes.Clear();
            solutionStartIDs.Clear();
            openSolutionNodes.Clear();
            openSolutionStartIDs.Clear();
            subjNodes.Dispose();
            subjStartIDs.Dispose();
            subj_openNodes.Dispose();
            subj_openStartIDs.Dispose();
            clipNodes.Dispose();
            clipStartIDs.Dispose();
            c_L.Dispose();

        } //bottom of num loop
        openSolutionNodes.Dispose();
        openSolutionStartIDs.Dispose();
        solutionNodes.Dispose();
        solutionStartIDs.Dispose();
    }
    private static bool IsInList(int num, int[] list)
    {
        foreach (int i in list) if (i == num) return true;
        return false;
    }
}