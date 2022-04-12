using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MoonSharp.HardwireGen
{
    public class TypeGenQueue
    {
        private HashSet<string> hs = new HashSet<string>();
        private Queue<ITypeSymbol> queue = new Queue<ITypeSymbol>();
        
        public bool Enqueue(ITypeSymbol item)
        {
            if (hs.Contains(item.TypeName())) return false;
            hs.Add(item.TypeName());
            queue.Enqueue(item);
            return true;
        }

        public int Count => queue.Count;

        public ITypeSymbol Dequeue() => queue.Dequeue();
    }
}