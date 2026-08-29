namespace HomeAssistantX.Protocol;

internal sealed class CancellationAwareStringEqualityComparer : IEqualityComparer<string>
{
    private readonly CancellationToken _cancellationToken;

    internal CancellationAwareStringEqualityComparer(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public bool Equals(string? x, string? y)
        => CancellationAwareString.EqualsOrdinalIgnoreCase(x, y, _cancellationToken);

    public int GetHashCode(string obj)
        => CancellationAwareString.GetOrdinalIgnoreCaseHashCode(obj, _cancellationToken);
}
