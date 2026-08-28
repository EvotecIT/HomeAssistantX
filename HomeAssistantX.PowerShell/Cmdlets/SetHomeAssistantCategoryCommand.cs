using System.Management.Automation;
using HomeAssistantX.Registries;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates or updates a Home Assistant category within an explicit scope.</summary>
/// <example><summary>Create an automation category</summary><code>Set-HomeAssistantCategory -Scope automation -Name Comfort -Icon mdi:sofa</code></example>
/// <example><summary>Clear an existing category icon</summary><code>Set-HomeAssistantCategory -Scope automation -CategoryId comfort -ClearIcon -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantCategory", SupportsShouldProcess = true, DefaultParameterSetName = "Create")]
[OutputType(typeof(HomeAssistantCategory))]
public sealed class SetHomeAssistantCategoryCommand : HomeAssistantCmdlet
{
    /// <summary>Category registry scope, such as automation or script.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Scope { get; set; } = string.Empty;

    /// <summary>Category name. Mandatory when creating and optional when updating.</summary>
    [Parameter(Mandatory = true, Position = 1, ParameterSetName = "Create")]
    [Parameter(ParameterSetName = "Update")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Native category ID; supplying it selects the update parameter set.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "Update")]
    [ValidateNotNullOrEmpty]
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Material Design icon identifier.</summary>
    [Parameter]
    public string? Icon { get; set; }

    /// <summary>Clears the current icon. Mutually exclusive with Icon.</summary>
    [Parameter(ParameterSetName = "Create")]
    [Parameter(ParameterSetName = "Update")]
    public SwitchParameter ClearIcon { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var scope = HomeAssistantRegistryValidation.Require(Scope, nameof(Scope));
        if (Icon is not null && ClearIcon)
        {
            throw new ArgumentException("-Icon cannot be combined with -ClearIcon.");
        }

        if (ParameterSetName == "Create" && ClearIcon)
        {
            throw new ArgumentException("-ClearIcon can only be used when updating a category.");
        }

        if (ParameterSetName == "Create")
        {
            var create = new HomeAssistantCategoryCreate(Name!) { Icon = Icon };
            if (ShouldProcess(scope + "/" + create.Name, "Create Home Assistant category"))
            {
                WriteObject(await Client.Registries.CreateCategoryAsync(scope, create, CancelToken).ConfigureAwait(false));
            }

            return;
        }

        var categoryId = HomeAssistantRegistryValidation.Require(CategoryId, nameof(CategoryId));
        var update = new HomeAssistantCategoryUpdate();
        var hasName = MyInvocation.BoundParameters.ContainsKey(nameof(Name));
        var hasIcon = MyInvocation.BoundParameters.ContainsKey(nameof(Icon)) || ClearIcon;
        if (!hasName && !hasIcon)
        {
            throw new ArgumentException("At least one category field must be supplied for an update.");
        }

        if (hasName) update.WithName(Name!);
        if (hasIcon) update.WithIcon(ClearIcon ? null : Icon);
        if (ShouldProcess(scope + "/" + categoryId, "Update Home Assistant category"))
        {
            WriteObject(await Client.Registries.UpdateCategoryAsync(scope, categoryId, update, CancelToken).ConfigureAwait(false));
        }
    }
}
