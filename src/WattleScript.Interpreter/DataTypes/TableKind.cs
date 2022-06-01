namespace WattleScript.Interpreter
{
    public enum TableKind
    {
        /// <summary>
        /// Regular Table
        /// </summary>
        Normal,
        /// <summary>
        /// Table initialised as an enum
        /// </summary>
        Enum,
        /// <summary>
        /// Table initialised as a class
        /// </summary>
        Class,
        /// <summary>
        /// Table initialised as a mixin
        /// </summary>
        Mixin
    }
}