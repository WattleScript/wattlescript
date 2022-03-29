using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class CSyntaxTests
    {
        DynValue RunScript(string source)
        {
            var script = new Script();
            script.Options.EnableCSyntax = true;
            return script.DoString(source);
        }
        
        [Test]
        public void IfBlock()
        {
            Assert.AreEqual(1.0, RunScript(@"
            if (1 > 2) {
                return 0;
            } else {
                return 1;
            }
            ").Number);
        }

        [Test]
        public void Power()
        {
            Assert.AreEqual(4.0, RunScript("return 2**2;").Number);
        }

        [Test]
        public void Let()
        {
            Assert.AreEqual(2.0, RunScript("let a = 2.0; return a;").Number);
        }
        
        [Test]
        public void Var()
        {
            Assert.AreEqual(2.0, RunScript("var a = 2.0; return a;").Number);
        }
        
        [Test]
        public void SingleLineComment()
        {
            Assert.AreEqual(2.0, RunScript("var a = 2.0; return a; //comment").Number);
        }

        [Test]
        public void ArrowFuncSingleArg()
        {
            Assert.AreEqual("hello", RunScript(@"var func = x => x; return func('hello');").String);
        }
        
        [Test]
        public void ArrowFuncTwoArgs()
        {
            Assert.AreEqual(2.0, RunScript(@"var func = (x, y) => x + y; return func(1, 1);").Number);
        }

        [Test]
        public void TableInitSyntax()
        {
            Assert.AreEqual("hello", RunScript(@"
var x = { 'name': 'hello' };
return x.name;").String);
        }

        [Test]
        public void FunctionDefinition()
        {
            Assert.AreEqual(2.0, RunScript(@"
function getnumber() {
    return 2.0;
}
return getnumber();
").Number);
        }
        
        
       
    }
}