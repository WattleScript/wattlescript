namespace WattleScript.Interpreter
{
    public class PreprocessorDefine
    {
        public string Name { get; private set; }
        
        public PreprocessorDefineType Type { get; private set; }
        public string String { get; private set; }
        public double Number { get; private set; }
        public bool Boolean { get; private set; }

        public PreprocessorDefine(string name, string value)
        {
            Name = name;
            String = value;
            Type = PreprocessorDefineType.String;
        }

        public PreprocessorDefine(string name, double value)
        {
            Name = name;
            Number = value;
            Type = PreprocessorDefineType.Number;
        }

        public PreprocessorDefine(string name, bool value)
        {
            Name = name;
            Boolean = value;
            Type = PreprocessorDefineType.Boolean;
        }

        public PreprocessorDefine(string name)
        {
            Name = name;
            Type = PreprocessorDefineType.Empty;
        }

        public override string ToString()
        {
            if (Name == null) return "NULL NAME";
            switch (Type)
            {
                case PreprocessorDefineType.String:
                    return $"#define {Name} \"{String}\"";
                case PreprocessorDefineType.Boolean:
                    return $"#define {Name} {Boolean}";
                case PreprocessorDefineType.Number:
                    return $"#define {Name} {Number}";
                default:
                    return $"#define {Name}";
            }
        }
    }
}