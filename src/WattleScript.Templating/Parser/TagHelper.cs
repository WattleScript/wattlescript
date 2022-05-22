namespace WattleScript.Templating;

public class TagHelper
{
    public string Name { get; set; }
    public string? Template { get; set; }
    public byte[]? TemplateBytecode { get; set; }

    public TagHelper(string name, string template, byte[] templateBytecode)
    {
        Name = name;
        Template = template;
        TemplateBytecode = templateBytecode;
    }
    
    public TagHelper(string name, string template)
    {
        Name = name;
        Template = template;
    }
    
    public TagHelper(string name, byte[] templateBytecode)
    {
        Name = name;
        TemplateBytecode = templateBytecode;
    }
}