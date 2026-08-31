namespace HomeAssistantX.Configuration;

using System.Text;

internal static class HomeAssistantUri
{
    private const int EscapeChunkLength = 16000;
    public static Uri NormalizeBaseUri(Uri baseUri)
    {
        if (baseUri is null)
        {
            throw new ArgumentNullException(nameof(baseUri));
        }

        if (!baseUri.IsAbsoluteUri || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Home Assistant base URI must be an absolute HTTP or HTTPS URI.", nameof(baseUri));
        }

        var builder = new UriBuilder(baseUri)
        {
            Path = baseUri.AbsolutePath.TrimEnd('/') + "/",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }

    public static Uri BuildWebSocketUri(Uri baseUri)
    {
        var endpoint = new Uri(NormalizeBaseUri(baseUri), "api/websocket");
        var builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme == Uri.UriSchemeHttps ? "wss" : "ws"
        };
        return builder.Uri;
    }

    internal static string EscapeDataString(string value, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (value.Length <= EscapeChunkLength)
        {
            return Uri.EscapeDataString(value);
        }

        return cancellationToken.CanBeCanceled
            ? HomeAssistantX.Protocol.HomeAssistantJson.RunCancellationIsolated(
                () => EscapeDataStringInline(value, cancellationToken),
                cancellationToken)
            : EscapeDataStringInline(value, cancellationToken);
    }

    internal static string EscapeDataStringInline(string value, CancellationToken cancellationToken)
    {
        var escaped = new StringBuilder(value.Length);
        for (var offset = 0; offset < value.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(EscapeChunkLength, value.Length - offset);
            if (offset + length < value.Length
                && char.IsHighSurrogate(value[offset + length - 1])
                && char.IsLowSurrogate(value[offset + length]))
            {
                length--;
            }

            escaped.Append(Uri.EscapeDataString(value.Substring(offset, length)));
            offset += length;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return escaped.ToString();
    }
}
