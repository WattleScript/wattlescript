using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WattleScript.Interpreter.Tests.EndToEnd
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
            TestScript.Run(@"
            local dbl = getdouble()
            assert.istrue(dbl.isblocking(), 'task wait should be blocking')
            assert.areequal(8, dbl.await())", sc =>
            { 
                sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            });
        }

        [Test]
        public async Task Async_AwaitDouble()
        {
            //Test awaiting tasks
            await TestScript.RunAsync(@"
            local dbl = getdouble()
            assert.isfalse(dbl.isblocking(), 'task wait should not be blocking')
            assert.areequal(8, dbl.await())", sc =>
            { 
                sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            });
        }
        
        [Test]
        public void Synchronous_AutoAwait()
        {
            //Test automatic Task.Wait() call
            TestScript.Run(@"assert.areequal(8, getdouble())", sc =>
            {
                sc.Options.AutoAwait = true;
                sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            });
        }

        [Test]
        public async Task Async_AutoAwait()
        {
            //Test automatic await
            await TestScript.RunAsync(@"assert.areequal(8, getdouble())", sc =>
            {
                sc.Options.AutoAwait = true;
                sc.Globals["getdouble"] = (Func<Task<double>>) GetDoubleDelay;
            });
        }

        [Test]
        public async Task Async_Delay()
        {
            await TestScript.RunAsync(@"
            local t1 = os.time()
            delay(1.1).await()
            local t2 = os.time()
            assert.istrue((t2 - t1) >= 1, 'time comparison')
            ", s => 
                s.Globals["delay"] = (Func<double, Task>) (time => Task.Delay((int)(time * 1000.0)))
            );
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