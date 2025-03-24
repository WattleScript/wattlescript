using NUnit.Framework;
using WattleScript.Interpreter.Tests;

namespace WattleScript.Interpreter.Tests.EndToEnd;

[TestFixture]
public class LocalRedefTest
{
    [Test]
    public void RedefinedLocal()
    {
        TestScript.Run(@"
function appendb(s)
    return s .. 'b'
end

local tbl = { 'a' }
for index, item in ipairs(tbl) do
    local item = appendb(item)
    global = item
end
assert.areequal('ab', global)
");
    }
    
}