using System.IO;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Clipper2SoA
{
    // OutRec: path data structure for clipping solutions
    public struct OutRecLL
    {
        public NativeList<int> owner;
        public NativeList<int> splitStartIDs;
        public NativeList<int> nextSplit;
        public NativeList<int> splits;
        public NativeList<int> frontEdge;
        public NativeList<int> backEdge;
        public NativeList<int> pts;
        public NativeList<int> polypath;
        public NativeList<Rect64> bounds;
        public NativeList<bool> isOpen;
        public bool IsCreated;

        public OutRecLL(int size, Allocator allocator)
        {
            owner = new NativeList<int>(size, allocator);
            splitStartIDs = new NativeList<int>(size, allocator); //where do splitoutRec for a given outRec start (in splits)
            nextSplit = new NativeList<int>(size, allocator); //points to next split
            splits = new NativeList<int>(size, allocator); //List of split outRecs
            frontEdge = new NativeList<int>(size, allocator);
            backEdge = new NativeList<int>(size, allocator);
            pts = new NativeList<int>(size, allocator);
            polypath = new NativeList<int>(size, allocator);
            bounds = new NativeList<Rect64>(size, allocator);
            isOpen = new NativeList<bool>(size, allocator);
            IsCreated = true;
        }
        public int AddOutRec(int owner_ID, bool _isOpen, int _pts)
        {
            int current = owner.Length;
            owner.Add(owner_ID);
            splitStartIDs.Add(-1);
            frontEdge.Add(-1);
            backEdge.Add(-1);
            pts.Add(_pts);
            polypath.Add(-1);
            bounds.Add(new Rect64());
            isOpen.Add(_isOpen);
            return current;
        }
        public int AddSplit(int _owningOutRec, int _splitOutRec)
        {
            int curID = splits.Length;
            splits.Add(_splitOutRec); //_splitOutRec is stored at index curID
            nextSplit.Add(-1);
            if (splitStartIDs[_owningOutRec] != -1)
            {
                //first, search the last index where splits of _owningOutRec are stored
                int splitsEnd, tmp = splitStartIDs[_owningOutRec];
                do
                {
                    splitsEnd = tmp;
                    tmp = nextSplit[tmp];
                } while (tmp != -1);                
                nextSplit[splitsEnd] = curID; //then point "next" of that end to the newly added _splitOutRec (stored at curID) 
            }
            else
            {
                splitStartIDs[_owningOutRec] = curID; //point Start of the _owningOutRec splitlist to the newly added _splitOutRec (stored at curID)
            }
            return curID;
        }
        public void Dispose()
        {
            if (owner.IsCreated) owner.Dispose();
            if (splitStartIDs.IsCreated) splitStartIDs.Dispose();
            if (nextSplit.IsCreated) nextSplit.Dispose();
            if (splits.IsCreated) splits.Dispose();
            if (frontEdge.IsCreated) frontEdge.Dispose();
            if (backEdge.IsCreated) backEdge.Dispose();
            if (pts.IsCreated) pts.Dispose();
            if (polypath.IsCreated) polypath.Dispose();
            if (bounds.IsCreated) bounds.Dispose();
            if (isOpen.IsCreated) isOpen.Dispose();
            IsCreated = false;
        }
        public void Dispose(JobHandle jobHandle)
        {
            if (owner.IsCreated) owner.Dispose(jobHandle);
            if (splitStartIDs.IsCreated) splitStartIDs.Dispose(jobHandle);
            if (nextSplit.IsCreated) nextSplit.Dispose(jobHandle);
            if (splits.IsCreated) splits.Dispose(jobHandle);
            if (frontEdge.IsCreated) frontEdge.Dispose(jobHandle);
            if (backEdge.IsCreated) backEdge.Dispose(jobHandle);
            if (pts.IsCreated) pts.Dispose(jobHandle);
            if (polypath.IsCreated) polypath.Dispose(jobHandle);
            if (bounds.IsCreated) bounds.Dispose(jobHandle);
            if (isOpen.IsCreated) isOpen.Dispose(jobHandle);
            IsCreated = false;
        }
        public void Clear()
        {
            if (owner.IsCreated) owner.Clear();
            if (splitStartIDs.IsCreated) splitStartIDs.Clear();
            if (nextSplit.IsCreated) nextSplit.Clear();
            if (splits.IsCreated) splits.Clear();
            if (frontEdge.IsCreated) frontEdge.Clear();
            if (backEdge.IsCreated) backEdge.Clear();
            if (pts.IsCreated) pts.Clear();
            if (polypath.IsCreated) polypath.Clear();
            if (bounds.IsCreated) bounds.Clear();
            if (isOpen.IsCreated) isOpen.Clear();
        }
    };

} //namespace