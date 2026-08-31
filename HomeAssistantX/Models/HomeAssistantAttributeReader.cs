using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Models;

internal static class HomeAssistantAttributeReader
{
    private const int MaximumFloatingPointTextLength = 128;

    public static string? GetString(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => GetString(attributes, name, default);

    internal static string? GetString(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryGetValue(attributes, name, out var value, cancellationToken))
        {
            return null;
        }

        var result = value.ValueKind switch
        {
            JsonValueKind.String => DecodeString(value, cancellationToken),
            JsonValueKind.Number => DecodeRawText(value, cancellationToken),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
        ObserveString(result, cancellationToken);
        return result;
    }

    internal static string? GetStrictString(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryGetValue(attributes, name, out var value, cancellationToken)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var result = DecodeString(value, cancellationToken);
        ObserveString(result, cancellationToken);
        return result;
    }

    public static double? GetDouble(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => GetDouble(attributes, name, default);

    internal static double? GetDouble(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryGetValue(attributes, name, out var value, cancellationToken))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            var rawNumber = DecodeRawText(value, cancellationToken);
            ObserveString(rawNumber, cancellationToken);
            if (rawNumber.Length <= MaximumFloatingPointTextLength
                && value.TryGetDouble(out var numericValue))
            {
                return IsFinite(numericValue) ? numericValue : null;
            }
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? DecodeString(value, cancellationToken) : null;
        ObserveString(text, cancellationToken);
        if (text is not null
            && text.Length <= MaximumFloatingPointTextLength
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && IsFinite(number))
        {
            return number;
        }

        return null;
    }

    public static long? GetInt64(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => GetInt64(attributes, name, default);

    internal static long? GetInt64(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryGetValue(attributes, name, out var value, cancellationToken))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number
            && TryParseIntegralInt64(DecodeRawText(value, cancellationToken), cancellationToken, out var integer))
        {
            return integer;
        }

        var text = value.ValueKind == JsonValueKind.String ? DecodeString(value, cancellationToken) : null;
        ObserveString(text, cancellationToken);
        if (text is not null && TryParseIntegralInt64(text, cancellationToken, out integer))
        {
            return integer;
        }

        return null;
    }

    public static long? GetNonNegativeInt64(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
        => GetNonNegativeInt64(attributes, name, default);

    internal static long? GetNonNegativeInt64(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        var value = GetInt64(attributes, name, cancellationToken);
        return value >= 0 ? value : null;
    }

    public static int? GetNonNegativeInt32(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
    {
        var value = GetInt64(attributes, name);
        return value >= 0 && value <= int.MaxValue
            ? (int)value.Value
            : null;
    }

    public static bool? GetBoolean(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => GetBoolean(attributes, name, default);

    internal static bool? GetBoolean(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryGetValue(attributes, name, out var value, cancellationToken))
        {
            return null;
        }

        var text = value.ValueKind == JsonValueKind.String ? DecodeString(value, cancellationToken) : null;
        ObserveString(text, cancellationToken);
        bool? parsed = value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when text is { Length: <= 5 } && bool.TryParse(text, out var parsedBoolean) => parsedBoolean,
            JsonValueKind.String when string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) => true,
            JsonValueKind.String when string.Equals(text, "no", StringComparison.OrdinalIgnoreCase) => false,
            _ => null
        };
        cancellationToken.ThrowIfCancellationRequested();
        return parsed;
    }

