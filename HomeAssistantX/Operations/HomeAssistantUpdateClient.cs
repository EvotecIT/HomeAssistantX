using System.Text.Json;
using HomeAssistantX.Models;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Discovers and installs updates through Home Assistant's update entity contract.</summary>
public sealed class HomeAssistantUpdateClient
{
    private readonly HomeAssistantStateClient _states;
    private readonly HomeAssistantServiceClient _services;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantUpdateClient(
        HomeAssistantStateClient states,
        HomeAssistantServiceClient services,
        HomeAssistantWebSocketClient webSocket)
    {
        _states = states;
        _services = services;
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantUpdate>> GetAllAsync(
        bool availableOnly = false,
        CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var updates = states
            .Where(state => string.Equals(state.Domain, "update", StringComparison.OrdinalIgnoreCase))
            .Select(ToUpdate)
            .Where(update => !availableOnly || update.IsAvailable)
            .ToArray();
        return updates;
    }

    public async Task<string?> GetReleaseNotesAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new ArgumentException("An update entity identifier is required.", nameof(entityId));
        }

        var result = await _webSocket.RequestAsync(
            "update/release_notes",
            new Dictionary<string, object?> { ["entity_id"] = entityId },
            cancellationToken).ConfigureAwait(false);
        return result.ValueKind == JsonValueKind.Null ? null : result.GetString();
    }

    public Task<HomeAssistantServiceCallResult> InstallAsync(
        string entityId,
        string? version = null,
        bool? backup = null,
        CancellationToken cancellationToken = default)
    {
        var call = HomeAssistantServiceCall.Create("update", "install").ForEntity(entityId);
        if (!string.IsNullOrWhiteSpace(version))
        {
            call.WithData("version", version);
        }

        if (backup.HasValue)
        {
            call.WithData("backup", backup.Value);
        }

        return _services.CallAsync(call, cancellationToken);
    }

    private static HomeAssistantUpdate ToUpdate(HomeAssistantState state)
    {
        return new HomeAssistantUpdate
        {
            State = state,
            Title = GetString(state, "title") ?? GetString(state, "friendly_name"),
            InstalledVersion = GetString(state, "installed_version"),
            LatestVersion = GetString(state, "latest_version"),
            IsAvailable = string.Equals(state.State, "on", StringComparison.OrdinalIgnoreCase),
            IsInProgress = GetBoolean(state, "in_progress"),
            ProgressPercentage = GetDouble(state, "update_percentage")
        };
    }

    private static string? GetString(HomeAssistantState state, string name)
    {
        return state.Attributes.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetBoolean(HomeAssistantState state, string name)
    {
        return state.Attributes.TryGetValue(name, out var value)
            && value.ValueKind == JsonValueKind.True;
    }

    private static double? GetDouble(HomeAssistantState state, string name)
    {
        return state.Attributes.TryGetValue(name, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number)
                ? number
                : null;
    }
}
