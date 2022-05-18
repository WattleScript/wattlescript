namespace WattleScript.Templating;

public class TemplatingEngineOptions
{
    public enum RunModes
    {
        Debug,
        Release
    }
    
    public static readonly TemplatingEngineOptions Default = new TemplatingEngineOptions() {Optimise = true};
    
    /// <summary>
    /// True = a slightly longer parsing, a slightly slower execution
    /// False = a slightly faster parsing, a slightly faster execution
    /// </summary>
    public bool Optimise { get; set; }
    
    /// <summary>
    /// Debug = emit #line, use string sources where possible
    /// Release = don't emit #line, use byte[] sources where possible
    /// </summary>
    public RunModes RunMode { get; set; }
}