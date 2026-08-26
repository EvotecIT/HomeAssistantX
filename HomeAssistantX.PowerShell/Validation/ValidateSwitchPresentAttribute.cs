using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Rejects an explicitly false selector switch before a parameter set can dispatch.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
internal sealed class ValidateSwitchPresentAttribute : ValidateArgumentsAttribute
{
    protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
    {
        if (arguments is not SwitchParameter selector || !selector.IsPresent)
        {
            throw new ValidationMetadataException(
                "Selector switches must be specified without an explicit false value.");
        }
    }
}
