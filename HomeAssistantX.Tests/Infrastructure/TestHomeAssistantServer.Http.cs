using System.Net.Sockets;

namespace HomeAssistantX.Tests.Infrastructure;

internal sealed partial class TestHomeAssistantServer
{
    private async Task HandleHttpAsync(
        NetworkStream stream,
        string requestLine,
        IReadOnlyDictionary<string, string> headers,
        string body)
    {
        var parts = requestLine.Split(' ');
        var method = parts[0];
        var path = parts.Length > 1 ? parts[1] : "/";
        var pathWithoutQuery = path.Split('?')[0];
        LastRequestBody = body;
        LastRequestPath = path;
        var currentAuthorization = headers.TryGetValue("Authorization", out var authorization)
            ? authorization
            : null;
        LastAuthorization = currentAuthorization;
        LastAuthorization = headers.TryGetValue("Authorization", out var requestAuthorization) ? requestAuthorization : null;

        if (method == "POST" && pathWithoutQuery == "/auth/token")
        {
            var form = ParseForm(body);
            OAuthTokenRequestCount++;
            var refresh = form.TryGetValue("grant_type", out var grantType)
                && string.Equals(grantType, "refresh_token", StringComparison.Ordinal);
            var valid = refresh
                ? HasExactValue(form, "refresh_token", "oauth-refresh-token")
                    && HasExactValue(form, "client_id", "https://app.example.net/")
                : HasExactValue(form, "grant_type", "authorization_code")
                    && HasExactValue(form, "code", "authorization-code")
                    && HasExactValue(form, "client_id", "https://app.example.net/");
            if (!valid)
            {
                await WriteHttpResponseAsync(stream, 400, "{\"error\":\"invalid_request\"}").ConfigureAwait(false);
                return;
            }

            await WriteHttpResponseAsync(
                stream,
                200,
                refresh
                    ? "{\"access_token\":\"refreshed-access-token\",\"token_type\":\"Bearer\",\"expires_in\":1800}"
                    : "{\"access_token\":\"oauth-access-token\",\"token_type\":\"Bearer\",\"expires_in\":1800,\"refresh_token\":\"oauth-refresh-token\"}")
                .ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/auth/revoke")
        {
            var form = ParseForm(body);
            form.TryGetValue("token", out var refreshToken);
            LastRevokedRefreshToken = refreshToken;
            await WriteHttpResponseAsync(stream, 200, string.Empty).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/test-webhook")
        {
            using var webhookDocument = System.Text.Json.JsonDocument.Parse(body);
            var root = webhookDocument.RootElement;
            if (root.TryGetProperty("encrypted", out var encrypted) && encrypted.ValueKind == System.Text.Json.JsonValueKind.True)
            {
                var encryptedResponse = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"version\":\"2026.8.3\"}"));
                await WriteHttpResponseAsync(stream, 200, "{\"encrypted\":true,\"encrypted_data\":\"" + encryptedResponse + "\"}").ConfigureAwait(false);
            }
            else
            {
                await WriteHttpResponseAsync(stream, 200, "{\"version\":\"2026.8.3\"}").ConfigureAwait(false);
            }
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/plaintext-encrypted")
        {
            await WriteHttpResponseAsync(stream, 200, "{\"version\":\"forged\"}").ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/invalid-encrypted")
        {
            await WriteHttpResponseAsync(stream, 200, "{\"encrypted\":true,\"encrypted_data\":\"\"}").ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/invalid-camera-response")
        {
            await WriteHttpResponseAsync(stream, 200, "[]").ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/redirect")
        {
            if (WebhookRedirectUri is null)
            {
                await WriteHttpResponseAsync(stream, 400, "{\"message\":\"Missing redirect target\"}").ConfigureAwait(false);
            }
            else
            {
                await WriteRedirectResponseAsync(stream, WebhookRedirectUri).ConfigureAwait(false);
            }

            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/truncated")
        {
            await WriteTruncatedResponseAsync(stream).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/stall")
        {
            await WriteHeadersAndStallAsync(stream, 10).ConfigureAwait(false);
            return;
        }

        if (method == "POST" && pathWithoutQuery == "/api/webhook/oversize")
        {
            await WriteHeadersAndStallAsync(stream, 100_000_000).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(currentAuthorization, "Bearer " + RequiredAccessToken, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _unauthorizedRequestCount);
            await WriteHttpResponseAsync(stream, 401, "{\"message\":\"Unauthorized\"}").ConfigureAwait(false);
            return;
        }

        Interlocked.Increment(ref _authenticatedRequestCount);

        if (method == "GET"
            && pathWithoutQuery.StartsWith("/api/states/", StringComparison.Ordinal)
            && ExactStateResponseJson is not null)
        {
            await WriteHttpResponseAsync(stream, 200, ExactStateResponseJson).ConfigureAwait(false);
            return;
        }

        switch (method + " " + pathWithoutQuery)
        {
            case "GET /supervisor/info":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\",\"data\":{\"version\":\"2026.08.0\",\"version_latest\":\"2026.08.1\",\"update_available\":true,\"arch\":\"amd64\",\"channel\":\"stable\",\"healthy\":true,\"supported\":true,\"timezone\":\"Europe/Warsaw\"}}")
                    .ConfigureAwait(false);
                break;
            case "GET /info":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\",\"data\":{\"supervisor\":\"2026.08.0\",\"homeassistant\":\"2026.8.3\",\"hassos\":\"17.0\",\"hostname\":\"test-host\",\"operating_system\":\"Home Assistant OS\",\"machine\":\"generic-x86-64\",\"arch\":\"amd64\",\"supported\":true,\"channel\":\"stable\",\"state\":\"running\",\"features\":[\"reboot\"]}}")
                    .ConfigureAwait(false);
                break;
            case "GET /available_updates":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\",\"data\":{\"available_updates\":[{\"update_type\":\"core\",\"version_latest\":\"2026.8.4\"}]}}")
                    .ConfigureAwait(false);
                break;
            case "GET /addons":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\",\"data\":{\"addons\":[{\"slug\":\"test_app\",\"name\":\"Test app\",\"installed\":true,\"available\":true}]}}")
                    .ConfigureAwait(false);
                break;
            case "GET /backups":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\",\"data\":{\"backups\":[{\"slug\":\"backup-1\",\"name\":\"Before update\",\"protected\":true,\"compressed\":true,\"content\":{\"homeassistant\":true}}]}}")
                    .ConfigureAwait(false);
                break;
            case "GET /core/logs":
                await WriteHttpResponseAsync(stream, 200, "2026-08-25 direct supervisor log line").ConfigureAwait(false);
                break;
            case "POST /addons/TEST_APP/restart":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\",\"data\":{}}").ConfigureAwait(false);
                break;
            case "GET /api/":
                await WriteHttpResponseAsync(stream, 200, "{\"message\":\"API running.\",\"custom_api_field\":true}").ConfigureAwait(false);
                break;
            case "GET /api/config":
                await WriteHttpResponseAsync(stream, 200, ConfigurationResponseJson).ConfigureAwait(false);
                break;
            case "GET /api/test/raw-dto":
                await WriteHttpResponseAsync(stream, 200, "{\"value\":1}").ConfigureAwait(false);
                break;
            case "GET /api/components":
                await WriteHttpResponseAsync(stream, 200, "[\"api\",\"websocket_api\",\"recorder\"]").ConfigureAwait(false);
                break;
            case "GET /api/events":
                await WriteHttpResponseAsync(stream, 200, "[{\"event\":\"state_changed\",\"listener_count\":5}]").ConfigureAwait(false);
                break;
            case "GET /api/services":
                await WriteHttpResponseAsync(stream, 200,
                    "[{\"domain\":\"light\",\"services\":{\"turn_on\":{\"fields\":{\"brightness_pct\":{}}}}}]")
                    .ConfigureAwait(false);
                break;
            case "GET /api/history/period":
            case "GET /api/history/period/2026-08-24T00%3A00%3A00.0000000%2B00%3A00":
                await WriteHttpResponseAsync(stream, 200, HistoryResponseJson).ConfigureAwait(false);
                break;
            case "GET /api/logbook":
            case "GET /api/logbook/2026-08-24T00%3A00%3A00.0000000%2B00%3A00":
                await WriteHttpResponseAsync(stream, 200, LogbookResponseJson).ConfigureAwait(false);
                break;
            case "GET /api/states":
                await WriteHttpResponseAsync(stream, 200, GetStates()).ConfigureAwait(false);
                break;
            case "GET /api/states/sensor.kitchen_temperature":
                await WriteHttpResponseAsync(stream, 200, KitchenTemperatureStateJson).ConfigureAwait(false);
                break;
            case "GET /api/states/weather.home":
                await WriteHttpResponseAsync(stream, 200,
                    "{\"entity_id\":\"weather.home\",\"state\":\"partlycloudy\",\"attributes\":{\"friendly_name\":\"Home\",\"temperature\":21.5,\"temperature_unit\":\"°C\",\"humidity\":55,\"wind_bearing\":180,\"supported_features\":3}}")
                    .ConfigureAwait(false);
                break;
            case "POST /api/states/sensor.virtual":
                await WriteHttpResponseAsync(stream, 200, StateMutationResponseJson).ConfigureAwait(false);
                break;
            case "DELETE /api/states/sensor.virtual":
                await WriteHttpResponseAsync(stream, 200, "{\"message\":\"Entity removed.\"}").ConfigureAwait(false);
                break;
            case "GET /api/error_log":
                await WriteHttpResponseAsync(stream, 200, "test integration warning").ConfigureAwait(false);
                break;
            case "GET /api/camera_proxy/camera.front":
                await WriteHttpResponseAsync(stream, 200, CameraImageResponse).ConfigureAwait(false);
                break;
            case "GET /api/config/automation/config/morning-routine":
                await WriteHttpResponseAsync(stream, 200, AutomationConfigurationResponseJson).ConfigureAwait(false);
                break;
            case "POST /api/config/automation/config/morning-routine":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\"}").ConfigureAwait(false);
                break;
            case "DELETE /api/config/automation/config/morning-routine":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"ok\"}").ConfigureAwait(false);
                break;
            case "GET /api/calendars":
                await WriteHttpResponseAsync(stream, 200, CalendarListResponseJson).ConfigureAwait(false);
                break;
            case "GET /api/calendars/calendar.home":
                await WriteHttpResponseAsync(stream, 200, CalendarEventsResponseJson)
                    .ConfigureAwait(false);
                break;
            case "POST /api/events/homeassistantx_test":
                await WriteHttpResponseAsync(stream, 200, "{\"message\":\"Event homeassistantx_test fired.\"}").ConfigureAwait(false);
                break;
            case "POST /api/template":
                await WriteHttpResponseAsync(stream, 200, "rendered value").ConfigureAwait(false);
                break;
            case "POST /api/config/core/check_config":
                await WriteHttpResponseAsync(stream, 200, "{\"result\":\"valid\",\"errors\":null}").ConfigureAwait(false);
                break;
            case "POST /api/config/config_entries/entry/entry-1/reload":
                await WriteHttpResponseAsync(stream, 200, "{\"require_restart\":false}").ConfigureAwait(false);
                break;
            case "POST /api/config/config_entries/flow":
                if (!IsReconfigurationRequest(body))
                {
                    await WriteHttpResponseAsync(stream, 400, "{\"message\":\"Invalid reconfiguration request\"}").ConfigureAwait(false);
                    break;
                }

