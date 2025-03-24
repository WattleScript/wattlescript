using WattleScript.Interpreter;

namespace WattleScript.HardwireGen.Test;

[WattleScriptUserData]
public class MyWattleData
{
    public string Value = "";
    public void SetString(string value)
    {
        Value = value;
    }

    public int Add4(int a)
    {
        return a + 4;
    }
}