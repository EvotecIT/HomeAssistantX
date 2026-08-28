using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Sets common boolean, number, select, text, date, time, and date-time helpers.</summary>
/// <example><summary>Set a numeric helper</summary><code>Set-HomeAssistantHelper -Entity input_number.volume -Domain InputNumber -Number 15</code></example>
/// <example><summary>Select the next input option</summary><code>Set-HomeAssistantHelper -Entity input_select.house_mode -Domain InputSelect -Next</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantHelper", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantHelperCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Native Home Assistant helper domain; it must match the selected entities.</summary>
    [Parameter(Mandatory = true)] public HomeAssistantHelperDomain Domain { get; set; }
    /// <summary>Value for an input_boolean helper.</summary>
    [Parameter] public bool? Boolean { get; set; }
    /// <summary>Finite value for a number or input_number entity.</summary>
    [Parameter] public double? Number { get; set; }
    /// <summary>Increments an input_number helper.</summary>
    [Parameter] public SwitchParameter Increment { get; set; }
    /// <summary>Decrements an input_number helper.</summary>
    [Parameter] public SwitchParameter Decrement { get; set; }
    /// <summary>Value for a text or input_text entity; an empty string is allowed.</summary>
    [Parameter][AllowEmptyString] public string? Text { get; set; }
    /// <summary>Option to select on a select or input_select entity.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string? Option { get; set; }
    /// <summary>Replacement option list for an input_select helper.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string[]? Options { get; set; }
    /// <summary>Selects the next option.</summary>
    [Parameter] public SwitchParameter Next { get; set; }
    /// <summary>Selects the previous option.</summary>
    [Parameter] public SwitchParameter Previous { get; set; }
    /// <summary>Allows next/previous selection to wrap around. The default is true.</summary>
    [Parameter] public bool Cycle { get; set; } = true;
    /// <summary>Date value for a date or input_datetime entity.</summary>
    [Parameter] public DateTime? Date { get; set; }
    /// <summary>Time-of-day value for a time or input_datetime entity.</summary>
    [Parameter] public TimeSpan? Time { get; set; }
    /// <summary>Date and time value for a datetime or input_datetime entity.</summary>
    [Parameter] public DateTimeOffset? DateTime { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantHelperDomain), Domain)) throw new ArgumentOutOfRangeException(nameof(Domain));
        if (Number.HasValue && (double.IsNaN(Number.Value) || double.IsInfinity(Number.Value))) throw new ArgumentOutOfRangeException(nameof(Number));
        if (Time.HasValue && (Time.Value < TimeSpan.Zero || Time.Value >= TimeSpan.FromDays(1))) throw new ArgumentOutOfRangeException(nameof(Time), "Time must be within one day.");
        if (Time.HasValue && Time.Value.Ticks % TimeSpan.TicksPerSecond != 0) throw new ArgumentException("Time must use whole-second precision.", nameof(Time));
        var textBound = MyInvocation.BoundParameters.ContainsKey(nameof(Text));
        var operationCount = (Boolean.HasValue ? 1 : 0) + (Number.HasValue ? 1 : 0) + (Increment ? 1 : 0) + (Decrement ? 1 : 0)
            + (textBound ? 1 : 0) + (Option is not null ? 1 : 0) + (Options is { Length: > 0 } ? 1 : 0) + (Next ? 1 : 0) + (Previous ? 1 : 0)
            + (Date.HasValue ? 1 : 0) + (Time.HasValue ? 1 : 0) + (DateTime.HasValue ? 1 : 0);
        if (operationCount != 1) throw new ArgumentException("Specify exactly one helper value or adjustment.");
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Cycle)) && !Next && !Previous) throw new ArgumentException("Cycle applies only to Next or Previous.", nameof(Cycle));
        var normalizedOption = Option is null ? null : ControlValidation.Required(Option, nameof(Option));
        var normalizedOptions = Options is null
            ? null
            : ControlValidation.RequiredValues(Options, nameof(Options), CancelToken);
        var expectedDomain = DomainName(Domain);
        ValidateOperation(expectedDomain, textBound);
        var target = await ResolveTargetAsync(expectedDomain).ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set helper value")) return;
        HomeAssistantServiceCallResult result;
        if (Boolean.HasValue) result = await Client.Controls.Helpers.SetBooleanAsync(target.Target, Boolean.Value, CancelToken).ConfigureAwait(false);
        else if (Number.HasValue) result = await Client.Controls.Helpers.SetNumberAsync(Domain, target.Target, Number.Value, CancelToken).ConfigureAwait(false);
        else if (Increment || Decrement) result = await Client.Controls.Helpers.AdjustNumberAsync(target.Target, Increment, CancelToken).ConfigureAwait(false);
        else if (textBound) result = await Client.Controls.Helpers.SetTextAsync(Domain, target.Target, Text ?? string.Empty, CancelToken).ConfigureAwait(false);
        else if (normalizedOption is not null) result = await Client.Controls.Helpers.SelectOptionAsync(Domain, target.Target, normalizedOption, CancelToken).ConfigureAwait(false);
        else if (normalizedOptions is not null) result = await Client.Controls.Helpers.SetSelectOptionsAsync(target.Target, normalizedOptions, CancelToken).ConfigureAwait(false);
        else if (Next || Previous) result = await Client.Controls.Helpers.CycleSelectAsync(Domain, target.Target, Next, Cycle, CancelToken).ConfigureAwait(false);
        else if (Date.HasValue) result = await Client.Controls.Helpers.SetDateAsync(Domain, target.Target, Date.Value, CancelToken).ConfigureAwait(false);
        else if (Time.HasValue) result = await Client.Controls.Helpers.SetTimeAsync(Domain, target.Target, Time.Value, CancelToken).ConfigureAwait(false);
        else result = await Client.Controls.Helpers.SetDateTimeAsync(Domain, target.Target, DateTime!.Value, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }

    private void ValidateOperation(string expectedDomain, bool textBound)
    {
        if (Boolean.HasValue && expectedDomain != "input_boolean") throw WrongOperation();
        if ((Number.HasValue || Increment || Decrement) && expectedDomain is not "number" and not "input_number") throw WrongOperation();
        if ((Increment || Decrement) && expectedDomain != "input_number") throw WrongOperation();
        if (textBound && expectedDomain is not "text" and not "input_text") throw WrongOperation();
        if ((Option is not null || Next || Previous) && expectedDomain is not "select" and not "input_select") throw WrongOperation();
        if (Options is not null && expectedDomain != "input_select") throw WrongOperation();
        if (Date.HasValue && expectedDomain is not "date" and not "input_datetime") throw WrongOperation();
        if (Time.HasValue && expectedDomain is not "time" and not "input_datetime") throw WrongOperation();
        if (DateTime.HasValue && expectedDomain is not "datetime" and not "input_datetime") throw WrongOperation();
    }

    private static ArgumentException WrongOperation() => new("The selected helper domain does not support the requested operation.");

    private static string DomainName(HomeAssistantHelperDomain domain) => domain switch
    {
        HomeAssistantHelperDomain.InputBoolean => "input_boolean",
        HomeAssistantHelperDomain.Number => "number",
        HomeAssistantHelperDomain.InputNumber => "input_number",
        HomeAssistantHelperDomain.Select => "select",
        HomeAssistantHelperDomain.InputSelect => "input_select",
        HomeAssistantHelperDomain.Text => "text",
        HomeAssistantHelperDomain.InputText => "input_text",
        HomeAssistantHelperDomain.Date => "date",
        HomeAssistantHelperDomain.Time => "time",
        HomeAssistantHelperDomain.DateTime => "datetime",
        HomeAssistantHelperDomain.InputDateTime => "input_datetime",
        _ => throw new ArgumentOutOfRangeException(nameof(domain))
    };
}
