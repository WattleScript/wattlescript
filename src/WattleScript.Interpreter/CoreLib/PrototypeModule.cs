
namespace WattleScript.Interpreter.CoreLib
{
    /// <summary>
    /// Class implementing prototype Wattle & Lua functions 
    /// </summary>
    [WattleScriptModule(Namespace = "prototype")]
    public class PrototypeModule
    {
        private const string NUMBER_PROTOTABLE = "681bf104-NUMBER-PROTO";
        private const string BOOLEAN_PROTOTABLE = "77813cb0-BOOLEAN-PROTO";
        private const string RANGE_PROTOTABLE = "88c45cc7-RANGE-PROTO";

        public static void WattleScriptInit(Table globalTable, Table proto)
        {
            var sc = globalTable.OwnerScript;
            var stringTable = globalTable.Get("string").Table;
            proto.Set("number", sc.Registry.Get(NUMBER_PROTOTABLE));
            proto.Set("boolean", sc.Registry.Get(BOOLEAN_PROTOTABLE));
            proto.Set("range", sc.Registry.Get(RANGE_PROTOTABLE));
            proto.Set("string", DynValue.NewTable(stringTable));
            proto.Set("table", DynValue.NewTable(sc.GetTablePrototype()));
        }

        public static void EnablePrototypes(Table globalTable)
        {
            var sc = globalTable.OwnerScript;
            var stringTable = globalTable.Get("string").Table;
            
            void Register(string prototableIdent, DataType protoType)
            {
                //register
                var tab = new Table(sc);
                var funcs = new Table(sc);
                tab.Set("__index", DynValue.NewTable(funcs));
                sc.SetTypeMetatable(protoType, tab);
                sc.Registry.Set(prototableIdent, DynValue.NewTable(funcs));
                //functions
                funcs.Set("tostring", DynValue.NewCallback(BasicModule.tostring, "tostring"));
            }
            
            if (sc.Registry.Get(NUMBER_PROTOTABLE).IsNil())
            {
                Register(NUMBER_PROTOTABLE, DataType.Number);
            }
            
            if (sc.Registry.Get(BOOLEAN_PROTOTABLE).IsNil())
            {
                Register(BOOLEAN_PROTOTABLE, DataType.Boolean);
            }
            
            if (sc.Registry.Get(RANGE_PROTOTABLE).IsNil())
            {
                Register(RANGE_PROTOTABLE, DataType.Range);
            }
            
            if (sc.GetTablePrototype() == null)
                sc.SetTablePrototype(new Table(sc));
            //Copy tostring to string table
            stringTable.Set("tostring", DynValue.NewCallback(BasicModule.tostring, "tostring"));
        }
    }
}