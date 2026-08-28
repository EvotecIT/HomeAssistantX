using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

public enum HomeAssistantHelperDomain
{
    InputBoolean,
    Number,
    InputNumber,
    Select,
    InputSelect,
    Text,
    InputText,
    Date,
    Time,
    DateTime,
    InputDateTime
}

/// <summary>Controls Home Assistant helper domains through typed values and domain-safe dispatch.</summary>
public sealed class HomeAssistantHelperClient
{
    private readonly HomeAssistantServiceClient _services;

    internal HomeAssistantHelperClient(HomeAssistantServiceClient services)
    {
        _services = services;
    }

    public Task<HomeAssistantServiceCallResult> SetBooleanAsync(HomeAssistantTarget target, bool value, CancellationToken cancellationToken = default)
        => CallAsync(HomeAssistantHelperDomain.InputBoolean, value ? "turn_on" : "turn_off", target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetNumberAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, double value, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.Number, HomeAssistantHelperDomain.InputNumber);
        return CallAsync(domain, "set_value", target, call => call.WithData("value", ControlValidation.Finite(value, nameof(value))!.Value), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> AdjustNumberAsync(HomeAssistantTarget target, bool increment, CancellationToken cancellationToken = default)
        => CallAsync(HomeAssistantHelperDomain.InputNumber, increment ? "increment" : "decrement", target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SelectOptionAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, string option, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.Select, HomeAssistantHelperDomain.InputSelect);
        return CallAsync(domain, "select_option", target, call => call.WithData("option", ControlValidation.RequiredUnchanged(option, nameof(option))), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetSelectOptionsAsync(HomeAssistantTarget target, IReadOnlyList<string> options, CancellationToken cancellationToken = default)
        => CallAsync(HomeAssistantHelperDomain.InputSelect, "set_options", target, call => call.WithData(
            "options",
            ControlValidation.RequiredValuesUnchanged(options, nameof(options), cancellationToken)), cancellationToken);

    public Task<HomeAssistantServiceCallResult> CycleSelectAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, bool forward, bool cycle = true, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.Select, HomeAssistantHelperDomain.InputSelect);
        return CallAsync(domain, forward ? "select_next" : "select_previous", target, call => call.WithData("cycle", cycle), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetTextAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, string value, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.Text, HomeAssistantHelperDomain.InputText);
        return CallAsync(domain, "set_value", target, call => call.WithData("value", value ?? throw new ArgumentNullException(nameof(value))), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetDateAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, DateTime value, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.Date, HomeAssistantHelperDomain.InputDateTime);
        return CallAsync(domain, domain == HomeAssistantHelperDomain.Date ? "set_value" : "set_datetime", target, call => call.WithData("date", value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetTimeAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, TimeSpan value, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.Time, HomeAssistantHelperDomain.InputDateTime);
        if (value < TimeSpan.Zero || value >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(value), "The time must be within one day.");
        if (value.Ticks % TimeSpan.TicksPerSecond != 0) throw new ArgumentException("The time must use whole-second precision.", nameof(value));
        return CallAsync(domain, domain == HomeAssistantHelperDomain.Time ? "set_value" : "set_datetime", target, call => call.WithData("time", value.ToString(@"hh\:mm\:ss", System.Globalization.CultureInfo.InvariantCulture)), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetDateTimeAsync(HomeAssistantHelperDomain domain, HomeAssistantTarget target, DateTimeOffset value, CancellationToken cancellationToken = default)
    {
        RequireDomain(domain, HomeAssistantHelperDomain.DateTime, HomeAssistantHelperDomain.InputDateTime);
        return CallAsync(domain, domain == HomeAssistantHelperDomain.DateTime ? "set_value" : "set_datetime", target, call => call.WithData("datetime", value.ToString("o", System.Globalization.CultureInfo.InvariantCulture)), cancellationToken);
    }

    private async Task<HomeAssistantServiceCallResult> CallAsync(HomeAssistantHelperDomain domain, string service, HomeAssistantTarget target, Action<HomeAssistantServiceCall>? configure, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null) throw new ArgumentNullException(nameof(target));
        var serviceDomain = ToDomain(domain);
        var call = HomeAssistantServiceCall.Create(serviceDomain, service).ForTarget(
            target.NormalizeRequiredForDomain(serviceDomain, cancellationToken: cancellationToken));
        configure?.Invoke(call);
        return await _services.CallControlAsync(call, cancellationToken).ConfigureAwait(false);
    }

    internal static string ToDomain(HomeAssistantHelperDomain domain) => domain switch
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
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unsupported helper domain.")
    };

    private static void RequireDomain(HomeAssistantHelperDomain actual, params HomeAssistantHelperDomain[] allowed)
    {
        if (!allowed.Contains(actual)) throw new ArgumentException("The selected helper domain does not support this operation.", nameof(actual));
    }
}
