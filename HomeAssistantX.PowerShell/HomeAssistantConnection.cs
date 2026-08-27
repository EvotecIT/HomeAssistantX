using HomeAssistantX.Authentication;

namespace HomeAssistantX.PowerShell;

/// <summary>An explicit, disposable Home Assistant session passed between cmdlets.</summary>
public sealed class HomeAssistantConnection : IDisposable
{
    private int _disposed;

    internal HomeAssistantConnection(string? name, HomeAssistantClient client)
    {
        var hasExplicitName = !string.IsNullOrWhiteSpace(name);
        Name = hasExplicitName ? name!.Trim() : client.Options.BaseUri.Host;
        ConfirmationName = hasExplicitName ? Name : "Home Assistant";
        Client = client;
    }

    internal string ConfirmationName { get; }

    public string Name { get; }

    public Uri Uri => Client.Options.BaseUri;

    public HomeAssistantClient Client { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public static HomeAssistantConnection Create(Uri uri, string accessToken, string? name = null)
    {
        return new HomeAssistantConnection(name, HomeAssistantClient.Create(uri, accessToken));
    }

    public static HomeAssistantConnection Create(
        Uri uri,
        IHomeAssistantAccessTokenProvider accessTokenProvider,
        string? name = null)
    {
        return new HomeAssistantConnection(
            name,
            new HomeAssistantClient(
                new Configuration.HomeAssistantClientOptions(uri, accessTokenProvider)));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Client.Dispose();
        }
    }

    public override string ToString()
    {
        return Name + " (" + Uri.GetLeftPart(UriPartial.Authority) + ")";
    }
}
