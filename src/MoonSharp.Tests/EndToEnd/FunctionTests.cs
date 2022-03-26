using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class FunctionTests
    {
        DynValue ListFilter(Script sc, CallbackArguments args)
        {
            Table tbl = new Table(sc);
            DynValue dv = DynValue.NewTable(tbl);

            Table toFilter = args[0].Table;
            Closure filterFn = args[1].Function;

            foreach (TablePair pair in toFilter.Pairs)
            {
                DynValue valPair = pair.Value;
                DynValue check = filterFn.Call(valPair);

                if (check.Boolean)
                {
                    tbl.Append(valPair);
                }
            }

            return dv;
        }

        [Test]
        public void ListFilter()
        {
            string script = @"
                arr = list_filter({10, 20, 30}, function (x) return x >= 20 end)
                return arr[1]
            ";

            var sc = new Script();
            sc.Globals["list_filter"] = (Func<Script, CallbackArguments, DynValue>)ListFilter;
            var x = sc.DoString(script);

            Assert.AreEqual(20, x.Number);
        }
    }
}