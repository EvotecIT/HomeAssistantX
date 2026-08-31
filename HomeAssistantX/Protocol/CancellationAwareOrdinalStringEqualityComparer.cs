namespace HomeAssistantX.Protocol;

internal sealed class CancellationAwareOrdinalStringEqualityComparer : IEqualityComparer<string>
{
    private readonly CancellationToken _cancellationToken;

    internal CancellationAwareOrdinalStringEqualityComparer(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public bool Equals(string? x, string? y)
        => CancellationAwareString.EqualsOrdinal(x, y, _cancellationToken);

    public int GetHashCode(string obj)
        => CancellationAwareString.GetOrdinalHashCode(obj, _cancellationToken);
}
