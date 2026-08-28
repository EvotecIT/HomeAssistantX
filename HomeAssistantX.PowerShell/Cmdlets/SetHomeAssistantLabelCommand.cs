using System.Management.Automation;
using HomeAssistantX.Registries;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates or updates a Home Assistant label while allowing nullable fields to be explicitly cleared.</summary>
/// <example><summary>Create a label</summary><code>Set-HomeAssistantLabel -Name Security -Color red -Icon mdi:shield</code></example>
/// <example><summary>Clear a label color and update its description</summary><code>Set-HomeAssistantLabel -LabelId security -ClearColor -Description 'Safety devices' -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantLabel", SupportsShouldProcess = true, DefaultParameterSetName = "Create")]
[OutputType(typeof(HomeAssistantLabel))]
public sealed class SetHomeAssistantLabelCommand : HomeAssistantCmdlet
{
    /// <summary>Label name. Mandatory when creating and optional when updating.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Create")]
    [Parameter(ParameterSetName = "Update")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Native label ID; supplying it selects the update parameter set.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Update")]
    [ValidateNotNullOrEmpty]
    public string LabelId { get; set; } = string.Empty;

    /// <summary>Theme color name or #RRGGBB color.</summary>
    [Parameter]
    public string? Color { get; set; }

    /// <summary>Optional label description.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Material Design icon identifier.</summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>Clears the current color. Mutually exclusive with Color.</summary>
    [Parameter(ParameterSetName = "Create")]
    [Parameter(ParameterSetName = "Update")]
    public SwitchParameter ClearColor { get; set; }

    /// <summary>Clears the current description. Mutually exclusive with Description.</summary>
    [Parameter(ParameterSetName = "Create")]
    [Parameter(ParameterSetName = "Update")]
    public SwitchParameter ClearDescription { get; set; }

    /// <summary>Clears the current icon. Mutually exclusive with Icon.</summary>
    [Parameter(ParameterSetName = "Create")]
    [Parameter(ParameterSetName = "Update")]
    public SwitchParameter ClearIcon { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        RejectConflict(Color, ClearColor, nameof(Color));
        RejectConflict(Description, ClearDescription, nameof(Description));
        RejectConflict(Icon, ClearIcon, nameof(Icon));
        if (ParameterSetName == "Create" && (ClearColor || ClearDescription || ClearIcon))
        {
            throw new ArgumentException("Clear switches can only be used when updating a label.");
        }
        if (ParameterSetName == "Create")
        {
            var create = new HomeAssistantLabelCreate(Name!) { Color = Color, Description = Description, Icon = Icon };
            if (ShouldProcess(create.Name, "Create Home Assistant label"))
            {
                WriteObject(await Client.Registries.CreateLabelAsync(create, CancelToken).ConfigureAwait(false));
            }

            return;
        }

        var labelId = HomeAssistantRegistryValidation.Require(LabelId, nameof(LabelId));
        var update = new HomeAssistantLabelUpdate();
        var hasName = MyInvocation.BoundParameters.ContainsKey(nameof(Name));
        var hasColor = MyInvocation.BoundParameters.ContainsKey(nameof(Color)) || ClearColor;
        var hasDescription = MyInvocation.BoundParameters.ContainsKey(nameof(Description)) || ClearDescription;
        var hasIcon = MyInvocation.BoundParameters.ContainsKey(nameof(Icon)) || ClearIcon;
        if (!hasName && !hasColor && !hasDescription && !hasIcon)
        {
            throw new ArgumentException("At least one label field must be supplied for an update.");
        }

        if (hasName) update.WithName(Name!);
        if (hasColor) update.WithColor(ClearColor ? null : Color);
        if (hasDescription) update.WithDescription(ClearDescription ? null : Description);
        if (hasIcon) update.WithIcon(ClearIcon ? null : Icon);
        if (ShouldProcess(labelId, "Update Home Assistant label"))
        {
            WriteObject(await Client.Registries.UpdateLabelAsync(labelId, update, CancelToken).ConfigureAwait(false));
        }
    }

    private static void RejectConflict(string? value, SwitchParameter clear, string name)
    {
        if (value is not null && clear)
        {
            throw new ArgumentException("-" + name + " cannot be combined with -Clear" + name + ".");
        }
    }
}
