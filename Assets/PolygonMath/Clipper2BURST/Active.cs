/*******************************************************************************
* Author    :  Angus Johnson                                                   *
* Version   :  10.0 (beta) - also known as Clipper2                            *
* Date      :  8 May 2022                                                      *
* Website   :  http://www.angusj.com                                           *
* Copyright :  Angus Johnson 2010-2022                                         *
* Purpose   :  This is the main polygon clipping module                        *
* Thanks    :  Special thanks to Thong Nguyen, Guus Kuiper, Phil Stopford,     *
*           :  and Daniel Gosnell for their invaluable assistance with C#.     *
* License   :  http://www.boost.org/LICENSE_1_0.txt                            *
*******************************************************************************/

using Unity.Collections;

namespace PolygonMath.Clipping.Clipper2LibBURST
{
    //ActiveLL: a linked list of edges that that may or may not be 'hot' (part of the clip solution).
    public struct ActiveLL
    {
        public NativeList<long2> bot;
        public NativeList<long2> top;
        public NativeList<long> curX;  //current (updated at every new scanline)
        public NativeList<double> dx;
        public NativeList<int> windDx;  //1 or -1 depending on winding direction
        public NativeList<int> windCount;  //1 or -1 depending on winding direction
        public NativeList<int> windCount2;  //1 or -1 depending on winding direction
        public NativeList<int> outrec;

        //AEL: 'active edge list' (Vatti's AET - active edge table)
        //     a linked list of all edges (from left to right) that are present
        //     (or 'active') within the current scanbeam (a horizontal 'beam' that
        //     sweeps from bottom to top over the paths in the clipping operation).
        public NativeList<int> prevInAEL;
        public NativeList<int> nextInAEL;

        //SEL: 'sorted edge list' (Vatti's ST - sorted table)
        //     linked list used when sorting edges into their new positions at the
        //     top of scanbeams, but also (re)used to process horizontals.
        public NativeList<int> prevInSEL;
        public NativeList<int> nextInSEL;
        public NativeList<int> jump;
        public NativeList<int> vertexTop;
        public NativeList<int> localMin;
        internal NativeList<bool> leftBound;
        public bool IsCreated;

        public ActiveLL(int size, Allocator allocator)
        {
            bot = new NativeList<long2>(size, allocator);
            top = new NativeList<long2>(size, allocator);
            curX = new NativeList<long>(size, allocator);
            dx = new NativeList<double>(size, allocator);
            windDx = new NativeList<int>(size, allocator);
            windCount = new NativeList<int>(size, allocator);
            windCount2 = new NativeList<int>(size, allocator);
            outrec = new NativeList<int>(size, allocator);
            prevInAEL = new NativeList<int>(size, allocator);
            nextInAEL = new NativeList<int>(size, allocator);
            prevInSEL = new NativeList<int>(size, allocator);
            nextInSEL = new NativeList<int>(size, allocator);
            jump = new NativeList<int>(size, allocator);
            vertexTop = new NativeList<int>(size, allocator);
            localMin = new NativeList<int>(size, allocator);
            leftBound = new NativeList<bool>(size, allocator);
            IsCreated = true;
        }
        public int AddActive(Active ae)
        {
            int current = bot.Length;
            bot.Add(ae.bot);
            top.Add(ae.top);
            curX.Add(ae.curX);
            dx.Add(ae.dx);
            windDx.Add(ae.windDx);
            windCount.Add(ae.windCount);
            windCount2.Add(ae.windCount2);
            outrec.Add(ae.outrec);
            prevInAEL.Add(-1);
            nextInAEL.Add(-1);
            prevInSEL.Add(-1);
            nextInSEL.Add(-1);
            jump.Add(-1);
            vertexTop.Add(ae.vertexTop);
            localMin.Add(ae.locMin_ID);
            leftBound.Add(ae.leftBound);

            return current;
        }

        public void Dispose()
        {
            if (bot.IsCreated) bot.Dispose();
            if (top.IsCreated) top.Dispose();
            if (curX.IsCreated) curX.Dispose();
            if (dx.IsCreated) dx.Dispose();
            if (windDx.IsCreated) windDx.Dispose();
            if (windCount.IsCreated) windCount.Dispose();
            if (windCount2.IsCreated) windCount2.Dispose();
            if (outrec.IsCreated) outrec.Dispose();
            if (prevInAEL.IsCreated) prevInAEL.Dispose();
            if (nextInAEL.IsCreated) nextInAEL.Dispose();
            if (prevInSEL.IsCreated) prevInSEL.Dispose();
            if (nextInSEL.IsCreated) nextInSEL.Dispose();
            if (jump.IsCreated) jump.Dispose();
            if (vertexTop.IsCreated) vertexTop.Dispose();
            if (localMin.IsCreated) localMin.Dispose();
            if (leftBound.IsCreated) leftBound.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (bot.IsCreated) bot.Clear();
            if (top.IsCreated) top.Clear();
            if (curX.IsCreated) curX.Clear();
            if (dx.IsCreated) dx.Clear();
            if (windDx.IsCreated) windDx.Clear();
            if (windCount.IsCreated) windCount.Clear();
            if (windCount2.IsCreated) windCount2.Clear();
            if (outrec.IsCreated) outrec.Clear();
            if (prevInAEL.IsCreated) prevInAEL.Clear();
            if (nextInAEL.IsCreated) nextInAEL.Clear();
            if (prevInSEL.IsCreated) prevInSEL.Clear();
            if (nextInSEL.IsCreated) nextInSEL.Clear();
            if (jump.IsCreated) jump.Clear();
            if (vertexTop.IsCreated) vertexTop.Clear();
            if (localMin.IsCreated) localMin.Clear();
            if (leftBound.IsCreated) leftBound.Clear();

        }
    };
    //Active: an edge in the AEL that may or may not be 'hot' (part of the clip solution).
    public struct Active
    {
        public long2 bot;
        public long2 top;
        public long curX;
        public double dx;
        public int windDx;
        public int windCount;
        public int windCount2;
        public int vertexTop;
        public int outrec;
        public int locMin_ID;
        internal bool leftBound;
        public Active(long curX)
        {
            bot = default;
            top = default;
            this.curX = curX;  //current (updated at every new scanline)
            dx = default;
            windDx = 1; //1 or -1 depending on winding direction
            windCount = default;
            windCount2 = default;
            vertexTop = default;
            outrec = -1;
            locMin_ID = -1;
            leftBound = false;
        }
    };

} //namespace