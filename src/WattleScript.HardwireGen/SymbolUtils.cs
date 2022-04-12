using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace WattleScript.HardwireGen
{
    public static class SymbolUtils
    {
        public static string TypeName(this ITypeSymbol type)
        {
            return type.ToDisplayString(new SymbolDisplayFormat(
                SymbolDisplayGlobalNamespaceStyle.Omitted,
                SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            ));
        }
        public static IEnumerable<(int level,ISymbol symbol)> GetPublicMembers(this ITypeSymbol ts)
        {
            return IterateHierarchy(ts).SelectMany(n =>
            {
                return n.Item2.GetMembers().Where(x => x.DeclaredAccessibility == Accessibility.Public)
                    .Select(y => (n.Item1, y));
            });
        }
        public static IEnumerable<(int level, ITypeSymbol type)> IterateHierarchy(this ITypeSymbol type)
        {
            int i = 0;
            var current = type;
            while (current != null)
            {
                yield return (i++,current);
                current = current.BaseType;
            }
        }
    }
}