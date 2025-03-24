using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WattleScript.HardwireGen;

[Generator]
public partial class HardwireSourceGenerator : IIncrementalGenerator
{
    static string Sanitize(string t)
    {
        return t.Replace(".", "_").Replace(",", "__").Replace("<", "___").Replace(">", "___");
    }
    
    class TypeFieldDesc
    {
        public ITypeSymbol Type = null!;
        public bool Read;
        public bool Write;
    }

    class TypeField
    {
        public int CurrLevel = 1000;
        public TypeFieldDesc Desc = null!;
        public string Name = null!;

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
        public string? PropertyName = null;
        private string? _name;

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
                   string.Join(";", Overloads.Select(x => x.ToDisplayString())) + ")";
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

    static bool IsUnsupported(ITypeSymbol type)
    {
        if (type.IsRefLikeType || type.TypeKind == TypeKind.Pointer ||
            type.TypeKind == TypeKind.FunctionPointer) return true;
        return false;
    }

    static bool IsHidden(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToString() == "WattleScript.Interpreter.WattleScriptHiddenAttribute")
                return true;
            if (attr.AttributeClass?.ToString() == "WattleScript.Interpreter.WattleScriptVisibleAttribute")
            {
                if (attr.ConstructorArguments[0].Value is false)
                    return true;
            }
        }

