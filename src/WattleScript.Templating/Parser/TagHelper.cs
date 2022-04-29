namespace WattleScript.Templating;

public class TagHelper
{
    public string Name { get; set; }
    public string Template { get; set; }

    public TagHelper(string name, string template)
    {
        Name = name;
        Template = template;
    }
}

public class TagHelperAttribute
{
    public string Name { get; set; }
    public bool Required { get; set; }
}