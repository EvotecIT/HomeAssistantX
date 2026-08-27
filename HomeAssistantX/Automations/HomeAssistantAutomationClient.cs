using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Rest;
using HomeAssistantX.Services;
using HomeAssistantX.States;

namespace HomeAssistantX.Automations;

/// <summary>Separates automation runtime state/execution from administrator-only editable definitions.</summary>
public sealed class HomeAssistantAutomationClient
{
    private readonly HomeAssistantStateClient _states;
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantServiceClient _services;

    internal HomeAssistantAutomationClient(HomeAssistantStateClient states, HomeAssistantRestClient rest, HomeAssistantServiceClient services)
    {
        _states = states;
        _rest = rest;
        _services = services;
    }

    public async Task<IReadOnlyList<HomeAssistantAutomationStatus>> GetAsync(CancellationToken cancellationToken = default)
        => HomeAssistantEntityId.RequireResponseDomainStates(
                await _states.GetAllAsync(cancellationToken).ConfigureAwait(false),
                "automation")
            .Select(ToStatus)
            .OrderBy(item => item.EntityId, StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task<HomeAssistantAutomationStatus> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = ValidateEntityId(entityId);
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return ToStatus(HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId));
    }

    /// <summary>Runs one or more automation entities without changing their configuration.</summary>
    public Task<HomeAssistantServiceCallResult> TriggerAsync(HomeAssistantTarget target, bool skipConditions = true, CancellationToken cancellationToken = default)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        var normalizedTarget = target.NormalizeForDomain("automation");
        if (!normalizedTarget.HasAnySelection())
            throw new ArgumentException("At least one automation target selection is required.", nameof(target));
        return _services.CallAsync(
            new HomeAssistantServiceCall("automation", "trigger")
                .ForTarget(normalizedTarget)
                .WithData("skip_condition", skipConditions),
            cancellationToken);
    }

    /// <summary>Reads an editable automation definition. Requires an administrator and an automation with a configuration id.</summary>
    public async Task<HomeAssistantAutomationConfiguration> GetConfigurationAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var id = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(automationId);
        var value = await _rest.SendAsync<JsonElement>(HttpMethod.Get, ConfigurationPath(id), null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object) throw new HomeAssistantProtocolException("Home Assistant returned a non-object automation definition.");
        if (HomeAssistantAutomationIdentifier.HasDuplicateProperties(value)) throw new HomeAssistantProtocolException("Home Assistant returned an automation definition with duplicate JSON properties.");
        var responseIds = value.EnumerateObject()
            .Where(property => property.NameEquals("id"))
            .Select(property => property.Value)
            .ToArray();
        if (responseIds.Length != 1
            || responseIds[0].ValueKind != JsonValueKind.String
            || !string.Equals(responseIds[0].GetString(), id, StringComparison.Ordinal))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an automation definition with a mismatched identifier.");
        }
        return new HomeAssistantAutomationConfiguration { AutomationId = id, Definition = value.Clone() };
    }

    /// <summary>Creates or replaces an editable automation definition and requests a targeted automation reload.</summary>
    public async Task<JsonElement> SaveConfigurationAsync(string automationId, JsonElement definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(automationId);
        HomeAssistantAutomationIdentifier.ValidateDefinitionForSave(id, definition, nameof(definition));
        return await _rest.SendAsync<JsonElement>(HttpMethod.Post, ConfigurationPath(id), definition.Clone(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes an editable automation definition. Requires an administrator.</summary>
    public Task<JsonElement> DeleteConfigurationAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var id = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(automationId);
        return _rest.SendAsync<JsonElement>(HttpMethod.Delete, ConfigurationPath(id), null, cancellationToken);
    }

    private static HomeAssistantAutomationStatus ToStatus(HomeAssistantState state)
    {
        if (!string.Equals(state.Domain, "automation", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The entity is not an automation.", nameof(state));
        if (string.IsNullOrWhiteSpace(state.State)) throw new HomeAssistantProtocolException("The Home Assistant automation state omitted its required state value.");
        return new HomeAssistantAutomationStatus
        {
            EntityId = state.EntityId,
            Name = HomeAssistantAttributeReader.GetString(state.Attributes, "friendly_name"),
            IsEnabled = string.Equals(state.State, "on", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(state.State, "off", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : null,
            LastTriggered = HomeAssistantAttributeReader.GetDateTimeOffset(state.Attributes, "last_triggered"),
            Mode = HomeAssistantAttributeReader.GetString(state.Attributes, "mode"),
            CurrentRuns = HomeAssistantAttributeReader.GetNonNegativeInt64(state.Attributes, "current"),
            RawState = state
        };
    }

    private static string ValidateEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "automation", out var normalized))
            throw new ArgumentException("An automation entity identifier is required.", nameof(entityId));
        return normalized;
    }

    private static string ConfigurationPath(string automationId)
        => "api/config/automation/config/" + Uri.EscapeDataString(automationId);
}
