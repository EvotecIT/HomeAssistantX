using System.Management.Automation;
using System.Net;
using System.Security;
using System.Text.Json;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates a full Supervisor backup with optional compression, location, and database exclusion.</summary>
/// <example>
///   <summary>Preview a full backup</summary>
///   <code>$ha | New-HomeAssistantBackup -Name 'Before update' -WhatIf</code>
///   <para>Shows the backup request without creating it.</para>
/// </example>
[Cmdlet(VerbsCommon.New, "HomeAssistantBackup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(JsonElement))]
public sealed class NewHomeAssistantBackupCommand : HomeAssistantCmdlet
{
    /// <summary>Optional display name for the full backup.</summary>
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Optional backup password as a secure string.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public SecureString? Password { get; set; }

    /// <summary>Controls compression. The default is <c>true</c>.</summary>
    [Parameter]
    public bool Compressed { get; set; } = true;

    /// <summary>Optional Supervisor backup-location identifier.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Location { get; set; }

    /// <summary>Excludes the Home Assistant database from the backup.</summary>
    [Parameter]
    public SwitchParameter ExcludeDatabase { get; set; }

    /// <summary>Starts backup creation in the background and returns the job response.</summary>
    [Parameter]
    public SwitchParameter Background { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var target = string.IsNullOrWhiteSpace(Name) ? ConnectionDisplayName : Name!;
        if (!ShouldProcess(target, "Create full Home Assistant backup"))
        {
            return;
        }

        var request = new HomeAssistantBackupRequest
        {
            Name = Name,
            Password = Password is null ? null : new NetworkCredential(string.Empty, Password).Password,
            Compressed = Compressed,
            Location = Location,
            ExcludeDatabase = ExcludeDatabase,
            Background = Background
        };
        WriteObject(await Client.Supervisor.CreateFullBackupAsync(request, CancelToken).ConfigureAwait(false));
    }
}
