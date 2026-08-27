using System.Globalization;
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
    {
        if (!TryGetValue(attributes, name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
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

        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            && exact >= long.MinValue
            && exact <= long.MaxValue
            && decimal.Truncate(exact) == exact)
        {
            result = decimal.ToInt64(exact);
            return true;
        }

        result = default;
        return false;
    }

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
