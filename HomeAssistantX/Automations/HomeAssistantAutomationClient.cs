using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
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
    {
        var states = HomeAssistantEntityId.RequireResponseDomainStates(
            await _states.GetAllAsync(cancellationToken).ConfigureAwait(false),
            "automation",
            cancellationToken);
        var result = new List<HomeAssistantAutomationStatus>();
        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(ToStatus(state, cancellationToken));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var comparer = new CancellationAwareStringComparer(StringComparison.OrdinalIgnoreCase, cancellationToken);
        CancellationAwareSort.Sort(result, (left, right) => comparer.Compare(left.EntityId, right.EntityId));
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public async Task<HomeAssistantAutomationStatus> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = ValidateEntityId(entityId, cancellationToken);
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return ToStatus(
            HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId, cancellationToken),
            cancellationToken);
    }

    /// <summary>Runs one or more automation entities without changing their configuration.</summary>
    public Task<HomeAssistantServiceCallResult> TriggerAsync(HomeAssistantTarget target, bool skipConditions = true, CancellationToken cancellationToken = default)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        var normalizedTarget = target.NormalizeForDomain("automation", cancellationToken);
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
        var id = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(automationId, cancellationToken);
        var value = await _rest.SendAsync<JsonElement>(HttpMethod.Get, ConfigurationPath(id, cancellationToken), null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object) throw new HomeAssistantProtocolException("Home Assistant returned a non-object automation definition.");
        if (HomeAssistantAutomationIdentifier.HasDuplicateProperties(value, cancellationToken)) throw new HomeAssistantProtocolException("Home Assistant returned an automation definition with duplicate JSON properties.");
        var responseIds = HomeAssistantAutomationIdentifier.GetDefinitionIds(value, cancellationToken);
        var responseId = responseIds.Count == 1 && responseIds[0].ValueKind == JsonValueKind.String
            ? await HomeAssistantJson.GetStringAsync(responseIds[0], cancellationToken).ConfigureAwait(false)
            : null;
        HomeAssistantJson.ThrowIfStringTraversalCanceled(responseId, cancellationToken);
        if (responseIds.Count != 1
            || responseId is null
            || !CancellationAwareString.EqualsOrdinal(responseId, id, cancellationToken))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an automation definition with a mismatched identifier.");
        }
        return new HomeAssistantAutomationConfiguration
        {
            AutomationId = id,
            Definition = HomeAssistantJson.DeserializeResponseIsolated<JsonElement>(
                value,
                "The automation definition could not be snapshotted.",
                cancellationToken: cancellationToken)
        };
    }

    /// <summary>Creates or replaces an editable automation definition and requests a targeted automation reload.</summary>
    public async Task<JsonElement> SaveConfigurationAsync(string automationId, JsonElement definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(automationId, cancellationToken);
        HomeAssistantAutomationIdentifier.ValidateDefinitionForSave(id, definition, nameof(definition), cancellationToken);
        var frozenDefinition = HomeAssistantJson.FreezeValue(
            definition,
            nameof(definition),
            "Automation definition",
            cancellationToken);
        return await _rest.SendAsync<JsonElement>(HttpMethod.Post, ConfigurationPath(id, cancellationToken), frozenDefinition, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes an editable automation definition. Requires an administrator.</summary>
    public Task<JsonElement> DeleteConfigurationAsync(string automationId, CancellationToken cancellationToken = default)
    {
        var id = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(automationId, cancellationToken);
        return _rest.SendAsync<JsonElement>(HttpMethod.Delete, ConfigurationPath(id, cancellationToken), null, cancellationToken);
    }

    internal static HomeAssistantAutomationStatus ToStatus(
        HomeAssistantState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CancellationAwareString.EqualsOrdinalIgnoreCase(state.Domain, "automation", cancellationToken)) throw new ArgumentException("The entity is not an automation.", nameof(state));
        if (!HasNonWhitespace(state.State, cancellationToken)) throw new HomeAssistantProtocolException("The Home Assistant automation state omitted its required state value.");
        return new HomeAssistantAutomationStatus
        {
            EntityId = state.EntityId,
            Name = HomeAssistantAttributeReader.GetString(state.Attributes, "friendly_name", cancellationToken),
            IsEnabled = CancellationAwareString.EqualsOrdinalIgnoreCase(state.State, "on", cancellationToken)
                ? true
                : CancellationAwareString.EqualsOrdinalIgnoreCase(state.State, "off", cancellationToken)
                    ? false
                    : null,
            LastTriggered = HomeAssistantAttributeReader.GetDateTimeOffset(state.Attributes, "last_triggered", cancellationToken),
            Mode = HomeAssistantAttributeReader.GetString(state.Attributes, "mode", cancellationToken),
            CurrentRuns = HomeAssistantAttributeReader.GetNonNegativeInt64(state.Attributes, "current", cancellationToken),
            RawState = state
        };
    }

    private static bool HasNonWhitespace(string? value, CancellationToken cancellationToken)
    {
        if (value is null) return false;
        var found = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            found |= !char.IsWhiteSpace(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return found;
    }

    private static string ValidateEntityId(string entityId, CancellationToken cancellationToken)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "automation", cancellationToken, out var normalized))
            throw new ArgumentException("An automation entity identifier is required.", nameof(entityId));
        return normalized;
    }

    private static string ConfigurationPath(string automationId, CancellationToken cancellationToken)
        => ConfigurationPathFromEscapedId(
            HomeAssistantAutomationIdentifier.EscapeConfigurationId(automationId, cancellationToken),
            cancellationToken);

    internal static string ConfigurationPathFromEscapedId(
        string escapedAutomationId,
        CancellationToken cancellationToken)
        => CancellationAwareString.Concat(
            "api/config/automation/config/",
            string.Empty,
            escapedAutomationId,
            cancellationToken);
}
