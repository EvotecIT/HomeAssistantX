namespace HomeAssistantX.Protocol;

internal sealed class CancellationAwareStringComparer : IComparer<string>
{
    private readonly StringComparison _comparison;
    private readonly CancellationToken _cancellationToken;

    internal CancellationAwareStringComparer(
        StringComparison comparison,
        CancellationToken cancellationToken)
    {
        if (comparison is not (StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(comparison));
        _comparison = comparison;
        _cancellationToken = cancellationToken;
    }

    public int Compare(string? x, string? y)
        => _comparison == StringComparison.Ordinal
            ? CancellationAwareString.CompareOrdinal(x, y, _cancellationToken)
            : CancellationAwareString.CompareOrdinalIgnoreCase(x, y, _cancellationToken);
}
