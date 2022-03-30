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
        public void AndSyntax()
        {
            TestScript.Run(@" 
            function andfunc(a, b) {
                return a && b;
            }
            assert.isfalse(andfunc(true, false), 'true && false')
            assert.istrue(andfunc(true, true), 'true && true')",
                s => s.Options.EnableCSyntax = true);
        }
        
        [Test]
        public void OrSyntax()
        {
            TestScript.Run(@" 
            function orfunc(a, b) {
                return a || b;
            }
            assert.istrue(orfunc(true, false), 'true || false')
            assert.isfalse(orfunc(false, false), 'false && false')",
                s => s.Options.EnableCSyntax = true);
        }

        [Test]
        public void CompoundAssignLcl()
        {
            TestScript.Run(@" 
            var a = 1;
            var b = 2;
            var c = 3;
            var d = 4;
            var e = 2;
            var f = 2;
            var g = 4;
            var h = 'a';
            a += 1; //add
            b -= 1; //sub
            c *= 2; //mul
            d /= 2; //div
            e **= 2; //pwr
            f ^= 2;  //pwr
            g %= 3; //mod
            h ..= 'bc'; //concat
            assert.areequal(2, a, '+=');
            assert.areequal(1, b, '-=');
            assert.areequal(6, c, '*=');
            assert.areequal(2, d, '/=');
            assert.areequal(4, e, '**=');
            assert.areequal(4, f, '^=');
            assert.areequal(1, g, '%=');
            assert.areequal('abc', h, '..=');
            ", s=> s.Options.EnableCSyntax = true);
        }

        [Test]
        public void CompoundAssignIndex()
        {
            TestScript.Run(@"
            var tbl = { 1, 2, 3, 4, 'a' };
            tbl[1] += 1;
            tbl[2] -= 1;
            tbl[3] *= 2;
            tbl[4] %= 3;
            tbl[5] ..= 'bc';
            assert.areequal(2, tbl[1], '+= (table)');
            assert.areequal(1, tbl[2], '-= (table)');
            assert.areequal(6, tbl[3], '*= (table)');
            assert.areequal(1, tbl[4], '%= (table)');
            assert.areequal('abc', tbl[5], '..= (table)');
            ", s => s.Options.EnableCSyntax = true);
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