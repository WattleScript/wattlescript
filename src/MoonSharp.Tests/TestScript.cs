using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests
{

    [MoonSharpUserData]
    public class LuaAssertApi
    {
        string GetFailMessage(ScriptExecutionContext executionContext, CallbackArguments args, int skip)
        {
            var msg = executionContext.CallingLocation.FormatLocation(executionContext.OwnerScript);
            var a = args.GetArray(skip);
            if(a.Length > 0) {
                msg += " - " + string.Join(' ', a.Select(x => x.ToString()));
            }
            return msg;
        }
        
        public void pass(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            Assert.Pass();
        }

        public void istrue(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var a = args.SkipMethodCall();
            if (a.Count < 1) {
                throw new Exception("Must compare two items");
            }
            Assert.IsTrue(a[0].CastToBool(), GetFailMessage(executionContext, a, 1));
        }
        
        public void isfalse(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var a = args.SkipMethodCall();
            if (a.Count < 1) {
                throw new Exception("Must compare two items");
            }
            Assert.IsFalse(a[0].CastToBool(), GetFailMessage(executionContext, a, 1));
        }
        
        public void areequal(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            var a = args.SkipMethodCall();
            if (a.Count < 2) {
                throw new Exception("Must compare two items");
            }
            Assert.AreEqual(a[0], a[1], GetFailMessage(executionContext, a, 2));
        }
        
        public void fail(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            Assert.Fail(GetFailMessage(executionContext, args.SkipMethodCall(), 0));
        }
    }
    
    public static class TestScript
    {
        static TestScript()
        {
            UserData.RegisterType<LuaAssertApi>();
        }

        public static void Run(string script, Action<Script> opts = null)
        {
            var sc = new Script();
            opts?.Invoke(sc);
            sc.Globals["assert"] = new LuaAssertApi();
            sc.DoString(script, null, "test");
        }

        public static async Task RunAsync(string script, Action<Script> opts = null)
        {
            var sc = new Script();
            opts?.Invoke(sc);
            sc.Globals["assert"] = new LuaAssertApi();
            await sc.DoStringAsync(script, null, "test");
        }
    }
}