namespace WattleScript.Interpreter
{
    public class PreprocessorDefine
    {
        public string Name { get; private set; }
        
        public DefineType Type { get; private set; }
        public string StringValue { get; private set; }
        public double NumberValue { get; private set; }
        public bool BooleanValue { get; private set; }

        public PreprocessorDefine(string name, string value)
        {
            Name = name;
            StringValue = value;
            Type = DefineType.String;
        }

        public PreprocessorDefine(string name, double value)
        {
            Name = name;
            NumberValue = value;
            Type = DefineType.Number;
        }

        public PreprocessorDefine(string name, bool value)
        {
            Name = name;
            BooleanValue = value;
            Type = DefineType.Boolean;
        }

        public PreprocessorDefine(string name)
        {
            Name = name;
            Type = DefineType.Empty;
        }

        public override string ToString()
        {
            if (Name == null) return "NULL NAME";
            switch (Type)
            {
                case DefineType.String:
                    return $"#define {Name} \"{StringValue}\"";
                case DefineType.Boolean:
                    return $"#define {Name} {BooleanValue}";
                case DefineType.Number:
                    return $"#define {Name} {NumberValue}";
                default:
                    return $"#define {Name}";
            }
        }
    }
}