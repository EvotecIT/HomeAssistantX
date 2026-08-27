using System.Management.Automation;
using HomeAssistantX.Models;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Runs a bounded Recorder maintenance task.</summary>
/// <example><summary>Preview purging old data</summary><code>Invoke-HomeAssistantRecorderMaintenance -Purge -KeepDays 30 -WhatIf</code></example>
[Cmdlet(VerbsLifecycle.Invoke, "HomeAssistantRecorderMaintenance", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class InvokeHomeAssistantRecorderMaintenanceCommand : HomeAssistantCmdlet
{
    private const string PurgeSet = "Purge";
    private const string EntitiesSet = "PurgeEntities";
    private const string EnableSet = "Enable";
    private const string DisableSet = "Disable";
    private const string IssuesSet = "RefreshStatisticsIssues";

    [Parameter(Mandatory = true, ParameterSetName = PurgeSet)][ValidateSwitchPresent] public SwitchParameter Purge { get; set; }
    [Parameter(ParameterSetName = PurgeSet)][ValidateRange(0, int.MaxValue)]
    [Parameter(ParameterSetName = EntitiesSet)] public int? KeepDays { get; set; }
    [Parameter(ParameterSetName = PurgeSet)] public SwitchParameter Repack { get; set; }
    [Parameter(ParameterSetName = PurgeSet)] public SwitchParameter ApplyFilter { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = EntitiesSet)][ValidateSwitchPresent] public SwitchParameter PurgeEntities { get; set; }
    [Parameter(ParameterSetName = EntitiesSet)][ValidateNotNullOrEmpty] public string[]? EntityId { get; set; }
    [Parameter(ParameterSetName = EntitiesSet)][ValidateNotNullOrEmpty] public string[]? Domain { get; set; }
    [Parameter(ParameterSetName = EntitiesSet)][ValidateNotNullOrEmpty] public string[]? EntityGlob { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = EnableSet)][ValidateSwitchPresent] public SwitchParameter Enable { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = DisableSet)][ValidateSwitchPresent] public SwitchParameter Disable { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = IssuesSet)][ValidateSwitchPresent] public SwitchParameter RefreshStatisticsIssues { get; set; }
    [Parameter(ParameterSetName = PurgeSet)]
    [Parameter(ParameterSetName = EntitiesSet)]
    [Parameter(ParameterSetName = EnableSet)]
    [Parameter(ParameterSetName = DisableSet)]
    public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        object? result = null;
        switch (ParameterSetName)
        {
            case PurgeSet:
                if (!ShouldProcess(ConnectionDisplayName, "Purge Recorder data")) return;
                result = await Client.Recorder.PurgeAsync(KeepDays, Repack, ApplyFilter, CancelToken).ConfigureAwait(false);
                break;
            case EntitiesSet:
                if ((EntityId is null || EntityId.Length == 0) && (Domain is null || Domain.Length == 0) && (EntityGlob is null || EntityGlob.Length == 0))
                    throw new ArgumentException("Specify at least one EntityId, Domain, or EntityGlob.");
                var entityIds = NormalizeEntityIds(EntityId);
                var domains = NormalizeDomains(Domain);
                var entityGlobs = NormalizeValues(EntityGlob, nameof(EntityGlob));
                if (!ShouldProcess(ConnectionDisplayName, "Purge matching Recorder entities")) return;
                result = await Client.Recorder.PurgeEntitiesAsync(entityIds, domains, entityGlobs, KeepDays, CancelToken).ConfigureAwait(false);
                break;
            case EnableSet:
            case DisableSet:
                var enable = ParameterSetName == EnableSet;
                if (!ShouldProcess(ConnectionDisplayName, enable ? "Enable Recorder" : "Disable Recorder")) return;
                result = await Client.Recorder.SetEnabledAsync(enable, CancelToken).ConfigureAwait(false);
                break;
            case IssuesSet:
                if (!ShouldProcess(ConnectionDisplayName, "Refresh Recorder statistics issues")) return;
                await Client.Recorder.UpdateStatisticsIssuesAsync(CancelToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("Unexpected Recorder maintenance parameter set.");
        }
        if (PassThru && result is not null) WriteObject(result);
    }

    private static string[]? NormalizeEntityIds(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var normalized = new List<string>();
        foreach (var value in values)
        {
            if (!HomeAssistantEntityId.TryNormalize(value, out var entityId))
            {
                throw new ArgumentException(
                    "EntityId must contain lowercase native Home Assistant entity identifiers.",
                    nameof(EntityId));
            }

            if (!normalized.Contains(entityId, StringComparer.Ordinal))
            {
                normalized.Add(entityId);
            }
        }

        return normalized.ToArray();
    }

    private static string[]? NormalizeDomains(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var normalized = new List<string>();
        foreach (var value in values)
        {
            if (!HomeAssistantEntityId.TryNormalizeDomain(value, out var domain))
            {
                throw new ArgumentException(
                    "Domain must contain lowercase native Home Assistant domains.",
                    nameof(Domain));
            }

            if (!normalized.Contains(domain, StringComparer.Ordinal))
            {
                normalized.Add(domain);
            }
        }

        return normalized.ToArray();
    }

    private static string[]? NormalizeValues(IEnumerable<string>? values, string parameterName)
    {
        if (values is null)
        {
            return null;
        }

        var normalized = values.Select(value => value?.Trim() ?? string.Empty).ToArray();
        if (normalized.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Selector values cannot be empty.", parameterName);
        }

        return normalized.Distinct(StringComparer.Ordinal).ToArray();
    }
}
