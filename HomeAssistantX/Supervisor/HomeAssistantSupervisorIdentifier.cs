namespace HomeAssistantX.Supervisor;

/// <summary>Validates native Home Assistant Supervisor identifiers.</summary>
public static class HomeAssistantSupervisorIdentifier
{
    /// <summary>Trims and validates a Home Assistant Supervisor app/add-on slug.</summary>
    public static bool TryNormalizeAppSlug(string? value, out string normalized)
        => TryNormalizeAppSlug(value, default, out normalized);

    internal static bool TryNormalizeAppSlug(
        string? value,
        CancellationToken cancellationToken,
        out string normalized)
    {
        cancellationToken.ThrowIfCancellationRequested();
        normalized = value is null ? string.Empty : Trim(value, cancellationToken);
        if (normalized.Length == 0 || normalized == "." || normalized == "..")
        {
            return false;
        }

        for (var index = 0; index < normalized.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = normalized[index];
            if (!((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character == '-'
                || character == '_'
                || character == '.'))
            {
                return false;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static string Trim(string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }
        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            if (((value.Length - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (end < start) return string.Empty;
        if (start == 0 && end == value.Length - 1) return value;
        var builder = new System.Text.StringBuilder(end - start + 1);
        for (var index = start; index <= end; index++)
        {
            if (((index - start) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            builder.Append(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return builder.ToString();
    }
}
