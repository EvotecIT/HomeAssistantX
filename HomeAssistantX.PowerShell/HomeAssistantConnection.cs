using HomeAssistantX.Authentication;
using System.Security.Cryptography;
using System.Text;

namespace HomeAssistantX.PowerShell;

/// <summary>An explicit, disposable Home Assistant session passed between cmdlets.</summary>
public sealed class HomeAssistantConnection : IDisposable
{
    private int _disposed;

    internal HomeAssistantConnection(string? name, HomeAssistantClient client)
    {
        var hasExplicitName = !string.IsNullOrWhiteSpace(name);
        Name = hasExplicitName ? name!.Trim() : client.Options.BaseUri.Host;
        ConfirmationName = CreateConfirmationName(hasExplicitName);
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

    private static string CreateConfirmationName(bool hasExplicitName)
    {
        if (!hasExplicitName)
        {
            return "Home Assistant";
        }

        // Operator-assigned names are useful in ordinary output but can contain private
        // installation URLs or other identifying text. Confirmations and transcripts use
        // a per-connection random tag so multiple explicit homes remain distinguishable
        // without echoing the supplied name.
        var bytes = new byte[4];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        var fingerprint = new StringBuilder(8);
        for (var index = 0; index < bytes.Length; index++)
        {
            fingerprint.Append(bytes[index].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return "Home Assistant connection [" + fingerprint + "]";
    }
}
