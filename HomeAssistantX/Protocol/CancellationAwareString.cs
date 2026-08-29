namespace HomeAssistantX.Protocol;

internal static class CancellationAwareString
{
    private const int ComparisonChunkLength = 256;

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

    internal static void Observe(string value, CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
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
        return Slice(value, start, end - start + 1, cancellationToken);
    }

    internal static string Slice(
        string value,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (start < 0 || length < 0 || start > value.Length - length)
            throw new ArgumentOutOfRangeException(nameof(start));
        cancellationToken.ThrowIfCancellationRequested();
        if (start == 0 && length == value.Length) return value;
        var result = new System.Text.StringBuilder(length);
        for (var index = 0; index < length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            result.Append(value[start + index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToString();
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
        for (var index = 0; index < left.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = GetOrdinalIgnoreCaseChunkLength(left, right, index);
            if (string.Compare(left, index, right, index, count, StringComparison.OrdinalIgnoreCase) != 0) return false;
            index += count;
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
            for (var index = 0; index < value.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = GetOrdinalIgnoreCaseChunkLength(value, null, index);
                var chunk = value.Substring(index, count);
                cancellationToken.ThrowIfCancellationRequested();
                hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(chunk);
                index += count;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return hash;
        }
    }

    internal static int GetOrdinalHashCode(
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
                hash = (hash * 31) + value[index];
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
        for (var index = 0; index < length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = GetOrdinalIgnoreCaseChunkLength(left, right, index);
            var comparison = string.Compare(left, index, right, index, count, StringComparison.OrdinalIgnoreCase);
            if (comparison != 0) return comparison;
            index += count;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return left.Length.CompareTo(right.Length);
    }

    private static int GetOrdinalIgnoreCaseChunkLength(string left, string? right, int index)
    {
        var remaining = right is null
            ? left.Length - index
            : Math.Min(left.Length, right.Length) - index;
        var count = Math.Min(ComparisonChunkLength, remaining);
        if (count == remaining) return count;

        var boundary = index + count;
        if (boundary > index
            && ((char.IsHighSurrogate(left[boundary - 1]) && char.IsLowSurrogate(left[boundary]))
                || (right is not null
                    && char.IsHighSurrogate(right[boundary - 1])
                    && char.IsLowSurrogate(right[boundary]))))
        {
            count--;
        }
        return count;
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
