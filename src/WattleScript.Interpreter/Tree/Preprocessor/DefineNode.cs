namespace WattleScript.Interpreter.Tree
{
    class DefineNode
    {
        public int StartLine;
        public int EndLine;
        public PreprocessorDefine Define;
        public DefineNode Next;
    }
}