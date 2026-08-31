namespace HomeAssistantX.Recorder;

/// <summary>Normalizes Recorder entity globs that can match canonical Home Assistant entity identifiers.</summary>
internal static class HomeAssistantRecorderEntityGlob
{
    internal static bool TryNormalize(
        string? value,
        out string normalized,
        CancellationToken cancellationToken = default)
    {
        normalized = string.Empty;
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return false;

        HomeAssistantX.Protocol.CancellationAwareString.Observe(value, cancellationToken);
        normalized = value;
        return true;
    }
}
