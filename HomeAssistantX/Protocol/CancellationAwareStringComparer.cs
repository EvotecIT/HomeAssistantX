namespace HomeAssistantX.Protocol;

internal sealed class CancellationAwareStringComparer : IComparer<string>
{
    private readonly StringComparer _comparer;
    private readonly CancellationToken _cancellationToken;

    internal CancellationAwareStringComparer(
        StringComparer comparer,
        CancellationToken cancellationToken)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _cancellationToken = cancellationToken;
    }

    public int Compare(string? x, string? y)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return _comparer.Compare(x, y);
    }
}