                await WriteHttpResponseAsync(stream, 200, "{\"type\":\"form\",\"flow_id\":\"flow-1\",\"handler\":\"test\",\"step_id\":\"reconfigure\"}").ConfigureAwait(false);
                break;
            case "POST /api/config/config_entries/flow/flow-1":
                await WriteHttpResponseAsync(stream, 200, "{\"type\":\"create_entry\",\"flow_id\":\"flow-1\",\"result\":{\"entry_id\":\"entry-1\"}}").ConfigureAwait(false);
                break;
            case "GET /api/diagnostics/config_entry/entry-1":
                await WriteHttpResponseAsync(stream, 200, "{\"data\":{\"token\":\"REDACTED\"}}").ConfigureAwait(false);
                break;
            case "GET /api/diagnostics/config_entry/entry-1/device/device-1":
                await WriteHttpResponseAsync(stream, 200, "{\"data\":{\"device\":\"device-1\"}}").ConfigureAwait(false);
                break;
            case "GET /api/hassio/core/logs":
            case "GET /api/hassio/supervisor/logs":
            case "GET /api/hassio/host/logs":
            case "GET /api/hassio/addons/test_app/logs":
                await WriteHttpResponseAsync(stream, 200, "2026-08-25 test log line").ConfigureAwait(false);
                break;
            case "POST /api/intent/handle":
                await WriteHttpResponseAsync(stream, 200, "{\"response\":{\"speech\":{\"plain\":{\"speech\":\"Done\"}}}}").ConfigureAwait(false);
                break;
            case "POST /api/conversation/process":
                await WriteHttpResponseAsync(stream, 200, "{\"conversation_id\":\"conversation-1\",\"continue_conversation\":false}").ConfigureAwait(false);
                break;
            case "POST /api/mobile_app/registrations":
                await WriteHttpResponseAsync(stream, 201, body.Contains("\"supports_encryption\":true", StringComparison.Ordinal)
                    ? "{\"webhook_id\":\"test-webhook\",\"secret\":\"test-secret\",\"cloudhook_url\":null,\"remote_ui_url\":null,\"future_field\":\"preserved\"}"
                    : "{\"webhook_id\":\"test-webhook\",\"secret\":null,\"cloudhook_url\":null,\"remote_ui_url\":null,\"future_field\":\"preserved\"}").ConfigureAwait(false);
                break;
            case "GET /api/test/oversize":
                await WriteHeadersAndStallAsync(stream, 100_000_000).ConfigureAwait(false);
                break;
            case "GET /api/test/stall":
                await WriteHeadersAndStallAsync(stream, 10).ConfigureAwait(false);
                break;
            case "GET /api/test/invalid-json":
                await WriteHttpResponseAsync(stream, 200, "{not-json").ConfigureAwait(false);
                break;
            case "POST /api/services/light/turn_on":
            case "POST /api/services/media_player/volume_set":
                LastServiceCallBody = body;
                _serviceCallBodies.Enqueue(body);
                await WriteHttpResponseAsync(stream, 200, "[]").ConfigureAwait(false);
                break;
            case "POST /api/services/test/fail":
                await WriteHttpResponseAsync(stream, 400, "{\"message\":\"Validation failed\"}").ConfigureAwait(false);
                break;
            default:
                await WriteHttpResponseAsync(stream, 404, "{\"message\":\"Not found\"}").ConfigureAwait(false);
                break;
        }
    }

    private static bool HasExactValue(
        IReadOnlyDictionary<string, string> form,
        string name,
        string expected)
    {
        return form.TryGetValue(name, out var value)
            && string.Equals(value, expected, StringComparison.Ordinal);
    }

    private static bool IsReconfigurationRequest(string body)
    {
        using var document = System.Text.Json.JsonDocument.Parse(body);
        var root = document.RootElement;
        return root.ValueKind == System.Text.Json.JsonValueKind.Object
            && root.TryGetProperty("handler", out var handler)
            && string.Equals(handler.GetString(), "test", StringComparison.Ordinal)
            && root.TryGetProperty("entry_id", out var entryId)
            && string.Equals(entryId.GetString(), "entry-1", StringComparison.Ordinal)
            && !root.TryGetProperty("context", out _);
    }

    private static IReadOnlyDictionary<string, string> ParseForm(string body)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in body.Split('&'))
        {
            var separator = field.IndexOf('=');
            var name = separator < 0 ? field : field.Substring(0, separator);
            var value = separator < 0 ? string.Empty : field.Substring(separator + 1);
            values[DecodeFormValue(name)] = DecodeFormValue(value);
        }

        return values;
    }

    private static string DecodeFormValue(string value)
    {
        return Uri.UnescapeDataString(value.Replace('+', ' '));
    }
}
