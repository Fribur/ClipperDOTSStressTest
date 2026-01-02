using System.Collections.Generic;
using Unity.Collections;

namespace Polybool
{
    public struct EventQueueComparer : IComparer<EventBool>
    {
        public StatusQueueComparer segmentComparer;
        public int sortDirection;

        /// <summary>
        /// Sort event queue decending for a 30% speedup of the algorithm. Adjust indexing into queue accordingly!
        /// </summary>
        public EventQueueComparer(StatusQueueComparer segmentComparer, int sortDirection)
        {
            // use "1" to sort event queue ascending (most left event is at index 0),
            // "-1" to sort event queue descending (most left event is at index ^1 (last element)),
            // -1 is about 30% faster as it eliminates shifting (=copying) the entire event queue upon each head removal
            this.segmentComparer = segmentComparer;
            this.sortDirection = sortDirection;
        }
        /// <summary> Sorts events in ASCENDING order on x-axis (and y-axis for same x)  </summary>
        /// <returns>Returns -1 if aEvent is smaller, 1 if bEvent is smaller, 0 if equal </returns>
        public int Compare(EventBool eventA, EventBool eventB)
        {
            return EventQueueComparerClass.Compare(eventA, eventB, segmentComparer.segments, sortDirection);
        }
    }
    public static class EventQueueComparerClass
    {
        public static int BinarySearch(NativeList<EventBool> eventQueue, EventBool ev, NativeList<Segment> segments, int sortDirection)
        {
            int lo = 0;
            int hi = eventQueue.Length - 1;
            while (lo <= hi)
            {
                int i = (int) (((uint) hi + (uint) lo) >> 1);
                int c = Compare(ev, eventQueue[i], segments, sortDirection);
                if (c == 0)
                    return i;
                else if (c > 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }
            // If none found, then a negative number that is the bitwise complement
            // of the index of the next element that is larger than or, if there is
            // no larger element, the bitwise complement of `length`, which
            // is `lo` at this point.
            return ~lo;
        }
        public static int Compare(EventBool eventA, EventBool eventB, NativeList<Segment> segments, int sortDirection)
        {
            // compare the selected points first
            var seg1 = segments[eventA.segmentID];
            var seg2 = segments[eventB.segmentID];
            var s1_s = seg1.start;
            var s1_e = seg1.end;
            var s1_pd = seg1.pd;
            var s2_s = seg2.start;
            var s2_e = seg2.end;
            var s2_pd = seg2.pd;
            //var s1_p0 = seg1.p0;
            //var s2_p0 = seg2.p0;

            var s1_p0 = seg1.p0;
            var s2_p0 = seg2.p0;

            if (!eventA.isStart)
                (s1_s, s1_e) = (s1_e, s1_s);
            if (!eventB.isStart)
                (s2_s, s2_e) = (s2_e, s2_s);

            // returns -1 if a1 (aSeq) is smaller b1 (bSeq) (=favour the smaller segment)
            int compSelected = Segment.Compare128(s1_p0, s1_pd, s1_s, s2_p0, s2_pd, s2_s);
            //int compSelected = Segment.Compare(s1_p0, s1_pd, s1_s, s2_p0, s2_pd, s2_s);            
            if (compSelected != 0)                              // the selected points are the same
                return compSelected * sortDirection;

            // then compare the  other (non-selected) points
            int compNonSelected = Segment.Compare128(s1_p0, s1_pd, s1_e, s2_p0, s2_pd, s2_e);
            //int compNonSelected = Segment.Compare(s1_p0, s1_pd, s1_e, s2_p0, s2_pd, s2_e);

            if (compNonSelected==0)                     // if the non-selected points are the same too...
                return 0;                               // then the segments are equal

            // check if one segment is a start and the other isn't...
            if (eventA.isStart != eventB.isStart)
                return eventA.isStart ? 1 * sortDirection : -1 * sortDirection;  // returns -1 when a1 is end (aSeq) (=favour end events)

            // abuse DESCENDING IComparer to behave as ASCENDING IComparer
            // by providing segments in flipped order (b - a instead of a - b)
            // so it will return -1 when aSeq is below (bSeq)
            // (=smaller segments on y axis come first)
            return StatusQueueComparerClass.Compare(eventB, eventA, segments) * sortDirection;
        }
    }
}
