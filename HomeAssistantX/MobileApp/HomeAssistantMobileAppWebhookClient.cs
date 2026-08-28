using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.MobileApp;

/// <summary>Calls one registered mobile-app webhook with optional host-supplied SecretBox protection.</summary>
public sealed class HomeAssistantMobileAppWebhookClient : IDisposable
{
    private readonly Uri _webhookUri;
    private readonly string? _secret;
    private readonly IHomeAssistantMobileAppPayloadProtector? _protector;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maximumResponseBytes;

    internal HomeAssistantMobileAppWebhookClient(Uri webhookUri, string? secret, IHomeAssistantMobileAppPayloadProtector? protector, TimeSpan requestTimeout, int maximumResponseBytes)
    {
        if (webhookUri is null || !webhookUri.IsAbsoluteUri || (webhookUri.Scheme != Uri.UriSchemeHttp && webhookUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("An absolute HTTP or HTTPS webhook URI is required.", nameof(webhookUri));
        }
        if (!string.IsNullOrEmpty(webhookUri.UserInfo))
        {
            throw new ArgumentException("Mobile-app webhook URIs cannot contain embedded credentials.", nameof(webhookUri));
        }

        if (secret is not null && string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("A mobile-app encryption secret cannot be empty or whitespace.", nameof(secret));
        }

        if (secret is not null && protector is null)
        {
            throw new ArgumentException("An encrypted registration requires a mobile-app payload protector.", nameof(protector));
        }

        _webhookUri = webhookUri;
        _secret = secret;
        _protector = protector;
        _requestTimeout = requestTimeout;
        _maximumResponseBytes = maximumResponseBytes;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseDefaultCredentials = false,
            Credentials = null,
            PreAuthenticate = false,
            UseProxy = false,
            UseCookies = false
        }, disposeHandler: true);
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public Task<JsonElement> GetConfigurationAsync(CancellationToken cancellationToken = default)
        => SendAsync("get_config", null, cancellationToken);

    public Task<JsonElement> GetZonesAsync(CancellationToken cancellationToken = default)
        => SendAsync("get_zones", null, cancellationToken);

