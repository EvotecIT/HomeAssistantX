using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
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
            var update = ToUpdate(state, cancellationToken);
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
        var normalizedEntityId = NormalizeEntityId(entityId, cancellationToken);
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

        var releaseNotes = result.GetString();
        HomeAssistantX.Protocol.HomeAssistantJson.ThrowIfStringTraversalCanceled(
            releaseNotes,
            cancellationToken);
        return releaseNotes;
    }

    public Task<HomeAssistantServiceCallResult> InstallAsync(
        string entityId,
        string? version = null,
        bool? backup = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var call = HomeAssistantServiceCall.Create("update", "install")
            .ForEntity(NormalizeEntityId(entityId, cancellationToken));
        if (version is not null)
        {
            call.WithData("version", RequireVersion(version, cancellationToken));
        }

        if (backup.HasValue)
        {
            call.WithData("backup", backup.Value);
        }

        return _services.CallAsync(call, cancellationToken);
    }

    private static string RequireVersion(string value, CancellationToken cancellationToken)
    {
        return CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken)
            ? throw new ArgumentException("A supplied update version cannot be empty.", nameof(value))
            : CancellationAwareString.Trim(value, cancellationToken);
    }

    private static string NormalizeEntityId(string entityId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "update", cancellationToken, out var normalized))
        {
            throw new ArgumentException("An update entity identifier is required.", nameof(entityId));
        }

        return normalized;
    }

    internal static HomeAssistantUpdate ToUpdate(
        HomeAssistantState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new HomeAssistantUpdate
        {
            State = state,
            Title = GetString(state, "title", cancellationToken) ?? GetString(state, "friendly_name", cancellationToken),
            InstalledVersion = GetString(state, "installed_version", cancellationToken),
            LatestVersion = GetString(state, "latest_version", cancellationToken),
            IsAvailable = CancellationAwareString.EqualsOrdinalIgnoreCase(state.State, "on", cancellationToken),
            IsInProgress = GetBoolean(state, "in_progress", cancellationToken),
            ProgressPercentage = GetDouble(state, "update_percentage", cancellationToken)
        };
    }

    private static string? GetString(
        HomeAssistantState state,
        string name,
        CancellationToken cancellationToken)
    {
        return HomeAssistantAttributeReader.GetStrictString(state.Attributes, name, cancellationToken);
    }

    private static bool GetBoolean(
        HomeAssistantState state,
        string name,
        CancellationToken cancellationToken)
    {
        return HomeAssistantAttributeReader.GetBoolean(state.Attributes, name, cancellationToken) == true;
    }

    private static double? GetDouble(
        HomeAssistantState state,
        string name,
        CancellationToken cancellationToken)
    {
        return HomeAssistantAttributeReader.GetDouble(state.Attributes, name, cancellationToken);
    }
}
