using Clipper2AoS;
using Clipper2Lib;
using NUnit.Framework;
using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class LinesTests
{
    [Test]
    public void Clipper2Lib_TestOpenPaths()
    {
        for (int i = 0; i <= 16; i++)
        {
            Clipper64 c64 = new();
            Paths64 subj = new(), subj_open = new(), clip = new();
            Paths64 solution = new(), solution_open = new();

            Assert.IsTrue(ClipperFileIO.LoadTestNum("Assets\\Tests\\Lines.txt",
              i, subj, subj_open, clip, out Clipper2Lib.ClipType clipType, out Clipper2Lib.FillRule fillrule,
              out long area, out int count, out _),
                string.Format("Loading test {0} failed.", i));

            c64.AddSubject(subj);
            c64.AddOpenSubject(subj_open);
            c64.AddClip(clip);
            c64.Execute(clipType, fillrule, solution, solution_open);

            if (area > 0)
            {
                double area2 = Clipper.Area(solution);
                double a = area / area2;
                Assert.IsTrue(a > 0.995 && a < 1.005,
                  string.Format("Incorrect area in test {0}", i));
            }

            if (count > 0 && Math.Abs(solution.Count - count) > 0)
            {
                Assert.IsTrue(Math.Abs(solution.Count - count) < 2,
                  string.Format("Incorrect count in test {0}", i));
            }

        } //bottom of num loop
    }
    [Test]
    public void Clipper2AoS_TestOpenPaths()
    {
        var openSolutionNodes = new NativeList<int2>(Allocator.TempJob);
        var openSolutionStartIDs = new NativeList<int>(Allocator.TempJob);
        var solutionNodes = new NativeList<int2>(Allocator.TempJob);
        var solutionStartIDs = new NativeList<int>(Allocator.TempJob);
        for (int i = 0; i <= 16; i++)
        {
            ClipperL c_L = new(Allocator.TempJob);
            Paths64 subj = new(), subj_open = new(), clip = new();

            Assert.IsTrue(ClipperFileIO.LoadTestNum("Assets\\Tests\\Lines.txt",
              i, subj, subj_open, clip, out Clipper2Lib.ClipType clipType, out Clipper2Lib.FillRule fillrule,
              out long area, out int count, out _),
                string.Format("Loading test {0} failed.", i));

            PathToNativeHelper.PathsToPolygon(subj, false, out NativeList<int2> subjNodes, out NativeList<int> subjStartIDs, Allocator.TempJob);
            PathToNativeHelper.PathsToPolygon(subj_open, true, out NativeList<int2> subj_openNodes, out NativeList<int> subj_openStartIDs, Allocator.TempJob);
            PathToNativeHelper.PathsToPolygon(clip, false, out NativeList<int2> clipNodes, out NativeList<int> clipStartIDs, Allocator.TempJob);
            var nativeFillRule = PathToNativeHelper.FillRule_ClipperToNative(fillrule);
            var nativeClipType = PathToNativeHelper.ClipType_ClipperToNative(clipType);

            c_L.AddSubject(subjNodes.AsArray(), subjStartIDs.AsArray());
            c_L.AddOpenSubject(subj_openNodes.AsArray(), subj_openStartIDs.AsArray());
            c_L.AddClip(clipNodes.AsArray(), clipStartIDs.AsArray());
            c_L.Execute(nativeClipType, nativeFillRule, ref solutionNodes, ref solutionStartIDs, ref openSolutionNodes, ref openSolutionStartIDs);

            if (area > 0)
            {
                double area2 = (long)PathToNativeHelper.SignedArea(solutionNodes, solutionStartIDs);
                double a = area / area2;
                Assert.IsTrue(a > 0.995 && a < 1.005,
                  string.Format("Incorrect area in test {0}", i));
            }
            var solutionCount = solutionStartIDs.Length - 1;
            if (count > 0 && Math.Abs(solutionCount - count) > 0)
            {
                Assert.IsTrue(Math.Abs(solutionCount - count) < 2,
                  string.Format("Incorrect count in test {0}", i));
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
}