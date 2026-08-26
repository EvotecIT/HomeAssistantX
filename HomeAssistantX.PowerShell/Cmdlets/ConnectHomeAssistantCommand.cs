using System.Management.Automation;
using HomeAssistantX.Authentication;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates, verifies, and optionally stores the runspace's default Home Assistant connection.</summary>
/// <example>
///   <summary>Connect with a token held in a variable</summary>
///   <code>Connect-HomeAssistant -Uri 'https://home.example.net' -AccessToken $token -Name 'Home' | Out-Null</code>
///   <para>Validates REST and WebSocket access and stores the connection as the runspace default.</para>
/// </example>
[Cmdlet(VerbsCommunications.Connect, "HomeAssistant", DefaultParameterSetName = TokenParameterSet)]
[OutputType(typeof(HomeAssistantConnection))]
public sealed class ConnectHomeAssistantCommand : AsyncPSCmdlet
{
    private const string TokenParameterSet = "Token";
    private const string ProviderParameterSet = "Provider";

    /// <summary>Home Assistant base URI, for example <c>https://home.example.net</c>.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Url")]
    [ValidateNotNull]
    public Uri Uri { get; set; } = null!;

    /// <summary>Long-lived or OAuth access token. Prefer a variable or secret store over a command-line literal.</summary>
    [Parameter(Mandatory = true, ParameterSetName = TokenParameterSet)]
    [ValidateNotNullOrEmpty]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Token provider that owns retrieval or refresh without exposing the token to the cmdlet.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ProviderParameterSet)]
    [ValidateNotNull]
    public IHomeAssistantAccessTokenProvider AccessTokenProvider { get; set; } = null!;

    /// <summary>Friendly connection name used in output and confirmation messages.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Returns the connection without replacing the current runspace default.</summary>
    [Parameter]
    public SwitchParameter NoDefault { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var runspaceId = NoDefault ? (Guid?)null : HomeAssistantSession.GetCurrentRunspaceId();
        var connection = ParameterSetName == ProviderParameterSet
            ? HomeAssistantConnection.Create(Uri, AccessTokenProvider, Name)
            : HomeAssistantConnection.Create(Uri, AccessToken, Name);
        try
        {
            await connection.Client.Rest.CheckApiAsync(CancelToken).ConfigureAwait(false);
            await connection.Client.WebSocket.ConnectAsync(CancelToken).ConfigureAwait(false);
            if (runspaceId.HasValue)
            {
                HomeAssistantSession.Set(runspaceId.Value, connection);
            }

            WriteObject(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