        return false;
    }

    static HardwireType FromTypeSymbol(INamedTypeSymbol symbol)
    {
        var n = symbol.TypeName();
        var tN = "T_" + IdGen.Create(n);

        Dictionary<string, TypeMethod> methods = new Dictionary<string, TypeMethod>();
        Dictionary<string, TypeField> fields = new Dictionary<string, TypeField>();
        List<CachedDiagnostic> diagnostics = new List<CachedDiagnostic>();

        // Get all accessible members
        foreach (var m in symbol.GetPublicMembers())
        {
            if (IsHidden(m.symbol))
            {
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
                        {
                            diagnostics.Add(new CachedDiagnostic(0, method.ToString(), null,
                                CachedLocation.Create(method.Locations.FirstOrDefault())));
                        }

                        continue;
                    }

                    if (method.MethodKind == MethodKind.Constructor)
                    {
                        if (!methods.ContainsKey("__new"))
                        {
                            var ctor = new TypeMethod() { Constructor = true };
                            ctor.SetName("__new");
                            methods.Add("__new", ctor);
                        }
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

                TypeFieldDesc desc = new TypeFieldDesc() { Type = property.Type };
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
                    fields.Add(property.Name, new TypeField() { Name = property.Name });
                fields[property.Name].Set(m.level, desc);
            }

            if (m.symbol is IFieldSymbol field)
            {
                if (IsUnsupported(field.Type)) continue;
                TypeFieldDesc desc = new TypeFieldDesc() { Type = field.Type };
                desc.Read = true;
                desc.Write = !field.IsReadOnly;
                if (!fields.ContainsKey(field.Name)) fields.Add(field.Name, new TypeField() { Name = field.Name });
                fields[field.Name].Set(m.level, desc);
            }
        }
        // Turn symbols into simple model

        List<HardwireMethod> hardwireMethods = new();
        List<HardwireField> hardwireFields = new();

        foreach (var m in methods.Values)
        {
            var overloads = new List<HardwireOverload>();
            for (int i = 0; i < m.Overloads.Count; i++)
            {
                var clsName = m.ClassName(i);
                var o = m.Overloads[i];
                var parameters = new List<HardwireParameter>();
                foreach (var p in o.Parameters)
                {
                    parameters.Add(new HardwireParameter() { Name = p.Name, Type = p.Type.TypeName() });
                }

                overloads.Add(new HardwireOverload(clsName, o.ReturnsVoid, new(parameters.ToArray())));
            }

            hardwireMethods.Add(new(m.Name, m.PropertyName, m.Property, m.Constructor, new(overloads.ToArray())));
        }

        foreach (var f in fields.Values)
        {
            hardwireFields.Add(new HardwireField(f.ClassName(), f.Name, f.Desc.Type.TypeName(), f.Desc.Read, f.Desc.Write));
        }


        return new HardwireType(n,
            tN,
            new(hardwireMethods.ToArray()),
            new(hardwireFields.ToArray()),
            new(diagnostics.ToArray()));
    }

    static HardwireType HardwireCreate(GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        var c = context.SemanticModel.GetDeclaredSymbol(context.TargetNode) as INamedTypeSymbol;
        return FromTypeSymbol(c!);
    }
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
    
    static bool TryGetTypeFromName(WellKnownTypeProvider provider, string type, out INamedTypeSymbol? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }
        if (type.Contains("<"))
        {
            var startIndex = type.IndexOf('<') + 1;
            var endIndex = type.LastIndexOf('>');
            if (endIndex == -1) return false;
            var generics = type.Substring(startIndex, endIndex - startIndex);
            var parts = GenericParts(generics).ToArray();
            var baseName = type.Substring(0, startIndex - 1) + "`" + parts.Length;
            if (!provider.TryGetOrCreateTypeByMetadataName(baseName, out var ts))
                return false;
            ITypeSymbol[] parameters = new ITypeSymbol[parts.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!TryGetTypeFromName(provider, parts[i], out var ptype))
                {
                    return false;
                }
                parameters[i] = ptype!;
               
            }
            resolved = ts.Construct(parameters);
            return true;
        }
        else
        {
            return provider.TryGetOrCreateTypeByMetadataName(type, out resolved);
        }
    }

    static ImmutableArray<TypeOrDiagnostic> CreateAdditionalTypes(
        ExtraClassList? extras,
        Compilation compilation)
    {
        if (extras == null)
            return ImmutableArray<TypeOrDiagnostic>.Empty;
        List<TypeOrDiagnostic> results = new();
        List<INamedTypeSymbol> toGenerate = new();
        var typeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

        foreach (var generate in extras.ExtraClasses)
        {
            if (TryGetTypeFromName(typeProvider, generate, out var sym))
            {
                toGenerate.Add(sym!);
            }
            else
            {
                results.Add(new(null, new CachedDiagnostic(HardwireInterop.TypeResolveWarning,
                    generate, extras.Path, null)));
            }
        }

        if (extras.ExtraClasses.Count == 0)
        {
            results.Add(new(null, new CachedDiagnostic(HardwireInterop.NoTypesWarning, 
                extras.Path, null, null)));
        }

        foreach(var sym in toGenerate)
        {
            results.Add(new(FromTypeSymbol(sym), null));
        }

        return results.ToImmutableArray();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var markedTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            "WattleScript.Interpreter.WattleScriptUserDataAttribute",
            predicate: (node, _) => node is ClassDeclarationSyntax || node is StructDeclarationSyntax,
            transform: HardwireCreate
        );

        var hardwireName = context.CompilationProvider
            .Select((x, token) => x.AssemblyName)
            .Select((x, _) => string.IsNullOrEmpty(x) ? "LuaHardwire" : $"LuaHardwire_{Sanitize(x!)}");

        context.RegisterSourceOutput(hardwireName, HardwireInterop.EntryPointPartial);

        var extraClasses =
            context
                .AdditionalTextsProvider
                .Where(x => x.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .Select((x, _) =>
                {
                    ExtraClassListXml? src = ExtraClassListXml.Get(x.Path);
                    return src == null
                        ? null
                        : new ExtraClassList(x.Path, new EquatableArray<string>(src.ExtraType));
                }).Where(x => x != null);

        var extraTypes =
            extraClasses.Combine(context.CompilationProvider)
                .Select((x, _) => CreateAdditionalTypes(x.Left, x.Right))
                .SelectMany((types, _) => types);
        
        var extraWarnings = extraTypes
            .Where(x => x.Diagnostic != null)
            .Select((x, _) => x.Diagnostic!.Value);

        var extraBindings = extraTypes
            .Where(x => x.Type != null)
            .Select((x, _) => x.Type!);
        
        context.RegisterImplementationSourceOutput(extraWarnings, HardwireInterop.EmitWarning);

        context.RegisterImplementationSourceOutput(markedTypes.Select((x, _) => x.InteropName).Collect().Combine(hardwireName), 
            HardwireInterop.MarkedEntryPoint);
        context.RegisterImplementationSourceOutput(
            extraBindings.Select((x, _) => x.InteropName).Collect().Combine(hardwireName),
            HardwireInterop.ExtraEntryPoint);
        context.RegisterImplementationSourceOutput(extraBindings.Combine(hardwireName), HardwireInterop.GenerateBinding);
        context.RegisterImplementationSourceOutput(markedTypes.Combine(hardwireName), HardwireInterop.GenerateBinding);
    }

}