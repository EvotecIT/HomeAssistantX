using System.Collections;
using System.Management.Automation;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Invokes any Home Assistant action with one target-oriented set of parameters.</summary>
/// <example>
///   <summary>Preview turning on lights in an area</summary>
///   <code>$ha | Invoke-HomeAssistantAction light turn_on -AreaId kitchen -Data @{ brightness_pct = 45 } -WhatIf</code>
///   <para>Uses the area parameter set and shows the action without changing devices.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HomeAssistantAction", SupportsShouldProcess = true, DefaultParameterSetName = DataParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class InvokeHomeAssistantActionCommand : HomeAssistantCmdlet
{
    private const string DataParameterSet = "Data";
    private const string EntityParameterSet = "Entity";
    private const string DeviceParameterSet = "Device";
    private const string AreaParameterSet = "Area";
    private const string FloorParameterSet = "Floor";
    private const string LabelParameterSet = "Label";

    /// <summary>Home Assistant action domain, such as <c>light</c> or <c>climate</c>.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Domain { get; set; } = string.Empty;

    /// <summary>Action name within the domain, such as <c>turn_on</c>. <c>Service</c> is an alias.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [Alias("Service")]
    [ValidateNotNullOrEmpty]
    public string Action { get; set; } = string.Empty;

    /// <summary>Targets one or more entity identifiers.</summary>
    [Parameter(Mandatory = true, ParameterSetName = EntityParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] EntityId { get; set; } = Array.Empty<string>();

    /// <summary>Targets one or more device identifiers.</summary>
    [Parameter(Mandatory = true, ParameterSetName = DeviceParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] DeviceId { get; set; } = Array.Empty<string>();

    /// <summary>Targets one or more area identifiers.</summary>
    [Parameter(Mandatory = true, ParameterSetName = AreaParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] AreaId { get; set; } = Array.Empty<string>();

    /// <summary>Targets one or more floor identifiers.</summary>
    [Parameter(Mandatory = true, ParameterSetName = FloorParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] FloorId { get; set; } = Array.Empty<string>();

    /// <summary>Targets one or more label identifiers.</summary>
    [Parameter(Mandatory = true, ParameterSetName = LabelParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] LabelId { get; set; } = Array.Empty<string>();

    /// <summary>Action-specific data. Keys must be non-empty strings.</summary>
    [Parameter(ParameterSetName = DataParameterSet)]
    [Parameter(ParameterSetName = EntityParameterSet)]
    [Parameter(ParameterSetName = DeviceParameterSet)]
    [Parameter(ParameterSetName = AreaParameterSet)]
    [Parameter(ParameterSetName = FloorParameterSet)]
    [Parameter(ParameterSetName = LabelParameterSet)]
    public Hashtable? Data { get; set; }

    /// <summary>Requests response data from actions that support it.</summary>
    [Parameter]
    public SwitchParameter ReturnResponse { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var call = HomeAssistantServiceCall.Create(Domain, Action);
        switch (ParameterSetName)
        {
            case EntityParameterSet:
                call.ForEntity(EntityId);
                break;
            case DeviceParameterSet:
                call.ForDevice(DeviceId);
                break;
            case AreaParameterSet:
                call.ForArea(AreaId);
                break;
            case FloorParameterSet:
                call.ForFloor(FloorId);
                break;
            case LabelParameterSet:
                call.ForLabel(LabelId);
                break;
        }

        if (Data is not null)
        {
            foreach (DictionaryEntry entry in Data)
            {
                if (entry.Key is not string name || string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Action data keys must be non-empty strings.", nameof(Data));
                }

                call.WithData(name, entry.Value);
            }
        }

        call.WithResponse(ReturnResponse);
        var target = DescribeTarget();
        if (ShouldProcess(target, Domain + "." + Action))
        {
            WriteObject(await Client.Services.CallAsync(call, CancelToken).ConfigureAwait(false));
        }
    }

    private string DescribeTarget()
    {
        return ParameterSetName switch
        {
            EntityParameterSet => "entities " + string.Join(", ", EntityId),
            DeviceParameterSet => "devices " + string.Join(", ", DeviceId),
            AreaParameterSet => "areas " + string.Join(", ", AreaId),
            FloorParameterSet => "floors " + string.Join(", ", FloorId),
            LabelParameterSet => "labels " + string.Join(", ", LabelId),
            _ => Connection.ToString()
        };
    }
}
