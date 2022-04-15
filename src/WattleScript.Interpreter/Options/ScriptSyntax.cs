namespace WattleScript.Interpreter
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
        /// WattleScript syntax
        /// e.g. ++ and -- operators
        /// </summary>
        WattleScript
    }
}