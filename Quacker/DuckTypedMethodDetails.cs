namespace Quacker;

internal record DuckTypedMethodDetails
{
    private readonly int _hashCode;
    public MethodKeyed MethodKeyed { get; init; }
    public IReadOnlyList<InvocationInfo> InvocationInfo { get; init; }

    public DuckTypedMethodDetails(
        MethodKeyed MethodKeyed,
        IReadOnlyList<InvocationInfo> InvocationInfo
    )
    {
        this.MethodKeyed = MethodKeyed;
        this.InvocationInfo = InvocationInfo;
        _hashCode = MethodKeyed.GetHashCode();

        foreach (var invocation in InvocationInfo)
        {
            _hashCode += invocation.GetHashCode();
        }
    }

    public virtual bool Equals(DuckTypedMethodDetails? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return _hashCode == other._hashCode;
    }

    public override int GetHashCode()
    {
        return _hashCode;
    }
}
