using WattleScript.Interpreter;

namespace WattleScript.HardwireGen.Test;

[WattleScriptUserData]
public class WithProperty
{
    public int Property { get; set; }
    public int Property2 { get; protected set; }
}