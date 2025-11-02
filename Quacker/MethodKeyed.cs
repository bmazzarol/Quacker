using Microsoft.CodeAnalysis;

namespace Quacker;

internal record MethodKeyed(IMethodSymbol Method, string MethodKey)
{
    public virtual bool Equals(MethodKeyed? other)
    {
        if (other is null)
            return false;
        return ReferenceEquals(this, other)
            || string.Equals(MethodKey, other.MethodKey, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(MethodKey);
    }
}
