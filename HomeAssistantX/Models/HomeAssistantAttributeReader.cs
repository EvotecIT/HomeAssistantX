using System.Globalization;
using System.Text.Json;

namespace HomeAssistantX.Models;

internal static class HomeAssistantAttributeReader
{
    public static string? GetString(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!attributes.TryGetValue(name, out var value))
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
        if (!attributes.TryGetValue(name, out var value))
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
        if (!attributes.TryGetValue(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer))
        {
            return integer;
        }

        var number = GetDouble(attributes, name);
        if (!number.HasValue
            || number.Value < long.MinValue
            || number.Value > long.MaxValue
            || Math.Truncate(number.Value) != number.Value)
        {
            return null;
        }

        return checked((long)number.Value);
    }

    public static bool? GetBoolean(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!attributes.TryGetValue(name, out var value))
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
        if (!attributes.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Array)
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
        var value = GetString(attributes, name);
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var result)
            ? result
            : null;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
