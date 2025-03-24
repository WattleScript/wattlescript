using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace WattleScript.HardwireGen;

using static ClassNames;

public static partial class HardwireInterop
{
    public const int ByRefWarning = 0;
    public const int NoTypesWarning = 1;
    public const int TypeResolveWarning = 2;
    
    private static DiagnosticDescriptor _byRefWarning = new("MS1001",
        "ByRef parameters can't be generated",
        "Method '{0}' has ref/out parameters and won't be described, mark with WattleScriptHiddenAttribute",
        "WattleScript.HardwireGen", DiagnosticSeverity.Warning, true);

    private static DiagnosticDescriptor _noTypesWarning = new("MS1002",
        "AdditionalFiles has no types listed",
        "File '{0}' has no additional types listed",
        "WattleScript.HardwireGen", DiagnosticSeverity.Warning, true);

    private static DiagnosticDescriptor _typeResolveWarning = new("MS1003",
        "Type could not be resolved",
        "Type '{0}' from AdditionalFile '{1}' could not be resolved",
        "WattleScript.HardwireGen", DiagnosticSeverity.Warning, true);

    private static DiagnosticDescriptor[] _diagnosticDescriptors = new[]
    {
        _byRefWarning, _noTypesWarning, _typeResolveWarning
    };
    
    static void GenerateField(TabbedWriter tw, string typeName, HardwireField f)
    {
        tw.Append("private sealed class ").Append(f.ClassName).Append(" : ").AppendLine(CLS_PROP_FIELD);
        tw.AppendLine("{").Indent();
        tw.Append("internal ").Append(f.ClassName).AppendLine("() :");
        var access = 0;
        if (f.Read) access += 1; //CanRead
        if (f.Write) access += 2; //CanWrite
        tw.Indent().Append("base(typeof(").Append(f.Type).Append("),")
            .Append(f.Name.ToLiteral()).Append(",false,")
            .Append("(WattleScript.Interpreter.Interop.BasicDescriptors.MemberDescriptorAccess)")
            .Append(access.ToString()).AppendLine(")").UnIndent();
        tw.AppendLine("{");
        tw.AppendLine("}");
        if (f.Read)
        {
            tw.AppendLine(
                    "protected override object GetValueImpl(WattleScript.Interpreter.Script script, object obj) {")
                .Indent();
            tw.Append("var self = (").Append(typeName).AppendLine(")obj;");
            tw.Append("return (object)self.").Append(f.Name).AppendLine(";");
            tw.UnIndent().AppendLine("}");
        }
        if (f.Write)
        {
            tw.AppendLine(
                    "protected override void SetValueImpl(WattleScript.Interpreter.Script script, object obj, object value) {")
                .Indent();
            tw.Append("var self = (").Append(typeName).AppendLine(")obj;");
            tw.Append("self.").Append(f.Name).Append(" = (").Append(f.Type).AppendLine(")value;");
            tw.UnIndent().AppendLine("}");
        }
        tw.UnIndent().AppendLine("}");
    }

    static void GenerateMethod(TabbedWriter tw, string typeName, HardwireMethod m)
    {
        var overloads = m.Overloads.AsSpan();
        for (int i = 0; i < m.Overloads.Count; i++)
        {
            var overload = overloads[i];
            tw.Append("private sealed class ").Append(overload.ClassName).Append(" : ").AppendLine(CLS_METHOD);
            tw.AppendLine("{").Indent();
            //ctor, add descriptors
            tw.Append("internal ").Append(overload.ClassName).Append("()");
            tw.AppendLine("{").Indent();
            //funcName, isStatic, ParameterDescriptor[], isExtensionMethod
            string isStatic = m.Constructor ? "true" : "false";
            tw.Append("this.Initialize(").Append(m.Name.ToLiteral()).Append($", {isStatic}, new ")
                .Append(CLS_PARAMETER).AppendLine("[] {");
            tw.Indent();
            int j = 0;
            foreach (var p in overload.Parameters)
            {
                j++;
                tw.Append("new ").Append(CLS_PARAMETER).Append("(");
                tw.Append(p.Name.ToLiteral()).Append(", ");
                tw.Append("typeof(").Append(p.Type).Append("), ");
                tw.Append("false, "); //hasDefault
                tw.Append("null, "); //default
                tw.Append("false, "); //out
                tw.Append("false, "); //ref
                if (p.IsParams) tw.Append("true");
                else tw.Append("false");
                if (j < overload.Parameters.Count) tw.AppendLine("),");
                else tw.AppendLine(")");
            }

            tw.UnIndent().AppendLine("}, false);");
            tw.UnIndent().AppendLine("}");
            //invoke
            tw.AppendLine(
                "protected override object Invoke(WattleScript.Interpreter.Script script, object obj, object[] pars, int argscount)");
            tw.AppendLine("{").Indent();
            if (!m.Constructor)
                tw.Append("var self = (").Append(typeName).AppendLine(")obj;");
            if (!overload.ReturnsVoid && !m.Constructor)
            {
                tw.Append("return (object)");
            }

            if (!m.Constructor)
            {
                tw.Append("self.");
                tw.Append(m.Name);
            }
            else
            {
                tw.Append("return new ");
                tw.Append(typeName);
            }

            var pr = overload.Parameters.AsSpan();
            if (!m.Property)
            {
                tw.Append("(");
                for (int k = 0; k < pr.Length; k++)
                {
                    tw.Append("(").Append(pr[k].Type).Append(")");
                    tw.Append("pars[");
                    tw.Append(k.ToString());
                    tw.Append("]");
                    if (k + 1 < pr.Length) tw.Append(", ");
                }

                tw.AppendLine(");");
            }
            else
            {
                if (pr.Length > 0)
                {
                    tw.Append(" = (").Append(pr[0].Type).Append(")pars[0]");
                }

                tw.AppendLine(";");
            }

            if (overload.ReturnsVoid && !m.Constructor) tw.AppendLine("return null;");
            tw.UnIndent().AppendLine("}");
            tw.UnIndent().AppendLine("}");
        }
    }

