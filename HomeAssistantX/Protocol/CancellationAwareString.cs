namespace HomeAssistantX.Protocol;

internal static class CancellationAwareString
{
    internal static bool IsNullOrWhiteSpace(
        string? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return true;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!char.IsWhiteSpace(value[index])) return false;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    internal static string Trim(
        string value,
        CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
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
            if (((value.Length - 1 - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (start == 0 && end == value.Length - 1) return value;
        if (end < start) return string.Empty;
        var result = value.Substring(start, end - start + 1);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static string Concat(
        string left,
        string separator,
        string right,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new System.Text.StringBuilder(left.Length + separator.Length + right.Length);
        Append(result, left, cancellationToken);
        Append(result, separator, cancellationToken);
        Append(result, right, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var combined = result.ToString();
        cancellationToken.ThrowIfCancellationRequested();
        return combined;
    }

    internal static bool EqualsOrdinal(
        string? left,
        string? right,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (left[index] != right[index]) return false;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    internal static bool EqualsOrdinalIgnoreCase(
        string? left,
        string? right,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (char.ToUpperInvariant(left[index]) != char.ToUpperInvariant(right[index])) return false;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    internal static int GetOrdinalIgnoreCaseHashCode(
        string value,
        CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        cancellationToken.ThrowIfCancellationRequested();
        unchecked
        {
            var hash = 17;
            for (var index = 0; index < value.Length; index++)
            {
                if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                hash = (hash * 31) + char.ToUpperInvariant(value[index]);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return hash;
        }
    }

    internal static int CompareOrdinalIgnoreCase(
        string? left,
        string? right,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            var leftValue = char.ToUpperInvariant(left[index]);
            var rightValue = char.ToUpperInvariant(right[index]);
            if (leftValue < rightValue) return -1;
            if (leftValue > rightValue) return 1;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return left.Length.CompareTo(right.Length);
    }

    private static void Append(
        System.Text.StringBuilder builder,
        string value,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            builder.Append(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
