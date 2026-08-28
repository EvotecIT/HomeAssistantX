using System.Globalization;
using System.Numerics;
using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Models;

internal static class HomeAssistantAttributeReader
{
    public static string? GetString(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!TryGetValue(attributes, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    public static double? GetDouble(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!TryGetValue(attributes, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return IsFinite(number) ? number : null;
        }

        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && IsFinite(number))
        {
            return number;
        }

        return null;
    }

    public static long? GetInt64(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!TryGetValue(attributes, name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number
            && TryParseIntegralInt64(value.GetRawText(), out var integer))
        {
            return integer;
        }

        if (value.ValueKind == JsonValueKind.String
            && TryParseIntegralInt64(value.GetString(), out integer))
        {
            return integer;
        }

        return null;
    }

    public static long? GetNonNegativeInt64(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
    {
        var value = GetInt64(attributes, name);
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
    {
        if (!TryGetValue(attributes, name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var result) => result,
            JsonValueKind.String when string.Equals(value.GetString(), "yes", StringComparison.OrdinalIgnoreCase) => true,
            JsonValueKind.String when string.Equals(value.GetString(), "no", StringComparison.OrdinalIgnoreCase) => false,
            _ => null
        };
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
        if (!TryGetValue(attributes, name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind == JsonValueKind.String
                && item.GetString() is string text
                && !string.IsNullOrWhiteSpace(text))
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
    {
        return TryGetValue(attributes, name, out var value)
            && value.ValueKind == JsonValueKind.String
            && HomeAssistantTimestamp.TryParse(value.GetString(), out var result)
            ? result
            : null;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool TryParseIntegralInt64(string? value, out long result)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        return TryParseExactIntegralInt64(value, out result);
    }

    private static bool TryParseExactIntegralInt64(string? value, out long result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value!.Trim();
        var index = 0;
        var negative = false;
        if (text[index] is '+' or '-')
        {
            negative = text[index] == '-';
            if (++index == text.Length) return false;
        }

        var digits = new System.Text.StringBuilder(text.Length);
        var fractionalDigits = 0;
        var sawDigit = false;
        while (index < text.Length && IsAsciiDigit(text[index]))
        {
            digits.Append(text[index++]);
            sawDigit = true;
        }

        if (index < text.Length && text[index] == '.')
        {
            index++;
            var fractionStart = digits.Length;
            while (index < text.Length && IsAsciiDigit(text[index]))
            {
                digits.Append(text[index++]);
                sawDigit = true;
            }
            fractionalDigits = digits.Length - fractionStart;
        }

        if (!sawDigit) return false;
        var exponent = 0;
        if (index < text.Length && text[index] is 'e' or 'E')
        {
            index++;
            var exponentStart = index;
            if (index < text.Length && text[index] is '+' or '-') index++;
            while (index < text.Length && IsAsciiDigit(text[index])) index++;
            if (index == exponentStart || (index == exponentStart + 1 && text[exponentStart] is '+' or '-')) return false;
            if (!int.TryParse(text.Substring(exponentStart, index - exponentStart), NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent)) return false;
        }

        if (index != text.Length) return false;
        var scale = (long)fractionalDigits - exponent;
        var digitText = digits.ToString();
        if (scale > 0)
        {
            if (scale > digitText.Length) return digitText.All(character => character == '0');
            var scaleCount = (int)scale;
            if (digitText.Substring(digitText.Length - scaleCount).Any(character => character != '0')) return false;
            digitText = digitText.Substring(0, digitText.Length - scaleCount);
        }
        else if (scale < 0)
        {
            var appendedZeroCount = -scale;
            if (appendedZeroCount > 20 || digitText.Length + appendedZeroCount > 20) return false;
            digitText += new string('0', (int)appendedZeroCount);
        }

        digitText = digitText.TrimStart('0');
        if (digitText.Length == 0) return true;
        if (digitText.Length > 19) return false;
        var exact = BigInteger.Parse(digitText, CultureInfo.InvariantCulture);
        if (negative) exact = -exact;
        if (exact < long.MinValue || exact > long.MaxValue) return false;
        result = (long)exact;
        return true;
    }

    private static bool IsAsciiDigit(char value) => value >= '0' && value <= '9';

    internal static bool TryGetValue(IReadOnlyDictionary<string, JsonElement> attributes, string name, out JsonElement value)
    {
        if (attributes.TryGetValue(name, out value)) return true;
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
