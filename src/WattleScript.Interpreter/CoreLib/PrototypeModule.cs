
namespace WattleScript.Interpreter.CoreLib
{
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
            if (sc.Registry.Get(NUMBER_PROTOTABLE).IsNil())
            {
                //register
                var nTab = new Table(sc);
                var nFuncs = new Table(sc);
                nTab.Set("__index", DynValue.NewTable(nFuncs));
                sc.SetTypeMetatable(DataType.Number, nTab);
                sc.Registry.Set(NUMBER_PROTOTABLE, DynValue.NewTable(nFuncs));
                //functions
                nFuncs.Set("tostring", DynValue.NewCallback(BasicModule.tostring, "tostring"));
            }
            if (sc.Registry.Get(BOOLEAN_PROTOTABLE).IsNil())
            {
                //register
                var bTab = new Table(sc);
                var bFuncs = new Table(sc);
                bTab.Set("__index", DynValue.NewTable(bFuncs));
                sc.SetTypeMetatable(DataType.Boolean, bTab);
                sc.Registry.Set(BOOLEAN_PROTOTABLE, DynValue.NewTable(bFuncs));
                //functions
                bFuncs.Set("tostring", DynValue.NewCallback(BasicModule.tostring, "tostring"));
            }
            if (sc.Registry.Get(RANGE_PROTOTABLE).IsNil())
            {
                //register
                var rTab = new Table(sc);
                var rFuncs = new Table(sc);
                rTab.Set("__index", DynValue.NewTable(rFuncs));
                sc.SetTypeMetatable(DataType.Range, rTab);
                sc.Registry.Set(RANGE_PROTOTABLE, DynValue.NewTable(rFuncs));
                //functions
                rFuncs.Set("tostring", DynValue.NewCallback(BasicModule.tostring, "tostring"));
            }
            if(sc.GetTablePrototype() == null)
                sc.SetTablePrototype(new Table(sc));
            //Copy tostring to string table
            stringTable.Set("tostring", DynValue.NewCallback(BasicModule.tostring, "tostring"));
        }
    }
}