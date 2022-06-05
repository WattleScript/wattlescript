using WattleScript.Interpreter;

namespace WattleScript.Templating;

public class TagHelper
{
    public string Name { get; set; }
    public string? Template { get; set; }
    public byte[]? TemplateBytecode { get; set; }
    public string FunctionName { get; set; }

    public TagHelper(string name, string template, byte[] templateBytecode)
    {
        Name = name;
        Template = template;
        TemplateBytecode = templateBytecode;
    }
    
    public TagHelper(string name, string template, string functionName)
    {
        Name = name;
        Template = template;
        FunctionName = functionName;
    }

    public TagHelper(string name, byte[] templateBytecode)
    {
        Name = name;
        TemplateBytecode = templateBytecode;
    }
}