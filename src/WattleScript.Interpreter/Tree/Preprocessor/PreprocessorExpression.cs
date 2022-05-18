using System;
using System.Collections.Generic;

namespace WattleScript.Interpreter.Tree
{
    abstract class PreprocessorExpression
    {
        public static PreprocessorExpression Create(DirectiveLexer lex)
        {
            var exp = PrimaryExpression(lex);
            var T = lex.Current;
            while (T.IsBinaryOperator())
            {
                var op = T.Type;
                lex.Next();
                var right = PrimaryExpression(lex);
                exp = new PreprocessorComparisonExpression(T, exp, right);
                T = lex.Current;
            }
            return exp;
        }
        
        static PreprocessorExpression PrimaryExpression(DirectiveLexer lex)
        {
            var T = lex.Current;
            switch (T.Type)
            {
                case TokenType.Preprocessor_Defined:
                    return new PreprocessorDefinedExpression(lex);
                case TokenType.Not:
                    return new PreprocessorNegateExpression(lex);
                case TokenType.Name:
                    return new DefineRefExpression(lex);
                case TokenType.True:
                    lex.Next();
                    return new LiteralPreprocessorExpression(DynValue.True);
                case TokenType.False:
                    lex.Next();
                    return new LiteralPreprocessorExpression(DynValue.False);
                case TokenType.String:
                    lex.Next();
                    return new LiteralPreprocessorExpression(DynValue.NewString(T.Text));
                case TokenType.Number: 
                case TokenType.Number_Hex:
                case TokenType.Number_HexFloat:
                    lex.Next();
                    return new LiteralPreprocessorExpression(DynValue.NewNumber(T.GetNumberValue()));
                case TokenType.Brk_Open_Round:
                    return new PreprocessorAdjustmentExpression(lex);
                case TokenType.Eof:
                    //TODO: Better error message
                    throw new SyntaxErrorException(T, "expected expression, got eof");
                default:
                    throw new SyntaxErrorException(T, "unexpected symbol {0}", T.Text);
            }
        }
        public bool GetBoolean(Dictionary<string, PreprocessorDefine> defines)
        {
            var val = Evaluate(defines);
            switch (val.Type) {
                case DataType.Boolean:
                    return val.Boolean;
                case DataType.Number:
                    return val.Number > 0;
                case DataType.String:
                    return !string.IsNullOrEmpty(val.String);
                default:
                    //This won't ever be thrown.
                    throw new InvalidOperationException("Invalid DynValue evaluated");
            }
        }
        
        protected void CheckTokenType(Token t, TokenType type)
        {
            if (t.Type != type)
            {
                //Premature stream termination not possible with preprocessor statements
                var display = t.Type == TokenType.Eof ? "\\n" : t.Text;
                throw new SyntaxErrorException(t, "unexpected symbol near '{0}'", display);
            }
        }
        
        public abstract DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines);
    }

    class PreprocessorComparisonExpression : PreprocessorExpression
    {
        public Token Token;
        public PreprocessorExpression Left;
        public PreprocessorExpression Right;
        public PreprocessorComparisonExpression(Token token, PreprocessorExpression left, PreprocessorExpression right)
        {
            Token = token; //Take the token so we can throw SyntaxErrorException on invalid comparisons
            Left = left;
            Right = right;
        }

        double GetNumber(PreprocessorExpression exp, Dictionary<string,PreprocessorDefine> defines)
        {
            var val = exp.Evaluate(defines);
            switch (val.Type)
            {
                case DataType.Boolean:
                    return val.Boolean ? 1 : 0;
                case DataType.Number:
                    return val.Number;
                case DataType.String:
                    throw new SyntaxErrorException(Token, "string may not be used in comparison");
                default:
                    throw new InvalidOperationException("Invalid DynValue evaluated");
            }
        }
        
        public override DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines)
        {
            switch (Token.Type)
            {
                case TokenType.And:
                    return DynValue.NewBoolean(Left.GetBoolean(defines) && Right.GetBoolean(defines));
                case TokenType.Or:
                    return DynValue.NewBoolean(Left.GetBoolean(defines) || Right.GetBoolean(defines));
                case TokenType.Op_Equal:
                    return DynValue.NewBoolean(Left.Evaluate(defines).Equals(Right.Evaluate(defines)));
                case TokenType.Op_NotEqual:
                    return DynValue.NewBoolean(!Left.Evaluate(defines).Equals(Right.Evaluate(defines)));
                case TokenType.Op_GreaterThan:
                    return DynValue.NewBoolean(GetNumber(Left, defines) > GetNumber(Right, defines));
                case TokenType.Op_GreaterThanEqual:
                    return DynValue.NewBoolean(GetNumber(Left, defines) >= GetNumber(Right, defines));
                case TokenType.Op_LessThan:
                    return DynValue.NewBoolean(GetNumber(Left, defines) < GetNumber(Right, defines));
                case TokenType.Op_LessThanEqual:
                    return DynValue.NewBoolean(GetNumber(Left, defines) <= GetNumber(Right, defines));
                default:
                    throw new NotImplementedException();
            }
        }
    }

    class PreprocessorNegateExpression : PreprocessorExpression
    {
        private PreprocessorExpression expression;

        public PreprocessorNegateExpression(DirectiveLexer lex)
        {
            lex.Next();
            expression = Create(lex);
        }

        public override DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines)
        {
            return DynValue.NewBoolean(!expression.GetBoolean(defines));
        }
    }

    class PreprocessorDefinedExpression : PreprocessorExpression
    {
        private string name;

        public PreprocessorDefinedExpression(DirectiveLexer lex)
        {
            lex.Next();
            CheckTokenType(lex.Next(), TokenType.Brk_Open_Round);
            var nameToken = lex.Next();
            CheckTokenType(nameToken, TokenType.Name);
            CheckTokenType(lex.Next(), TokenType.Brk_Close_Round);
            name = nameToken.Text;
        }

        public override DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines)
        {
            return DynValue.NewBoolean(defines.ContainsKey(name));
        }
    }

    class PreprocessorAdjustmentExpression : PreprocessorExpression
    {
        private PreprocessorExpression expression;
        
        public PreprocessorAdjustmentExpression(DirectiveLexer lex)
        {
            lex.Next();
            expression = Create(lex);
            CheckTokenType(lex.Next(), TokenType.Brk_Close_Round);
        }
        
        public override DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines)
        {
            return expression.Evaluate(defines);
        }
    }

    class LiteralPreprocessorExpression : PreprocessorExpression
    {
        private DynValue value;
        public LiteralPreprocessorExpression(DynValue value)
        {
            this.value = value;
        }
        public override DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines)
        {
            return value;
        }
    }

    class DefineRefExpression : PreprocessorExpression
    {
        public string Name;
        public DefineRefExpression(DirectiveLexer lex)
        {
            Name = lex.Current.Text;
            lex.Next();
        }

        public override DynValue Evaluate(Dictionary<string, PreprocessorDefine> defines)
        {
            if (!defines.TryGetValue(Name, out var def))
                return DynValue.False; //Doesn't exist = false
            switch (def.Type)
            {
                case PreprocessorDefineType.Boolean:
                    return DynValue.NewBoolean(def.Boolean);
                case PreprocessorDefineType.Number:
                    return DynValue.NewNumber(def.Number);
                case PreprocessorDefineType.String:
                    return DynValue.NewString(def.String);
                default:
                    return DynValue.True; //No value = true
            }
        }
    }
}