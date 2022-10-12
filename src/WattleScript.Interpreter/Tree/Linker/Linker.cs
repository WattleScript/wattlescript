using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Tree.Fast_Interface;
using WattleScript.Interpreter.Tree.Statements;
using static WattleScript.Interpreter.Tree.NodeBase;

namespace WattleScript.Interpreter.Tree
{
    class LinkerException : Exception
    {
        public LinkerException(string message) : base(message) { }
    }
    
    internal class Linker
    {
        public string CurrentNamespace { get; set; } = "";
        
        private Script script;
        private int sourceIndex;
        private bool outputChars = true;
        private ScriptLoadingContext lcontextLocal;
        private bool firstUsingEncountered = false;
        private bool anyNonUsingEncounterd = false;
        private string text;
        private StringBuilder usingIdent = new StringBuilder();
        private string lastNamespace;

        //public string ProcessedSource => output.ToString();
        public Dictionary<string, Module> ResolvedUsings = new Dictionary<string, Module>();
        public Dictionary<string, Dictionary<string, IStaticallyImportableStatement>> ImportMap = new Dictionary<string, Dictionary<string, IStaticallyImportableStatement>>();


        public Linker(Script script, int sourceIndex, string text, Dictionary<string, DefineNode> defines = null)
        {
            this.script = script;
            this.sourceIndex = sourceIndex;
            this.text = text;

            lcontextLocal = Loader_Fast.CreateLoadingContext(script, script.GetSourceCode(sourceIndex), text, defines, false, true, this);
        }

        public void StoreNamespace()
        {
            lastNamespace = CurrentNamespace;
        }

        public void RestoreNamespace()
        {
            CurrentNamespace = lastNamespace;
        }

        void ProcessNamespaceStatement(ScriptLoadingContext lcontext)
        {
            CheckTokenType(lcontext, TokenType.Namespace);
            bool canBeDot = false;
            StringBuilder namespaceIdent = new StringBuilder();

            while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
            {
                Token tkn = lcontext.Lexer.Current;

                if (!canBeDot && tkn.Type != TokenType.Name)
                {
                    break;
                }

                if (canBeDot && tkn.Type != TokenType.Dot)
                {
                    break;
                }

                canBeDot = !canBeDot;

                namespaceIdent.Append(tkn.Text);
                lcontext.Lexer.Next();
            }

            string namespaceIdentStr = namespaceIdent.ToString();
            lcontext.Linker.CurrentNamespace = namespaceIdentStr;

            if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly)
            {
                CheckTokenType(lcontext, TokenType.Brk_Open_Curly);
                Loop(lcontext, true);
            }
        }
        
        void ProcessUsingStatement(ScriptLoadingContext lcontext)
        {
            CheckTokenType(lcontext, TokenType.Using);
            int currentLineFrom = lcontext.Lexer.Current.FromLine;
            bool canBeDot = false;
            int charTo = 0;
            Token prev = null;
            usingIdent.Clear();

            while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
            {
                Token tkn = lcontext.Lexer.Current;

                if (tkn.FromLine != currentLineFrom)
                {
                    break;
                }

                prev = lcontext.Lexer.Current;

                canBeDot = canBeDot switch
                {
                    false when tkn.Type != TokenType.Name => throw new SyntaxErrorException(tkn, $"unexpected token '{tkn.Text}' found in using statement"),
                    true when tkn.Type != TokenType.Dot => throw new SyntaxErrorException(tkn, $"unexpected token '{tkn.Text}' found in using statement"),
                    _ => !canBeDot
                };

                usingIdent.Append(tkn.Text);
                lcontext.Lexer.Next();
            }

            string usingIdentStr = usingIdent.ToString();

            if (ResolvedUsings.ContainsKey(usingIdentStr))
            {
                throw new SyntaxErrorException(prev, $"duplicate using '{usingIdentStr}' found");
            }

            Module resolvedModule = script.Options.ScriptLoader.UsingResolver(usingIdentStr);

            if (resolvedModule == null)
            {
                throw new LinkerException($"module '{usingIdentStr}' failed to resolve");
            }

            if (resolvedModule.IsEntryRef)
            {
                return;
            }
            
            ResolvedUsings.Add(usingIdentStr, resolvedModule);
            Process(usingIdentStr, resolvedModule.Code);
        }

        void EnlistNamespace(string nmspc)
        {
            ImportMap.GetOrCreate(nmspc, () => new Dictionary<string, IStaticallyImportableStatement>());
        }

        void EnlistMember(IStaticallyImportableStatement statement)
        {
            EnlistNamespace("test");

            if (ImportMap["test"].TryGetValue(statement.NameToken.Text, out _))
            {
                throw new SyntaxErrorException(statement.NameToken, $"Member '{statement.NameToken.Text}' is already defined as {statement.DefinitionType}");
            }

            ImportMap["test"].Add(statement.NameToken.Text, statement);
        }

        public List<Statement> Export()
        {
            List<Statement> statements = new List<Statement>();

            foreach (KeyValuePair<string, Dictionary<string, IStaticallyImportableStatement>> nmspc in ImportMap)
            {
                foreach (KeyValuePair<string, IStaticallyImportableStatement> member in nmspc.Value)
                {
                    statements.Add((Statement) member.Value);
                }
            }

            return statements;
        }

        void Process(string nmspc, string code)
        {
            Script tmp = new Script(script.Options.CoreModules);
            SourceCode source = new SourceCode($"linker - {nmspc}", code, 0, tmp);
            Preprocessor preprocess = new Preprocessor(tmp, source.SourceID, source.Code);
            preprocess.Process();
            
            ScriptLoadingContext lcontextLib = Loader_Fast.CreateLoadingContext(script, source, preprocess.ProcessedSource, staticImport: this);

            Process(lcontextLib);
        }

        void Loop(ScriptLoadingContext lcontext, bool breakOnNextBlockEnd = false)
        {
            while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
            {
                lcontext.Lexer.Next();
                afterUsingStatement:
                Token tkn = lcontext.Lexer.Current;

                switch (tkn.Type)
                {
                    case TokenType.Brk_Close_Curly when breakOnNextBlockEnd:
                        break;
                    case TokenType.Namespace:
                        anyNonUsingEncounterd = true;
                        ProcessNamespaceStatement(lcontext);
                        break;
                    case TokenType.Using:
                        firstUsingEncountered = true;
                        ProcessUsingStatement(lcontext);
                        goto afterUsingStatement;
                    case TokenType.Enum:
                        anyNonUsingEncounterd = true;
                        EnlistMember(new EnumDefinitionStatement(lcontext));
                        break;
                    case TokenType.Class:
                        anyNonUsingEncounterd = true;
                        EnlistMember(new ClassDefinitionStatement(lcontext));
                        break;
                    case TokenType.Mixin:
                        anyNonUsingEncounterd = true;
                        EnlistMember(new MixinDefinitionStatement(lcontext));
                        break;
                    default:
                        anyNonUsingEncounterd = true;
                        break;
                }
            }
        }

        void Process(ScriptLoadingContext lcontext)
        {
            if (!text.Contains("using")) // heuristic, will trip on comments, variable names, etc. but might be worth it
            {
                // PushToOutput(text);
                // return;
            }

            Loop(lcontext);
            int z = 0;
        }

        public void Process()
        {
            Process(lcontextLocal);
        }
    }
}