namespace HomeAssistantX.Protocol;

internal static class HomeAssistantRootRelativePath
{
    private static readonly Uri ValidationBase = new("https://homeassistant.invalid/", UriKind.Absolute);

    internal static bool IsValid(string? path)
        => path is not null
            && !string.IsNullOrWhiteSpace(path)
            && string.Equals(path, path.Trim(), StringComparison.Ordinal)
            && path.StartsWith("/", StringComparison.Ordinal)
            && !path.StartsWith("//", StringComparison.Ordinal)
            && !path.Contains('\\')
            && Uri.TryCreate(path, UriKind.Relative, out _)
            && Uri.TryCreate(ValidationBase, path, out var resolved)
            && string.Equals(resolved.PathAndQuery, path, StringComparison.Ordinal);
}
