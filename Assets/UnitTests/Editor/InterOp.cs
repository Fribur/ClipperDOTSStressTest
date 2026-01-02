using Polybool;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Unity.Collections;

internal static class ClipperPolyboolInterop
{
	public static void PathFromStr(string s, NativeList<long2> nodes, NativeList<int> startIDs)
	{		
		int len = s.Length, i = 0;
		while (i < len)
		{
			while (s[i] < 33 && i < len) i++;
			if (i >= len) break;
			//get X ...
			bool isNeg = s[i] == 45;
			if (isNeg) i++;
			if (i >= len || s[i] < 48 || s[i] > 57) break;
			int j = i + 1;
			while (j < len && s[j] > 47 && s[j] < 58) j++;
			if (!long.TryParse(s.Substring(i, j - i), out long x)) break;
			if (isNeg) x = -x;
			//skip space or comma between X & Y ...
			i = j;
			while (i < len && (s[i] == 32 || s[i] == 44)) i++;
			//get Y ...
			if (i >= len) break;
			isNeg = s[i] == 45;
			if (isNeg) i++;
			if (i >= len || s[i] < 48 || s[i] > 57) break;
			j = i + 1;
			while (j < len && s[j] > 47 && s[j] < 58) j++;
			if (!long.TryParse(s.Substring(i, j - i), out long y)) break;
			if (isNeg) y = -y;
			nodes.Add(new long2(x, y));
			//skip trailing space, comma ...
			i = j;
			int nlCnt = 0;
			while (i < len && (s[i] < 33 || s[i] == 44))
			{
				if (i >= len) break;
				if (s[i] == 10)
				{
					nlCnt++;
					if (nlCnt == 2)
					{
						if (nodes.Length > startIDs[^1])
						{
							if(nodes[startIDs[^1]] != nodes[^1])
								nodes.Add(nodes[startIDs[^1]]);//add start point to close polygon
							startIDs.Add(nodes.Length);
						}
					}
				}
				i++;
			}
		}
		if (nodes.Length > startIDs[^1]) //close Polgyon
		{
			nodes.Add(nodes[startIDs[^1]]);//add start point to close polygon
			startIDs.Add(nodes.Length);
		}
	}
	public static bool LoadTestNum(string filename, int num,
	  NativeList<long2> subject, NativeList<int> subjectStartIDs, 
	  NativeList<long2> subject_open, NativeList<int> subject_openStartIDs,
	  NativeList<long2> clip, NativeList<int> clipStartIDs,
	  out ClipType ct, out FillRule fillRule, out long area, out int count, out string caption)
	{
		subject.Clear();
		subjectStartIDs.Clear();
		subjectStartIDs.Add(0);
		subject_open.Clear();
		subject_openStartIDs.Clear();
		subject_openStartIDs.Add(0);
		clip.Clear();
		clipStartIDs.Clear();
		clipStartIDs.Add(0);
		ct = ClipType.Intersection;
		fillRule = FillRule.EvenOdd;
		bool result = false;
		if (num < 1) num = 1;
		caption = "";
		area = 0;
		count = 0;
		StreamReader reader;

		NativeList<long2> nodes = new NativeList<long2>(Allocator.Temp);
		NativeList<int> startIDs = new NativeList<int>(Allocator.Temp);
		startIDs.Add(0);
		try
		{
			reader = new StreamReader(filename);
		}
		catch
		{
			return false;
		}
		while (true)
		{
			string? s = reader.ReadLine();
			if (s == null) break;

			if (s.IndexOf("CAPTION: ", StringComparison.Ordinal) == 0)
			{
				num--;
				if (num != 0) continue;
				caption = s.Substring(9);
				result = true;
				continue;
			}

			if (num > 0) continue;

			if (s.IndexOf("CLIPTYPE: ", StringComparison.Ordinal) == 0)
			{
				if (s.IndexOf("INTERSECTION", StringComparison.Ordinal) > 0) ct = ClipType.Intersection;
				else if (s.IndexOf("UNION", StringComparison.Ordinal) > 0) ct = ClipType.Union;
				else if (s.IndexOf("DIFFERENCE", StringComparison.Ordinal) > 0) ct = ClipType.Difference;
				else ct = ClipType.Xor;
				continue;
			}

			if (s.IndexOf("FILLTYPE: ", StringComparison.Ordinal) == 0 ||
				s.IndexOf("FILLRULE: ", StringComparison.Ordinal) == 0)
			{
				if (s.IndexOf("EVENODD", StringComparison.Ordinal) > 0) fillRule = FillRule.EvenOdd;
				else if (s.IndexOf("POSITIVE", StringComparison.Ordinal) > 0) fillRule = FillRule.Positive;
				else if (s.IndexOf("NEGATIVE", StringComparison.Ordinal) > 0) fillRule = FillRule.Negative;
				else fillRule = FillRule.NonZero;
				continue;
			}

			if (s.IndexOf("SOL_AREA: ", StringComparison.Ordinal) == 0)
			{
				area = long.Parse(s.Substring(10));
				continue;
			}

			if (s.IndexOf("SOL_COUNT: ", StringComparison.Ordinal) == 0)
			{
				count = int.Parse(s.Substring(11));
				continue;
			}

			int GetIdx;
			if (s.IndexOf("SUBJECTS_OPEN", StringComparison.Ordinal) == 0) GetIdx = 2;
			else if (s.IndexOf("SUBJECTS", StringComparison.Ordinal) == 0) GetIdx = 1;
			else if (s.IndexOf("CLIPS", StringComparison.Ordinal) == 0) GetIdx = 3;
			else continue;

			while (true)
			{
				s = reader.ReadLine();
				if (s == null) break;
				PathFromStr(s, nodes, startIDs); //0 or 1 path
				if (nodes.Length == 0)
				{
					if (GetIdx == 3) return result;
					if (s.IndexOf("SUBJECTS_OPEN", StringComparison.Ordinal) == 0) GetIdx = 2;
					else if (s.IndexOf("CLIPS", StringComparison.Ordinal) == 0) GetIdx = 3;
					else return result;
					continue;
				}
				int startIndex;
				switch (GetIdx)
				{
					case 1:
						subject.AddRange(nodes.AsArray());
						startIndex = subjectStartIDs[^1];
						//for (int i = 0, ii=startIDs.Length; i<ii; i++)
							subjectStartIDs.Add(startIndex + startIDs[1]);
						nodes.Clear();
						startIDs.Clear();
						startIDs.Add(0);
						break;
					case 2:
						subject_open.AddRange(nodes.AsArray());
						startIndex = subject_openStartIDs[^1];
						//for (int i = 0, ii = startIDs.Length; i < ii; i++)
							subject_openStartIDs.Add(startIndex + startIDs[1]);
						nodes.Clear();
						startIDs.Clear();
						startIDs.Add(0);
						break;
					default:
						clip.AddRange(nodes.AsArray());
						startIndex = clipStartIDs[^1];
						//for (int i = 0, ii = startIDs.Length; i < ii; i++)
							clipStartIDs.Add(startIndex + startIDs[1]);
						nodes.Clear();
						startIDs.Clear();
						startIDs.Add(0);
						break;
				}
			}
		}
		return result;
	}
	/// <summary>
	/// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double SignedArea(NativeList<long2> data, int start, int end)
	{
		double area = default;
		for (int i = start, prev = end - 1; i < end; prev = i++) //from (0, prev) until (end, prev)
			area += ((double)data[prev].x - (double)data[i].x) * ((double)data[i].y + (double)data[prev].y);
		return area * 0.5;
	}
	/// <summary>
	/// positive area = CCW, negative area = CW (works for closed and open polygon (identical result))
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static double SignedArea(NativeList<long2> nodes, NativeList<int> startIDs)
	{
		double area = 0;
		for (int k = 0, length = startIDs.Length - 1; k < length; k++)
		{
			int start = startIDs[k];
			int end = startIDs[k + 1];
			area += SignedArea(nodes, start, end);
			//Debug.Log($"Area: {area}");
		}
		return area;
	}
	public static Polybool.ClipType ClipType_ClipperToPolybool(ClipType clipType)
	{
		switch (clipType)
		{
			case ClipType.NoClip: return Polybool.ClipType.NoClip;
			case ClipType.Intersection: return Polybool.ClipType.Intersection;
			case ClipType.Union: return Polybool.ClipType.Union;
			case ClipType.Difference: return Polybool.ClipType.Difference;
			case ClipType.Xor: return Polybool.ClipType.Xor;
			default: return Polybool.ClipType.NoClip;
		}
	}
	public static Polybool.FillRule FillRule_ClipperToPolybool(FillRule clipType)
	{
		switch (clipType)
		{
			case FillRule.NonZero: return Polybool.FillRule.NonZero;
			case FillRule.Positive: return Polybool.FillRule.Positive;
			case FillRule.Negative: return Polybool.FillRule.Negative;
			case FillRule.EvenOdd: return Polybool.FillRule.EvenOdd;
			default: return Polybool.FillRule.NonZero;
		}
	}

