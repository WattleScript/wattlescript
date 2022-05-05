using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter
{
    public class Range : RefIdObject
    {
        public enum RangeClosingTypes
        {
            Inclusive,
            LeftExclusive,
            RightExclusive,
            Exclusive
        }
        
        public int From { get; set; }
        public int To { get; set; }
        public RangeClosingTypes ClosingType { get; set; } = RangeClosingTypes.Inclusive;
        
        public Script OwnerScript { get; }

        public Range(Script ownerScript, int from, int to)
        {
            OwnerScript = ownerScript;
            From = from;
            To = to;
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