using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WattleScript.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class CSyntaxTests
    {
        DynValue RunScript(string source)
        {
            var script = new Script();
            script.Options.Syntax = ScriptSyntax.WattleScript;
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
                s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void UsingDirective()
        {
            var s = new Script();
            s.Options.Syntax = ScriptSyntax.WattleScript;
            s.Options.Directives.Add("using");
            var chunk = s.LoadString("using a.b.c;");
            var a = chunk.Function.Annotations[0];
            Assert.AreEqual("using", a.Name);
            Assert.AreEqual("a.b.c", a.Value.String);
        }
        
        [Test]
        public void ChunkAnnotations()
        {
            var s = new Script();
            s.Options.Syntax = ScriptSyntax.WattleScript;
            var chunk = s.LoadString(@"
            @@number (1.0)
            @@string ('hello')
            @@boolean (true)
            @@nilarg (nil)
            @@empty
            @@table ({ key: 'value' })");
            //Go through bytecode to make sure all the annotations are kept
            using (var mem = new MemoryStream())
            {
                s.Dump(chunk, mem);
                mem.Seek(0, SeekOrigin.Begin);
                chunk = s.LoadStream(mem);
            }
            //Check values
            var a = chunk.Function.Annotations;
            
            Assert.AreEqual("number", a[0].Name);
            Assert.AreEqual(1.0, a[0].Value.Number);
            
            Assert.AreEqual("string", a[1].Name);
            Assert.AreEqual("hello", a[1].Value.String);
            
            Assert.AreEqual("boolean", a[2].Name);
            Assert.AreEqual(true, a[2].Value.Boolean);
            
            Assert.AreEqual("nilarg", a[3].Name);
            Assert.IsTrue(a[3].Value.IsNil());
            
            Assert.AreEqual("empty", a[4].Name);
            Assert.IsTrue(a[4].Value.IsNil());
            
            Assert.AreEqual("table", a[5].Name);
            Assert.AreEqual("value", a[5].Value.Table["key"]);
        }

        [Test]
        public void FunctionAnnotations()
        {
            var s = new Script();
            s.Options.Syntax = ScriptSyntax.WattleScript;
            s.DoString(@"
            @bind ({name: 'hello', value: 10 })
            function myfunc(args)
            {
            }
            ");
            var myfunc = s.Globals.Get("myfunc").Function;
            Assert.AreEqual("hello", myfunc.Annotations[0].Value.Table["name"]);
            Assert.AreEqual(10, myfunc.Annotations[0].Value.Table["value"]);
        }

        [Test]
        public void SwitchBasic()
        {
            TestScript.Run(@"
            function doswitch(arg) {
                switch(arg) {
                    case nil:
                        return 1;
                    case true:
                        return 2;
                    default:
                        return 3;
                    case 'hello':
                        return 4;
                    case 5:
                        return 5;
                }
            }
            assert.areequal(1, doswitch(), 'nil');
            assert.areequal(2, doswitch(true), 'true');
            assert.areequal(3, doswitch('abcd'), 'default');
            assert.areequal(4, doswitch('hello'), 'string');
            assert.areequal(5, doswitch(5), 'number');
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void DotThisCall()
        {
            TestScript.Run(@"
            var tbl = { 
                str = 'hello',
                func1 = (x) => {   
                    assert.areequal('hello', this?.str, 'func1 - arrow lambda');
                    assert.areequal(7, x);
                },
                func2 = function(x) {
                    assert.areequal('hello', this?.str, 'func2');
                    assert.areequal(7, x);
                }
            }
            function tbl:func3(num) {
                assert.areequal('hello', this?.str, 'tbl:func3')
                assert.areequal(7, num);
            }
            function tbl.func4(num) {
                assert.areequal('hello', this?.str, 'tbl.func4')
                assert.areequal(7, num);
            }       
            local function func5(num) {
                assert.areequal(nil, this?.str, 'func5 - NOT table')
                assert.areequal(7, num);
            }         
            tbl.func5 = func5; //'this' does not carry over, it is decided at the definition time

            tbl.func1(7);
            tbl::func2(7);
            tbl.func3(7);
            tbl.func4(7);
            tbl.func5(7);
            table.insert(tbl, 1, 'goodbye');
            assert.areequal('goodbye', tbl[1]);
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void ImplicitThis()
        {
            TestScript.Run(@"
            this = 'hello'; //making sure 'this' local is defined
            var tbl = {}
            function tbl.implicitthis(arg)
            {
                assert.areequal(tbl, this);
                assert.areequal(7, arg);
            }
            function tbl.shouldnil(arg)
            {
                assert.areequal(nil, this);
                assert.areequal(7, arg);
            }
            tbl.implicitthis(7); //dot call will pass implicit 'this' parameter
            tbl['implicitthis'](7); //regular indexing = also pass implicit 'this'
            local fun = tbl.shouldnil;
            fun(7); //no left hand side = no this to pass
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public async Task HostThisCall()
        {
            UserData.RegisterType<LuaAssertApi>();
            var sc = new Script();
            sc.Options.Syntax = ScriptSyntax.WattleScript;
            sc.Globals["assert"] = new LuaAssertApi();
            
            var tbl = sc.DoString(@"
            var tbl = {}
            tbl.str = 'hello'
            function tbl.func(arg)
            {
                assert.areequal('hello', this?.str);
                assert.areequal(7, arg);
            }
            function tbl.func2(arg1, arg2)
            {
                assert.areequal(nil, this);
                assert.areequal(2, arg1);
                assert.areequal(3, arg2);
            }
            return tbl;
            ");
            var function = tbl.Table.Get("func").Function;
            var function2 = tbl.Table.Get("func2").Function;
            //sync this call
            function.ThisCall(tbl, DynValue.NewNumber(7));
            sc.ThisCall(function, tbl, 7);
            //async this call
            await function.ThisCallAsync(tbl, DynValue.NewNumber(7));
            await sc.ThisCallAsync(function, tbl, DynValue.NewNumber(7));
            //regular calls
            function2.Call(2, 3);
            await function2.CallAsync(2, 3);
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
", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void CallDefaultFunc()
        {
            TestScript.Run(@"
            f = (x = () => {print('yes')}) => {
                    x()
            }
            f();", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
                s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
                s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
                s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
                s.Options.Syntax = ScriptSyntax.WattleScript;
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
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
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void NilCheck()
        {
            TestScript.Run(@"
                local tbl = {
                    extra = 2.0
                };
                assert.areequal(nil, tbl?.x?.y()?.z);
                assert.areequal(nil, tbl['hello']?[2]);
                assert.areequal(2.0, tbl[asdf?['hello'] ?? 'extra']);
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void LengthProperty()
        {
            TestScript.Run(@"
            assert.areequal(nil, blah?.length);
            local tbl = { 1, 2 };
            tbl['length'] = 1;
            assert.areequal(2, tbl.length);
            assert.areequal(1, tbl['length']);
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void LengthStringLiteral()
        {
            TestScript.Run(@"
                assert.areequal(5, 'hello'.length);
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        [Test]
        public void LengthPropertyReadonly()
        {
            Assert.Throws<SyntaxErrorException>(() =>
            {
                TestScript.Run(@"
                    table = {}
                    table.length = 3;
                ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
            });
        }

        [Test]
        public void EscapeStringTemplate()
        {
            TestScript.Run(@"assert.areequal('${hello}', `$\{hello}`);", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void StringTemplate()
        {
            TestScript.Run(@"
            assert.areequal('3', `{3}`);
            function getFirst(tbl) { return tbl[1]; }
            assert.areequal('hello', `{ //4
                getFirst({ //5
                     'hello' //6
                }) //7
            }`);
            assert.areequal('hello world', `{'hello'} {'world'}`);
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }

        
        [Test]
        public void BitNot()
        {
            TestScript.Run(@"
            function bnot(a) { return ~a };
            assert.areequal(-4096, ~4095);
            assert.areequal(-4096, bnot(4095));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void BitAnd()
        {
            TestScript.Run(@"
            function band(a,b) { return a & b };
            assert.areequal(255, 4095 & 255);
            assert.areequal(255, band(4095, 255));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void BitOr()
        {
            TestScript.Run(@"
            function bor(a,b) { return a | b };
            assert.areequal(1279, 1024 | 255);
            assert.areequal(1279, bor(1024, 255));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void BitXor()
        {
            TestScript.Run(@"
            function bxor(a,b) { return a ^ b };
            assert.areequal(3840, 4095 ^ 255);
            assert.areequal(3840, bxor(4095, 255));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void BitLShift()
        {
            TestScript.Run(@"
            function blshift(a,b) { return a << b };
            assert.areequal(64, 16 << 2);
            assert.areequal(64, blshift(16,2));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void BitRShiftA()
        {
            TestScript.Run(@"
            function brshifta(a,b) { return a >> b };
            assert.areequal(-256, -1024 >> 2);
            assert.areequal(-256, brshifta(-1024, 2));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }
        
        [Test]
        public void BitRShiftL()
        {
            TestScript.Run(@"
            function brshiftl(a,b) { return a >>> b };
            assert.areequal(0x3FFFFF00, -1024 >>> 2);
            assert.areequal(0x3FFFFF00, brshiftl(-1024, 2));
            ", s => s.Options.Syntax = ScriptSyntax.WattleScript);
        }


        [Test]
        public void Not()
        {
            TestScript.Run(@"assert.istrue(!false); assert.isfalse(!true);",
                s => s.Options.Syntax = ScriptSyntax.WattleScript);
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