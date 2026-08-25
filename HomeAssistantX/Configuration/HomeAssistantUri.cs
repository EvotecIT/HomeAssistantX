namespace HomeAssistantX.Configuration;

internal static class HomeAssistantUri
{
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
}
