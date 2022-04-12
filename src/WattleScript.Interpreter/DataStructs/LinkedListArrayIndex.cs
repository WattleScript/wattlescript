using System;
using System.Collections.Generic;

namespace WattleScript.Interpreter.DataStructs
{
    internal class LinkedListArrayIndex<TValue> : LinkedListIndex<int, TValue>
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkedListArrayIndex{TValue}"/> class.
        /// </summary>
        /// <param name="linkedList">The linked list to be indexed.</param>
        public LinkedListArrayIndex(LinkedList<TValue> linkedList) : base(linkedList) { }

        private LinkedListNode<TValue>[] positive;
        
        /// <summary>
        /// Finds the node indexed by the specified key, or null.
        /// </summary>
        /// <param name="key">The key.</param>
        public override LinkedListNode<TValue> Find(int key)
        {
            if (key >= 0)
            {
                if (positive != null && key < positive.Length)
                    return positive[key];
            }
            return base.Find(key);
        }
        
        /// <summary>
        /// Updates or creates a new node in the linked list, indexed by the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>The previous value of the element</returns>
        public override TValue Set(int key, TValue value)
        {
            LinkedListNode<TValue> node = Find(key);

            if (node == null)
            {
                Add(key, value);
                return default(TValue);
            }
            else
            {
                TValue val = node.Value;
                node.Value = value;
                return val;
            }
        }

        /// <summary>
        /// Creates a new node in the linked list, indexed by the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public override void Add(int key, TValue value)
        {
            if (key >= 0)
            {
                if (positive == null || key >= positive.Length)
                {
                    int pLen = positive?.Length ?? 0;
                    int newLen = Math.Max(pLen + 16, pLen * 2);
                    if (key > newLen)
                        base.Add(key,value);
                    else
                    {
                        if (positive == null) positive = new LinkedListNode<TValue>[16];
                        else Array.Resize(ref positive, newLen);
                        var node = m_LinkedList.AddLast(value);
                        positive[key] = node;
                    }
                }
                else
                {
                    var node = m_LinkedList.AddLast(value);
                    positive[key] = node;
                }
            }
            else
            {
                base.Add(key, value);
            }
        }
        
        /// <summary>
        /// Removes the specified key from the index, and the node indexed by the key from the linked list.
        /// </summary>
        /// <param name="key">The key.</param>
        public override bool Remove(int key)
        {
            if (key >= 0 && positive != null && key < positive.Length)
            {
                if (positive[key] != null)
                {
                    positive[key] = null;
                    return true;
                }
            }
            return base.Remove(key);
        }
        
        /// <summary>
        /// Clears this instance (removes all elements)
        /// </summary>
        public override void Clear()
        {
            if(positive != null) Array.Clear(positive, 0, positive.Length);
            base.Clear();
        }

        public override bool ContainsKey(int key)
        {
            if (key >= 0 && positive != null && key < positive.Length)
            {
                if (positive[key] != null)
                {
                    return true;
                }
            }
            return base.ContainsKey(key);
        }
    }
}