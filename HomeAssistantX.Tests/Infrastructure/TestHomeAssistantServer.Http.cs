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

        LastAuthorization = headers.TryGetValue("Authorization", out var authorization) ? authorization : null;
        if (!string.Equals(LastAuthorization, "Bearer " + RequiredAccessToken, StringComparison.Ordinal))
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
            case "GET /api/history/period/2026-08-24T00%3A00%3A00.0000000%2B00%3A00":
                await WriteHttpResponseAsync(stream, 200, HistoryResponseJson).ConfigureAwait(false);
                break;
            case "GET /api/logbook/2026-08-24T00%3A00%3A00.0000000%2B00%3A00":
                await WriteHttpResponseAsync(stream, 200,
                    "[{\"when\":\"2026-08-24T12:00:00+00:00\",\"name\":\"Kitchen light\",\"message\":\"turned on\",\"domain\":\"light\",\"entity_id\":\"light.kitchen\"}]")
                    .ConfigureAwait(false);
                break;
            case "GET /api/states":
                await WriteHttpResponseAsync(stream, 200, GetStates()).ConfigureAwait(false);
                break;
            case "GET /api/states/sensor.kitchen_temperature":
                await WriteHttpResponseAsync(stream, 200, KitchenTemperatureStateJson).ConfigureAwait(false);
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
                await WriteHttpResponseAsync(stream, 200, "test-image-bytes").ConfigureAwait(false);
                break;
            case "GET /api/calendars":
                await WriteHttpResponseAsync(stream, 200, "[{\"entity_id\":\"calendar.home\",\"name\":\"Home\"}]").ConfigureAwait(false);
                break;
            case "GET /api/calendars/calendar.home":
                await WriteHttpResponseAsync(stream, 200,
                    "[{\"summary\":\"Dinner\",\"start\":{\"dateTime\":\"2026-08-25T18:00:00+02:00\"},\"end\":{\"dateTime\":\"2026-08-25T20:00:00+02:00\"},\"location\":\"Home\"}]")
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
                LastServiceCallBody = body;
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
