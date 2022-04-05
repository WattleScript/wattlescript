using System.Text;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;


namespace MoonSharp.Interpreter.Tree.Statements
{
    class UsingStatement : Statement
    {
        SourceRef m_Ref;

        public UsingStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            m_Ref = CheckTokenType(lcontext, TokenType.Using).GetSourceRef();
            lcontext.Source.Refs.Add(m_Ref);
            
            Token current = lcontext.Lexer.Current;

            if (current.Type != TokenType.Name)
            {
                throw new SyntaxErrorException(current, "using statement LHS must start with an identifier")
                {
                    IsPrematureStreamTermination = current.Type == TokenType.Eof
                };
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(current.Text);

            TokenType lastTokenType = TokenType.Name;
            
            while (true)
            {
                lcontext.Lexer.Next();
                current = lcontext.Lexer.Current;
                
                if (lastTokenType == TokenType.Dot)
                {
                    if (current.Type == TokenType.Name)
                    {
                        lastTokenType = TokenType.Name;
                        sb.Append(current.Text);
                        continue;
                    }
                }

                if (lastTokenType == TokenType.Name)
                {
                    if (current.Type == TokenType.Dot)
                    {
                        lastTokenType = TokenType.Dot;
                        sb.Append(current.Text);
                        continue;
                    }
                }

                break;
            }

            string str = sb.ToString();

            if (string.IsNullOrEmpty(str))
            {
                throw new SyntaxErrorException(current, "using statement LHS cannot be empty")
                {
                    IsPrematureStreamTermination = current.Type == TokenType.Eof
                };
            }

            if (str.EndsWith("."))
            {
                throw new SyntaxErrorException(current, "using statement cannot end with .")
                {
                    IsPrematureStreamTermination = current.Type == TokenType.Eof
                };
            }

            if (lcontext.Script is ScriptWithMetadata scriptExt)
            {
                scriptExt.Usings.Add(str);
            }
        }



        public override void Compile(ByteCode bc)
        {
            
        }
    }
}