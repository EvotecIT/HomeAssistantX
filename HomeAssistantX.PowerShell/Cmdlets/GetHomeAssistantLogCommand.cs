using System.Management.Automation;
using HomeAssistantX.Operations;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets structured system-log entries or bounded Core, Supervisor, host, and app log lines.</summary>
/// <example>
///   <summary>Read the last 200 Core log lines</summary>
///   <code>$ha | Get-HomeAssistantLog -Core -Tail 200</code>
///   <para>Returns bounded plaintext log lines through the authenticated Supervisor proxy.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantLog", DefaultParameterSetName = SystemLogParameterSet)]
[OutputType(typeof(HomeAssistantSystemLogEntry))]
[OutputType(typeof(HomeAssistantLogLine))]
public sealed class GetHomeAssistantLogCommand : HomeAssistantCmdlet
{
    private const string SystemLogParameterSet = "SystemLog";
    private const string LegacyParameterSet = "Legacy";
    private const string CoreParameterSet = "Core";
    private const string SupervisorParameterSet = "Supervisor";
    private const string HostParameterSet = "Host";
    private const string AppParameterSet = "App";

    /// <summary>Returns structured Core system-log entries. This is the default source.</summary>
    [Parameter(ParameterSetName = SystemLogParameterSet)]
    public SwitchParameter SystemLog { get; set; }

    /// <summary>Returns the legacy plaintext Core error log when that endpoint is enabled.</summary>
    [Parameter(Mandatory = true, ParameterSetName = LegacyParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter LegacyErrorLog { get; set; }

    /// <summary>Returns bounded Home Assistant Core container logs through Supervisor.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CoreParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Core { get; set; }

    /// <summary>Returns bounded Supervisor logs.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Supervisor { get; set; }

    /// <summary>Returns bounded host-system logs.</summary>
    [Parameter(Mandatory = true, ParameterSetName = HostParameterSet)]
    [Alias("Host")]
    [ValidateSwitchPresent]
    public SwitchParameter HostSystem { get; set; }

    /// <summary>Supervisor app/add-on slug whose logs should be returned.</summary>
    [Parameter(Mandatory = true, ParameterSetName = AppParameterSet)]
    [ValidateNotNullOrEmpty]
    public string? App { get; set; }

    /// <summary>Maximum number of trailing plaintext log lines, from 1 through 10000.</summary>
    [Parameter(ParameterSetName = CoreParameterSet)]
    [Parameter(ParameterSetName = SupervisorParameterSet)]
    [Parameter(ParameterSetName = HostParameterSet)]
    [Parameter(ParameterSetName = AppParameterSet)]
    [ValidateRange(1, 10000)]
    public int Tail { get; set; } = 100;

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == SystemLogParameterSet)
        {
            WriteObject(
                await Client.Operations.Logs.GetSystemLogAsync(CancelToken).ConfigureAwait(false),
                enumerateCollection: true);
            return;
        }

        if (ParameterSetName == LegacyParameterSet)
        {
            WriteLines("CoreLegacy", await Client.Operations.Logs.GetLegacyErrorLogAsync(CancelToken).ConfigureAwait(false));
            return;
        }

        var target = ParameterSetName switch
        {
            CoreParameterSet => HomeAssistantSupervisorLogTarget.Core,
            SupervisorParameterSet => HomeAssistantSupervisorLogTarget.Supervisor,
            HostParameterSet => HomeAssistantSupervisorLogTarget.Host,
            AppParameterSet => HomeAssistantSupervisorLogTarget.App,
            _ => throw new InvalidOperationException("Unexpected log parameter set.")
        };
        var text = await Client.Supervisor.GetLogAsync(target, Tail, App, CancelToken).ConfigureAwait(false);
        WriteLines(target == HomeAssistantSupervisorLogTarget.App ? "App:" + App : target.ToString(), text);
    }

    private void WriteLines(string source, string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (index == lines.Length - 1 && lines[index].Length == 0)
            {
                continue;
            }

            WriteObject(new HomeAssistantLogLine
            {
                Source = source,
                LineNumber = index + 1,
                Text = lines[index]
            });
        }
    }
}
