namespace WattleScript.Templating;

public class TemplatingEngineOptions
{
    public static readonly TemplatingEngineOptions Default = new TemplatingEngineOptions() {Optimise = true};
    
    /// <summary>
    /// True = a slightly longer parsing, a slightly slower execution
    /// False = a slightly faster parsing, a slightly faster execution
    /// </summary>
    public bool Optimise { get; set; }
}