	//public static Paths64 PolygonToPaths(this Polygon polygon)
	//{
	//	var nodes = polygon.nodes;
	//	var startIDs = polygon.startIDs;
	//	var paths = new Paths64();
	//	for (int i = 0, length = startIDs.Count - 1; i < length; i++)
	//	{
	//		int start = startIDs[i];
	//		int end = startIDs[i + 1];
	//		var path = new Path64(end - start);
	//		for (int j = start; j < end; j++)
	//			path.Add(new Point64(nodes[j].x, nodes[j].y));
	//		paths.Add(path);
	//	}
	//	return paths;
	//}

	//static void AddPathToPolygon(List<Point64> path, bool isOpen, Polygon polygon)
	//{
	//	var nodes = polygon.nodes;
	//	polygon.startIDs.Add(nodes.Count);
	//	var end = path.Count;
	//	for (int k = 0; k < end; k++)
	//		nodes.Add(new long2(path[k].X, path[k].Y));
	//	if (!isOpen && path[0] != path[end - 1])
	//		nodes.Add(new long2(path[0].X, path[0].Y));
	//}
	//public static void PathsToPolygon(this Paths64 paths, bool isOpen, out Polygon polygon)
	//{
	//	polygon = new Polygon(256, paths.Count, false);
	//	for (int i = 0, length = paths.Count; i < length; i++)
	//	{
	//		var path = paths[i];
	//		AddPathToPolygon(path, isOpen, polygon);
	//	}
	//	polygon.startIDs.Add(polygon.nodes.Count);//close Polygon
	//}
}
