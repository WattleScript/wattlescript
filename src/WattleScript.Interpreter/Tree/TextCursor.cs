using System;
using System.Text;

namespace WattleScript.Interpreter.Tree
{
    //Utility struct for stepping through a string
    struct TextCursor
    {
        public string Input;
        public int Index;
        public int Line;
        public int DefaultLine;
        public int Column;
        public bool StartOfLine;

        public TextCursor(string input)
        {
            Input = input;
            Index = 0;
            Line = 1;
            DefaultLine = 1;
            Column = 1;
            StartOfLine = true;
        }

        public bool NotEof() => Index < Input.Length;
        
        public void SkipWhiteSpace(StringBuilder output = null, bool doOutput = true)
        {
            for (; NotEof() && char.IsWhiteSpace(Char()); Next())
            {
                if (doOutput || Char() == '\n')
                    output?.Append(Char());
            }
        }

        public void Next()
        {
            if (NotEof())
            {
                Column++;
                if (Char() == '\n')
                {
                    Line++;
                    DefaultLine++;
                    Column = 1;
                    StartOfLine = true;
                }
                else if (!char.IsWhiteSpace(Char()))
                {
                    StartOfLine = false;
                }
                Index += 1;
            }
        }

        public char Char()
        {
            if (Index < Input.Length)
                return Input[Index];
            else
                return '\0'; //  sentinel
        }

        public char PeekNext()
        {
            if (Index + 1 < Input.Length)
                return Input[Index + 1];
            else
                return '\0';
        }

        public char CharNext()
        {
            Next();
            return Char();
        }
    }
}