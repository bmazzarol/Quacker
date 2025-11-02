using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Quacker.Tests.Extensions;

public sealed class TestAdditionalFile(string path, SourceText text) : AdditionalText
{
    public override string Path => path;

    public override SourceText GetText(CancellationToken cancellationToken)
    {
        return text;
    }
}

public static class GeneratorDriverExtensions
{
    internal static GeneratorDriver BuildDriver(
        this string? source,
        ImmutableArray<AdditionalText> additionalTexts,
        Func<CSharpGeneratorDriver, GeneratorDriver>? configure = null
    )
    {
        var compilation = CSharpCompilation.Create(
            "name",
            source != null ? [CSharpSyntaxTree.ParseText(source)] : [],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DuckTypedAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var generator = new SourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        var generatorDriver = configure?.Invoke(driver) ?? driver;
        return generatorDriver.AddAdditionalTexts(additionalTexts).RunGenerators(compilation);
    }

    internal static SettingsTask IgnoreStandardSupportCode(this SettingsTask settings)
    {
        return settings.IgnoreGeneratedResult(x => x.HintName is "DuckTypedAttribute.g.cs");
    }
}
