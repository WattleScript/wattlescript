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
            script.Options.Syntax = ScriptSyntax.CLike;
            return script.DoString(source);
        }

        [Test]
        public void IfBlock()
        {
            TestScript.Run(@"
            function if_1(a, b) {
                if a < b return a;
                return b;
            }
            function if_2(a, b) {
                if a > 3 { return a; }
                else return b + 3;
            }
            assert.areequal(1, if_1(1,2));
            assert.areequal(4, if_2(1,1));
            //Can use elseif or chain else to if
            if (3 < 2) 
                assert.fail(); 
            elseif (4 < 2) 
                assert.fail();
            else if (1 < 2) 
                assert.pass(); 
            else 
                assert.fail();",
                s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void ForEach()
        {
            TestScript.Run(@"
            local values = { 'a', 'b', 'c' };
            //of is an alias for in
            for (v of values) {
                assert.areequal('a', v);
                break;
            }
            for (v, k in values) {
                assert.areequal(1, k);
                assert.areequal('a', v);
                break;
            }
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void ForRange()
        {
            TestScript.Run(@"
            local values = { };
            for(x in 1..10)
            {
                values[x] = x;
            }
            assert.areequal(10, #values);
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void CFor()
        {
            TestScript.Run(@"
            //out of scope local
            local a = 0;
            local b = 0;
            for(a = 1; a <= 5; a++) {
                b++;
            }
            assert.areequal(6, a, 'outer local');
            assert.areequal(5, b, 'outer local');
            b = 0;
            for(var z = 1; z <= 5; z++) {
                assert.istrue(z != nil, 'z should not be nil');
                b++;
            }
            assert.areequal(nil, z, 'inner local'); //scope doesn't leak
            assert.areequal(5, b, 'inner local');
            //no init
            for(; a < 20; a++) {
                b++;
            }
            assert.areequal(20, a, 'no init');
            //no condition
            for(;; a++) {
                if (a > 25) break;
            }
            assert.areequal(26, a, 'no condition or init')
            for(a = 0;;a++) {
                if (a == 3) break;
            }
            assert.areequal(3, a, 'no condition');
            for(a = 0; a < 5;) {
                a++;
            }
            assert.areequal(5, a, 'no modify');
            for(;;) {
                a++;
                if(a == 10) break;
            }
            assert.areequal(10, a, 'empty for');
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void SquareBracketTable()
        {
            TestScript.Run(@"
                local table = [1, 2, [3, 4], [5], ['g']: 5];
                assert.areequal(1, table[1], 'list');
                assert.areequal(3, table[3][1], 'nested list');
                assert.areequal(5, table[4][1], 'nested list 2');
                assert.areequal(5, table['g'], 'map field');
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void Continue()
        {
            TestScript.Run(@"
            local a = 0;
            while(a < 3) {
                a++;
                continue;
                a = 5; //should be dead
            }
            assert.areequal(3, a, 'while');
            a = 0;
            repeat {
                a++;
                continue;
                a = 5;
            } until(a == 3)
            assert.areequal(3, a, 'repeat');
            for(a = 0; a < 3; a++) {
                continue;
                a = 5;
            }
            assert.areequal(3, a, 'c for');
            a = 0;
            local t = { 1, 2, 3 };
            for(v in t) {
                a++;
                continue;
                a = 5;
            }
            assert.areequal(3, a, 'iterate for');
            a = 0;
            for(v = 3,1,-1) { 
                a++;
                continue;
                a = 5;
            }
            assert.areequal(3, a, 'numeric for');
", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void CFor_Closure()
        {
            TestScript.Run(@"
            var tbl = {}
            for(local i = 1; i < 4; i++) {
                tbl[i] = () => i;
            }
            assert.areequal(1, tbl[1]());
            assert.areequal(2, tbl[2]());
            assert.areequal(3, tbl[3]());
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void AddConcat()
        {
            TestScript.Run(@"
            function getstring() {
                return 'hello';
            }
            t = {}
            mt = { __tostring= () => 'TABLE'; }
            setmetatable(t, mt)
            assert.areequal('hello world', getstring() + ' world');
            assert.areequal('pancakes123', 'pancakes' + 123);
            assert.areequal('t is TABLE', 't is ' +  t);
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
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
                s => s.Options.Syntax = ScriptSyntax.CLike);
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
                s => s.Options.Syntax = ScriptSyntax.CLike);
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
            //f ^= 2;  //pwr
            g %= 3; //mod
            h ..= 'bc'; //concat
            assert.areequal(2, a, '+=');
            assert.areequal(1, b, '-=');
            assert.areequal(6, c, '*=');
            assert.areequal(2, d, '/=');
            assert.areequal(4, e, '**=');
            //assert.areequal(4, f, '^=');
            assert.areequal(1, g, '%=');
            assert.areequal('abc', h, '..=');
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void DoLoop()
        {
            TestScript.Run(@"
                var a = 1;
                do {
                    a++;
                } while (a < 5);
                assert.areequal(5, a);
                do {
                    a--;
                } while (a > 0);
                assert.areequal(0, a);
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
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
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void PrefixIncDec()
        {
            TestScript.Run(@"
            var a = 1;
            assert.areequal(2, ++a, '++');
            assert.areequal(1, --a, '--');
            var t = { 1 }
            assert.areequal(2, ++t[1], '++ (table)');
            assert.areequal(1, --t[1], '-- (table)');
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void WhileLoop()
        {
            TestScript.Run(@"
            var a = 1;
            while (a < 5) a++;
            assert.areequal(5, a);
            while (a > 0) { a--; }
            assert.areequal(0, a);
            while (true) if (a > 5) break; else a++;
            assert.areequal(6, a);",
                s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void PostfixIncDec()
        {
            TestScript.Run(@"
            var a = 1;
            assert.areequal(1, a++, '++');
            assert.areequal(2, a, 'after ++');
            var b = 1;
            assert.areequal(1, b--, '--');
            assert.areequal(0, b, 'after --');
            var t = { 1 };
            assert.areequal(1, t[1]++, '++ (table)');
            assert.areequal(2, t[1], 'after ++ (table)');
            assert.areequal(2, t[1]--, '-- (table)');
            assert.areequal(1, t[1], 'after -- (table)');
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void Ternary()
        {
            TestScript.Run(@"
            // optimized
            assert.areequal(2, true ? 2 : 1, '? true lit');
            assert.areequal(1, false ? 2 : 1, '? false lit');
            // func call - bytecode
            assert.areequal(2, gettrue() ? 2 : 1, '? true func');
            assert.areequal(1, getfalse() ? 2 : 1, '? false func');",
                s =>
            {
                s.Options.Syntax = ScriptSyntax.CLike;
                s.Globals["gettrue"] = (Func<bool>)(() => true);
                s.Globals["getfalse"] = (Func<bool>) (() => false);
            });
        }

        [Test]
        public void CMultilineComment()
        {
            TestScript.Run(@"
            var a = /*
            a = 5*/  4;
            var b = 3; /* b = 4 */
            /*
                b = 5;
                b = 6;
            */
            assert.areequal(4, a);
            assert.areequal(3, b);
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void CLabel()
        {
            TestScript.Run(@"
            var a = 7;
            goto fin
            a = 2;
            fin:
            assert.areequal(7, a);
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        [Test]
        public void NilCoalesce()
        {
            TestScript.Run(@"
            local tbl = { 1 }
            assert.areequal(1.0, nil ?? 1.0);
            assert.areequal(2.0, 2.0 ?? 1.0);
            assert.areequal(1.0, tbl[3] ?? 1.0);
            assert.areequal(1.0, tbl[1] ?? 2.0);
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }

        
        [Test]
        public void BitNot()
        {
            TestScript.Run(@"
            function bnot(a) { return ~a };
            assert.areequal(-4096, ~4095);
            assert.areequal(-4096, bnot(4095));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }
        
        [Test]
        public void BitAnd()
        {
            TestScript.Run(@"
            function band(a,b) { return a & b };
            assert.areequal(255, 4095 & 255);
            assert.areequal(255, band(4095, 255));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }
        
        [Test]
        public void BitOr()
        {
            TestScript.Run(@"
            function bor(a,b) { return a | b };
            assert.areequal(1279, 1024 | 255);
            assert.areequal(1279, bor(1024, 255));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }
        
        [Test]
        public void BitXor()
        {
            TestScript.Run(@"
            function bxor(a,b) { return a ^ b };
            assert.areequal(3840, 4095 ^ 255);
            assert.areequal(3840, bxor(4095, 255));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }
        
        [Test]
        public void BitLShift()
        {
            TestScript.Run(@"
            function blshift(a,b) { return a << b };
            assert.areequal(64, 16 << 2);
            assert.areequal(64, blshift(16,2));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }
        
        [Test]
        public void BitRShiftA()
        {
            TestScript.Run(@"
            function brshifta(a,b) { return a >> b };
            assert.areequal(-256, -1024 >> 2);
            assert.areequal(-256, brshifta(-1024, 2));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }
        
        [Test]
        public void BitRShiftL()
        {
            TestScript.Run(@"
            function brshiftl(a,b) { return a >>> b };
            assert.areequal(0x3FFFFF00, -1024 >>> 2);
            assert.areequal(0x3FFFFF00, brshiftl(-1024, 2));
            ", s => s.Options.Syntax = ScriptSyntax.CLike);
        }


        [Test]
        public void Not()
        {
            TestScript.Run(@"assert.istrue(!false); assert.isfalse(!true);",
                s => s.Options.Syntax = ScriptSyntax.CLike);
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