    public async Task<HomeAssistantMobileAppCameraStream> GetCameraStreamAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "camera", out var normalizedEntityId))
            throw new ArgumentException("A camera entity ID is required.", nameof(entityId));
        var result = await SendAsync("stream_camera", new Dictionary<string, object?> { ["camera_entity_id"] = normalizedEntityId }, cancellationToken).ConfigureAwait(false);
        var stream = HomeAssistantJson.DeserializeResponse<HomeAssistantMobileAppCameraStream>(
            result,
            "Home Assistant returned an invalid camera-stream response.",
            cancellationToken: cancellationToken);
        var hasMjpeg = !string.IsNullOrWhiteSpace(stream.MjpegPath);
        var hasHls = !string.IsNullOrWhiteSpace(stream.HlsPath);
        if (stream.Success == false
            || (stream.MjpegPath is not null && !hasMjpeg)
            || (stream.HlsPath is not null && !hasHls)
            || (!hasMjpeg && !hasHls))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an unsuccessful camera-stream response.");
        }
        return stream;
    }

    public Task<JsonElement> UpdateRegistrationAsync(HomeAssistantMobileAppRegistrationUpdate update, CancellationToken cancellationToken = default)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        cancellationToken.ThrowIfCancellationRequested();
        update.Validate();
        var snapshot = new HomeAssistantMobileAppRegistrationUpdate
        {
            AppVersion = update.AppVersion,
            DeviceName = update.DeviceName,
            Manufacturer = update.Manufacturer,
            Model = update.Model,
            OperatingSystemVersion = update.OperatingSystemVersion,
            AppData = HomeAssistantJson.FreezeObject(update.AppData, nameof(update.AppData), "AppData", cancellationToken)
        };
        return SendAsync("update_registration", snapshot, cancellationToken);
    }

    /// <summary>Calls a forward-compatible mobile-app webhook command. The command data is encrypted when the registration contains a secret.</summary>
    public async Task<JsonElement> SendAsync(string commandType, object? data, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandType)) throw new ArgumentException("A webhook command type is required.", nameof(commandType));
        cancellationToken.ThrowIfCancellationRequested();
        var command = commandType.Trim();
        var frozenData = data is null
            ? HomeAssistantJson.FreezeValue(new Dictionary<string, object?>(), nameof(data), "Data", cancellationToken)
            : HomeAssistantJson.FreezeValue(data, nameof(data), "Data", cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        var operationToken = timeout.Token;
        try
        {
            object envelope;
            if (_secret is null)
            {
                envelope = new Dictionary<string, object?> { ["type"] = command, ["data"] = frozenData };
            }
            else
            {
                var requestPlaintext = HomeAssistantJson.SerializeToUtf8Bytes(frozenData, operationToken);
                var encrypted = await _protector!.ProtectAsync(requestPlaintext, _secret!, operationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(encrypted)) throw new HomeAssistantProtocolException("The mobile-app payload protector returned an empty request payload.");
                envelope = new Dictionary<string, object?> { ["type"] = command, ["encrypted"] = true, ["encrypted_data"] = encrypted };
            }

            var requestBytes = HomeAssistantJson.SerializeToUtf8Bytes(envelope, operationToken);
            using var request = new HttpRequestMessage(HttpMethod.Post, _webhookUri)
            {
                Content = new ByteArrayContent(requestBytes)
            };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            using var response = await SendRequestAsync(request, operationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HomeAssistantCommandException("http_" + (int)response.StatusCode, "Home Assistant rejected the mobile-app webhook request with HTTP " + (int)response.StatusCode + ".");
            }

            var bytes = await ReadResponseAsync(response.Content, operationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                if (_secret is not null)
                {
                    throw new HomeAssistantProtocolException("Home Assistant returned an unencrypted response for an encrypted mobile-app request.");
                }

                return await ParseResponseAsync(new byte[] { (byte)'{', (byte)'}' }, operationToken)
                    .ConfigureAwait(false);
            }

            var root = await ParseResponseAsync(bytes, operationToken).ConfigureAwait(false);
            var isEncryptedResponse = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("encrypted", out var encryptedFlag)
                && encryptedFlag.ValueKind == JsonValueKind.True;
            if (_secret is not null && !isEncryptedResponse)
            {
                throw new HomeAssistantProtocolException("Home Assistant returned an unencrypted response for an encrypted mobile-app request.");
            }

            if (isEncryptedResponse)
            {
                if (_secret is null
                    || _protector is null
                    || !root.TryGetProperty("encrypted_data", out var encryptedData)
                    || encryptedData.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(encryptedData.GetString()))
                {
                    throw new HomeAssistantProtocolException("Home Assistant returned an encrypted mobile-app response that cannot be decrypted.");
                }

                var plaintext = await _protector.UnprotectAsync(encryptedData.GetString()!, _secret!, operationToken).ConfigureAwait(false);
                return await ParseResponseAsync(plaintext, operationToken).ConfigureAwait(false);
            }

            operationToken.ThrowIfCancellationRequested();
            return root;
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new HomeAssistantConnectionException("The Home Assistant mobile-app webhook request timed out.", new TimeoutException());
        }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant mobile-app webhook request failed.", ex);
        }
    }

    private async Task<byte[]> ReadResponseAsync(HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            return await ReadBoundedAsync(content, _maximumResponseBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant mobile-app webhook response could not be read.", ex);
        }
    }

    internal static async Task<JsonElement> ParseResponseAsync(
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        JsonDocument? document = null;
        var ownershipTransferred = false;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ownershipTransferred = true;
            return document.RootElement;
        }
        catch (JsonException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new HomeAssistantProtocolException("Home Assistant returned invalid mobile-app JSON.", ex);
        }
        finally
        {
            if (!ownershipTransferred) document?.Dispose();
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximumResponseBytes, CancellationToken cancellationToken)
    {
        try
        {
            if (content.Headers.ContentLength is long length && length > maximumResponseBytes) throw new HomeAssistantProtocolException("The mobile-app response exceeded the size limit.");
#if NET10_0_OR_GREATER
            using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
            using var registration = cancellationToken.Register(state => ((Stream)state!).Dispose(), stream);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0) return output.ToArray();
                if (output.Length + read > maximumResponseBytes) throw new HomeAssistantProtocolException("The mobile-app response exceeded the size limit.");
                output.Write(buffer, 0, read);
            }
        }
        catch (Exception ex) when ((ex is IOException || ex is ObjectDisposedException) && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The mobile-app response read was canceled.", ex, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant mobile-app webhook response could not be read.", ex);
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
