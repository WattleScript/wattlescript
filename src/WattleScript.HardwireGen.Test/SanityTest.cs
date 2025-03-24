using NUnit.Framework;
using WattleScript.Interpreter;

namespace WattleScript.HardwireGen.Test;

public class SanityTest
{
    [SetUp]
    public void Setup()
    {
        UserData.DefaultAccessMode = InteropAccessMode.Hardwired;
        LuaHardwire_WattleScript_HardwireGen_Test.Initialize();
    }

    [Test]
    public void CallMethod()
    {
        var sc = new Script(CoreModules.None);
        sc.Globals["data"] = new MyWattleData();
        Assert.AreEqual(8, sc.DoString("return data.Add4(4)").CastToInt());
    }

    [Test]
    public void GetField()
    {
        var sc = new Script(CoreModules.None);
        sc.Globals["data"] = new MyWattleData() { Value = "Hello" };
        Assert.AreEqual("Hello", sc.DoString("return data.Value").CastToString());
    }

    [Test]
    public void GetFieldOnExtra()
    {
        var sc = new Script(CoreModules.None);
        sc.Globals["data"] = new GenericExtra<string>() { Value = "Hello" };
        Assert.AreEqual("Hello", sc.DoString("return data.Value").CastToString());
    }

    [Test]
    public void CallMethodOnExtra()
    {
        var sc = new Script(CoreModules.None);
        sc.Globals["data"] = new GenericExtra<string>() { Value = "Hello" };
        Assert.AreEqual(1234, sc.DoString("return data.DoThing()").CastToInt());
    }
}