namespace WattleScript.Templating;

public class TagHelper
{
    public string Name { get; set; }
}

public class TagHelperAttribute
{
    public string Name { get; set; }
    public bool Required { get; set; }
}