    public static IReadOnlyList<string> GetStringList(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
        => GetStringList(attributes, name, default);

    internal static IReadOnlyList<string> GetStringList(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetValue(attributes, name, out var value, cancellationToken) || value.ValueKind != JsonValueKind.Array)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind == JsonValueKind.String
                && DecodeString(item, cancellationToken) is string text
                && HasNonWhitespace(text, cancellationToken))
            {
                result.Add(text);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public static DateTimeOffset? GetDateTimeOffset(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
        => GetDateTimeOffset(attributes, name, default);

    internal static DateTimeOffset? GetDateTimeOffset(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!TryGetValue(attributes, name, out var value, cancellationToken)
            || value.ValueKind != JsonValueKind.String)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
        var text = DecodeString(value, cancellationToken);
        ObserveString(text, cancellationToken);
        return HomeAssistantTimestamp.TryParse(text, out var result)
            ? result
            : null;
    }

    private static bool HasNonWhitespace(string value, CancellationToken cancellationToken)
    {
        var found = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            found |= !char.IsWhiteSpace(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return found;
    }

    private static void ObserveString(string? value, CancellationToken cancellationToken)
    {
        if (value is null) return;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string? DecodeString(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = value.GetString();
        ObserveString(result, cancellationToken);
        return result;
    }

    private static string DecodeRawText(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = value.GetRawText();
        ObserveString(result, cancellationToken);
        return result;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool TryParseIntegralInt64(
        string? value,
        CancellationToken cancellationToken,
        out long result)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is not null
            && value.Length <= 32
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        return TryParseExactIntegralInt64(value, cancellationToken, out result);
    }

    private static bool TryParseExactIntegralInt64(
        string? value,
        CancellationToken cancellationToken,
        out long result)
    {
        result = default;
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return false;

        var start = 0;
        var end = value.Length;
        while (start < end && char.IsWhiteSpace(value[start]))
        {
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }
        while (end > start && char.IsWhiteSpace(value[end - 1]))
        {
            if ((end & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (start == end) return false;

        var index = start;
        var negative = false;
        if (value[index] is '+' or '-')
        {
            negative = value[index] == '-';
            if (++index == end) return false;
        }

        var integerStart = index;
        long totalDigits = 0;
        long fractionalDigits = 0;
        var sawDigit = false;
        var sawNonZeroDigit = false;
        while (index < end && IsAsciiDigit(value[index]))
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            sawNonZeroDigit |= value[index] != '0';
            index++;
            totalDigits++;
            sawDigit = true;
        }

        var fractionStart = -1;
        var fractionEnd = -1;
        if (index < end && value[index] == '.')
        {
            index++;
            fractionStart = index;
            while (index < end && IsAsciiDigit(value[index]))
            {
                if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                sawNonZeroDigit |= value[index] != '0';
                index++;
                totalDigits++;
                fractionalDigits++;
                sawDigit = true;
            }
            fractionEnd = index;
        }

        var integerEnd = fractionStart < 0 ? index : fractionStart - 1;

        if (!sawDigit) return false;
        long exponent = 0;
        if (index < end && value[index] is 'e' or 'E')
        {
            index++;
            var exponentNegative = false;
            if (index < end && value[index] is '+' or '-')
            {
                exponentNegative = value[index++] == '-';
            }
            var exponentStart = index;
            var exponentLimit = exponentNegative ? 2147483648L : int.MaxValue;
            while (index < end && IsAsciiDigit(value[index]))
            {
                if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                var digit = value[index++] - '0';
                if (exponent > (exponentLimit - digit) / 10) return false;
                exponent = (exponent * 10) + digit;
            }
            if (index == exponentStart) return false;
            if (exponentNegative) exponent = -exponent;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (index != end) return false;
        if (!sawNonZeroDigit)
        {
            result = 0;
            return true;
        }
        var scale = fractionalDigits - exponent;
        var effectiveLength = totalDigits;
        if (scale > 0)
        {
            effectiveLength = totalDigits - scale;
        }
        else if (scale < 0)
        {
            var appendedZeroCount = -scale;
            // Leading source zeroes do not contribute to Int64 magnitude. Bound
            // only the appended significant zeroes; ConsumeIntegralDigit keeps
            // the final 19-digit/overflow contract exact.
            if (appendedZeroCount > 19) return false;
            effectiveLength = totalDigits + appendedZeroCount;
        }

        ulong magnitude = 0;
        var significantDigits = 0;
        long digitPosition = 0;
        var maximumMagnitude = negative ? 9223372036854775808UL : (ulong)long.MaxValue;
        for (var sourceIndex = integerStart; sourceIndex < integerEnd; sourceIndex++)
        {
            if ((sourceIndex & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!ConsumeIntegralDigit(value[sourceIndex], digitPosition++, effectiveLength, maximumMagnitude, ref magnitude, ref significantDigits)) return false;
        }
        if (fractionStart >= 0)
        {
            for (var sourceIndex = fractionStart; sourceIndex < fractionEnd; sourceIndex++)
            {
                if ((sourceIndex & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (!ConsumeIntegralDigit(value[sourceIndex], digitPosition++, effectiveLength, maximumMagnitude, ref magnitude, ref significantDigits)) return false;
            }
        }
        for (; digitPosition < effectiveLength; digitPosition++)
        {
            if ((digitPosition & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!ConsumeIntegralDigit('0', digitPosition, effectiveLength, maximumMagnitude, ref magnitude, ref significantDigits)) return false;
        }

        if (negative)
            result = magnitude == 9223372036854775808UL ? long.MinValue : -(long)magnitude;
        else
            result = (long)magnitude;
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static bool ConsumeIntegralDigit(
        char character,
        long position,
        long effectiveLength,
        ulong maximumMagnitude,
        ref ulong magnitude,
        ref int significantDigits)
    {
        var digit = character - '0';
        if (position >= effectiveLength) return digit == 0;
        if (significantDigits == 0 && digit == 0) return true;
        if (++significantDigits > 19 || magnitude > (maximumMagnitude - (uint)digit) / 10UL) return false;
        magnitude = (magnitude * 10UL) + (uint)digit;
        return true;
    }

    private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';

    internal static bool TryGetValue(IReadOnlyDictionary<string, JsonElement> attributes, string name, out JsonElement value)
        => TryGetValue(attributes, name, out value, default);

    internal static bool TryGetValue(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        out JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (attributes.TryGetValue(name, out value)) return true;
        foreach (var attribute in attributes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = default;
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }
}
