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
    public void Constructor()
    {
        var sc = new Script(CoreModules.None);
        sc.Globals["MyWattleData"] = typeof(MyWattleData);
        var r = sc.DoString("return MyWattleData.__new('Hello')");
        Assert.IsInstanceOf<MyWattleData>(r.ToObject());
        var wd = r.ToObject<MyWattleData>();
        Assert.AreEqual("Hello", wd.Value);
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

    [Test]
    public void Property()
    {
        var sc = new Script(CoreModules.None);
        var p = new WithProperty();
        sc.Globals["data"] = p;
        Assert.AreEqual(6, sc.DoString(@"
        data.Property = 3
        return data.Property + 3
        ").CastToInt());
        Assert.AreEqual(3, p.Property);
    }
}