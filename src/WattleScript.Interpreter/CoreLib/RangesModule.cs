using System;

namespace WattleScript.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing string Wattle & Lua functions 
    /// </summary>
    [WattleScriptModule(Namespace = "range")]
    public class RangesModule 
    {
        public static void WattleScriptInit(Table globalTable, Table rangesTable)
        {
            globalTable.OwnerScript.Registry.Set(PrototypeModule.RANGE_PROTOTABLE, DynValue.NewTable(rangesTable));
            
            Table stringMetatable = new Table(globalTable.OwnerScript);
            stringMetatable.Set("__index", DynValue.NewTable(rangesTable));
            globalTable.OwnerScript.SetTypeMetatable(DataType.Range, stringMetatable);
        }

        [WattleScriptModuleMethod]
        public static DynValue totable(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;
            Table table = new Table(executionContext.OwnerScript);
            
            if (range.From < range.To)
            {
                for (int i = range.From; i <= range.To; i++)
                {
                    table.Append(DynValue.NewNumber(i));
                }
            }
            else if (range.From > range.To)
            {
                for (int i = range.To; i >= range.From; i--)
                {
                    table.Append(DynValue.NewNumber(i));
                }
            }

            return DynValue.NewTable(table);
        }
        
        [WattleScriptModuleMethod]
        public static DynValue sort(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;

            if (range.From > range.To)
            {
                (range.To, range.From) = (range.From, range.To);
            }
            
            return rangeDv;
        }
        
        [WattleScriptModuleMethod]
        public static DynValue sum(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;
            
            return DynValue.NewNumber(range.From + range.To);
        }
        
        [WattleScriptModuleMethod]
        public static DynValue swap(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;
            
            (range.To, range.From) = (range.From, range.To);

            return rangeDv;
        }
        
        [WattleScriptModuleMethod]
        public static DynValue issorted(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;
            
            return DynValue.NewBoolean(range.From <= range.To);
        }
        
        [WattleScriptModuleMethod]
        public static DynValue max(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;
            
            return DynValue.NewNumber(Math.Max(range.From, range.To));
        }
        
        [WattleScriptModuleMethod]
        public static DynValue min(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue rangeDv = args.AsType(0, "table", DataType.Range);
            Range range = rangeDv.Range;
            
            return DynValue.NewNumber(Math.Min(range.From, range.To));
        }
    }
}