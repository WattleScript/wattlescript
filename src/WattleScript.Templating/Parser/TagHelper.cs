namespace WattleScript.Templating;

public class TagHelper
{
    public string Name { get; set; }

    public TagHelper(string name)
    {
        Name = name;
    }
}

public class TagHelperAttribute
{
    public string Name { get; set; }
    public bool Required { get; set; }
}