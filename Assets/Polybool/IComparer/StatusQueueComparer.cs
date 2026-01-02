using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Polybool
{
    public struct StatusQueueComparer : IComparer<EventBool>
    {
        public NativeList<Segment> segments;
        public StatusQueueComparer(NativeList<Segment> segments)
        {
            this.segments = segments;
        }

        /// <summary> Sorts events in DESCENDING order on y-axis</summary>
        /// <returns>Returns -1 if eventA is above eventB, 1 if eventB is above eventB, 0 if equal</returns>
        public int Compare(EventBool eventA, EventBool eventB)
        {
            return StatusQueueComparerClass.Compare(eventA, eventB, segments);
        }
    }
    public static class StatusQueueComparerClass
    {
        public static int BinarySearch(NativeList<EventBool> statusQueue, EventBool ev, NativeList<Segment> segments)
        {
            int lo = 0;
            int hi = statusQueue.Length - 1;
            while (lo <= hi)
            {
                int i = (int) (((uint) hi + (uint) lo) >> 1);
                int c = Compare(ev, statusQueue[i], segments);
                if (c == 0)
                    return i;
                else if (c > 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }
            return ~lo;
        }

        /// <summary> Sorts events in DESCENDING order on y-axis</summary>
        /// <returns>Returns -1 if eventA is above eventB, 1 if eventB is above eventB, 0 if equal</returns>
        public static int Compare(EventBool eventA, EventBool eventB, NativeList<Segment> segments)
        {
            if (eventA == eventB)
                return 0;
            var seg1 = segments[eventA.segmentID];
            var seg2 = segments[eventB.segmentID];
            var s1_s = seg1.start;
            var s1_e = seg1.end;
            var s2_s = seg2.start;
            var s2_e = seg2.end;
            var s1_p0 = seg1.p0;
            var s1_p1 = seg1.p1;
            var s2_p0 = seg2.p0;
            var s2_p1 = seg2.p1;

            // orientation of p with respect to a segment:
            // <0 = CW = left. Because p0 is always left of p1, this means here also "above" 
            // >0 = CCW = right. Because p0 is always left of p1, this means here also "below" 
            // =0 = colinear

            //if seg2 is left of seg1...
            //use of 128 version for functions would allow for coordiantes >1e6..but who needs that when it reduces  performance by 2x
            if (Segment.CompareCoord128(s1_p0.x, seg1.pd.x, s1_s, s2_p0.x, seg2.pd.x, s2_s) > 0)             
            //if (Segment.CompareCoord(s1_p0.x, seg1.pd.x, s1_s, s2_p0.x, seg2.pd.x, s2_s) > 0)
            {
                //...then determine oriention of seg1 against seg2(c, d)
                // seg1 is "above" seg2, when it's points are CCW of seg 2...so when orient2d is positive (CCW), we need to return negative!                
                var orient2d = PointUtils128.Orient2DParamPoint128(s2_p0, s2_p1, s1_s, ref seg1);
                //var orient2d = PointUtils.Orient2DParamPoint(s2_p0, s2_p1, s1_s, ref seg1);
                if (orient2d == 0)           // a collinear with seg2 (c,d)?
                {
                    orient2d = PointUtils128.Orient2DParamPoint128(s2_p0, s2_p1, s1_e, ref seg1);
                    //orient2d = PointUtils.Orient2DParamPoint(s2_p0, s2_p1, s1_e, ref seg1);
                    if (orient2d == 0)       // b collinear with seg2 (c,d)?
                        return 0;                                       // both a and b are colinear with seg2 (c,d), so segments are coincident                    
                    else                                                // orientation of seg1 (b) with respect to seg2 (c,d).
                        return orient2d < 0 ? 1 : -1;                   // <0 = CW = left of seg2 means "above" (eventA is below eventB, return 1)
                }
                else                                                    // orientation of seg1 (a) with respect to seg2 (c,d).
                    return orient2d < 0 ? 1 : -1;                       // <0 = CW = left of seg2 means "above" (eventA is below eventB, return 1)
            }
            else
            {
                //...determine oriention of seg2 against seg1(a, b)
                // seg1 is "above" seg2, when seg2 points are are CW of seg 1..which is directly the result of orient2D                
                var orient2d = PointUtils128.Orient2DParamPoint128(s1_p0, s1_p1, s2_s, ref seg2);
                //var orient2d = PointUtils.Orient2DParamPoint(s1_p0, s1_p1, s2_s, ref seg2);
                if (orient2d == 0)           // c collinear with seg1 (a,b)?
                {
                    orient2d = PointUtils128.Orient2DParamPoint128(s1_p0, s1_p1, s2_e, ref seg2); 
                    //orient2d = PointUtils.Orient2DParamPoint(s1_p0, s1_p1, s2_e, ref seg2);
                    if (orient2d == 0)       // d collinear with seg1 (a,b)?                
                        return 0;                                       // both c and d are colinear with seg1 (a,b), so segments are coincident
                    else                                                // orientation of seg2 (d) with respect to seg1 (a,b).
                        return orient2d < 0 ? -1 : 1;                   // <0 = CW = left of seg1 means "below" (eventA is above eventB, return -1)
                }
                else                                                    // orientation of seg2 (c) with respect to seg1 (a,b).
                    return orient2d < 0 ? -1 : 1;                       // <0 = CW = left of seg1 means "below" (eventA is above eventB, return -1)
            }
        }
    }
}