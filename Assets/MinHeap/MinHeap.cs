//using System;
//using Unity.Collections;
//using Unity.Jobs;

//namespace Chart3D.MinHeap
//{
//    public enum Comparison
//    {
//        Min,
//        Max
//    };
//    public struct MinHeap<T> : IDisposable where T : unmanaged, IComparable<T>
//    {
//        public NativeList<T> _stack;
//        int m_CompareMultiplier;
//        public int Count { get { return _stack.Length; } }
//        public bool IsCreated { get { return _stack.IsCreated; } }
//        public bool IsEmpty { get { return _stack.Length == 0; } }

//        public void Clear()
//        {
//            _stack.Clear();
//        }
//        public MinHeap(int size, Allocator _allocator, Comparison comparison = Comparison.Min)
//        {
//            _stack = new NativeList<T>(size, _allocator);//needed size depends on precision
//            m_CompareMultiplier = (comparison == Comparison.Min) ? 1 : -1;
//        }
//        public void Push(T value)
//        {
//            _stack.Add(value);
//            BubbleUp(_stack.Length - 1);
//        }
//        public T Pop()
//        {
//            T result = default;
//            if (!IsEmpty)
//            {
//                result = _stack[0];
//                DeleteRoot();
//            }
//            return result;
//        }
//        public T Peek()
//        {
//            T result = default;
//            if (!IsEmpty)
//            {
//                result = _stack[0];
//            }
//            return result;
//        }
//        public void BubbleUp(int childIndex)
//        {
//            while (childIndex > 0)
//            {
//                int parentIndex = (childIndex - 1) / 2;
//                if (_stack[childIndex].CompareTo(_stack[parentIndex]) * m_CompareMultiplier < 0)
//                {
//                    Swap(parentIndex, childIndex);
//                    childIndex = parentIndex;
//                }
//                else break;
//            }
//        }

//        public void DeleteRoot()
//        {
//            if (_stack.Length <= 1)
//            {
//                _stack.Clear();
//                return;
//            }
//            _stack.RemoveAtSwapBack(0);   //using _stack.RemoveAt(0) would destroy parent child relationships of heap, so RemoveAtSwapBack(0) is essential!
//            BubbleDown(0);
//        }
//        public void BubbleDown(int index)
//        {
//            while (true)
//            {
//                // on each iteration exchange element with its smallest child
//                int leftChildIndex = index * 2 + 1;
//                int rightChildIndex = index * 2 + 2;
//                int smallestItemIndex = index; // The index of the parent

//                if (leftChildIndex < _stack.Length && _stack[leftChildIndex].CompareTo(_stack[smallestItemIndex]) * m_CompareMultiplier < 0)
//                    smallestItemIndex = leftChildIndex;
//                if (rightChildIndex < _stack.Length && _stack[rightChildIndex].CompareTo(_stack[smallestItemIndex]) * m_CompareMultiplier < 0)
//                    smallestItemIndex = rightChildIndex;

//                if (smallestItemIndex != index)
//                {
//                    Swap(smallestItemIndex, index);
//                    index = smallestItemIndex;
//                }
//                else break;
//            }
//        }
//        public void Swap(int pos1, int pos2)
//        {
//            (_stack[pos1], _stack[pos2]) = (_stack[pos2], _stack[pos1]);
//        }

//        public void Dispose()
//        {
//            _stack.Dispose();
//        }
//        public void Dispose(JobHandle jobHandle)
//        {
//            _stack.Dispose(jobHandle);
//        }
//    }
//}
