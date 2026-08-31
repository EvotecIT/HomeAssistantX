namespace HomeAssistantX.Protocol;

internal static class HomeAssistantRootRelativePath
{
    internal const int MaximumLength = 16 * 1024;
    private static readonly Uri ValidationBase = new("https://homeassistant.invalid/", UriKind.Absolute);

    internal static bool IsValid(string? path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (path is null
            || path.Length == 0
            || path.Length > MaximumLength
            || path[0] != '/'
            || (path.Length > 1 && path[1] == '/')) return false;

        for (var index = 0; index < path.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (path[index] == '\\' || (char.IsWhiteSpace(path[index]) && (index == 0 || index == path.Length - 1))) return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(path, UriKind.Relative, out _)) return false;
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(ValidationBase, path, out var resolved)) return false;
        cancellationToken.ThrowIfCancellationRequested();
        return EqualsWithCancellation(resolved.PathAndQuery, path, cancellationToken);
    }

    private static bool EqualsWithCancellation(string left, string right, CancellationToken cancellationToken)
    {
        if (left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (left[index] != right[index]) return false;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }
}
