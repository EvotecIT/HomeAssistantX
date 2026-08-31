namespace HomeAssistantX.Protocol;

internal sealed class CancellationAwareOrdinalStringEqualityComparer : IEqualityComparer<string>
{
    private readonly CancellationToken _cancellationToken;
    private readonly Action<int>? _hashTraversalObserver;

    internal CancellationAwareOrdinalStringEqualityComparer(CancellationToken cancellationToken)
        : this(cancellationToken, hashTraversalObserver: null)
    {
    }

    internal CancellationAwareOrdinalStringEqualityComparer(
        CancellationToken cancellationToken,
        Action<int>? hashTraversalObserver)
    {
        _cancellationToken = cancellationToken;
        _hashTraversalObserver = hashTraversalObserver;
    }

    public bool Equals(string? x, string? y)
        => CancellationAwareString.EqualsOrdinal(x, y, _cancellationToken);

    public int GetHashCode(string obj)
        => CancellationAwareString.GetOrdinalHashCode(obj, _cancellationToken, _hashTraversalObserver);
}
