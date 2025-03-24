using WattleScript.Interpreter;

namespace WattleScript.HardwireGen.Test;

[WattleScriptUserData]
public class MyWattleData
{
    public string Value = "";

    public int Add4(int a)
    {
        return a + 4;
    }

    public MyWattleData()
    {
    }

    public MyWattleData(string value)
    {
        Value = value;
    }
}