
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

namespace Chart3D.MinHeap
{
    public struct MinHeapManaged<T> : IEnumerable
    {
        public List<T> _stack;
        public void Clear() { _stack.Clear(); }
        public int Length { get { return _stack.Count; } }
        public bool IsEmpty { get { return _stack.Count == 0; } }
        IComparer<T> Comparer { get; set; }
        public IEnumerator<T> GetEnumerator() { return _stack.GetEnumerator(); } // the enumeration won't be sorted!
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public MinHeapManaged(IComparer<T> comparer)
        {
            _stack = new List<T>();
            Comparer = comparer;
        }
        public void Push(T value)
        {
            _stack.Add(value);
            BubbleUp(_stack.Count - 1);
        }
        public T Pop()
        {
            T result = default;
            if (!IsEmpty)
            {
                result = _stack[0];
                DeleteRoot();
            }
            return result;
        }
        public T Peek()
        {
            T result = default;
            if (!IsEmpty)
            {
                result = _stack[0];
            }
            return result;
        }
        public void BubbleUp(int childIndex)
        {
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (Comparer.Compare(_stack[childIndex], _stack[parentIndex]) < 0) //Minheap <0, MaxHeap: >0
                {
                    Swap(parentIndex, childIndex);
                    childIndex = parentIndex;
                }
                else break;
            }
        }

        public void DeleteRoot()
        {
            if (_stack.Count <= 1)
            {
                _stack.Clear();
                return;
            }
            _stack.RemoveAtSwapBack(0);   //using _stack.RemoveAt(0) would destroy parent child relationships of heap, so RemoveAtSwapBack(0) is essential!
            BubbleDown(0);
        }


        public void BubbleDown(int index)
        {
            while (true)
            {
                // on each iteration exchange element with its smallest child
                int leftChildIndex = index * 2 + 1;
                int rightChildIndex = index * 2 + 2;
                int smallestItemIndex = index; // The index of the parent

                if (leftChildIndex < _stack.Count && Comparer.Compare(_stack[leftChildIndex], _stack[smallestItemIndex]) < 0) //Minheap <0, MaxHeap: >0
                    smallestItemIndex = leftChildIndex;
                if (rightChildIndex < _stack.Count && Comparer.Compare(_stack[rightChildIndex], _stack[smallestItemIndex]) < 0) //Minheap <0, MaxHeap: >0
                    smallestItemIndex = rightChildIndex;

                if (smallestItemIndex != index)
                {
                    Swap(smallestItemIndex, index);
                    index = smallestItemIndex;
                }
                else break;
            }
        }

        public void Swap(int pos1, int pos2)
        {
            var temp = _stack[pos1];
            _stack[pos1] = _stack[pos2];
            _stack[pos2] = temp;
        }

    }
}
