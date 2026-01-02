using NUnit.Framework;
using Polybool;
using System;
using Unity.Collections;

public class PolyboolPolygonTests
{	
	[Test]
	public void PolygonTests()
	{
		var subjectNodes = new NativeList<long2>(Allocator.TempJob);
		var subjectStartIDs = new NativeList<int>(Allocator.TempJob);
		var subject_openNodes = new NativeList<long2>(Allocator.TempJob);
		var subject_openStartIDs = new NativeList<int>(Allocator.TempJob);
		var clipNodes = new NativeList<long2>(Allocator.TempJob);
		var clipStartIDs = new NativeList<int>(Allocator.TempJob);

        NativeList<double> areaDiffRatioList = new NativeList<double>(Allocator.TempJob);
        NativeList<double> solutionCountList= new NativeList<double>(Allocator.TempJob);

        Intersecter polyboolIntersecter = new Intersecter(true, 4048, FillRule.EvenOdd, Allocator.TempJob);
		int testNum = 0;
		while (true)
		{
			testNum++;
			var loadingSuccess = ClipperPolyboolInterop.LoadTestNum("Assets\\Tests\\Polygons.txt", testNum,
				subjectNodes, subjectStartIDs,
				subject_openNodes, subject_openStartIDs,
				clipNodes, clipStartIDs,
				out ClipType clipType, out FillRule fillrule, out long storedArea, out int storedCount, out _);			
			if (!loadingSuccess )
			{
				Assert.IsTrue(testNum > 180, string.Format("Loading test polygon {0} failed.", testNum));
				break;
			}            

            var subject = new Polygon(subjectNodes, subjectStartIDs, false);			
			var clip = new Polygon(clipNodes, clipStartIDs, false);
            polyboolIntersecter.Reset(true, fillrule);
            var result = PolyboolClipper.Operate(subject, clip, clipType, fillrule, ref polyboolIntersecter);
			//if (testNum == 86)
			//	TextMeshDOTS.Polybool.Utils.WritePolygonToFile("solution 86.txt", result);
			var measuredArea = (long)ClipperPolyboolInterop.SignedArea(result.nodes, result.startIDs);
			var measuredCount = result.startIDs.Length - 1;

			int countDiff = storedCount > 0 ? Math.Abs(storedCount - measuredCount) : 0;
			long areaDiff = storedArea > 0 ? Math.Abs(storedArea - measuredArea) : 0;
			double areaDiffRatio = storedArea <= 0 ? 0 : (double)areaDiff / storedArea;

            areaDiffRatioList.Add(areaDiffRatio);
            solutionCountList.Add(countDiff);

            //if (IsInList(testNum, new int[] { 4, 8, 14, 21, 45, 56, 58, 64, 66, 67, 83, 85, 86, 87, 91, 92, 93, 94, 103, 106, 107, 110, 118, 119, 120, 121, 122 })) //5, 8, 14, 21,45,56,58,64,66,67,83,85
            //    continue;
            //if (storedCount > 0)
            //{
            //	if (IsInList(testNum, new int[] { 140, 150, 165, 166, 172, 173, 176, 177, 179 }))
            //	{
            //		Assert.IsTrue(countDiff <= 9, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
            //	}
            //	else if (testNum >= 120)
            //	{
            //		Assert.IsTrue(countDiff <= 6, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
            //	}
            //	else if (IsInList(testNum, new int[] { 27, 121, 126 }))
            //		Assert.IsTrue(countDiff <= 2, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
            //	else if (IsInList(testNum, new int[] { 23, 37, 43, 45, 87, 102, 111, 118, 119 }))
            //		Assert.IsTrue(countDiff <= 1, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
            //	else
            //		Assert.IsTrue(countDiff == 0, $"Failed at test {testNum}. storedCount {storedCount} / measasuredCount {measuredCount}");
            //}
            // check polygon areas
            //if (storedArea > 0)
            //{
            //	if (IsInList(testNum, new int[] { 19, 22, 23, 24 }))
            //		Assert.IsTrue(areaDiffRatio <= 0.5, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //	else if (testNum == 193)
            //		Assert.IsTrue(areaDiffRatio <= 0.25, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //	else if (testNum == 63)
            //		Assert.IsTrue(areaDiffRatio <= 0.1, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //	else if (testNum == 16)
            //		Assert.IsTrue(areaDiffRatio <= 0.075, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //	else if (IsInList(testNum, new int[] { 15, 26 }))
            //		Assert.IsTrue(areaDiffRatio <= 0.05, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //	else if (IsInList(testNum, new int[] { 52, 53, 54, 59, 60, 64, 117, 118, 119, 184 }))
            //		Assert.IsTrue(areaDiffRatio <= 0.02, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //	else
            //		Assert.IsTrue(areaDiffRatio <= 0.01, $"Failed at test {testNum}. storedArea {storedArea} / measasuredCount {measuredArea} (diff ratio: {areaDiffRatio})");
            //}
            subjectNodes.Clear();
			subjectStartIDs.Clear();
			subject_openNodes.Clear();
			subject_openStartIDs.Clear();
			clipNodes.Clear();
			clipStartIDs.Clear();
		}
        //Utils.WriteDoubleListToFile("AreaDiff.txt", areaDiffRatioList);
        //Utils.WriteDoubleListToFile("SolutonCountDiff.txt", solutionCountList);

        subjectNodes.Dispose();
		subjectStartIDs.Dispose();
		subject_openNodes.Dispose();
		subject_openStartIDs.Dispose();
		clipNodes.Dispose();
		clipStartIDs.Dispose();
        areaDiffRatioList.Dispose();
        solutionCountList.Dispose();

    }
	private static bool IsInList(int num, int[] list)
	{
		foreach (int i in list) if (i == num) return true;
		return false;
	}
}
