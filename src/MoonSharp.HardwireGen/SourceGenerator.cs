using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MoonSharp.HardwireGen
{
    [Generator]
    public partial class HardwireSourceGenerator : ISourceGenerator
    {
        private static DiagnosticDescriptor ByRefWarning = new("MS1001",
            "ByRef parameters can't be generated",
            "Method '{0}' has ref/out parameters and won't be described, mark with MoonSharpHiddenAttribute.",
            "MoonSharp.HardwireGen", DiagnosticSeverity.Warning, true);

        private static DiagnosticDescriptor NoFilesWarning = new("MS1002",
            "AdditionalFiles has no types listed",
            "File '{0}' has no additional types listed.",
            "MoonSharp.HardwireGen", DiagnosticSeverity.Warning, true);

        private static DiagnosticDescriptor TypeResolveWarning = new("MS1003",
            "Type could not be resolved",
            "Type '{0}' from AdditionalFile '{1}' could not be resolved.",
            "MoonSharp.HardwireGen", DiagnosticSeverity.Warning, true);

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new UserDataSyntaxReceiver());
        }

        static string Sanitize(string t)
        {
            return t.Replace(".", "_").Replace(",", "__").Replace("<", "___").Replace(">", "___");
        }


        private static string[] SkipTypes =
        {
            "System.Object", "System.Type"
        };

        TypeGenQueue types = new TypeGenQueue();
        private List<string> generatedClasses = new List<string>();
        private HashSet<string> blacklist = new HashSet<string>();

        static IEnumerable<string> GenericParts(string input)
        {
            var s = "";
            int bC = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '<')
                    bC++;
                if (input[i] == '>')
                    bC--;
                if (bC == 0 && input[i] == ',')
                {
                    yield return s;
                    s = "";
                }
                else
                {
                    s += input[i];
                }
            }

            if (s != "")
                yield return s;
        }

        static ITypeSymbol GetTypeFromName(ref GeneratorExecutionContext context, string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return null;
            if (type.Contains("<"))
            {
                var startIndex = type.IndexOf('<') + 1;
                var endIndex = type.LastIndexOf('>');
                if (endIndex == -1) return null;
                var generics = type.Substring(startIndex, endIndex - startIndex);
                var parts = GenericParts(generics).ToArray();
                var baseName = type.Substring(0, startIndex - 1) + "`" + parts.Length;
                INamedTypeSymbol ts = context.Compilation.GetTypeByMetadataName(baseName);
                if (ts == null) return null;
                ITypeSymbol[] parameters = new ITypeSymbol[parts.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    parameters[i] = GetTypeFromName(ref context, parts[i]);
                    if (parameters[i] == null) return null;
                }

                return ts.Construct(parameters);
            }
            else
            {
                return context.Compilation.GetTypeByMetadataName(type);
            }
        }

        void ProcessAdditionalFile(ref GeneratorExecutionContext context, AdditionalText file)
        {
            ExtraClassList ec;
            if ((ec = ExtraClassList.Get(file.Path)) != null)
            {
                if (ec.ExtraType != null && ec.ExtraType.Length > 0)
                {
                    foreach (var t in ec.ExtraType)
                    {
                        var x = t.Trim();
                        var ts = GetTypeFromName(ref context, x);
                        if (ts == null)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(TypeResolveWarning, null, x, file.Path));
                        }
                        else
                        {
                            types.Enqueue(ts);
                        }
                    }
                }
                else
                {
                    context.ReportDiagnostic(Diagnostic.Create(NoFilesWarning, null, file.Path));
                }

                if (ec.BlacklistType != null && ec.BlacklistType.Length > 0)
                {
                    foreach (var t in ec.BlacklistType)
                    {
                        var x = t.Trim();
                        var ts = GetTypeFromName(ref context, x);
                        if (ts == null)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(TypeResolveWarning, null, x, file.Path));
                        }
                        else
                        {
                            blacklist.Add(ts.TypeName());
                        }
                    }
                }
            }
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                var receiver = (UserDataSyntaxReceiver) context.SyntaxReceiver;
                var name = context.Compilation.Assembly.Name;
                if (string.IsNullOrEmpty(name))
                    name = IdGen.Create(context.Compilation.Assembly.NamespaceNames.FirstOrDefault() ??
                                        "_MoonSharp");
                name = "LuaHardwire_" + Sanitize(name);

                foreach (var classDeclaration in receiver.Candidates)
                {
                    var model = context.Compilation.GetSemanticModel(classDeclaration.SyntaxTree, true);
                    var type = ModelExtensions.GetDeclaredSymbol(model, classDeclaration) as ITypeSymbol;
                    if (type is null || !IsUserData(type))
                        continue;
                    types.Enqueue(type);
                }

                foreach (var file in context.AdditionalFiles)
                {
                    ProcessAdditionalFile(ref context, file);
                }

                while (types.Count > 0)
                {
                    TryGenerate(ref context, name, types.Dequeue());
                }

                var writer = new TabbedWriter();
                writer.AppendLine("// <auto-generated />");
                writer.Append("partial class ").AppendLine(name);
                writer.AppendLine("{").Indent();
                writer.AppendLine("public static void Initialize()");
                writer.AppendLine("{").Indent();
                foreach (var str in generatedClasses)
                {
                    writer.Append("MoonSharp.Interpreter.UserData.RegisterType(new ").Append(str).AppendLine("());");
                }

                writer.UnIndent().AppendLine("}");
                writer.UnIndent().AppendLine("}");
                context.AddSource($"{name}.g.cs", writer.ToString());
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + "> " + e.StackTrace.Replace('\n', ';'));
            }
        }

        static bool IsUserData(ITypeSymbol type)
        {
            return type.GetAttributes()
                .Any(a => a.AttributeClass?.ToString() == "MoonSharp.Interpreter.MoonSharpUserDataAttribute");
        }

        static bool IsHidden(ISymbol symbol)
        {
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass?.ToString() == "MoonSharp.Interpreter.MoonSharpHiddenAttribute")
                    return true;
                if (attr.AttributeClass?.ToString() == "MoonSharp.Interpreter.MoonSharpVisibleAttribute")
                {
                    if (attr.ConstructorArguments[0].Value is false)
                        return true;
                }
            }

            return false;
        }

        static string TypeName(ITypeSymbol type)
        {
            return type.ToDisplayString(new SymbolDisplayFormat(
                SymbolDisplayGlobalNamespaceStyle.Omitted,
                SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.ExpandNullable
            ));
        }


        void TryGenerate(ref GeneratorExecutionContext context, string containingName, ITypeSymbol type)
        {
            //skip blacklisted
            if (blacklist.Any(x => x == type.TypeName())) return;
            if (SkipTypes.Any(x => x == type.ToString())) return;
            //gen
            GenerateCode(ref context, containingName, type);
        }

        static bool IsUnsupported(ITypeSymbol type)
        {
            if (type.IsRefLikeType || type.TypeKind == TypeKind.Pointer ||
                type.TypeKind == TypeKind.FunctionPointer) return true;
            return false;
        }

        void GenerateCode(ref GeneratorExecutionContext context, string containingName, ITypeSymbol type)
        {
            var builder = new TabbedWriter();
            var typeName = TypeName(type);
            string classname = "T_" + IdGen.Create(typeName);
            generatedClasses.Add(classname);
            Dictionary<string, TypeMethod> methods = new Dictionary<string, TypeMethod>();
            Dictionary<string, TypeField> fields = new Dictionary<string, TypeField>();
            builder.AppendLine("// <auto-generated />");
            builder.Append("// UserData description for: ").AppendLine(typeName);

            builder.Append("internal partial class ").Append(containingName).AppendLine().AppendLine("{").Indent();

            builder.Append("public sealed class ").Append(classname).Append(" : ").AppendLine(CLS_USERDATA);
            builder.AppendLine("{").Indent();

            foreach (var m in type.GetPublicMembers())
            {
                if (IsHidden(m.symbol))
                {
                    builder.AppendLine("//Hidden Symbol: " + m.symbol.ToDisplayString());
                    continue;
                }

                if (m.symbol.IsStatic) continue;
                if (m.symbol is IMethodSymbol method)
                {
                    if (method.MethodKind == MethodKind.Ordinary ||
                        (method.MethodKind == MethodKind.Constructor && m.level == 0))
                    {
                        if (method.IsGenericMethod) continue;
                        bool byref = method.ReturnsByRef || method.ReturnsByRefReadonly;
                        foreach (var p in method.Parameters)
                        {
                            if (p.RefKind == RefKind.Ref || p.RefKind == RefKind.Out ||
                                p.RefKind == RefKind.RefReadOnly)
                            {
                                byref = true;
                                break;
                            }
                        }

                        bool unsupportedTypes = false;
                        foreach (var p in method.Parameters)
                        {
                            if (IsUnsupported(p.Type))
                            {
                                unsupportedTypes = true;
                                break;
                            }
                        }

                        if (unsupportedTypes) continue;
                        if (byref)
                        {
                            if (m.level == 0)
                                context.ReportDiagnostic(Diagnostic.Create(ByRefWarning,
                                    method.Locations.FirstOrDefault(), method.ToString()));
                            continue;
                        }

                        if (method.MethodKind == MethodKind.Constructor)
                        {
                            if (!methods.ContainsKey("__new"))
                                methods.Add("__new", new TypeMethod() {Constructor = true});
                            methods["__new"].AddMethod(0, method);
                        }
                        else
                        {
                            if (!methods.ContainsKey(method.Name)) methods.Add(method.Name, new TypeMethod());
                            methods[method.Name].AddMethod(m.level, method);
                        }
                    }
                }

                if (m.symbol is IPropertySymbol property)
                {
                    if (IsUnsupported(property.Type)) continue;
                    if (property.RefKind == RefKind.Ref)
                    {
                        continue;
                    }

                    if (property.IsIndexer)
                    {
                        continue; //name is this[]
                    }

                    TypeFieldDesc desc = new TypeFieldDesc() {Type = property.Type};
                    if (property.GetMethod != null)
                    {
                        desc.Read = true;
                        var name = "get_" + property.Name;
                        if (!methods.ContainsKey(name))
                            methods.Add(name, new TypeMethod()
                            {
                                Property = true,
                                PropertyName = property.Name
                            });
                        methods[name].SetName(name);
                        methods[name].AddMethod(m.level, property.GetMethod);
                    }
                    if (property.SetMethod != null)
                    {
                        desc.Write = true;
                        var name = "set_" + property.Name;
                        if (!methods.ContainsKey(name))
                            methods.Add(name, new TypeMethod()
                            {
                                Property = true,
                                PropertyName = property.Name,
                            });
                        methods[name].SetName(name);
                        methods[name].AddMethod(m.level, property.SetMethod);
                    }
                    if (!fields.ContainsKey(property.Name))
                        fields.Add(property.Name, new TypeField() {Name = property.Name});
                    fields[property.Name].Set(m.level, desc);
                }

                if (m.symbol is IFieldSymbol field)
                {
                    if (IsUnsupported(field.Type)) continue;
                    TypeFieldDesc desc = new TypeFieldDesc() {Type = field.Type};
                    desc.Read = true;
                    desc.Write = !field.IsReadOnly;
                    if (!fields.ContainsKey(field.Name)) fields.Add(field.Name, new TypeField() {Name = field.Name});
                    fields[field.Name].Set(m.level, desc);
                }
            }

            foreach (var m in methods.Values)
            {
                GenerateMethod(builder, typeName, m);
            }
            foreach(var f in fields.Values)
            {
                GenerateField(builder, typeName, f);
            }

            //Constructor
            builder.Append("internal ").Append(classname).Append("() : base(typeof(").Append(typeName).AppendLine("))");
            builder.AppendLine("{").Indent();
            foreach (var kv in methods)
            {
                builder.Append("this.AddMember(");
                builder.Append(kv.Key.ToLiteral());
                //new OverloadedMethodMemberDescriptor(name, typeof(type), new IOverloadableMemberDescriptor[] { classes });
                builder.Append(", new ").Append(CLS_OVERLOAD).Append(" (").Append(kv.Key.ToLiteral())
                    .Append(", typeof(").Append(typeName).Append("), new ").Append(CLS_OVERLOAD_MEMBER)
                    .AppendLine("[] { ");
                builder.Indent();
                for (int i = 0; i < kv.Value.Overloads.Count; i++)
                {
                    builder.Append("new ").Append(kv.Value.ClassName(i)).Append("()");
                    if (i + 1 < kv.Value.Overloads.Count)
                        builder.AppendLine(",");
                    else
                        builder.AppendLine();
                }

                builder.UnIndent();
                builder.AppendLine("}));");
            }

            builder.UnIndent().AppendLine("}");
            builder.UnIndent().AppendLine("}");
            builder.UnIndent().AppendLine("}");
            context.AddSource($"{classname}.g.cs", builder.ToString());
        }

        void GenerateField(TabbedWriter builder, string typeName, TypeField f)
        {
            builder.Append("private sealed class ").Append(f.ClassName()).Append(" : ").AppendLine(CLS_PROP_FIELD);
            builder.AppendLine("{").Indent();
            builder.Append("internal ").Append(f.ClassName()).AppendLine("() :");
            var access = 0;
            if (f.Desc.Read) access += 1; //CanRead
            if (f.Desc.Write) access += 2; //CanWrite
            builder.Indent().Append("base(typeof(").Append(f.Desc.Type.TypeName()).Append("),")
                .Append(f.Name.ToLiteral()).Append(",false,")
                .Append("(MoonSharp.Interpreter.Interop.BasicDescriptors.MemberDescriptorAccess)")
                .Append(access.ToString()).AppendLine(")").UnIndent();
            builder.AppendLine("{");
            builder.AppendLine("}");
            if (f.Desc.Read)
            {
                builder.AppendLine(
                        "protected override object GetValueImpl(MoonSharp.Interpreter.Script script, object obj) {")
                    .Indent();
                builder.Append("var self = (").Append(typeName).AppendLine(")obj;");
                builder.Append("return (object)self.").Append(f.Name).AppendLine(";");
                builder.UnIndent().AppendLine("}");
            }
            if (f.Desc.Write)
            {
                builder.AppendLine(
                        "protected override void SetValueImpl(MoonSharp.Interpreter.Script script, object obj, object value) {")
                    .Indent();
                builder.Append("var self = (").Append(typeName).AppendLine(")obj;");
                builder.Append("self.").Append(f.Name).Append(" = (").Append(f.Desc.Type.TypeName()).AppendLine(")value;");
                builder.UnIndent().AppendLine("}");
            }
            builder.UnIndent().AppendLine("}");
        }
        void GenerateMethod(TabbedWriter builder, string typeName, TypeMethod m)
        {
            for (int i = 0; i < m.Overloads.Count; i++)
            {
                var method = m.Overloads[i];
                builder.Append("private sealed class ").Append(m.ClassName(i)).Append(" : ").AppendLine(CLS_METHOD);
                builder.AppendLine("{").Indent();
                //ctor, add descriptors
                builder.Append("internal ").Append(m.ClassName(i)).Append("()");
                builder.AppendLine("{").Indent();
                //funcName, isStatic, ParameterDescriptor[], isExtensionMethod
                string isStatic = m.Constructor ? true : false;
                builder.Append("this.Initialize(").Append(m.Name.ToLiteral()).Append($", {isStatic}, new ")
                    .Append(CLS_PARAMETER).AppendLine("[] {");
                builder.Indent();
                int j = 0;
                foreach (var p in method.Parameters)
                {
                    j++;
                    builder.Append("new ").Append(CLS_PARAMETER).Append("(");
                    builder.Append(p.Name.ToLiteral()).Append(", ");
                    builder.Append("typeof(").Append(TypeName(p.Type)).Append("), ");
                    builder.Append("false, "); //hasDefault
                    builder.Append("null, "); //default
                    builder.Append("false, "); //out
                    builder.Append("false, "); //ref
                    if (p.IsParams) builder.Append("true");
                    else builder.Append("false");
                    if (j < method.Parameters.Length) builder.AppendLine("),");
                    else builder.AppendLine(")");
                }

                builder.UnIndent().AppendLine("}, false);");
                builder.UnIndent().AppendLine("}");
                //invoke
                builder.AppendLine(
                    "protected override object Invoke(MoonSharp.Interpreter.Script script, object obj, object[] pars, int argscount)");
                builder.AppendLine("{").Indent();
                if (!m.Constructor)
                    builder.Append("var self = (").Append(typeName).AppendLine(")obj;");
                if (!method.ReturnsVoid && !m.Constructor)
                {
                    builder.Append("return (object)");
                }

                if (!m.Constructor)
                {
                    builder.Append("self.");
                    builder.Append(m.PropertyName ?? method.Name);
                }
                else
                {
                    builder.Append("return new ");
                    builder.Append(typeName);
                }

                if (!m.Property)
                {
                    builder.Append("(");
                    for (int k = 0; k < method.Parameters.Length; k++)
                    {
                        builder.Append("(").Append(TypeName(method.Parameters[k].Type)).Append(")");
                        builder.Append("pars[");
                        builder.Append(k.ToString());
                        builder.Append("]");
                        if (k + 1 < method.Parameters.Length) builder.Append(", ");
                    }

                    builder.AppendLine(");");
                }
                else
                {
                    if (method.Parameters.Length > 0)
                    {
                        builder.Append(" = (").Append(TypeName(method.Parameters[0].Type)).Append(")pars[0]");
                    }

                    builder.AppendLine(";");
                }

                if (method.ReturnsVoid && !m.Constructor) builder.AppendLine("return null;");
                builder.UnIndent().AppendLine("}");
                builder.UnIndent().AppendLine("}");
            }
        }

        class TypeFieldDesc
        {
            public ITypeSymbol Type;
            public bool Read;
            public bool Write;
        }

        class TypeField
        {
            public int CurrLevel = 1000;
            public TypeFieldDesc Desc;
            public string Name;

            public string ClassName() => "F_" + Name;
            public void Set(int level, TypeFieldDesc desc)
            {
                if (level < CurrLevel)
                {
                    CurrLevel = level;
                    Desc = desc;
                }
            }
        }

        class TypeMethod
        {
            public List<IMethodSymbol> Overloads = new List<IMethodSymbol>();
            public int CurrentLevel = 1000;
            public bool Constructor = false;
            public bool Property = false;
            public string PropertyName = null;
            private string _name;

            public void SetName(string n)
            {
                _name = n;
            }

            public string Name => _name ?? (Overloads.Count > 0 ? Overloads[0].Name : "");

            public string ClassName(int overload)
            {
                return "M" + overload + "_" + Sanitize(Name);
            }

            public override string ToString()
            {
                if (Overloads.Count < 0) return "[empty]";
                return Overloads[0].Name + " (" +
                       string.Join(';', Overloads.Select(x => x.ToDisplayString())) + ")";
            }

            public void AddMethod(int level, IMethodSymbol symbol)
            {
                if (level > CurrentLevel) return;
                if (level < CurrentLevel)
                {
                    Overloads = new List<IMethodSymbol>();
                    CurrentLevel = level;
                }
                Overloads.Add(symbol);
            }
        }
    }

    public class UserDataSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> Candidates { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is not AttributeSyntax attribute)
                return;

            var name = ExtractName(attribute.Name);

            if (name != "MoonSharpUserData" && name != "MoonSharpUserDataAttribute")
                return;

            // "attribute.Parent" is "AttributeListSyntax"
            // "attribute.Parent.Parent" is a C# fragment the attribute is applied to
            if (attribute.Parent?.Parent is ClassDeclarationSyntax classDeclaration)
                Candidates.Add(classDeclaration);
        }

        private static string ExtractName(TypeSyntax type)
        {
            while (type != null)
            {
                switch (type)
                {
                    case IdentifierNameSyntax ins:
                        return ins.Identifier.Text;

                    case QualifiedNameSyntax qns:
                        type = qns.Right;
                        break;

                    default:
                        return null;
                }
            }

            return null;
        }
    }
}