    public static void GenerateBinding(SourceProductionContext context, (HardwireType Left, string Right) data)
    {
        var (type, hardwireName) = data;

        // report diagnostics
        foreach (var d in type.Diagnostics)
        {
            EmitWarning(context, d);
        }

        // write interop classes
        var tw = new TabbedWriter();
        tw.AppendLine("// <auto-generated />");
        tw.AppendLine("// ReSharper disable All");
        tw.AppendLine($"partial class {hardwireName}");
        tw.AppendLine("{").Indent();
        tw.AppendLine($"private sealed class {type.InteropName} : {CLS_USERDATA}");
        using (tw.Block())
        {
            foreach (var m in type.Methods)
            {
                GenerateMethod(tw, type.TypeName, m);
            }
            foreach(var f in type.Fields)
            {
                GenerateField(tw, type.TypeName, f);
            }

            //Constructor
            tw.Append("internal ").Append(type.InteropName).Append("() : base(typeof(").Append(type.TypeName).AppendLine("))");
            using (tw.Block())
            {
                foreach (var kv in type.Methods)
                {
                    tw.Append("this.AddMember(");
                    tw.Append(kv.Name.ToLiteral());
                    //new OverloadedMethodMemberDescriptor(name, typeof(type), new IOverloadableMemberDescriptor[] { classes });
                    tw.Append(", new ").Append(CLS_OVERLOAD).Append(" (").Append(kv.Name.ToLiteral())
                        .Append(", typeof(").Append(type.TypeName).Append("), new ").Append(CLS_OVERLOAD_MEMBER)
                        .AppendLine("[] { ");
                    tw.Indent();
                    var overloads = kv.Overloads.AsSpan();
                    for (int i = 0; i < overloads.Length; i++)
                    {
                        tw.Append("new ").Append(overloads[i].ClassName).Append("()");
                        if (i + 1 < overloads.Length)
                            tw.AppendLine(",");
                        else
                            tw.AppendLine();
                    }

                    tw.UnIndent();
                    tw.AppendLine("}));");
                }

                foreach (var field in type.Fields)
                {
                    tw.Append("this.AddMember(");
                    tw.Append(field.Name.ToLiteral());
                    tw.Append(", ");
                    tw.Append("new ").Append(field.ClassName).AppendLine("());");
                }
            }
        }

        tw.UnIndent().AppendLine("}");
        context.AddSource($"{type.InteropName}.g.cs", tw.ToString());
    }

    public static void ExtraEntryPoint(SourceProductionContext context,
        (ImmutableArray<string> Left, string Right) data)
    {
        EntryPointImpl(context, data, "Extra");
    }

    public static void MarkedEntryPoint(SourceProductionContext context,
        (ImmutableArray<string> Left, string Right) data)
    {
        EntryPointImpl(context, data, "Marked");
    }

    public static void EmitWarning(SourceProductionContext context, CachedDiagnostic d)
    {
        if (d.Target2 != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(_diagnosticDescriptors[d.DiagnosticIndex],
                CachedLocation.ToLocation(d.Location), d.Target, d.Target2));
        }
        else
        {
            context.ReportDiagnostic(Diagnostic.Create(_diagnosticDescriptors[d.DiagnosticIndex],
                CachedLocation.ToLocation(d.Location), d.Target));
        }
    }

    static void EntryPointImpl(SourceProductionContext context, (ImmutableArray<string> Left, string Right) data, string kind)
    {
        var tw = new TabbedWriter();
        tw.AppendLine("// <auto-generated />");
        tw.AppendLine("// ReSharper disable All");
        tw.AppendLine($"internal partial class {data.Right}");
        using (tw.Block())
        {
            tw.AppendLine($"static partial void Register{kind}()");
            using (tw.Block())
            {
                foreach (var str in data.Left)
                {
                    tw.Append("WattleScript.Interpreter.UserData.RegisterType(new ").Append(str).AppendLine("());");
                }
            }
        }

        context.AddSource($"{data.Right}.impl{kind}.g.cs", tw.ToString());
    }

    public static void EntryPointPartial(SourceProductionContext context, string name)
    {
        var tw = new TabbedWriter();
        tw.AppendLine("// <auto-generated />");
        tw.AppendLine("// ReSharper disable All");
        tw.AppendLine($"internal partial class {name}");
        using (tw.Block())
        {
            tw.AppendLine("static partial void RegisterMarked();");
            tw.AppendLine("static partial void RegisterExtra();");
            tw.AppendLine("public static void Initialize()");
            using (tw.Block())
            {
                tw.AppendLine("RegisterMarked();");
                tw.AppendLine("RegisterExtra();");
            }
        }

        context.AddSource($"{name}.partial.g.cs", tw.ToString());
    }
}