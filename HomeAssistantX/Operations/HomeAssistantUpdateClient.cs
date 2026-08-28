using System.Text.Json;
using HomeAssistantX.Exceptions;
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
        var updates = new List<HomeAssistantUpdate>();
        foreach (var state in HomeAssistantEntityId.RequireResponseDomainStates(states, "update", cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var update = ToUpdate(state);
            if (!availableOnly || update.IsAvailable)
            {
                updates.Add(update);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return updates;
    }

    public async Task<string?> GetReleaseNotesAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        var result = await _webSocket.RequestAsync(
            "update/release_notes",
            new Dictionary<string, object?> { ["entity_id"] = normalizedEntityId },
            cancellationToken).ConfigureAwait(false);
        if (result.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (result.ValueKind != JsonValueKind.String)
        {
            throw new HomeAssistantProtocolException("The Home Assistant update release-notes response was not a string.");
        }

        return result.GetString();
    }

    public Task<HomeAssistantServiceCallResult> InstallAsync(
        string entityId,
        string? version = null,
        bool? backup = null,
        CancellationToken cancellationToken = default)
    {
        var call = HomeAssistantServiceCall.Create("update", "install").ForEntity(NormalizeEntityId(entityId));
        if (version is not null)
        {
            call.WithData("version", RequireVersion(version));
        }

        if (backup.HasValue)
        {
            call.WithData("backup", backup.Value);
        }

        return _services.CallAsync(call, cancellationToken);
    }

    private static string RequireVersion(string value)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A supplied update version cannot be empty.", nameof(value))
            : value.Trim();

    private static string NormalizeEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "update", out var normalized))
        {
            throw new ArgumentException("An update entity identifier is required.", nameof(entityId));
        }

        return normalized;
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
        return HomeAssistantAttributeReader.GetString(state.Attributes, name);
    }

    private static bool GetBoolean(HomeAssistantState state, string name)
    {
        return HomeAssistantAttributeReader.GetBoolean(state.Attributes, name) == true;
    }

    private static double? GetDouble(HomeAssistantState state, string name)
    {
        return HomeAssistantAttributeReader.GetDouble(state.Attributes, name);
    }
}
