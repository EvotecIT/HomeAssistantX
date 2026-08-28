using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Systems;

/// <summary>Documented WebSocket commands for configuration, panels, targets, validation, and signed paths.</summary>
public sealed class HomeAssistantSystemClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantSystemClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    /// <summary>Gets the Home Assistant configuration through WebSocket.</summary>
    public async Task<HomeAssistantConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync("get_config", null, cancellationToken).ConfigureAwait(false);
        return HomeAssistantJson.DeserializeResponse<HomeAssistantConfiguration>(
            result,
            "The Home Assistant configuration could not be decoded.",
            cancellationToken: cancellationToken);
    }

    /// <summary>Gets registered frontend panels.</summary>
    public Task<JsonElement> GetPanelsAsync(CancellationToken cancellationToken = default)
    {
        return _webSocket.RequestAsync("get_panels", null, cancellationToken);
    }

    /// <summary>Validates trigger, condition, and action configuration fragments.</summary>
    public Task<JsonElement> ValidateConfigAsync(
        object? trigger = null,
        object? condition = null,
        object? action = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>();
        if (trigger is not null)
        {
            payload["trigger"] = trigger;
        }

        if (condition is not null)
        {
            payload["condition"] = condition;
        }

        if (action is not null)
        {
            payload["action"] = action;
        }

        if (payload.Count == 0)
        {
            throw new ArgumentException("At least one trigger, condition, or action fragment is required.");
        }

        return _webSocket.RequestAsync("validate_config", payload, cancellationToken);
    }

    /// <summary>Resolves the entities, devices, and areas selected by a target.</summary>
    public Task<JsonElement> ExtractFromTargetAsync(
        HomeAssistantTarget target,
        bool expandGroup = false,
        CancellationToken cancellationToken = default)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        return _webSocket.RequestAsync(
            "extract_from_target",
            new Dictionary<string, object?>
            {
                ["target"] = target.Normalize(),
                ["expand_group"] = expandGroup
            },
            cancellationToken);
    }

    /// <summary>Gets device-automation triggers applicable to a target.</summary>
    public Task<JsonElement> GetTriggersForTargetAsync(
        HomeAssistantTarget target,
        bool expandGroup = true,
        CancellationToken cancellationToken = default)
    {
        return GetForTargetAsync("get_triggers_for_target", target, expandGroup, cancellationToken);
    }

    /// <summary>Gets device-automation conditions applicable to a target.</summary>
    public Task<JsonElement> GetConditionsForTargetAsync(
        HomeAssistantTarget target,
        bool expandGroup = true,
        CancellationToken cancellationToken = default)
    {
        return GetForTargetAsync("get_conditions_for_target", target, expandGroup, cancellationToken);
    }

    /// <summary>Gets services/actions applicable to a target.</summary>
    public Task<JsonElement> GetServicesForTargetAsync(
        HomeAssistantTarget target,
        bool expandGroup = true,
        CancellationToken cancellationToken = default)
    {
        return GetForTargetAsync("get_services_for_target", target, expandGroup, cancellationToken);
    }

    /// <summary>Gets the entity registry projection intended for display clients.</summary>
    public Task<JsonElement> GetEntityRegistryForDisplayAsync(CancellationToken cancellationToken = default)
    {
        return _webSocket.RequestAsync("config/entity_registry/list_for_display", null, cancellationToken);
    }

    /// <summary>Gets voice-assistant exposure settings.</summary>
    public Task<JsonElement> GetExposedEntitiesAsync(CancellationToken cancellationToken = default)
    {
        return _webSocket.RequestAsync("homeassistant/expose_entity/list", null, cancellationToken);
    }

    /// <summary>Changes whether an entity is exposed to an assistant.</summary>
    public Task<JsonElement> SetEntityExposureAsync(
        string entityId,
        string assistant,
        bool shouldExpose,
        CancellationToken cancellationToken = default)
    {
        return SetEntityExposureAsync(
            new[] { entityId },
            new[] { assistant },
            shouldExpose,
            cancellationToken);
    }

    /// <summary>Changes whether one or more entities are exposed to one or more assistants.</summary>
    public Task<JsonElement> SetEntityExposureAsync(
        IReadOnlyList<string> entityIds,
        IReadOnlyList<string> assistants,
        bool shouldExpose,
        CancellationToken cancellationToken = default)
    {
        var validatedEntityIds = ValidateEntityIds(entityIds, nameof(entityIds));
        var validatedAssistants = ValidateIdentifiers(assistants, nameof(assistants));
        return _webSocket.RequestAsync(
            "homeassistant/expose_entity",
            new Dictionary<string, object?>
            {
                ["entity_ids"] = validatedEntityIds,
                ["assistants"] = validatedAssistants,
                ["should_expose"] = shouldExpose
            },
            cancellationToken);
    }

    /// <summary>Signs a Home Assistant path for temporary unauthenticated access.</summary>
    public async Task<string> SignPathAsync(
        string path,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        if (!HomeAssistantRootRelativePath.IsValid(path) || ContainsSignatureQueryParameter(path))
        {
            throw new ArgumentException("A root-relative Home Assistant path is required.", nameof(path));
        }

        var payload = new Dictionary<string, object?> { ["path"] = path };
        if (expiration.HasValue)
        {
            var totalSeconds = expiration.Value.TotalSeconds;
            if (totalSeconds <= 0 || totalSeconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(expiration));
            }

            payload["expires"] = checked((int)Math.Ceiling(totalSeconds));
        }

        var result = await _webSocket.RequestAsync("auth/sign_path", payload, cancellationToken).ConfigureAwait(false);
        if (result.ValueKind != JsonValueKind.Object
            || !result.TryGetProperty("path", out var signedPath)
            || signedPath.ValueKind != JsonValueKind.String)
        {
            throw new HomeAssistantProtocolException("Home Assistant did not return a signed path.");
        }

        var signed = signedPath.GetString()!;
        var expectedSeparator = path.IndexOf('?') >= 0 ? '&' : '?';
        var suffix = signed.Length > path.Length + 1
            ? signed.Substring(path.Length + 1)
            : string.Empty;
        if (!HomeAssistantRootRelativePath.IsValid(signed)
            || !signed.StartsWith(path, StringComparison.Ordinal)
            || signed.Length <= path.Length
            || signed[path.Length] != expectedSeparator
            || !HasValidSignatureSuffix(suffix))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a signed path for a different route.");
        }

        return signed;
    }

    private static bool HasValidSignatureSuffix(string suffix)
    {
        if (suffix.IndexOf('&') >= 0 || !suffix.StartsWith("authSig=", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return !string.IsNullOrWhiteSpace(Uri.UnescapeDataString(suffix.Substring("authSig=".Length)));
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static bool ContainsSignatureQueryParameter(string path)
    {
        var queryStart = path.IndexOf('?');
        if (queryStart < 0) return false;
        foreach (var pair in path.Substring(queryStart + 1).Split('&'))
        {
            var separator = pair.IndexOf('=');
            var encodedName = separator < 0 ? pair : pair.Substring(0, separator);
            try
            {
                if (string.Equals(Uri.UnescapeDataString(encodedName), "authSig", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            catch (UriFormatException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates a long-lived access token for the current user. Persist the returned secret immediately.</summary>
    public async Task<string> CreateLongLivedAccessTokenAsync(
        string clientName,
        int lifespanDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientName))
        {
            throw new ArgumentException("A client name is required.", nameof(clientName));
        }

        if (lifespanDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifespanDays));
        }

        var result = await _webSocket.RequestAsync(
            "auth/long_lived_access_token",
            new Dictionary<string, object?>
            {
                ["client_name"] = clientName,
                ["lifespan"] = lifespanDays
            },
            cancellationToken).ConfigureAwait(false);
        if (result.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(result.GetString()))
        {
            throw new HomeAssistantProtocolException("Home Assistant did not return a long-lived access token.");
        }

        return result.GetString()!;
    }

    /// <summary>Processes conversation text through WebSocket.</summary>
    public Task<JsonElement> ProcessConversationAsync(
        string text,
        string? language = null,
        string? agentId = null,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Conversation text is required.", nameof(text));
        }

        var payload = new Dictionary<string, object?> { ["text"] = text };
        if (language is not null)
        {
            payload["language"] = RequireConversationSelector(language, nameof(language));
        }

        if (agentId is not null)
        {
            payload["agent_id"] = RequireConversationSelector(agentId, nameof(agentId));
        }

        if (conversationId is not null)
        {
            payload["conversation_id"] = RequireConversationSelector(conversationId, nameof(conversationId));
        }

        return _webSocket.RequestAsync("conversation/process", payload, cancellationToken);
    }

    private static string RequireConversationSelector(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A supplied conversation selector cannot be empty.", parameterName)
            : value.Trim();

    private Task<JsonElement> GetForTargetAsync(
        string command,
        HomeAssistantTarget target,
        bool expandGroup,
        CancellationToken cancellationToken)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        return _webSocket.RequestAsync(
            command,
            new Dictionary<string, object?>
            {
                ["target"] = target.Normalize(),
                ["expand_group"] = expandGroup
            },
            cancellationToken);
    }

    private static string[] ValidateIdentifiers(IReadOnlyList<string> values, string parameterName)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty identifier is required.", parameterName);
        }

        return values.Select(value => value.Trim()).ToArray();
    }

    private static string[] ValidateEntityIds(IReadOnlyList<string> values, string parameterName)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one entity identifier is required.", parameterName);
        }

        var normalized = new string[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            if (!HomeAssistantEntityId.TryNormalize(values[index], out normalized[index]))
            {
                throw new ArgumentException("Entity identifiers must use the native Home Assistant format.", parameterName);
            }
        }

        return normalized;
    }
}
