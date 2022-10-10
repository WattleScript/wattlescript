using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WattleScript.Interpreter.CoreLib.StringLib
{
    static class PatternRegex
    {
        /*
         * Lua Pattern Quirks:
         * ^ is only observed in position 0 or directly after [
         * $ is only observed at the very end of the string, does NOT match \n (.NET equivalent is \z)
         * %z matches \0, not end of string (\x00 in regex)
         * ? * + - are regular characters in position 0
         * Single-line mode is required as '.' also matches \n in lua
         * Named capture groups must be excluded from the output, as we use them to implement %b()
         */
        public static Regex PatternToRegex(string pattern, out bool[] captureInfo)
        {
            return new Regex(PatternToRegexInternal(pattern, out captureInfo, false), RegexOptions.Singleline);
        }
        static string PatternToRegexInternal(string pattern, out bool[] captureInfo, bool captureList)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                captureInfo = Array.Empty<bool>();
                return "";
            }
            
            const string GROUP_NAMES = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int groupNameIdx = 0;
            
            var builder = new StringBuilder();
            bool isEscape = false;
            List<bool> captures = new List<bool>();
            int paren = 0;
            bool inCharList = false;
            
            int start = 1;
            
            //Special handling for first character, we need to do the regular escaping
            //and also escaping some extra characters that lua will process as regular
            //characters if they are in position zero
            if ("=#|*?$+-\\".IndexOf(pattern[0]) != -1) 
                builder.Append('\\');
            if (pattern[0] == '(')
            {
                paren++;
                captures.Add(1 < pattern.Length && pattern[1] == ')');
            }

            if (pattern[0] == '[')
            {
                builder.Append('[');
                inCharList = true;
                if (1 < pattern.Length && pattern[1] == '^') {
                    builder.Append('^');
                    start++;
                }
            } 
            else if (pattern[0] == '%') 
            {
                isEscape = true;
            }
            else
            {
                builder.Append(pattern[0]);
            }
            
            //Translate the rest of the string
            for (int i = start; i < pattern.Length; i++)
            {
                if (isEscape)
                {
                    switch (pattern[i])
                    {
                        case 'a':
                            builder.Append(@"\p{L}");
                            break;
                        case 'A':
                            builder.Append(@"\P{L}");
                            break;
                        case 's':
                            builder.Append(@"\s");
                            break;
                        case 'S':
                            builder.Append(@"\S");
                            break;
                        case 'd':
                            builder.Append(@"\d");
                            break;
                        case 'D':
                            builder.Append(@"\D");
                            break;
                        case 'w':
                            builder.Append(@"\w");
                            break;
                        case 'W':
                            builder.Append(@"\W");
                            break;
                        case 'c':
                            builder.Append(@"\p{C}");
                            break;
                        case 'C':
                            builder.Append(@"\P{C}");
                            break;
                        case 'g':
                            builder.Append(@"[^\p{C}\s]");
                            break;
                        case 'G':
                            builder.Append(@"[\p{C}\s]");
                            break;
                        case 'p':
                            builder.Append(@"\p{P}");
                            break;
                        case 'P':
                            builder.Append(@"\P{P}");
                            break;
                        case 'l':
                            builder.Append(@"\p{Ll}");
                            break;
                        case 'L':
                            builder.Append(@"\P{Ll}");
                            break;
                        case 'u':
                            builder.Append(@"\p{Lu}");
                            break;
                        case 'U':
                            builder.Append(@"\P{Lu}");
                            break;
                        case 'x':
                            builder.Append(@"[0-9A-Fa-f]");
                            break;
                        case 'X':
                            builder.Append(@"[^0-9A-Fa-f]");
                            break;
                        case 'b':
                            //Match balanced pairs using named capture groups
                            if (i < pattern.Length - 2)
                            {
                                var c1 = Regex.Escape(pattern[i + 1].ToString());
                                var c2 = Regex.Escape(pattern[i + 2].ToString());
                                var gn = GROUP_NAMES[groupNameIdx++];
                                builder.Append($@"{c1}(?>{c1}(?<{gn}>)|[^{c1}{c2}]+|{c2}(?<-{gn}>))*(?({gn})(?!)){c2}");
                                i += 2;
                            }
                            else
                                throw new ScriptRuntimeException("malformed pattern (missing arguments to '%b')");
                            break;
                        case 'f':
                            //Frontier pattern
                            //Get character class
                            if (i + 1 >= pattern.Length ||
                                pattern[i + 1] != '[')
                                throw new ScriptRuntimeException("missing '[' after '%f' in pattern");
                            int closeIndex = pattern.IndexOf(']', i + 1);
                            if (closeIndex == -1)
                            {
                                throw new ScriptRuntimeException("malformed pattern (missing ']')");
                            }
                            var subPattern = pattern.Substring(i + 2, closeIndex - (i + 2));
                            var translated = PatternToRegexInternal(subPattern, out _, true);
                            //First inverse lookahead of pattern (note ?! doesn't work here)
                            if(translated[0] == '^')
                                builder.Append("(?<=[").Append(translated.Substring(1)).Append("])");
                            else
                                builder.Append("(?<=[^").Append(translated).Append("])");
                            //Then regular lookahead of pattern
                            builder.Append("(?=[").Append(translated).Append("])");
                            i = closeIndex;
                            break;
                        case 'z':
                            builder.Append(@"\x00");
                            break;
                        case 'Z':
                            builder.Append(@"[^\x00]");
                            break;
                        case '.':
                        case '(':
                        case ')':
                        case '[':
                        case ']':
                        case '?':
                        case '+':
                        case '-':
                        case '*':
                        case '^':
                        case '$':
                        case '|':
                        case '#':
                            builder.Append('\\').Append(pattern[i]);
                            break;
                        default:
                            if (pattern[i] >= '0' && pattern[i] <= '9')
                            {
                                int idx =  (pattern[i] - '0');
                                if (captures.Count < idx) throw new ScriptRuntimeException($"invalid capture index %{pattern[i]}");
                                builder.Append('\\');
                            }

                            builder.Append(pattern[i]);
                            break;
                    }

                    isEscape = false;
                }
                else
                {
                    switch (pattern[i])
                    {
                        case '$' when i != (pattern.Length - 1):
                        case '|':
                        case '=':
                        case '^': //escape all ^ characters that aren't position 0
                        case '#':
                            builder.Append('\\').Append(pattern[i]); //Escape
                            break;
                        case '$' when i == (pattern.Length - 1):
                            builder.Append(@"\z");
                            break;
                        case '(':
                            paren++;
                            builder.Append('(');
                            captures.Add(i + 1 < pattern.Length && pattern[i + 1] == ')');
                            break;
                        case ')':
                            builder.Append(')');
                            if (--paren < 0) {
                                throw new ScriptRuntimeException("invalid pattern capture");
                            }
                            break;
                        case '[':
                            inCharList = true;
                            builder.Append('[');
                            //Negate
                            if (i + 1 < pattern.Length && pattern[i + 1] == '^') {
                                builder.Append('^');
                                i++;
                            }
                            break;
                        case ']':
                            if (inCharList)
                                inCharList = false;
                            builder.Append(']');
                            break;
                        case '%':
                            isEscape = true;
                            break;
                        case '-' when !inCharList && !captureList:
                            builder.Append("*?");
                            break;
                        case '\\':
                            builder.Append(@"\\");
                            break;
                        default:
                            builder.Append(pattern[i]);
                            break;
                    }
                }
            }
            if (paren > 0)
                throw new ScriptRuntimeException("unfinished capture");
            if (isEscape) 
                throw new ScriptRuntimeException("malformed pattern (ends with '%')");
            if (inCharList) 
                throw new ScriptRuntimeException("malformed pattern (missing ']')");
            captureInfo = captures.ToArray();
            return builder.ToString();
        }
    }
}