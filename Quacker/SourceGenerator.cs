using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pasted;

namespace Quacker;

/// <summary>
/// Source generator for generating implementations of duck typing enabled methods.
/// </summary>
#pragma warning disable RS1038
[Generator]
#pragma warning restore RS1038
public sealed class SourceGenerator : IIncrementalGenerator
{
    private const string DefaultNamespace = nameof(Quacker);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource(
                $"{nameof(DuckTypedAttribute)}.g",
                EmbeddedFiles.DuckTypedAttribute_Source
            )
        );

        var annotatedMethodsRaw = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                $"{DefaultNamespace}.{nameof(DuckTypedAttribute)}",
                static (node, _) => node is TypeParameterSyntax,
                static (ctx, _) =>
                {
                    var typeParam = (ITypeParameterSymbol)ctx.TargetSymbol;
                    var method = typeParam.DeclaringMethod;
                    if (method is null)
                    {
                        // Not a method generic parameter; ignore for now
                        return null;
                    }
                    var key = GetSimpleMethodKey(method);
                    return new MethodKeyed(method, key);
                }
            )
            .Where(static x => x is not null)
            .Select(static (x, _) => x!);

        // De-duplicate methods that have multiple attributed generic parameters
        var annotatedMethods = annotatedMethodsRaw
            .Collect()
            .Select(static (list, _) => list.Distinct().ToArray())
            .SelectMany(static (arr, _) => arr);

        var invocations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntaxCtx, ct) =>
                {
                    var invocation = (InvocationExpressionSyntax)syntaxCtx.Node;

                    // Resolve the invoked symbol
                    if (
                        syntaxCtx
                            .SemanticModel.GetSymbolInfo(invocation, ct)
                            .CandidateSymbols.FirstOrDefault()
                        is not IMethodSymbol symbol
                    )
                    {
                        return null;
                    }

                    // Build a stable key for the target method
                    var methodKey = GetSimpleMethodKey(symbol);

                    // Extract details
                    var location = invocation.GetLocation();
                    var lineSpan = location.GetLineSpan().Span;
                    var filePath = location.SourceTree?.FilePath ?? "";

                    // Receiver type (if any)
                    string? receiverType = null;
                    if (invocation.Expression is MemberAccessExpressionSyntax maes)
                    {
                        var receiverTypeInfo = syntaxCtx
                            .SemanticModel.GetTypeInfo(maes.Expression, ct)
                            .Type;
                        receiverType = receiverTypeInfo?.ToDisplayString(
                            SymbolDisplayFormat.FullyQualifiedFormat
                        );
                    }

                    // Generic type arguments (if any)
                    var typeArgs = new List<string>();
                    if (invocation.Expression is GenericNameSyntax gns)
                    {
                        typeArgs.AddRange(
                            from ta in gns.TypeArgumentList.Arguments
                            let taType = syntaxCtx.SemanticModel.GetTypeInfo(ta, ct).Type
                            select taType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                ?? ta.ToString()
                        );
                    }

                    // Arguments (names, types, and raw text)
                    var args = (
                        from arg in invocation.ArgumentList.Arguments
                        let argType = syntaxCtx.SemanticModel.GetTypeInfo(arg.Expression, ct).Type
                        select (
                            arg.NameColon?.Name.Identifier.ValueText,
                            argType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                ?? "unknown",
                            arg.Expression.ToString()
                        )
                    ).ToList();

                    var viaConditional =
                        invocation.Expression is MemberBindingExpressionSyntax
                        || invocation.Parent is ConditionalAccessExpressionSyntax;

                    return new InvocationInfo(
                        methodKey,
                        filePath,
                        lineSpan,
                        receiverType,
                        typeArgs,
                        args,
                        viaConditional
                    );
                }
            )
            .Where(static i => i is not null)
            .Select(static (i, _) => i!);

        var invocationsByKey = invocations.Collect();

        var methodsWithCalls = annotatedMethods
            .Combine(invocationsByKey)
            .Select(
                static (pair, _) =>
                {
                    var (methodKeyed, allInvocations) = pair;
                    var calls = allInvocations
                        .Where(i =>
                            string.Equals(
                                i.MethodKey,
                                methodKeyed.MethodKey,
                                StringComparison.Ordinal
                            )
                        )
                        .ToArray();
                    return new DuckTypedMethodDetails(methodKeyed, calls);
                }
            );

        context.RegisterSourceOutput(methodsWithCalls, GenerateMethods);
    }

    private void GenerateMethods(SourceProductionContext ctx, DuckTypedMethodDetails details)
    {
        var method = details.MethodKeyed.Method;

        // Skip methods we cannot safely generate overloads for
        if (method.Parameters.Length == 0)
            return;
        if (method.Parameters.Any(p => p.RefKind != RefKind.None))
            return; // Minimal implementation: skip ref/out/in for now
        if (method.ContainingType is null)
            return;
        if (method.ContainingType.TypeKind is TypeKind.Interface)
            return; // do not attempt to add to interfaces

        // Build the original parameter type signature (fully qualified)
        var originalParamTypes = method
            .Parameters.Select(p =>
                p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            )
            .ToArray();
        var originalSignatureKey = string.Join(";", originalParamTypes);

        // Determine which method parameters are affected by duck-typed generic parameters
        var affectedTypeParams = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tp in method.TypeParameters)
        {
            foreach (var attr in tp.GetAttributes())
            {
                var attrName = attr.AttributeClass?.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                );
                if (
                    string.Equals(
                        attrName,
                        $"global::{DefaultNamespace}.{nameof(DuckTypedAttribute)}",
                        StringComparison.Ordinal
                    )
                )
                {
                    affectedTypeParams.Add(tp.Name);
                    break;
                }
            }
        }

        // Map affected parameter indices (parameters whose type is one of the affected type parameters)
        var affectedIndices = new List<int>();
        for (int i = 0; i < method.Parameters.Length; i++)
        {
            if (
                method.Parameters[i].Type is ITypeParameterSymbol tps
                && affectedTypeParams.Contains(tps.Name)
            )
            {
                affectedIndices.Add(i);
            }
        }

        if (affectedIndices.Count == 0)
            return; // nothing to generate

        // Collect distinct invocation type combinations, replacing only affected indices
        var combos = new HashSet<string>(StringComparer.Ordinal);
        var orderedCombos = new List<string[]>();
        foreach (
            var inv in details.InvocationInfo.Where(inv =>
                inv.Arguments.Count == method.Parameters.Length
            )
        )
        {
            // Start with original types, then replace affected ones from invocation
            var types = (string[])originalParamTypes.Clone();
            foreach (var idx in affectedIndices)
            {
                types[idx] = inv.Arguments[idx].Type;
            }

            var key = string.Join(";", types);
            if (string.Equals(key, originalSignatureKey, StringComparison.Ordinal))
                continue; // skip original signature
            if (combos.Add(key))
            {
                orderedCombos.Add(types);
            }
        }

        if (orderedCombos.Count == 0)
            return; // nothing to generate

        // Determine namespace
        var namespaceName = method.ContainingNamespace?.ToDisplayString();

        // Generate method overloads for each unique combination
        foreach (var combo in orderedCombos)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");

            if (!string.IsNullOrWhiteSpace(namespaceName))
            {
                sb.Append("namespace ").Append(namespaceName).AppendLine(";");
            }

            int indent = 0;
            void AppendIndent()
            {
                sb.Append(' ', indent * 4);
            }

            // Obtain the original method and containing type syntax to emit verbatim headers/footers
            MethodDeclarationSyntax? originalDecl;
            TypeDeclarationSyntax? typeDecl;
            var declRef = method.DeclaringSyntaxReferences.FirstOrDefault();
            if (declRef?.GetSyntax(ctx.CancellationToken) is MethodDeclarationSyntax mds)
            {
                originalDecl = mds;
                typeDecl = mds.Parent as TypeDeclarationSyntax;
            }
            else
            {
                continue;
            }

            // Emit the containing type header verbatim (attributes, modifiers, spacing), up to and including the '{'
            if (typeDecl is not null)
            {
                // We want the exact text from the first token (including attributes/trivia) to the end of the open brace
                var text = typeDecl.SyntaxTree.GetText(ctx.CancellationToken);
                var firstToken = typeDecl.GetFirstToken(includeZeroWidth: true);
                var headerStart = firstToken.FullSpan.Start;
                var headerEnd = typeDecl.OpenBraceToken.Span.End;

                var header = text.ToString(
                    new Microsoft.CodeAnalysis.Text.TextSpan(headerStart, headerEnd - headerStart)
                );
                sb.Append(header);
                sb.AppendLine();
                indent = 1; // we are now inside the type block
            }
            else
            {
                continue;
            }

            // Signature line
            AppendIndent();
            // Accessibility
            sb.Append(
                method.DeclaredAccessibility switch
                {
                    Accessibility.Public => "public ",
                    Accessibility.Internal => "internal ",
                    Accessibility.Private => "private ",
                    Accessibility.Protected => "protected ",
                    Accessibility.ProtectedAndInternal => "protected internal ",
                    Accessibility.ProtectedOrInternal => "private protected ",
                    _ => string.Empty,
                }
            );
            if (method.IsStatic)
            {
                sb.Append("static ");
            }
            // Return type and name
            var returnType = method.ReturnType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );
            sb.Append(returnType).Append(' ').Append(method.Name).Append('(');

            var paramList = new List<string>();
            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var p = method.Parameters[i];
                var newType = combo[i];
                var param = new StringBuilder();
                // preserve params modifier
                if (p.IsParams)
                    param.Append("params ");
                param.Append(newType).Append(' ').Append(p.Name);
                paramList.Add(param.ToString());
            }
            sb.Append(string.Join(", ", paramList));
            sb.AppendLine(")");
            // Copy the original method body syntax unaltered (arrow or full block)
            if (originalDecl.ExpressionBody is not null)
            {
                // Emit the exact expression-bodied arrow clause
                sb.Append(originalDecl.ExpressionBody.ToFullString());
            }
            else if (originalDecl.Body is not null)
            {
                // Emit the original block body including braces as-is
                var bodyText = originalDecl.Body.ToFullString();
                sb.Append(bodyText);
            }

            sb.AppendLine("}");

            // Emit source with a deterministic hint name
            var hintName = new StringBuilder()
                .Append("Quack.")
                .Append(method.ContainingType.Name)
                .Append('.')
                .Append(method.Name)
                .Append('_')
                // index of the combination
                .Append(orderedCombos.IndexOf(combo))
                .Append(".g.cs")
                .ToString();

            ctx.AddSource(hintName, sb.ToString());
        }
    }

    private static string GetSimpleMethodKey(IMethodSymbol method)
    {
        var sb = new StringBuilder();

        // Assembly name
        sb.Append(method.ContainingAssembly.Name).Append("::");

        // Containing type (with namespace)
        sb.Append(method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .Append('.')
            .Append(method.Name);

        return sb.ToString();
    }
}
