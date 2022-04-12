using System;
using Microsoft.CodeAnalysis.CSharp;

namespace MoonSharp.HardwireGen
{
    public static class StringUtils
    {
        public static string ToLiteral(this string input)
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(input)).ToFullString();
        }

    }
}