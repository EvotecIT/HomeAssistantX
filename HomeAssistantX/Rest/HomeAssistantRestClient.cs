using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Configuration;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;

namespace HomeAssistantX.Rest;

/// <summary>Typed and raw access to the Home Assistant REST API.</summary>
public sealed partial class HomeAssistantRestClient : IDisposable
{
    private readonly HomeAssistantClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HomeAssistantRestClient(HomeAssistantClientOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = options.BaseUri;
        }
    }

    public Task<HomeAssistantApiStatus> CheckApiAsync(CancellationToken cancellationToken = default)
    {
        return SendHomeAssistantAsync<HomeAssistantApiStatus>(HttpMethod.Get, "api/", null, cancellationToken);
    }

    public Task<HomeAssistantConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return SendHomeAssistantAsync<HomeAssistantConfiguration>(HttpMethod.Get, "api/config", null, cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantState>> GetStatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendHomeAssistantAsync<HomeAssistantState[]>(HttpMethod.Get, "api/states", null, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task<HomeAssistantState> GetStateAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        var state = await SendHomeAssistantAsync<HomeAssistantState>(
            HttpMethod.Get,
            "api/states/" + EscapePath(normalizedEntityId),
            null,
            cancellationToken).ConfigureAwait(false);
        return HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId);
    }

    public Task<JsonElement> GetServicesAsync(CancellationToken cancellationToken = default)
    {
        return SendHomeAssistantAsync<JsonElement>(HttpMethod.Get, "api/services", null, cancellationToken);
    }

    public async Task<HomeAssistantServiceCallResult> CallServiceAsync(
        HomeAssistantServiceCall call,
        CancellationToken cancellationToken = default)
    {
        if (call is null)
        {
            throw new ArgumentNullException(nameof(call));
        }

        var path = "api/services/" + EscapePath(call.Domain) + "/" + EscapePath(call.Service)
            + (call.ReturnResponse ? "?return_response" : string.Empty);
        var result = await SendHomeAssistantAsync<JsonElement>(HttpMethod.Post, path, call.ToRestPayload(), cancellationToken).ConfigureAwait(false);

        if (result.ValueKind == JsonValueKind.Array)
        {
            return new HomeAssistantServiceCallResult
            {
                ChangedStates = HomeAssistantJson.DeserializeResponse<HomeAssistantState[]>(
                    result,
                    "The Home Assistant changed-state response could not be decoded.",
                    cancellationToken: cancellationToken)
            };
        }

        if (result.ValueKind == JsonValueKind.Object)
        {
            var response = new HomeAssistantServiceCallResult();
            if (result.TryGetProperty("changed_states", out var changedStates))
            {
                response.ChangedStates = HomeAssistantJson.DeserializeResponse<HomeAssistantState[]>(
                    changedStates,
                    "The Home Assistant changed-state response could not be decoded.",
                    cancellationToken: cancellationToken);
            }

            if (result.TryGetProperty("service_response", out var serviceResponse))
            {
                response.Response = await HomeAssistantJson.SnapshotResponseAsync(
                    serviceResponse,
                    "The Home Assistant service response could not be snapshotted.",
                    cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return response;
        }

        return new HomeAssistantServiceCallResult();
    }

    public async Task<byte[]> GetBytesAsync(string pathOrAbsoluteUri, CancellationToken cancellationToken = default)
    {
        return await ExecuteWithTimeoutAsync(
            async operationToken =>
            {
                using var response = await SendWithAuthenticationRecoveryAsync(
                    HttpMethod.Get,
                    pathOrAbsoluteUri,
                    null,
                    operationToken).ConfigureAwait(false);
                return await ReadBoundedContentAsync(
                    response.Content,
                    _options.MaximumRestResponseBytes,
                    operationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<T> SendAsync<T>(
        HttpMethod method,
        string pathOrAbsoluteUri,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        return SendTypedAsync<T>(
            method,
            pathOrAbsoluteUri,
            body,
            HomeAssistantJson.RawSerializerOptions,
            validateHomeAssistantResponse: false,
            cancellationToken);
    }

    internal Task<T> SendHomeAssistantAsync<T>(
        HttpMethod method,
        string pathOrAbsoluteUri,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        return SendTypedAsync<T>(
            method,
            pathOrAbsoluteUri,
            body,
            HomeAssistantJson.SerializerOptions,
            validateHomeAssistantResponse: true,
            cancellationToken);
    }

    /// <summary>Sends an authenticated request and returns its bounded response body as text.</summary>
    public Task<string> SendTextAsync(
        HttpMethod method,
        string pathOrAbsoluteUri,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        return SendStringAsync(method, pathOrAbsoluteUri, body, cancellationToken);
    }

    private async Task<T> SendTypedAsync<T>(
        HttpMethod method,
        string pathOrAbsoluteUri,
        object? body,
        JsonSerializerOptions serializerOptions,
        bool validateHomeAssistantResponse,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithTimeoutAsync(
            async operationToken =>
            {
                using var response = await SendWithAuthenticationRecoveryAsync(
                    method,
                    pathOrAbsoluteUri,
                    body,
                    operationToken).ConfigureAwait(false);
                var bytes = await ReadBoundedContentAsync(
                    response.Content,
                    _options.MaximumRestResponseBytes,
                    operationToken).ConfigureAwait(false);
                operationToken.ThrowIfCancellationRequested();
                using var stream = new MemoryStream(bytes, writable: false);
                var value = await JsonSerializer.DeserializeAsync<T>(
                    stream,
                    serializerOptions,
                    operationToken).ConfigureAwait(false);
                operationToken.ThrowIfCancellationRequested();
                var result = value ?? throw new HomeAssistantProtocolException("Home Assistant returned an empty JSON response.");
                return validateHomeAssistantResponse
                    ? HomeAssistantJson.RequireNoNullCollectionEntries(
                        result,
                        "The Home Assistant response contained a null collection entry.",
                        cancellationToken: operationToken)
                    : result;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> SendStringAsync(
        HttpMethod method,
        string pathOrAbsoluteUri,
        object? body,
        CancellationToken cancellationToken)
    {
        return await ExecuteWithTimeoutAsync(
            async operationToken =>
            {
                using var response = await SendWithAuthenticationRecoveryAsync(
                    method,
                    pathOrAbsoluteUri,
                    body,
                    operationToken).ConfigureAwait(false);
                var bytes = await ReadBoundedContentAsync(
                    response.Content,
                    _options.MaximumRestResponseBytes,
                    operationToken).ConfigureAwait(false);
                return Encoding.UTF8.GetString(bytes);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string pathOrAbsoluteUri,
        byte[]? serializedBody,
        CancellationToken cancellationToken)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        if (string.IsNullOrWhiteSpace(pathOrAbsoluteUri))
        {
            throw new ArgumentException("A request path is required.", nameof(pathOrAbsoluteUri));
        }

        var token = await _options.AccessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new HomeAssistantAuthenticationException("The access token provider returned an empty token.");
        }

        var requestUri = Uri.TryCreate(pathOrAbsoluteUri, UriKind.Absolute, out var absoluteUri)
            ? absoluteUri
            : new Uri(_options.BaseUri, pathOrAbsoluteUri.TrimStart('/'));
        if (!HasSameOrigin(_options.BaseUri, requestUri))
        {
            throw new ArgumentException(
                "An authenticated Home Assistant request cannot target a different origin.",
                nameof(pathOrAbsoluteUri));
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (serializedBody is not null)
        {
            request.Content = new ByteArrayContent(serializedBody);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendWithAuthenticationRecoveryAsync(
        HttpMethod method,
        string pathOrAbsoluteUri,
        object? body,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var serializedBody = body is null
            ? null
            : HomeAssistantJson.SerializeToUtf8Bytes(body, cancellationToken);
        for (var attempt = 0; ; attempt++)
        {
            using var request = await CreateRequestAsync(method, pathOrAbsoluteUri, serializedBody, cancellationToken)
                .ConfigureAwait(false);
            var rejectedAccessToken = request.Headers.Authorization?.Parameter;
            try
            {
                return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HomeAssistantAuthenticationException) when (
                attempt == 0
                && !string.IsNullOrWhiteSpace(rejectedAccessToken)
                && _options.AccessTokenProvider is IHomeAssistantAccessTokenRecovery)
            {
                var recovery = (IHomeAssistantAccessTokenRecovery)_options.AccessTokenProvider;
                await recovery.RecoverAccessTokenAsync(rejectedAccessToken!, cancellationToken).ConfigureAwait(false);
                WriteDiagnostic(
                    HomeAssistantDiagnosticLevel.Information,
                    "rest.authentication_recovered",
                    "Recovered a Home Assistant REST access token after an unauthorized response.");
            }
        }
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var statusCode = response.StatusCode;
        string? detail;
        try
        {
            detail = await ReadSafeErrorAsync(response, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            response.Dispose();
        }
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            throw new HomeAssistantAuthenticationException("Home Assistant rejected the access token.");
        }

        var rejectionMessage = string.IsNullOrWhiteSpace(detail)
            ? "Home Assistant rejected the request with HTTP " + (int)statusCode + "."
            : detail!;
        throw new HomeAssistantCommandException(
            "http_" + ((int)statusCode).ToString(System.Globalization.CultureInfo.InvariantCulture),
            rejectionMessage);
    }

    private static bool HasSameOrigin(Uri expected, Uri actual)
    {
        return string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase)
            && expected.Port == actual.Port;
    }

    private static async Task<string?> ReadSafeErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string content;
        try
        {
            var bytes = await ReadBoundedContentAsync(response.Content, 4096, cancellationToken).ConfigureAwait(false);
            content = Encoding.UTF8.GetString(bytes);
        }
        catch (HomeAssistantProtocolException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return "Home Assistant rejected the request.";
    }

    private async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.RequestTimeout);
        try
        {
            return await operation(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HomeAssistantConnectionException("The Home Assistant request timed out.", new TimeoutException());
        }
        catch (HttpRequestException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant request could not be completed.", ex);
        }
        catch (IOException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant response ended unexpectedly.", ex);
        }
        catch (JsonException ex)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned invalid JSON.", ex);
        }
    }

    private static async Task<byte[]> ReadBoundedContentAsync(
        HttpContent content,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long declaredLength && declaredLength > maximumBytes)
        {
            throw new HomeAssistantProtocolException("The Home Assistant REST response exceeded the configured size limit.");
        }

#if NET10_0_OR_GREATER
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var cancellationRegistration = cancellationToken.Register(
            state => ((Stream)state!).Dispose(),
            stream);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return buffer.ToArray();
                }

                if (buffer.Length + read > maximumBytes)
                {
                    throw new HomeAssistantProtocolException("The Home Assistant REST response exceeded the configured size limit.");
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The Home Assistant response read was canceled.", cancellationToken);
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The Home Assistant response read was canceled.", cancellationToken);
        }
    }

    private static string EscapePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty path identifier is required.", nameof(value));
        }

        return HomeAssistantUri.EscapeDataString(value.Trim(), CancellationToken.None);
    }

    private static string NormalizeEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalize(entityId, out var normalized))
        {
            throw new ArgumentException("A Home Assistant entity identifier is required.", nameof(entityId));
        }

        return normalized;
    }

    private void WriteDiagnostic(
        HomeAssistantDiagnosticLevel level,
        string name,
        string message,
        Exception? exception = null)
    {
        try
        {
            _options.Diagnostics.Write(new HomeAssistantDiagnosticEvent(level, name, message, exception));
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
