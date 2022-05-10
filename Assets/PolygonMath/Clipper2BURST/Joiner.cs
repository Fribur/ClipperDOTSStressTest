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
    public struct JoinerLL
    {
        public NativeList<int> idx;
        public NativeList<int> op1;
        public NativeList<int> op2;
        public NativeList<int> next1;
        public NativeList<int> next2;
        public NativeList<int> nextH;
        public bool IsCreated;

        public JoinerLL(int size, Allocator allocator)
        {
            idx = new NativeList<int>(size, allocator);
            op1 = new NativeList<int>(size, allocator);
            op2 = new NativeList<int>(size, allocator);
            next1 = new NativeList<int>(size, allocator);
            next2 = new NativeList<int>(size, allocator);
            nextH = new NativeList<int>(size, allocator);
            IsCreated = true;
        }
        public void Dispose()
        {
            if (idx.IsCreated) idx.Dispose();
            if (op1.IsCreated) op1.Dispose();
            if (op2.IsCreated) op2.Dispose();
            if (next1.IsCreated) next1.Dispose();
            if (next2.IsCreated) next2.Dispose();
            if (nextH.IsCreated) nextH.Dispose();
            IsCreated = false;
        }
        public void Clear()
        {
            if (idx.IsCreated) idx.Clear();
            if (op1.IsCreated) op1.Clear();
            if (op2.IsCreated) op2.Clear();
            if (next1.IsCreated) next1.Clear();
            if (next2.IsCreated) next2.Clear();
            if (nextH.IsCreated) nextH.Clear();
        }
    };

} //namespace