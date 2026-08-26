using System.Management.Automation;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets backups from a Supervisor-managed Home Assistant installation.</summary>
/// <example>
///   <summary>List existing backups</summary>
///   <code>$ha | Get-HomeAssistantBackup</code>
///   <para>Returns backup metadata without downloading backup content.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantBackup")]
[OutputType(typeof(HomeAssistantBackup))]
public sealed class GetHomeAssistantBackupCommand : HomeAssistantCmdlet
{
    protected override async Task ProcessRecordAsync()
    {
        WriteObject(await Client.Supervisor.GetBackupsAsync(CancelToken).ConfigureAwait(false), enumerateCollection: true);
    }
}
