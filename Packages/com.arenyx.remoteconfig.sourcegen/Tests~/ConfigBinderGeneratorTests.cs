#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Arenyx.RemoteConfig.SourceGen.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Arenyx.RemoteConfig.SourceGen.Tests
{
    [TestFixture]
    public sealed class ConfigBinderGeneratorTests
    {
        // Minimal Core interfaces needed for compilation.
        private const string CoreInterfaces = @"
#nullable enable
namespace Arenyx.RemoteConfig.Core
{
    public readonly record struct RemoteConfigValue(bool HasValue, bool Bool, long Long, double Double, string String);
    public readonly record struct RemoteConfigSettings(int FetchTimeoutMs, long MinimumFetchIntervalMs);

    public interface IRemoteConfigBackend
    {
        RemoteConfigValue GetValue(string key);
    }

    public interface IConfigBinder<T>
    {
        System.Collections.Generic.IReadOnlyDictionary<string, object> Defaults { get; }
        T Build(IRemoteConfigBackend backend, IJsonCodec? json, IConfigDiagnostics? diag);
    }

    public interface IJsonCodec
    {
        bool TryDeserialize<T>(string json, out T? value);
    }

    public interface IConfigDiagnostics
    {
        void OnMissingKey(string key);
        void OnParseError(string key, string? raw, string error);
        void OnSanitized(string key, string? raw, string reason);
    }
}
";

        // Attribute source (mirrors the real attributes).
        private const string Attributes = @"
#nullable enable
using System;

namespace Arenyx.RemoteConfig.SourceGen
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConfigModelAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ConfigKeyAttribute : Attribute
    {
        public string Key { get; }
        public bool DefaultBool { get; set; }
        public long DefaultLong { get; set; }
        public double DefaultDouble { get; set; }
        public string DefaultString { get; set; } = """";
        public long MinLong { get; set; } = long.MinValue;
        public long MaxLong { get; set; } = long.MaxValue;
        public double MinDouble { get; set; } = double.MinValue;
        public double MaxDouble { get; set; } = double.MaxValue;
        public bool Required { get; set; }
        public string? SanitizeReason { get; set; }
        public ConfigKeyAttribute(string key) { Key = key; }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ConfigJsonAttribute : Attribute
    {
        public string Key { get; }
        public string DefaultJson { get; set; } = ""{}"";
        public bool Required { get; set; }
        public ConfigJsonAttribute(string key) { Key = key; }
    }
}
";

        private const string SampleModel = @"
#nullable enable
using Arenyx.RemoteConfig.SourceGen;

namespace TestModels
{
    public sealed record BalanceData
    {
        public int Hp { get; init; }
        public int Atk { get; init; }
    }

    [ConfigModel]
    public sealed record GameplayConfig
    {
        [ConfigKey(""ff_new_shop"", DefaultBool = false)]
        public bool FfNewShop { get; init; }

        [ConfigKey(""pvp_turn_time_ms"", DefaultLong = 30000, MinLong = 5000, MaxLong = 60000)]
        public long PvpTurnTimeMs { get; init; }

        [ConfigJson(""balance_json_v1"", DefaultJson = ""{\""Hp\"":100,\""Atk\"":10}"")]
        public BalanceData? Balance { get; init; }
    }
}
";

        private (Compilation outputCompilation, ImmutableArray<Diagnostic> diagnostics) RunGenerator(params string[] sources)
        {
            var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            };

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ConfigBinderGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
            return (outputCompilation, diagnostics);
        }

        [Test]
        public void Generator_ProducesBinderSource()
        {
            var (output, diag) = RunGenerator(CoreInterfaces, Attributes, SampleModel);
            var generatorDiags = diag.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            Assert.That(generatorDiags, Is.Empty, $"Generator errors: {string.Join(", ", generatorDiags)}");

            var generatedTrees = output.SyntaxTrees.Where(t => t.FilePath.Contains("Binder.g.cs")).ToList();
            Assert.That(generatedTrees, Has.Count.GreaterThan(0), "No binder source generated.");
        }

        [Test]
        public void GeneratedCode_ContainsDefaultsDictionary()
        {
            var (output, _) = RunGenerator(CoreInterfaces, Attributes, SampleModel);
            var generatedSource = GetGeneratedSource(output, "GameplayConfigBinder");

            Assert.That(generatedSource, Does.Contain("Defaults"));
            Assert.That(generatedSource, Does.Contain("ff_new_shop"));
            Assert.That(generatedSource, Does.Contain("pvp_turn_time_ms"));
            Assert.That(generatedSource, Does.Contain("balance_json_v1"));
        }

        [Test]
        public void GeneratedCode_ContainsClampLogic()
        {
            var (output, _) = RunGenerator(CoreInterfaces, Attributes, SampleModel);
            var generatedSource = GetGeneratedSource(output, "GameplayConfigBinder");

            Assert.That(generatedSource, Does.Contain("5000L"));
            Assert.That(generatedSource, Does.Contain("60000L"));
            Assert.That(generatedSource, Does.Contain("OnSanitized"));
        }

        [Test]
        public void GeneratedCode_ContainsJsonParseCalls()
        {
            var (output, _) = RunGenerator(CoreInterfaces, Attributes, SampleModel);
            var generatedSource = GetGeneratedSource(output, "GameplayConfigBinder");

            Assert.That(generatedSource, Does.Contain("TryDeserialize"));
            Assert.That(generatedSource, Does.Contain("OnParseError"));
        }

        [Test]
        public void GeneratedCode_CompilesWithCoreInterfaces()
        {
            var (output, _) = RunGenerator(CoreInterfaces, Attributes, SampleModel);
            var errors = output.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            Assert.That(errors, Is.Empty,
                $"Generated code has compile errors:\n{string.Join("\n", errors.Select(e => e.ToString()))}");
        }

        private static string GetGeneratedSource(Compilation compilation, string binderName)
        {
            var tree = compilation.SyntaxTrees
                .FirstOrDefault(t => t.FilePath.Contains(binderName));

            Assert.That(tree, Is.Not.Null, $"Generated source for {binderName} not found.");
            return tree!.GetText().ToString();
        }
    }
}
