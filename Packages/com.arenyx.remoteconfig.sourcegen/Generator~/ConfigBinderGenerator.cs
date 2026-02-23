#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Arenyx.RemoteConfig.SourceGen.Generator
{
    [Generator]
    public sealed class ConfigBinderGenerator : IIncrementalGenerator
    {
        private const string ConfigModelAttributeName = "Arenyx.RemoteConfig.SourceGen.ConfigModelAttribute";
        private const string ConfigKeyAttributeName = "Arenyx.RemoteConfig.SourceGen.ConfigKeyAttribute";
        private const string ConfigJsonAttributeName = "Arenyx.RemoteConfig.SourceGen.ConfigJsonAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    ConfigModelAttributeName,
                    predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                    transform: static (ctx, _) => GetModelInfo(ctx))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

            // Emit individual binder files.
            context.RegisterSourceOutput(classDeclarations, static (spc, model) =>
            {
                var source = GenerateBinderSource(model);
                spc.AddSource($"{model.ClassName}Binder.g.cs", SourceText.From(source, Encoding.UTF8));
            });

            // Collect all models and emit a single module that registers all binders.
            var allModels = classDeclarations.Collect();
            context.RegisterSourceOutput(allModels, static (spc, models) =>
            {
                if (models.Length == 0) return;
                var source = GenerateModuleSource(models);
                spc.AddSource("GeneratedRemoteConfigModule.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }

        private static ConfigModelInfo? GetModelInfo(GeneratorAttributeSyntaxContext ctx)
        {
            var symbol = ctx.TargetSymbol as INamedTypeSymbol;
            if (symbol is null) return null;

            var properties = new List<ConfigPropertyInfo>();

            foreach (var member in symbol.GetMembers().OfType<IPropertySymbol>())
            {
                foreach (var attr in member.GetAttributes())
                {
                    var attrName = attr.AttributeClass?.ToDisplayString();

                    if (attrName == ConfigKeyAttributeName)
                    {
                        var prop = ParseConfigKeyAttribute(member, attr);
                        if (prop != null) properties.Add(prop);
                    }
                    else if (attrName == ConfigJsonAttributeName)
                    {
                        var prop = ParseConfigJsonAttribute(member, attr);
                        if (prop != null) properties.Add(prop);
                    }
                }
            }

            if (properties.Count == 0) return null;

            return new ConfigModelInfo
            {
                Namespace = symbol.ContainingNamespace.ToDisplayString(),
                ClassName = symbol.Name,
                FullTypeName = symbol.ToDisplayString(),
                Properties = properties
            };
        }

        private static ConfigPropertyInfo? ParseConfigKeyAttribute(IPropertySymbol prop, AttributeData attr)
        {
            if (attr.ConstructorArguments.Length == 0) return null;

            var key = attr.ConstructorArguments[0].Value?.ToString() ?? "";
            var info = new ConfigPropertyInfo
            {
                PropertyName = prop.Name,
                Key = key,
                IsJson = false,
                TypeName = prop.Type.ToDisplayString()
            };

            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "DefaultBool": info.DefaultBool = (bool)(named.Value.Value ?? false); break;
                    case "DefaultLong": info.DefaultLong = (long)(named.Value.Value ?? 0L); break;
                    case "DefaultDouble": info.DefaultDouble = (double)(named.Value.Value ?? 0.0); break;
                    case "DefaultString": info.DefaultString = named.Value.Value?.ToString() ?? ""; break;
                    case "MinLong": info.MinLong = (long)(named.Value.Value ?? long.MinValue); break;
                    case "MaxLong": info.MaxLong = (long)(named.Value.Value ?? long.MaxValue); break;
                    case "MinDouble": info.MinDouble = (double)(named.Value.Value ?? double.MinValue); break;
                    case "MaxDouble": info.MaxDouble = (double)(named.Value.Value ?? double.MaxValue); break;
                    case "Required": info.Required = (bool)(named.Value.Value ?? false); break;
                    case "SanitizeReason": info.SanitizeReason = named.Value.Value?.ToString(); break;
                }
            }

            // Determine the primitive kind from the property type.
            info.PrimitiveKind = prop.Type.SpecialType switch
            {
                SpecialType.System_Boolean => PrimitiveKind.Bool,
                SpecialType.System_Int64 => PrimitiveKind.Long,
                SpecialType.System_Double => PrimitiveKind.Double,
                SpecialType.System_String => PrimitiveKind.String,
                _ => PrimitiveKind.String // fallback
            };

            return info;
        }

        private static ConfigPropertyInfo? ParseConfigJsonAttribute(IPropertySymbol prop, AttributeData attr)
        {
            if (attr.ConstructorArguments.Length == 0) return null;

            var key = attr.ConstructorArguments[0].Value?.ToString() ?? "";
            var info = new ConfigPropertyInfo
            {
                PropertyName = prop.Name,
                Key = key,
                IsJson = true,
                TypeName = prop.Type.ToDisplayString()
            };

            foreach (var named in attr.NamedArguments)
            {
                switch (named.Key)
                {
                    case "DefaultJson": info.DefaultJson = named.Value.Value?.ToString() ?? "{}"; break;
                    case "Required": info.Required = (bool)(named.Value.Value ?? false); break;
                }
            }

            return info;
        }

        private static string GenerateBinderSource(ConfigModelInfo model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Arenyx.RemoteConfig.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace Arenyx.RemoteConfig.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public sealed partial class {model.ClassName}Binder : IConfigBinder<{model.FullTypeName}>");
            sb.AppendLine("    {");

            // ---- Defaults property ----
            sb.AppendLine("        public IReadOnlyDictionary<string, object> Defaults { get; } = new Dictionary<string, object>");
            sb.AppendLine("        {");
            foreach (var prop in model.Properties)
            {
                if (prop.IsJson)
                {
                    var escaped = (prop.DefaultJson ?? "{}").Replace("\"", "\\\"");
                    sb.AppendLine($"            [\"{prop.Key}\"] = \"{escaped}\",");
                }
                else
                {
                    sb.AppendLine($"            [\"{prop.Key}\"] = {FormatDefaultValue(prop)},");
                }
            }
            sb.AppendLine("        };");
            sb.AppendLine();

            // ---- Build method ----
            sb.AppendLine($"        public {model.FullTypeName} Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag)");
            sb.AppendLine("        {");

            foreach (var prop in model.Properties)
            {
                GeneratePropertyRead(sb, prop);
                sb.AppendLine();
            }

            // Construct the model.
            sb.AppendLine($"            var result = new {model.FullTypeName}");
            sb.AppendLine("            {");
            foreach (var prop in model.Properties)
            {
                sb.AppendLine($"                {prop.PropertyName} = __{prop.PropertyName},");
            }
            sb.AppendLine("            };");
            sb.AppendLine();
            sb.AppendLine("            PostProcess(ref result);");
            sb.AppendLine("            return result;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        partial void PostProcess(ref {model.FullTypeName} model);");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateModuleSource(ImmutableArray<ConfigModelInfo> models)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using Arenyx.RemoteConfig.Core;");
            sb.AppendLine();
            sb.AppendLine("namespace Arenyx.RemoteConfig.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Auto-generated module that registers all [ConfigModel] binders.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public sealed class GeneratedRemoteConfigModule : IRemoteConfigModule");
            sb.AppendLine("    {");
            sb.AppendLine("        public void Register(IRemoteConfigHostBuilder builder)");
            sb.AppendLine("        {");
            foreach (var model in models)
            {
                sb.AppendLine($"            builder.Add(new {model.ClassName}Binder());");
            }
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void GeneratePropertyRead(StringBuilder sb, ConfigPropertyInfo prop)
        {
            var varName = $"__{prop.PropertyName}";

            if (prop.IsJson)
            {
                // Strip trailing '?' so we don't emit BalanceConfig?? when the property is already nullable.
                var baseType = prop.TypeName.TrimEnd('?');
                var nullableType = baseType + "?";

                // JSON property
                sb.AppendLine($"            var {varName}Val = backend.GetValue(\"{prop.Key}\");");
                sb.AppendLine($"            {nullableType} {varName} = default;");
                sb.AppendLine($"            if ({varName}Val.HasValue && json != null)");
                sb.AppendLine("            {");
                sb.AppendLine($"                if (!json.TryDeserialize<{baseType}>({varName}Val.String, out var parsed))");
                sb.AppendLine("                {");
                sb.AppendLine($"                    diag?.OnParseError(\"{prop.Key}\", {varName}Val.String, \"Failed to deserialize {baseType}\");");
                var escapedDefault = (prop.DefaultJson ?? "{}").Replace("\"", "\\\"");
                sb.AppendLine($"                    json.TryDeserialize<{baseType}>(\"{escapedDefault}\", out {varName});");
                sb.AppendLine("                }");
                sb.AppendLine("                else");
                sb.AppendLine("                {");
                sb.AppendLine($"                    {varName} = parsed;");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                if (prop.Required)
                    sb.AppendLine($"                if (!{varName}Val.HasValue) diag?.OnMissingKey(\"{prop.Key}\");");
                sb.AppendLine($"                json?.TryDeserialize<{baseType}>(\"{escapedDefault}\", out {varName});");
                sb.AppendLine("            }");
            }
            else
            {
                // Primitive property
                sb.AppendLine($"            var {varName}Val = backend.GetValue(\"{prop.Key}\");");

                switch (prop.PrimitiveKind)
                {
                    case PrimitiveKind.Bool:
                        sb.AppendLine($"            var {varName} = {varName}Val.HasValue ? {varName}Val.Bool : {FormatDefaultValue(prop)};");
                        break;
                    case PrimitiveKind.Long:
                        sb.AppendLine($"            var {varName} = {varName}Val.HasValue ? {varName}Val.Long : {FormatDefaultValue(prop)};");
                        break;
                    case PrimitiveKind.Double:
                        sb.AppendLine($"            var {varName} = {varName}Val.HasValue ? {varName}Val.Double : {FormatDefaultValue(prop)};");
                        break;
                    case PrimitiveKind.String:
                        sb.AppendLine($"            var {varName} = {varName}Val.HasValue ? {varName}Val.String : {FormatDefaultValue(prop)};");
                        break;
                }

                if (prop.Required)
                    sb.AppendLine($"            if (!{varName}Val.HasValue) diag?.OnMissingKey(\"{prop.Key}\");");

                // Clamp for long
                if (prop.PrimitiveKind == PrimitiveKind.Long && (prop.MinLong != long.MinValue || prop.MaxLong != long.MaxValue))
                {
                    if (prop.MinLong != long.MinValue)
                    {
                        var reason = prop.SanitizeReason ?? $"Clamped to min {prop.MinLong}";
                        sb.AppendLine($"            if ({varName} < {prop.MinLong}L)");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                diag?.OnSanitized(\"{prop.Key}\", {varName}.ToString(), \"{reason}\");");
                        sb.AppendLine($"                {varName} = {prop.MinLong}L;");
                        sb.AppendLine("            }");
                    }
                    if (prop.MaxLong != long.MaxValue)
                    {
                        var reason = prop.SanitizeReason ?? $"Clamped to max {prop.MaxLong}";
                        sb.AppendLine($"            if ({varName} > {prop.MaxLong}L)");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                diag?.OnSanitized(\"{prop.Key}\", {varName}.ToString(), \"{reason}\");");
                        sb.AppendLine($"                {varName} = {prop.MaxLong}L;");
                        sb.AppendLine("            }");
                    }
                }

                // Clamp for double
                if (prop.PrimitiveKind == PrimitiveKind.Double && (prop.MinDouble != double.MinValue || prop.MaxDouble != double.MaxValue))
                {
                    if (prop.MinDouble != double.MinValue)
                    {
                        var reason = prop.SanitizeReason ?? $"Clamped to min {prop.MinDouble}";
                        sb.AppendLine($"            if ({varName} < {prop.MinDouble})");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                diag?.OnSanitized(\"{prop.Key}\", {varName}.ToString(), \"{reason}\");");
                        sb.AppendLine($"                {varName} = {prop.MinDouble};");
                        sb.AppendLine("            }");
                    }
                    if (prop.MaxDouble != double.MaxValue)
                    {
                        var reason = prop.SanitizeReason ?? $"Clamped to max {prop.MaxDouble}";
                        sb.AppendLine($"            if ({varName} > {prop.MaxDouble})");
                        sb.AppendLine("            {");
                        sb.AppendLine($"                diag?.OnSanitized(\"{prop.Key}\", {varName}.ToString(), \"{reason}\");");
                        sb.AppendLine($"                {varName} = {prop.MaxDouble};");
                        sb.AppendLine("            }");
                    }
                }
            }
        }

        private static string FormatDefaultValue(ConfigPropertyInfo prop)
        {
            return prop.PrimitiveKind switch
            {
                PrimitiveKind.Bool => prop.DefaultBool ? "true" : "false",
                PrimitiveKind.Long => $"{prop.DefaultLong}L",
                PrimitiveKind.Double => $"{prop.DefaultDouble}D",
                PrimitiveKind.String => $"\"{(prop.DefaultString ?? "").Replace("\"", "\\\"")}\"",
                _ => "\"\""
            };
        }
    }

    internal enum PrimitiveKind { Bool, Long, Double, String }

    internal sealed class ConfigModelInfo
    {
        public string Namespace { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string FullTypeName { get; set; } = "";
        public List<ConfigPropertyInfo> Properties { get; set; } = new();
    }

    internal sealed class ConfigPropertyInfo
    {
        public string PropertyName { get; set; } = "";
        public string Key { get; set; } = "";
        public string TypeName { get; set; } = "";
        public bool IsJson { get; set; }
        public PrimitiveKind PrimitiveKind { get; set; }

        // Primitive defaults
        public bool DefaultBool { get; set; }
        public long DefaultLong { get; set; }
        public double DefaultDouble { get; set; }
        public string? DefaultString { get; set; }

        // Clamp
        public long MinLong { get; set; } = long.MinValue;
        public long MaxLong { get; set; } = long.MaxValue;
        public double MinDouble { get; set; } = double.MinValue;
        public double MaxDouble { get; set; } = double.MaxValue;

        // JSON
        public string? DefaultJson { get; set; }

        // Diagnostics
        public bool Required { get; set; }
        public string? SanitizeReason { get; set; }
    }
}
