using Chart3D.MathExtensions;
using Unity.Collections;
using Unity.Jobs;

namespace Clipper2SoA
{
    ///////////////////////////////////////////////////////////////////
    // Important: UP and DOWN here are premised on Y-axis positive down
    // displays, which is the orientation used in Clipper's development.
    ///////////////////////////////////////////////////////////////////
    internal enum JoinWith { None, Left, Right };
    internal enum HorzPosition { Bottom, Middle, Top };

    public struct ActiveLL
    {
        public NativeList<long2> bot;
        public NativeList<long2> top;
        public NativeList<long> curX;  // current (updated at every new scanline)
        public NativeList<double> dx;
        public NativeList<int> windDx;  // 1 or -1 depending on winding direction
        public NativeList<int> windCount;
        public NativeList<int> windCount2;  // winding count of the opposite polytype
        public NativeList<int> outrec;

        // AEL: 'active edge list' (Vatti's AET - active edge table)
        //     a linked list of all edges (from left to right) that are present
        //     (or 'active') within the current scanbeam (a horizontal 'beam' that
        //     sweeps from bottom to top over the paths in the clipping operation).
        public NativeList<int> prevInAEL;
        public NativeList<int> nextInAEL;

        // SEL: 'sorted edge list' (Vatti's ST - sorted table)
        //     linked list used when sorting edges into their new positions at the
        //     top of scanbeams, but also (re)used to process horizontals.
        public NativeList<int> prevInSEL;
        public NativeList<int> nextInSEL;
        public NativeList<int> jump;
        public NativeList<int> vertexTop;
        public NativeList<int> localMin;
        internal NativeList<bool> isLeftBound;
        internal NativeList<JoinWith> joinWith;
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
            isLeftBound = new NativeList<bool>(size, allocator);
            joinWith = new NativeList<JoinWith>(size, allocator);
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
            isLeftBound.Add(ae.isleftBound);
            joinWith.Add(ae.joinWith);
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
            if (isLeftBound.IsCreated) isLeftBound.Dispose();
            if (joinWith.IsCreated) joinWith.Dispose();
            IsCreated = false;
        }
        public void Dispose(JobHandle jobHandle)
        {
            if (bot.IsCreated) bot.Dispose(jobHandle);
            if (top.IsCreated) top.Dispose(jobHandle);
            if (curX.IsCreated) curX.Dispose(jobHandle);
            if (dx.IsCreated) dx.Dispose(jobHandle);
            if (windDx.IsCreated) windDx.Dispose(jobHandle);
            if (windCount.IsCreated) windCount.Dispose(jobHandle);
            if (windCount2.IsCreated) windCount2.Dispose(jobHandle);
            if (outrec.IsCreated) outrec.Dispose(jobHandle);
            if (prevInAEL.IsCreated) prevInAEL.Dispose(jobHandle);
            if (nextInAEL.IsCreated) nextInAEL.Dispose(jobHandle);
            if (prevInSEL.IsCreated) prevInSEL.Dispose(jobHandle);
            if (nextInSEL.IsCreated) nextInSEL.Dispose(jobHandle);
            if (jump.IsCreated) jump.Dispose(jobHandle);
            if (vertexTop.IsCreated) vertexTop.Dispose(jobHandle);
            if (localMin.IsCreated) localMin.Dispose(jobHandle);
            if (isLeftBound.IsCreated) isLeftBound.Dispose(jobHandle);
            if (joinWith.IsCreated) joinWith.Dispose(jobHandle);
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
            if (isLeftBound.IsCreated) isLeftBound.Clear();
            if (joinWith.IsCreated) joinWith.Clear();

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
        internal bool isleftBound;
        internal JoinWith joinWith;
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
            isleftBound = false;
            joinWith = JoinWith.None;
        }
    };

} //namespace