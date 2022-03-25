using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
    [TestFixture]
    public class AsyncTests
    {
        static async Task<double> GetDoubleDelay()
        {
            await Task.Delay(100);
            return 8;
        }

        [Test]
        public void Synchronous_WaitDouble()
        {
            //Test Task.Wait() call
            string script = @"
            local dbl = getdouble()
            if not dbl.isblocking() then
                error('task wait should be blocking')
            end
            return dbl.await()";
            var sc = new Script();
            sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            var x= sc.DoString(script);
            Assert.AreEqual(8, x.Number);
        }

        [Test]
        public async Task Async_AwaitDouble()
        {
            //Test awaiting tasks
            string script = @"
            local dbl = getdouble()
            if dbl.isblocking() then
                error('task wait should not be blocking')
            end
            return dbl.await()";
            var sc = new Script();
            sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            var x= await sc.DoStringAsync(script);
            Assert.AreEqual(8, x.Number);
        }
        
        [Test]
        public void Synchronous_AutoAwait()
        {
            //Test automatic Task.Wait() call
            string script = "return getdouble()";
            var sc = new Script();
            sc.Options.AutoAwait = true;
            sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            var x= sc.DoString(script);
            Assert.AreEqual(8, x.Number);
        }

        [Test]
        public async Task Async_AutoAwait()
        {
            //Test automatic await
            string script = "return getdouble()";
            var sc = new Script();
            sc.Options.AutoAwait = true;
            sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            var x= await sc.DoStringAsync(script);
            Assert.AreEqual(8, x.Number);
        }

        [Test]
        public async Task Async_Delay()
        {
            string script = @"
            local t1 = os.time()
            delay(1.1).await()
            local t2 = os.time()
            return t2 - t1
            ";
            var sc = new Script();
            sc.Globals["delay"] = (Func<double, Task>) ((time) => Task.Delay((int)(time * 1000.0)));
            var x = await sc.DoStringAsync(script);
            Assert.GreaterOrEqual(x.Number, 1);
        }

        static async Task<double> GetDoubleExcept()
        {
            await Task.Delay(100);
            throw new InvalidOperationException();
        }
        
        [Test]
        public void Synchronous_TaskException()
        {
            var sc = new Script();
            sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleExcept;
            try
            {
                sc.DoString("getdouble().await()");
                Assert.Fail("Exception was not thrown");
            }
            catch (Exception)
            {
                Assert.Pass();
            }
        }
        
        [Test]
        public async Task Async_TaskException()
        {
            var sc = new Script();
            sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleExcept;
            try
            {
                var val = await sc.DoStringAsync("getdouble().await()");
                Assert.Fail("Exception was not thrown");
            }
            catch (Exception)
            {
                Assert.Pass();
            }
        }
    }
}