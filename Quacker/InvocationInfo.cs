using Microsoft.CodeAnalysis.Text;

namespace Quacker;

internal record InvocationInfo(
    string MethodKey,
    string FilePath,
    LinePositionSpan LineSpan,
    string? ReceiverType,
    IReadOnlyList<string> TypeArguments,
    IReadOnlyList<(string? Name, string Type, string ExpressionText)> Arguments,
    bool ViaConditionalAccess
);
