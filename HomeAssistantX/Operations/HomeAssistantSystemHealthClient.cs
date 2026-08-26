using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Collects Home Assistant's streamed system-health response into a completed snapshot.</summary>
public sealed class HomeAssistantSystemHealthClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantSystemHealthClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task<HomeAssistantSystemHealthSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var domains = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IHomeAssistantSubscription? subscription = null;
        using var registration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));

        try
        {
            subscription = await _webSocket.SubscribeAsync(
                "system_health/info",
                null,
                (message, _) =>
                {
                    ProcessMessage(message, domains, completion);
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);

            var completed = await Task.WhenAny(completion.Task, subscription.Completion).ConfigureAwait(false);
            if (completed == subscription.Completion)
            {
                await subscription.Completion.ConfigureAwait(false);
                throw new HomeAssistantProtocolException(
                    "The Home Assistant system-health stream ended before its finish event.");
            }

            await completion.Task.ConfigureAwait(false);
            return new HomeAssistantSystemHealthSnapshot
            {
                Domains = new Dictionary<string, JsonElement>(domains, StringComparer.OrdinalIgnoreCase)
            };
        }
        finally
        {
            if (subscription is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await subscription.StopAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HomeAssistantException
                    || ex is OperationCanceledException
                    || ex is ObjectDisposedException)
                {
                    // Collection is already complete or canceled; cleanup is best-effort and bounded.
                }

                subscription.Dispose();
            }
        }
    }

    private static void ProcessMessage(
        JsonElement message,
        IDictionary<string, JsonElement> domains,
        TaskCompletionSource<bool> completion)
    {
        if (message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("type", out var typeProperty)
            || typeProperty.ValueKind != JsonValueKind.String)
        {
            completion.TrySetException(new HomeAssistantProtocolException(
                "The Home Assistant system-health stream returned an invalid event."));
            return;
        }

        var type = typeProperty.GetString();
        if (string.Equals(type, "initial", StringComparison.Ordinal))
        {
            if (!message.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                completion.TrySetException(new HomeAssistantProtocolException(
                    "The Home Assistant system-health stream omitted its initial data."));
                return;
            }

            domains.Clear();
            foreach (var domainProperty in data.EnumerateObject())
            {
                domains[domainProperty.Name] = domainProperty.Value.Clone();
            }

            return;
        }

        if (string.Equals(type, "finish", StringComparison.Ordinal))
        {
            completion.TrySetResult(true);
            return;
        }

        if (!string.Equals(type, "update", StringComparison.Ordinal)
            || !TryGetRequiredString(message, "domain", out var domainName)
            || !TryGetRequiredString(message, "key", out var key))
        {
            completion.TrySetException(new HomeAssistantProtocolException(
                "The Home Assistant system-health stream returned an invalid update."));
            return;
        }

        JsonElement value;
        if (message.TryGetProperty("success", out var success)
            && success.ValueKind == JsonValueKind.True
            && message.TryGetProperty("data", out var updateData))
        {
            value = updateData.Clone();
        }
        else
        {
            var errorMessage = "System-health update failed.";
            if (message.TryGetProperty("error", out var errorData)
                && errorData.ValueKind == JsonValueKind.Object
                && errorData.TryGetProperty("msg", out var errorMessageData)
                && errorMessageData.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(errorMessageData.GetString()))
            {
                errorMessage = errorMessageData.GetString()!;
            }

            value = ToElement(new Dictionary<string, object?>
            {
                ["error"] = true,
                ["value"] = errorMessage
            });
        }

        domains[domainName] = MergeDomain(
            domains.TryGetValue(domainName, out var existingDomain) ? existingDomain : default,
            key,
            value);
    }

    private static JsonElement MergeDomain(JsonElement domain, string key, JsonElement value)
    {
        var domainValues = domain.ValueKind == JsonValueKind.Object
            ? domain.Deserialize<Dictionary<string, JsonElement>>(HomeAssistantJson.SerializerOptions)
                ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var infoValues = domainValues.TryGetValue("info", out var info) && info.ValueKind == JsonValueKind.Object
            ? info.Deserialize<Dictionary<string, JsonElement>>(HomeAssistantJson.SerializerOptions)
                ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        infoValues[key] = value.Clone();
        domainValues["info"] = ToElement(infoValues);
        return ToElement(domainValues);
    }

    private static bool TryGetRequiredString(JsonElement value, string name, out string result)
    {
        if (value.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString()))
        {
            result = property.GetString()!;
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static JsonElement ToElement(object value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, HomeAssistantJson.SerializerOptions);
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }
}
