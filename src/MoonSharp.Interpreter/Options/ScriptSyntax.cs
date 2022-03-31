namespace MoonSharp.Interpreter
{
    /// <summary>
    /// Defines the syntax used by the compiler
    /// </summary>
    public enum ScriptSyntax
    {
        /// <summary>
        /// Standard Lua syntax + lambdas
        /// </summary>
        Lua,
        /// <summary>
        /// Backwards compatible C-like syntax
        /// </summary>
        CompatibleCLike,
        /// <summary>
        /// C-like syntax including breaking changes
        /// e.g. ++ and -- operators
        /// </summary>
        CLike,
    }
}