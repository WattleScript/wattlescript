using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter
{
    public class Range : RefIdObject
    {
        /// <summary>
        /// Inclusive start
        /// </summary>
        public int From { get; set; }
        
        /// <summary>
        /// Inclusive end
        /// </summary>
        public int To { get; set; }

        public Script OwnerScript { get; }

        public Range(Script ownerScript, int from, int to)
        {
            OwnerScript = ownerScript;
            From = from;
            To = to;
        }

        public override string ToString()
        {
            return $"Range ({From} - {To})";
        }

        internal IEnumerable<DynValue> ReversePair
        {
            get
            {
                var x = Enumerable.Range(From, To - From + 1).Select(x => DynValue.NewNumber(x));
                return x;
            }
        }
    }
}