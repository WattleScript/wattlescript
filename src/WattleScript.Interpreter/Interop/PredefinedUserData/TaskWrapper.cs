using System;
using System.Reflection;
using System.Threading.Tasks;
using WattleScript.Interpreter.Interop.Converters;

namespace WattleScript.Interpreter.Interop
{
    internal class TaskWrapper
    {
        internal Task Task;
        private bool waited = false;
        
        public TaskWrapper(Task task)
        {
            Task = task;
        }
        
        public static DynValue TaskResultToDynValue(Script script, Task task)
        {
            Type voidTaskType = typeof (Task<>).MakeGenericType(Type.GetType("System.Threading.Tasks.VoidTaskResult"));
            if (voidTaskType.IsInstanceOfType(task))
            {
                return DynValue.Nil; //no return type
            }
            var property = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                return DynValue.Nil;
            return ClrToScriptConversions.ObjectToDynValue(script, property.GetValue(task));
        }

        public DynValue isblocking(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            return DynValue.NewBoolean(!executionContext.CanAwait);
        }
        
        public DynValue await(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            if (!waited) {
                waited = true;
                if (executionContext.CanAwait) {
                    return DynValue.NewAwaitReq(Task);
                }
                else {
                    Task.Wait();
                }
            }
            if (Task.Exception != null) throw Task.Exception;
            return TaskResultToDynValue(executionContext.OwnerScript, Task);
        }
    }
}