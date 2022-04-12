namespace WattleScript.Interpreter
{
    public class Annotation
    {
        public string Name { get; private set; }
        public DynValue Value { get; private set; }

        public Annotation(string name, DynValue value)
        {
            Name = name;
            Value = value;
        }

        public override string ToString()
        {
            return $"{Name}: {Value}";
        }
    }
}