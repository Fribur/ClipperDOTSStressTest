using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Chart3D.Helper.MinHeap
{
    public struct MinHeapBigData<T, COMPARER> : System.IDisposable where T : unmanaged where COMPARER : unmanaged, System.Collections.Generic.IComparer<HeapItem>
    {
        public NativeList<HeapItem> _stack;
        public NativeList<T> _nodes;
        public NativeList<int> emptyPositions;

        COMPARER _comparer;
        public int Length   { get { return _stack.Length; } }
        public bool IsCreated { get { return _stack.IsCreated; } }
        public bool IsEmpty { get { return _stack.Length == 0; } }

        public void Clear()
        {
            _stack.Clear();
            _nodes.Clear();
            emptyPositions.Clear();
        }
        public MinHeapBigData(int size, Allocator _allocator, COMPARER comparer)
        {
            _stack = new NativeList<HeapItem>(size, _allocator);//needed size depends on precision
            _nodes = new NativeList<T>(size, _allocator);
            emptyPositions = new NativeList<int>(16, _allocator);
            _comparer = comparer;
        }
        public void Push(T value, double cost)
        {
            if (emptyPositions.Length > 0)
            {
                _nodes[emptyPositions[0]] = value;
                _stack.Add(new HeapItem { Id = emptyPositions[0], Cost = cost }); 
                emptyPositions.RemoveAtSwapBack(0);
            }
            else
            { 
                _stack.Add(new HeapItem { Id = _nodes.Length, Cost = cost }); //Added Node is always last element
                _nodes.Add(value);
            }
            ;
            MinHeapifyUp(_stack.Length - 1);
        }
        public T Pop()
        { 
            T result=default;
            if (!IsEmpty)
            {
                result = _nodes[_stack[0].Id];
                DeleteRoot();
            }
            return result;
        }
        public T Peek()
        {
            T result = default;
            if (!IsEmpty)
            {
                result = _nodes[_stack[0].Id];
            }
            return result;
        }
        public void MinHeapifyUp(int childIndex)
        {
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (_comparer.Compare(_stack[childIndex], _stack[parentIndex]) < 0)
                {
                    ExchangeElements(parentIndex, childIndex);
                    childIndex = parentIndex;
                }
                else break;
            }
        }

        public void DeleteRoot()
        {
            if (_stack.Length <= 1)
            {
                Clear();
                return;
            }
            emptyPositions.Add(_stack[0].Id); //no nead to bother deleting Node from _Nodelist, sufficient to mark it as overwritable
            _stack.RemoveAtSwapBack(0);   //using _stack.RemoveAt(0) would destroy parent child relationships of heap, so RemoveAtSwapBack(0) is essential!
            MinHeapifyDown(0);
        }
        public void MinHeapifyDown(int index)
        {
            while (true)
            {
                // on each iteration exchange element with its smallest child
                int leftChildIndex = index * 2 + 1;
                int rightChildIndex = index * 2 + 2;
                int smallestItemIndex = index; // The index of the parent

                if (leftChildIndex < _stack.Length && _comparer.Compare(_stack[leftChildIndex], _stack[smallestItemIndex]) < 0)
                    smallestItemIndex = leftChildIndex;
                if (rightChildIndex < _stack.Length && _comparer.Compare(_stack[rightChildIndex], _stack[smallestItemIndex]) < 0)
                    smallestItemIndex = rightChildIndex;

                if (smallestItemIndex != index)
                {
                    ExchangeElements(smallestItemIndex, index);
                    index = smallestItemIndex;
                }
                else break;
            }
        }
        public void ExchangeElements(int pos1, int pos2)
        {
            var temp = _stack[pos1];
            _stack[pos1] = _stack[pos2];
            _stack[pos2] = temp;
        }

        public void Dispose()
        {
            _stack.Dispose();
            _nodes.Dispose();
            emptyPositions.Dispose();
        }
    }
}
