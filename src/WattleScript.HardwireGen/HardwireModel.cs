using Microsoft.CodeAnalysis.Text;

namespace WattleScript.HardwireGen;
using Microsoft.CodeAnalysis;

public record HardwireType(
    string TypeName,
    string InteropName,
    EquatableArray<HardwireMethod> Methods,
    EquatableArray<HardwireField> Fields,
    EquatableArray<CachedDiagnostic> Diagnostics);

public record struct TypeOrDiagnostic(HardwireType? Type, CachedDiagnostic? Diagnostic);

public record struct HardwireField(string ClassName, string Name, string Type, bool Read, bool Write);
public record struct HardwireParameter(string Name, string Type, bool IsParams);
public record struct HardwireOverload(string ClassName, bool ReturnsVoid, EquatableArray<HardwireParameter> Parameters);
public record struct HardwireMethod(string Name, bool Property,  bool Constructor, EquatableArray<HardwireOverload> Overloads);

public record struct CachedDiagnostic(int DiagnosticIndex, string Target, string? Target2, CachedLocation? Location);

public record struct CachedLocation(string Path, int TextStart, int TextLength, int StartLine, int StartChar, int EndLine, int EndChar)
{
    public static CachedLocation? Create(Location? location)
    {
        if (location is null)
        {
            return null;
        }
        var sp = location.GetLineSpan();
        return new(sp.Path, location.SourceSpan.Start, location.SourceSpan.Length, sp.Span.Start.Line, sp.Span.Start.Character, sp.Span.End.Line, sp.Span.End.Character);
    }

    public static Location? ToLocation(CachedLocation? self)
    {
        if (self is null)
        {
            return null;
        }
        var s = self.Value;
        return Location.Create(s.Path, new TextSpan(s.TextStart, s.TextLength),
            new LinePositionSpan(new LinePosition(s.StartLine, s.StartChar), new LinePosition(s.EndLine, s.EndChar)));
    }
}

public record ExtraClassList(string Path, EquatableArray<string> ExtraClasses);