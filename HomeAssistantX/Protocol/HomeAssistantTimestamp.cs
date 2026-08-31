using System.Globalization;

namespace HomeAssistantX.Protocol;

internal static class HomeAssistantTimestamp
{
    private static readonly string[] UtcFormats =
    {
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'"
    };

    private static readonly string[] OffsetFormats =
    {
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    };

    public static bool TryParse(string? value, out DateTimeOffset result)
    {
        result = default;
        if (value is null)
        {
            return false;
        }

        return DateTimeOffset.TryParseExact(
                value,
                UtcFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out result)
            || DateTimeOffset.TryParseExact(
                value,
                OffsetFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
    }
}
