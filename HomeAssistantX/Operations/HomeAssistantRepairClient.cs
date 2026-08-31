using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Reads and manages issues in Home Assistant's repairs registry.</summary>
public sealed class HomeAssistantRepairClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantRepairClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantRepairIssue>> GetIssuesAsync(
        bool includeIgnored = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync("repairs/list_issues", null, cancellationToken).ConfigureAwait(false);
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("issues", out var issues))
        {
            throw new HomeAssistantProtocolException("The Home Assistant repairs response had an unexpected shape.");
        }

        var decoded = HomeAssistantJson.DeserializeResponse<HomeAssistantRepairIssue[]>(
            issues,
            "The Home Assistant repairs issues could not be decoded.",
            cancellationToken: cancellationToken);
        return includeIgnored ? decoded : decoded.Where(issue => !issue.Ignored).ToArray();
    }

    public Task<JsonElement> GetIssueDataAsync(
        string domain,
        string issueId,
        CancellationToken cancellationToken = default)
    {
        return _webSocket.RequestAsync(
            "repairs/get_issue_data",
            new Dictionary<string, object?>
            {
                ["domain"] = Required(domain, nameof(domain)),
                ["issue_id"] = Required(issueId, nameof(issueId))
            },
            cancellationToken);
    }

    public Task<JsonElement> SetIgnoredAsync(
        string domain,
        string issueId,
        bool ignored,
        CancellationToken cancellationToken = default)
    {
        return _webSocket.RequestAsync(
            "repairs/ignore_issue",
            new Dictionary<string, object?>
            {
                ["domain"] = Required(domain, nameof(domain)),
                ["issue_id"] = Required(issueId, nameof(issueId)),
                ["ignore"] = ignored
            },
            cancellationToken);
    }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty identifier is required.", parameterName)
            : value;
    }
}
