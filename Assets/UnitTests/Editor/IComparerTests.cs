using NUnit.Framework;
using Polybool;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

public class IComparerTests
{
	[Test]
	public void StatusQueueComparer_Events01()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(20, 120), new long2(90, 120), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(50, 90), new long2(55, 30), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(36.25, 60), new long2(75, 60), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(30, 10), new long2(100, 10), startParamPoint, endParamPoint, true,true)
		};
		var statusQueueComparer = new StatusQueueComparer(segments);

		var expected = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
		};

		var actual = new NativeList<EventBool>(16, Allocator.Temp)
		{
            //expected[1],
            //expected[2],
            //expected[0],
            //expected[3],

            expected[0],
			expected[2],
			expected[3],
		};

		// Act
		//actual.Sort(eventSegmentComparer);
		var eventIndexInEvents = actual.BinarySearch(expected[1], statusQueueComparer);
		eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;
		actual.InsertRange(eventIndexInEvents, 1);
		actual[eventIndexInEvents] = expected[1];

		// Assert
		for (int i = 0; i < expected.Length; i++)
			Assert.AreEqual(expected[i], actual[i]);
	}
	[Test]
	public void StatusQueueComparer_Events02()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(10.7464456558228, 41.6892700195313), new long2(14.0866765975952, 43.9831390380859), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(11.5650564562206, 26.9340842967891), new long2(12.9574165344238, 28.3071022033691), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(11.3999061584473, 19.153974444502), new long2(11.6985530853271, 20.0355453491211), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(11.5650564562206, 26.9340842967891), new long2(11.5676536560059, 26.9832744598389), startParamPoint, endParamPoint, true,true),
		};
		var statusQueueComparer = new StatusQueueComparer(segments);

		var statusQueue = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
		};
		var newEvent = new EventBool(true, 3);


		// Act
		var eventIndexInEvents = statusQueue.BinarySearch(newEvent, statusQueueComparer);
		eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;

		// Assert
		Assert.AreEqual(1, eventIndexInEvents);
	}
	[Test]
	public void SweepStatusRandomLinesSort()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0.548, 0.9443), new long2(0.7455, 0.8477), startParamPoint, endParamPoint, true, true), // 0
            new Segment(new long2(0.548, 0.9443), new long2(0.7438, 0.6878), startParamPoint, endParamPoint, true, true), // 1
            new Segment(new long2(0.5334, 0.945), new long2(0.7438, 0.6878), startParamPoint, endParamPoint, true, true), // 2
            new Segment(new long2(0.5122, 0.9013), new long2(0.6366, 0.9417), startParamPoint, endParamPoint, true, true),// 3
            new Segment(new long2(0.5732, 0.706), new long2(0.7169, 0.6187), startParamPoint, endParamPoint, true, true), // 4
            new Segment(new long2(0.5658, 0.6475), new long2(0.7169, 0.6187), startParamPoint, endParamPoint, true, true),// 5
            new Segment(new long2(0.5658, 0.6475), new long2(0.6321, 0.6197), startParamPoint, endParamPoint, true, true),// 6
            new Segment(new long2(0.4867, 0.5998), new long2(0.6095, 0.6079), startParamPoint, endParamPoint, true, true),// 7
            new Segment(new long2(0.4767, 0.5368), new long2(0.6615, 0.525), startParamPoint, endParamPoint, true, true), // 8
            new Segment(new long2(0.5824, 0.5157), new long2(0.9235, 0.3918), startParamPoint, endParamPoint, true, true),// 9
            new Segment(new long2(0.5824, 0.5157), new long2(0.9756, 0.3288), startParamPoint, endParamPoint, true, true),//10
            new Segment(new long2(0.4127, 0.4774), new long2(0.6615, 0.525), startParamPoint, endParamPoint, true, true), //11
            new Segment(new long2(0.4771, 0.4543), new long2(0.5913, 0.411), startParamPoint, endParamPoint, true, true), //12
            new Segment(new long2(0.5218, 0.2095), new long2(0.5839, 0.1114), startParamPoint, endParamPoint, true, true),//13
            new Segment(new long2(0.4544, 0.0837), new long2(0.6762, 0.4073), startParamPoint, endParamPoint, true, true),//14
            new Segment(new long2(0.4544, 0.0837), new long2(0.7364, 0.3326), startParamPoint, endParamPoint, true, true),//15
        };
		var eventSegmentComparer = new StatusQueueComparer(segments);

		var expected = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
			new EventBool(true, 4),
			new EventBool(true, 5),
			new EventBool(true, 6),
			new EventBool(true, 7),
			new EventBool(true, 8),
			new EventBool(true, 9),
			new EventBool(true, 10),
			new EventBool(true, 11),
			new EventBool(true, 12),
			new EventBool(true, 13),
			new EventBool(true, 14),
			new EventBool(true, 15),
		};

		var actual = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 9),
			new EventBool(true, 10),
			new EventBool(true, 4),
			new EventBool(true, 6),
			new EventBool(true, 5),
			new EventBool(true, 1),
			new EventBool(true, 0),
			new EventBool(true, 2),
			new EventBool(true, 13),
			new EventBool(true, 3),
			new EventBool(true, 7),
			new EventBool(true, 12),
			new EventBool(true, 8),
			new EventBool(true, 14),
			new EventBool(true, 15),
			new EventBool(true, 11),
		};

		// Act
		actual.Sort(eventSegmentComparer);

		// Assert
		for (int i = 0; i < expected.Length; i++)
			Assert.AreEqual(expected[i], actual[i]);
	}
	[Test]
	public void SweepStatusRandomLinesAddSorted()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0.548, 0.9443), new long2(0.7455, 0.8477), startParamPoint, endParamPoint, true, true), // 0
            new Segment(new long2(0.548, 0.9443), new long2(0.7438, 0.6878), startParamPoint, endParamPoint, true, true), // 1
            new Segment(new long2(0.5334, 0.945), new long2(0.7438, 0.6878), startParamPoint, endParamPoint, true, true), // 2
            new Segment(new long2(0.5122, 0.9013), new long2(0.6366, 0.9417), startParamPoint, endParamPoint, true, true),// 3
            new Segment(new long2(0.5732, 0.706), new long2(0.7169, 0.6187), startParamPoint, endParamPoint, true, true), // 4
            new Segment(new long2(0.5658, 0.6475), new long2(0.7169, 0.6187), startParamPoint, endParamPoint, true, true),// 5
            new Segment(new long2(0.5658, 0.6475), new long2(0.6321, 0.6197), startParamPoint, endParamPoint, true, true),// 6
            new Segment(new long2(0.4867, 0.5998), new long2(0.6095, 0.6079), startParamPoint, endParamPoint, true, true),// 7
            new Segment(new long2(0.4767, 0.5368), new long2(0.6615, 0.525), startParamPoint, endParamPoint, true, true), // 8
            new Segment(new long2(0.5824, 0.5157), new long2(0.9235, 0.3918), startParamPoint, endParamPoint, true, true),// 9
            new Segment(new long2(0.5824, 0.5157), new long2(0.9756, 0.3288), startParamPoint, endParamPoint, true, true),//10
            new Segment(new long2(0.4127, 0.4774), new long2(0.6615, 0.525), startParamPoint, endParamPoint, true, true), //11
            new Segment(new long2(0.4771, 0.4543), new long2(0.5913, 0.411), startParamPoint, endParamPoint, true, true), //12
            new Segment(new long2(0.5218, 0.2095), new long2(0.5839, 0.1114), startParamPoint, endParamPoint, true, true),//13
            new Segment(new long2(0.4544, 0.0837), new long2(0.6762, 0.4073), startParamPoint, endParamPoint, true, true),//14
            new Segment(new long2(0.4544, 0.0837), new long2(0.7364, 0.3326), startParamPoint, endParamPoint, true, true),//15
        };
		var eventSegmentComparer = new StatusQueueComparer(segments);

		var expected = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
			new EventBool(true, 4),
			new EventBool(true, 5),
			new EventBool(true, 6),
			new EventBool(true, 7),
			new EventBool(true, 8),
			new EventBool(true, 9),
			new EventBool(true, 10),
			new EventBool(true, 11),
			new EventBool(true, 12),
			new EventBool(true, 13),
			new EventBool(true, 14),
			new EventBool(true, 15),
		};

		var unsorted = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 9),
			new EventBool(true, 10),
			new EventBool(true, 4),
			new EventBool(true, 6),
			new EventBool(true, 5),
			new EventBool(true, 1),
			new EventBool(true, 0),
			new EventBool(true, 2),
			new EventBool(true, 13),
			new EventBool(true, 3),
			new EventBool(true, 7),
			new EventBool(true, 12),
			new EventBool(true, 8),
			new EventBool(true, 14),
			new EventBool(true, 15),
			new EventBool(true, 11),
		};

		var actual = new NativeList<EventBool>(16, Allocator.Temp);

		// Act
		foreach (var eventBool in unsorted)
		{
			var eventIndexInEvents = actual.BinarySearch(eventBool, eventSegmentComparer);
			eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;
			actual.InsertRange(eventIndexInEvents, 1);
			actual[eventIndexInEvents] = eventBool;
		}

		// Assert
		for (int i = 0; i < expected.Length; i++)
			Assert.AreEqual(expected[i], actual[i]);
	}
	[Test]
	public void SweepStatusRandomLines2Sort()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0.0119, 0.9843), new long2(0.1166, 0.9564), startParamPoint, endParamPoint, true, true), // 0
            new Segment(new long2(0.0119, 0.9843), new long2(0.2696, 0.7221), startParamPoint, endParamPoint, true, true), // 1
            new Segment(new long2(0.02439, 0.621), new long2(0.1274, 0.614), startParamPoint, endParamPoint, true, true), // 2
            new Segment(new long2(0.02201, 0.5214), new long2(0.3929, 0.5001), startParamPoint, endParamPoint, true, true),// 3
            new Segment(new long2(0.02706, 0.3861), new long2(0.0599, 0.4676), startParamPoint, endParamPoint, true, true), // 4
            new Segment(new long2(0.00284, 0.37), new long2(0.0727, 0.1129), startParamPoint, endParamPoint, true, true),// 5
        };
		var eventSegmentComparer = new StatusQueueComparer(segments);

		var expected = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
			new EventBool(true, 4),
			new EventBool(true, 5),
		};

		var actual = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 0),
			new EventBool(true, 4),
			new EventBool(true, 5),
			new EventBool(true, 3),
		};

		// Act
		actual.Sort(eventSegmentComparer);


		// Assert
		for (int i = 0; i < expected.Length; i++)
			Assert.AreEqual(expected[i], actual[i]);
	}

	[Test]
	public void SweepStatusAlmostColinear()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0.748177388071028, 0.252521310568105), new long2(0.891378804016348, 0.0914800094089202), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0.767495622537829, 0.196033183454203), new long2(0.93007516434479, 0.0485036888695129), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0.730744232854894, 0.222373678490038), new long2(0.879152990839225, 0.0396937662613766), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0.745655513303832, 0.175603611231761), new long2(0.879152990839225, 0.0396937662613766), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0.769217402074968, 0.174986034223823), new long2(0.807632298462112, 0.259456103935893), startParamPoint, endParamPoint, true, true),
		};
		var statusQueueComparer = new StatusQueueComparer(segments);

		var statusQueue = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
		};

		var ev0 = new EventBool(true, 4);

		// Act
		var index_ev0 = statusQueue.BinarySearch(ev0, statusQueueComparer);
		index_ev0 = index_ev0 < 0 ? ~index_ev0 : index_ev0;

		// Assert
		Assert.AreEqual(3, index_ev0);
	}
	[Test]
	public void SweepStatusSteps()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0, 4), new long2(3, 4), startParamPoint, endParamPoint, true, true), // 0
            new Segment(new long2(0, 2), new long2(3, 2), startParamPoint, endParamPoint, true, true), // 1
            new Segment(new long2(3, 2), new long2(3, -2), startParamPoint, endParamPoint, true,true), // 2
            new Segment(new long2(3, -2), new long2(6, -2), startParamPoint, endParamPoint, true,true),// 3
            new Segment(new long2(3, -4), new long2(6, -4), startParamPoint, endParamPoint, true,true), // 4
        };
		var statusQueueComparer = new StatusQueueComparer(segments);

		var expected = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
			new EventBool(true, 4),
		};

		var unsorted = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
			new EventBool(true, 3),
			new EventBool(true, 4),
		};

		var actual = new NativeList<EventBool>(16, Allocator.Temp);

		// Act
		foreach (var eventBool in unsorted)
		{
			var eventIndexInEvents = actual.BinarySearch(eventBool, statusQueueComparer);
			eventIndexInEvents = eventIndexInEvents < 0 ? ~eventIndexInEvents : eventIndexInEvents;
			actual.InsertRange(eventIndexInEvents, 1);
			actual[eventIndexInEvents] = eventBool;
		}

		// Assert
		for (int i = 0; i < expected.Length; i++)
			Assert.AreEqual(expected[i], actual[i]);
	}
	[Test]
	public void Rectangle()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var expectedSegments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0, 0), new long2(3, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(3, 3), new long2(7, -1), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(4, -4), new long2(7, -1), startParamPoint, endParamPoint, true,true),
			new Segment(new long2(0, 0), new long2(4, -4), startParamPoint, endParamPoint, true,true),
		};

		var statusQueueComparer = new StatusQueueComparer(expectedSegments);
		var eventComparer = new EventQueueComparer(statusQueueComparer, 1);

		var expectedEvents = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 3),
			new EventBool(true, 0),
			new EventBool(false, 0),
			new EventBool(true, 1),
			new EventBool(false, 3),
			new EventBool(true, 2),
			new EventBool(false, 2 ),
			new EventBool(false, 1 ),
		};

		var actualSegments = new NativeList<Segment>(16, Allocator.Temp);
		var actualEvents = new NativeList<EventBool>(16, Allocator.Temp);
		var rectangle = new List<long2> { new(3.0, 3.0), new(7.0, -1.0), new(4.0, -4.0), new(0.0, 0.0) };
		int start = 0, end = rectangle.Count;
		CreateEventBoolFromRegion(actualSegments, actualEvents, rectangle, start, end);


		// Act
		actualEvents.Sort(eventComparer);

		// Assert
		Assert.AreEqual(expectedEvents.Length, actualEvents.Length);
		Assert.AreEqual(expectedSegments.Length, actualSegments.Length);
		for (int i = 0; i < expectedEvents.Length; i++)
			Assert.AreEqual(expectedEvents[i], actualEvents[i]);

		for (int i = 0; i < expectedSegments.Length; i++)
			Assert.AreEqual(expectedSegments[i], actualSegments[i]);
	}

	[Test]
	public void Vertical_Triangle()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var expectedSegments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0, 0), new long2(3, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(3, -4), new long2(3, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 0), new long2(3, -4), startParamPoint, endParamPoint, true, true),
		};

		var statusQueueComparer = new StatusQueueComparer(expectedSegments);
		var eventComparer = new EventQueueComparer(statusQueueComparer, 1);

		var expectedEvents = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 2),
			new EventBool(true, 0),
			new EventBool(false, 2),
			new EventBool(true, 1),
			new EventBool(false, 1),
			new EventBool(false, 0),
		};

		var actualSegments = new NativeList<Segment>(16, Allocator.Temp);
		var actualEvents = new NativeList<EventBool>(16, Allocator.Temp);
		var vertical_Triangle = new List<long2> { new(3.0, 3.0), new(3.0, -4.0), new(0.0, 0.0) };
		int start = 0, end = vertical_Triangle.Count;
		CreateEventBoolFromRegion(actualSegments, actualEvents, vertical_Triangle, start, end);

		//Act
		actualEvents.Sort(eventComparer);

		// Assert
		Assert.AreEqual(expectedEvents.Length, actualEvents.Length);
		Assert.AreEqual(expectedSegments.Length, actualSegments.Length);
		for (int i = 0; i < expectedEvents.Length; i++)
			Assert.AreEqual(expectedEvents[i], actualEvents[i]);

		for (int i = 0; i < expectedSegments.Length; i++)
			Assert.AreEqual(expectedSegments[i], actualSegments[i]);
	}

	[Test]
	public void Vertical_LoweredTriangle()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var expectedSegments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0, -5), new long2(3, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(3, -4), new long2(3, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, -5), new long2(3, -4), startParamPoint, endParamPoint, true, true),
		};

		var statusQueueComparer = new StatusQueueComparer(expectedSegments);
		var eventComparer = new EventQueueComparer(statusQueueComparer, 1);

		var expectedEvents = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 2),
			new EventBool(true, 0),
			new EventBool(false, 2),
			new EventBool(true, 1),
			new EventBool(false, 1),
			new EventBool(false, 0),
		};

		var actualSegments = new NativeList<Segment>(16, Allocator.Temp);
		var actualEvents = new NativeList<EventBool>(16, Allocator.Temp);
		var vertical_LoweredTriangle = new List<long2> { new(3.0, 3.0), new(3.0, -4.0), new(0.0, -5.0) };
		int start = 0, end = vertical_LoweredTriangle.Count;
		CreateEventBoolFromRegion(actualSegments, actualEvents, vertical_LoweredTriangle, start, end);

		//Act
		actualEvents.Sort(eventComparer);

		// Assert
		Assert.AreEqual(expectedEvents.Length, actualEvents.Length);
		Assert.AreEqual(expectedSegments.Length, actualSegments.Length);
		for (int i = 0; i < expectedEvents.Length; i++)
			Assert.AreEqual(expectedEvents[i], actualEvents[i]);

		for (int i = 0; i < expectedSegments.Length; i++)
			Assert.AreEqual(expectedSegments[i], actualSegments[i]);
	}

	[Test]
	public void Vertical_MirrorTriangle()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var expectedSegments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0, -4), new long2(3, -5), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, -4), new long2(0, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 3), new long2(3, -5), startParamPoint, endParamPoint, true, true),
		};

		var expectedEvents = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(false, 1),
			new EventBool(true, 2),
			new EventBool(false, 0),
			new EventBool(false, 2),
		};

		var actualSegments = new NativeList<Segment>(16, Allocator.Temp);
		var actualEvents = new NativeList<EventBool>(16, Allocator.Temp);
		var vertical_MirrorTriangle = new List<long2> { new(0.0, -4.0), new(0.0, 3.0), new(3.0, -5.0) };
		int start = 0, end = vertical_MirrorTriangle.Count;
		CreateEventBoolFromRegion(actualSegments, actualEvents, vertical_MirrorTriangle, start, end);

		var statusQueueComparer = new StatusQueueComparer(actualSegments);
		var eventComparer = new EventQueueComparer(statusQueueComparer, 1);
		//Act

		actualEvents.Sort(eventComparer);

		// Assert
		Assert.AreEqual(expectedEvents.Length, actualEvents.Length);
		Assert.AreEqual(expectedSegments.Length, actualSegments.Length);
		for (int i = 0; i < expectedEvents.Length; i++)
			Assert.AreEqual(expectedEvents[i], actualEvents[i]);

		for (int i = 0; i < expectedSegments.Length; i++)
			Assert.AreEqual(expectedSegments[i], actualSegments[i]);
	}
	[Test]
	public void FindTransition()
	{
		// Arrange
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		var segments = new NativeList<Segment>(16, Allocator.Temp)
		{
			new Segment(new long2(0, 5), new long2(5, 5), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 3), new long2(5, 3), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 1), new long2(5, 1), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 6), new long2(5, 6), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 3), new long2(5, 4), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 3), new long2(5, 2), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 2), new long2(5, 2), startParamPoint, endParamPoint, true, true),
			new Segment(new long2(0, 0), new long2(5, 0), startParamPoint, endParamPoint, true, true),
		};
		var statusQueueComparer = new StatusQueueComparer(segments);
		var eventQueueComparer = new EventQueueComparer(statusQueueComparer, 1);

		var statusQueue = new NativeList<EventBool>(16, Allocator.Temp)
		{
			new EventBool(true, 0),
			new EventBool(true, 1),
			new EventBool(true, 2),
		};

		var ev6 = new EventBool(true, 3);
		var ev34 = new EventBool(true, 4);
		var ev32 = new EventBool(true, 5);
		var ev2 = new EventBool(true, 6);
		var ev0 = new EventBool(true, 7);

		//Act
		var tmp = statusQueue.BinarySearch(ev6, statusQueueComparer);
		var index_ev6 = tmp < 0 ? ~tmp : tmp;

		tmp = statusQueue.BinarySearch(ev34, statusQueueComparer);
		var index_ev34 = tmp < 0 ? ~tmp : tmp;

		tmp = statusQueue.BinarySearch(ev32, statusQueueComparer);
		var index_ev32 = tmp < 0 ? ~tmp : tmp;

		tmp = statusQueue.BinarySearch(ev2, statusQueueComparer);
		var index_ev2 = tmp < 0 ? ~tmp : tmp;

		tmp = statusQueue.BinarySearch(ev0, statusQueueComparer);
		var index_ev0 = tmp < 0 ? ~tmp : tmp;

		// Assert
		Assert.AreEqual(0, index_ev6);
		Assert.AreEqual(1, index_ev34);
		Assert.AreEqual(2, index_ev32);
		Assert.AreEqual(2, index_ev2);
		Assert.AreEqual(3, index_ev0);
	}

	static void CreateEventBoolFromRegion(NativeList<Segment> segments, NativeList<EventBool> eventQueue, List<long2> nodes, int start, int end)
	{
		var startParamPoint = new Rational(0, 1);
		var endParamPoint = new Rational(1, 1);
		long2 from;
		long2 to = nodes[end - 1];
		for (int i = start; i < end; i++)
		{
			from = to;
			to = nodes[i];

			int forward = from.CompareTo(to);
			if (forward == 0)
				continue; // points are equal, so we have a zero-length segment

			var segNew = forward < 0 ? new Segment(from, to, startParamPoint, endParamPoint, true, true) : new Segment(to, from, startParamPoint, endParamPoint, true, true);
			var segID = segments.Length;
			segments.Add(segNew);
			var evStart = new EventBool(true, segID);
			var evEnd = new EventBool(false, segID);

			eventQueue.Add(evStart);//just add, sort at the end in one go to avoid array shifts
			eventQueue.Add(evEnd);
		}
	}

	//// A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
	//// `yield return null;` to skip a frame.
	//[UnityTest]
	//public IEnumerator NewTestScriptWithEnumeratorPasses()
	//{
	//    // Use the Assert class to test conditions.
	//    // Use yield to skip a frame.
	//    yield return null;
	//}
}
