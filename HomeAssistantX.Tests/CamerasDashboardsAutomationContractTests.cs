#if NET10_0
using System.Text;
using System.Text.Json;
using HomeAssistantX.Automations;
using HomeAssistantX.Cameras;
using HomeAssistantX.Dashboards;
using HomeAssistantX.Exceptions;
using HomeAssistantX.IO;
using HomeAssistantX.Media;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class CamerasDashboardsAutomationContractTests
{
    [Fact]
    public void FrontendPanelSortingHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantDashboardClient.SortPanels(
                new List<HomeAssistantPanel>
                {
                    new() { UrlPath = "settings" },
                    new() { UrlPath = "energy" }
                },
                cancellation.Token));
    }

    [Theory]
    [InlineData(":")]
    [InlineData("mdi:")]
    [InlineData(":home")]
    [InlineData("custom:room_kitchen")]
    public void DashboardIconsFollowHomeAssistantColonContract(string icon)
    {
        Assert.True(HomeAssistantDashboardIdentifier.TryNormalizeIcon(icon, out var normalized));
        Assert.Equal(icon, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GlobalMediaSelectorsRejectBlankValuesBeforeDispatch(string selector)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Media.BrowseSourcesAsync(selector));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Media.SearchSourcesResponseAsync("music", selector));
        Assert.Null(server.GetLastWebSocketCommand("media_source/browse_media"));
        Assert.Null(server.GetLastWebSocketCommand("media_source/search_media"));
    }

    [Fact]
    public async Task CameraSurfaceIsTypedBoundedSignedAndPushCapable()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[" +
            "{\"entity_id\":\"camera.front\",\"state\":\"idle\",\"attributes\":{\"friendly_name\":\"Front\",\"brand\":\"Test\",\"model_name\":\"One\",\"motion_detection\":true,\"supported_features\":3}}," +
            "{\"entity_id\":\"camera.bad\",\"state\":\"idle\",\"attributes\":{\"supported_features\":4294967297}}]");
        using var client = TestClientFactory.Create(server);

        var cameras = await client.Cameras.GetAsync();
        var camera = Assert.Single(cameras, value => value.EntityId == "camera.front");
        Assert.Equal(HomeAssistantCameraFeature.OnOff | HomeAssistantCameraFeature.Stream, camera.SupportedFeatures);
        Assert.Equal(
            HomeAssistantCameraFeature.None,
            Assert.Single(cameras, value => value.EntityId == "camera.bad").SupportedFeatures);
        Assert.True(camera.MotionDetectionEnabled);
        Assert.Equal("test-image-bytes", Encoding.UTF8.GetString(await client.Cameras.GetSnapshotAsync("camera.front", 640, 360)));
        Assert.Contains("width=640", server.LastRequestPath);
        var capabilities = await client.Cameras.GetCapabilitiesAsync("camera.front");
        Assert.Contains("web_rtc", capabilities.FrontendStreamTypes);
        Assert.True(capabilities.AdditionalData["future_capability"].GetBoolean());
        var stream = await client.Cameras.GetStreamAsync("camera.front");
        Assert.EndsWith("master_playlist.m3u8", stream.Path);
        Assert.True(stream.AdditionalData["future_stream_field"].GetBoolean());
        Assert.Equal("also-kept", stream.AdditionalData["Future_Stream_Field"].GetString());
        Assert.Equal(HomeAssistantCameraOrientation.Rotate180, (await client.Cameras.GetPreferencesAsync("camera.front")).Orientation);
        await client.Cameras.SavePreferencesAsync("camera.front", new HomeAssistantCameraPreferencesUpdate { Orientation = HomeAssistantCameraOrientation.Rotate180 });
        using (var prefs = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("camera/update_prefs"))))
            Assert.Equal(3, prefs.RootElement.GetProperty("orientation").GetInt32());
        var signedImage = await client.Cameras.GetSignedImagePathAsync("camera.front", width: 640, height: 360);
        Assert.Contains("authSig=signed", signedImage);
        Assert.Contains("width=640&height=360", signedImage);
        using (var imageSign = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("auth/sign_path"))))
            Assert.Equal("/api/camera_proxy/camera.front", imageSign.RootElement.GetProperty("path").GetString());
        var signedMjpeg = await client.Cameras.GetSignedMjpegStreamPathAsync("camera.front", intervalSeconds: 1.5);
        Assert.Contains("authSig=signed", signedMjpeg);
        Assert.Contains("interval=1.5", signedMjpeg);
        using (var signed = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("auth/sign_path"))))
            Assert.Equal("/api/camera_proxy_stream/camera.front", signed.RootElement.GetProperty("path").GetString());
    }

    [Fact]
    public async Task CameraPreflightRejectsInvalidDimensionsAndPreferencesBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Cameras.GetSnapshotAsync("camera.front", 640));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Cameras.GetSnapshotAsync("camera."));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Cameras.GetCapabilitiesAsync("camera.front.extra"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.GetCameraImageAsync("camera.front.extra"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Cameras.GetSignedMjpegStreamPathAsync("camera.front", intervalSeconds: 0.1));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Cameras.SavePreferencesAsync("camera.front", new HomeAssistantCameraPreferencesUpdate()));
        Assert.Null(server.GetLastWebSocketCommand("camera/update_prefs"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"frontend_stream_types\":null}")]
    [InlineData("{\"frontend_stream_types\":{}}")]
    [InlineData("{\"frontend_stream_types\":[\"\"]}")]
    [InlineData("{\"frontend_stream_types\":[\" hls \"]}")]
    [InlineData("{\"frontend_stream_types\":[\"HLS\"]}")]
    [InlineData("{\"frontend_stream_types\":[\"hls\",\"hls\"]}")]
    public async Task CameraCapabilitiesRequireTheFrontendStreamTypeArray(string response)
    {
        using var server = new TestHomeAssistantServer { CameraCapabilitiesResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetCapabilitiesAsync("camera.front"));
    }

    [Fact]
    public async Task CameraCapabilitiesPreserveCanonicalFutureStreamTypes()
    {
        using var server = new TestHomeAssistantServer
        {
            CameraCapabilitiesResponseJson = "{\"frontend_stream_types\":[\"future_stream\"]}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Equal("future_stream", Assert.Single((await client.Cameras.GetCapabilitiesAsync("camera.front")).FrontendStreamTypes));
    }

    [Theory]
    [InlineData("{\"url\":\" stream.m3u8 \"}")]
    [InlineData("{\"url\":\"stream.m3u8\"}")]
    [InlineData("{\"url\":\"//other.example/stream.m3u8\"}")]
    [InlineData("{\"url\":\"/api\\\\hls\\\\stream.m3u8\"}")]
    [InlineData("{\"url\":\"https://other.example/stream.m3u8\"}")]
    [InlineData("{\"url\":\"/api/hls/stream.m3u8#fragment\"}")]
    [InlineData("{\"url\":\"/api/one/../hls/stream.m3u8\"}")]
    [InlineData("{\"url\":\"/api/%2e%2e/hls/stream.m3u8\"}")]
    public async Task CameraStreamsRequireCanonicalRootRelativePaths(string response)
    {
        using var server = new TestHomeAssistantServer { CameraStreamResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetStreamAsync("camera.front"));
    }

    [Fact]
    public async Task CameraMutationsRejectEmptySnapshotsAndMismatchedPreferences()
    {
        using var server = new TestHomeAssistantServer { CameraImageResponse = string.Empty };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetSnapshotAsync("camera.front"));

        server.CameraPreferencesResponseJson = "{\"preload_stream\":false,\"orientation\":3}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.SavePreferencesAsync(
            "camera.front",
            new HomeAssistantCameraPreferencesUpdate { PreloadStream = true }));

        server.CameraPreferencesResponseJson = "{\"preload_stream\":true,\"orientation\":1}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.SavePreferencesAsync(
            "camera.front",
            new HomeAssistantCameraPreferencesUpdate { Orientation = HomeAssistantCameraOrientation.Rotate180 }));

        server.CameraPreferencesResponseJson = "{\"orientation\":3}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.SavePreferencesAsync(
            "camera.front",
            new HomeAssistantCameraPreferencesUpdate { PreloadStream = false }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task CameraPreferenceReadsRejectUndefinedOrientations(int orientation)
    {
        using var server = new TestHomeAssistantServer { CameraPreferencesResponseJson = "{\"preload_stream\":true,\"orientation\":" + orientation + "}" };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetPreferencesAsync("camera.front"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.SavePreferencesAsync(
            "camera.front", new HomeAssistantCameraPreferencesUpdate { PreloadStream = true }));
    }

    [Fact]
    public async Task CameraSignedPathsRejectMismatchedRoutes()
    {
        using var server = new TestHomeAssistantServer
        {
            SignedPathResponseJson = "{\"path\":\"/api/camera_proxy/camera.other?authSig=signed\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Cameras.GetSignedImagePathAsync("camera.front"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Cameras.GetSignedMjpegStreamPathAsync("camera.front"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"preload_stream\":true}")]
    [InlineData("{\"orientation\":3}")]
    [InlineData("{\"preload_stream\":null,\"orientation\":3}")]
    [InlineData("{\"preload_stream\":true,\"orientation\":\"3\"}")]
    public async Task CameraPreferencesRequireCompleteTypedResponses(string response)
    {
        using var server = new TestHomeAssistantServer { CameraPreferencesResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetPreferencesAsync("camera.front"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.SavePreferencesAsync(
            "camera.front",
            new HomeAssistantCameraPreferencesUpdate { PreloadStream = true }));
    }

    [Fact]
    public async Task CameraWebSocketResponsesRejectDuplicateProperties()
    {
        using var server = new TestHomeAssistantServer
        {
            CameraCapabilitiesResponseJson = "{\"frontend_stream_types\":[\"mjpeg\"],\"frontend_stream_types\":[\"hls\"]}",
            CameraStreamResponseJson = "{\"url\":\"/api/hls/other.m3u8\",\"url\":\"/api/hls/stream.m3u8\"}",
            CameraPreferencesResponseJson = "{\"preload_stream\":false,\"preload_stream\":true,\"orientation\":1}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetCapabilitiesAsync("camera.front"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetStreamAsync("camera.front"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetPreferencesAsync("camera.front"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.SavePreferencesAsync(
            "camera.front",
            new HomeAssistantCameraPreferencesUpdate { PreloadStream = true }));
    }

    [Fact]
    public void CameraStreamTypeValidationObservesCancellationDuringTraversal()
    {
        using var cancellation = new CancellationTokenSource();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantCameraClient.ValidateStreamTypes(CancelAfterFirstStreamType(cancellation), cancellation.Token));
    }

    [Theory]
    [InlineData("House_Main")]
    [InlineData("House-main")]
    [InlineData("house--main")]
    [InlineData("-house-main")]
    [InlineData("house-main-")]
    public async Task DashboardCreationRejectsNonCanonicalUrlSlugsBeforeDispatch(string urlPath)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Dashboards.CreateDashboardAsync(
            new HomeAssistantDashboardCreate { UrlPath = urlPath, Title = "House" }));
        Assert.Null(server.GetLastWebSocketCommand("lovelace/dashboards/create"));
    }

    [Fact]
    public async Task DashboardSlugValidationObservesCancellationDuringTraversal()
    {
        var longValue = "house-" + new string('a', 16_000_000);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Run(() =>
        {
            started.TrySetResult(true);
            return HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(
                longValue,
                allowSingleWord: false,
                out _,
                cancellation.Token);
        });
        await started.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
    }

    [Fact]
    public async Task AutomationIdentifierNormalizationObservesCancellationDuringTraversal()
    {
        var longValue = "automation-" + new string('a', 16_000_000);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Run(() =>
        {
            started.TrySetResult(true);
            return HomeAssistantAutomationIdentifier.NormalizeConfigurationId(longValue, cancellation.Token);
        });
        await started.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
    }

    [Theory]
    [InlineData("{\"title\":\"Music\",\"media_content_type\":\"library\",\"can_play\":true,\"children\":[]}")]
    [InlineData("{\"title\":\"Music\",\"media_content_id\":\"media-source://media_source\",\"can_expand\":true,\"children\":[{\"title\":\"Child\",\"media_content_id\":\"child\",\"can_play\":true,\"children\":[]}]}")]
    [InlineData("{\"title\":\"Music\",\"media_class\":\"music\",\"media_content_id\":\" track-1 \",\"media_content_type\":\"audio/mpeg\",\"can_play\":true,\"can_expand\":false,\"can_search\":false,\"children\":[]}")]
    [InlineData("{\"title\":\"Music\",\"media_class\":\"music\",\"media_content_id\":\"track-1\",\"media_content_type\":\" audio/mpeg \",\"can_play\":true,\"can_expand\":false,\"can_search\":false,\"children\":[]}")]
    [InlineData("{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\" search-root \",\"media_content_type\":\"library\",\"can_play\":false,\"can_expand\":false,\"can_search\":true,\"children\":[]}")]
    [InlineData("{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\"search-root\",\"media_content_type\":\" library \",\"can_play\":false,\"can_expand\":false,\"can_search\":true,\"children\":[]}")]
    public async Task MediaBrowseRejectsInvalidActionableContentIdentity(string response)
    {
        using var server = new TestHomeAssistantServer { MediaBrowseResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
    }

    [Fact]
    public async Task CallerJsonParsingReturnsPromptlyWhenCancellationArrives()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        try
        {
            var parsing = HomeAssistantJson.ParseDocumentAsync(
                "{}",
                cancellation.Token,
                value =>
                {
                    started.TrySetResult(true);
                    release.Task.GetAwaiter().GetResult();
                    return JsonDocument.Parse(value);
                });
            await started.Task;
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => parsing);
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [Fact]
    public async Task MediaBrowseAllowsNonActionableProviderMessagesWithoutSelectors()
    {
        using var server = new TestHomeAssistantServer
        {
            MediaBrowseResponseJson = "{\"title\":\"Provider unavailable\",\"media_class\":\"message\",\"media_content_id\":\"\",\"media_content_type\":\"\",\"can_play\":false,\"can_expand\":false,\"can_search\":false,\"children\":[]}"
        };
        using var client = TestClientFactory.Create(server);

        var item = await client.Media.BrowseSourcesAsync();

        Assert.Equal("Provider unavailable", item.Title);
        Assert.Empty(item.MediaContentId);
        Assert.Empty(item.MediaContentType);
    }

    [Theory]
    [InlineData("{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\"root\",\"media_content_type\":\"library\",\"can_play\":false,\"can_expand\":true,\"can_search\":false,\"not_shown\":-1,\"children\":[]}")]
    [InlineData("{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\"root\",\"media_content_type\":\"library\",\"can_play\":false,\"can_expand\":true,\"can_search\":false,\"children\":[{\"title\":\"Hidden\",\"media_class\":\"music\",\"media_content_id\":\"child\",\"media_content_type\":\"audio/mpeg\",\"can_play\":true,\"can_expand\":false,\"can_search\":false,\"not_shown\":-1}]}")]
    public async Task MediaBrowseRejectsNegativeHiddenCounts(string response)
    {
        using var server = new TestHomeAssistantServer { MediaBrowseResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
    }

    [Fact]
    public async Task MediaSearchRejectsNegativeHiddenCounts()
    {
        using var server = new TestHomeAssistantServer
        {
            MediaSearchResponseJson = "{\"result\":[{\"title\":\"Music\",\"media_class\":\"music\",\"media_content_id\":\"item\",\"media_content_type\":\"audio/mpeg\",\"can_play\":true,\"can_expand\":false,\"can_search\":false,\"not_shown\":-1}]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.SearchSourcesAsync("music"));
    }

    [Fact]
    public async Task MediaBrowseValidationHonorsCancellationBeforeTraversingResults()
    {
        using var document = JsonDocument.Parse(
            "{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\"root\",\"media_content_type\":\"library\",\"can_play\":false,\"can_expand\":true,\"can_search\":false,\"children\":[null]}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HomeAssistantMediaBrowserClient.DecodeItemAsync(document.RootElement, cancellation.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" music ")]
    public async Task MediaBrowseRejectsNoncanonicalSearchMediaClasses(string mediaClass)
    {
        using var server = new TestHomeAssistantServer
        {
            MediaBrowseResponseJson =
                "{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\"root\",\"media_content_type\":\"library\",\"can_play\":false,\"can_expand\":true,\"can_search\":true,\"search_media_classes\":[\""
                + mediaClass + "\"],\"children\":[]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
    }

    [Fact]
    public async Task TypedMediaResponsesRejectDuplicateProperties()
    {
        using var server = new TestHomeAssistantServer
        {
            MediaBrowseResponseJson =
                "{\"title\":\"Music\",\"media_class\":\"directory\",\"media_content_id\":\"other\",\"media_content_id\":\"root\",\"media_content_type\":\"library\",\"can_play\":false,\"can_expand\":true,\"can_search\":false,\"children\":[]}",
            MediaSearchResponseJson =
                "{\"result\":[{\"title\":\"Music\",\"media_class\":\"music\",\"media_content_id\":\"other\",\"media_content_id\":\"item\",\"media_content_type\":\"audio/mpeg\",\"can_play\":true,\"can_expand\":false,\"can_search\":false}]}",
            ResolvedMediaResponseJson = "{\"url\":\"/api/media/other\",\"url\":\"/api/media/file\",\"mime_type\":\"audio/mpeg\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.SearchSourcesAsync("music"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.ResolveAsync("media-source://media_source/local/file.mp3"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" audio/mpeg")]
    [InlineData("audio /mpeg")]
    [InlineData("audio")]
    [InlineData("audio/mpeg/extra")]
    public async Task MediaResolveRejectsMalformedMimeTypes(string mimeType)
    {
        using var server = new TestHomeAssistantServer
        {
            ResolvedMediaResponseJson = JsonSerializer.Serialize(new { url = "/api/media/file", mime_type = mimeType })
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Media.ResolveAsync("media-source://media_source/local/file.mp3"));
    }

    [Theory]
    [InlineData("application/json;charset=utf-8")]
    [InlineData("audio/mpeg; profile=\"provider-v1\"")]
    public async Task MediaResolveAcceptsParameterizedMimeTypes(string mimeType)
    {
        using var server = new TestHomeAssistantServer
        {
            ResolvedMediaResponseJson = JsonSerializer.Serialize(new { url = "/api/media/file", mime_type = mimeType })
        };
        using var client = TestClientFactory.Create(server);

        Assert.Equal(
            mimeType,
            (await client.Media.ResolveAsync("media-source://media_source/local/file.mp3")).MimeType);
    }

    [Fact]
    public async Task CameraEntityValidationObservesCancellationDuringTraversal()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        var entityId = "camera." + new string('a', 16_000_000);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Factory.StartNew(
            async () =>
            {
                started.TrySetResult(true);
                await client.Cameras.GetAsync(entityId, cancellation.Token);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        Assert.Null(server.GetLastWebSocketCommand("get_states"));
    }

    [Fact]
    public async Task MediaClassFiltersPreserveOrderWhileDeduplicatingCaseInsensitively()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Media.SearchSourcesAsync("music", mediaClasses: new[] { " music ", "Music", "video" });

        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("media_source/search_media")));
        Assert.Equal(
            new[] { "music", "video" },
            command.RootElement.GetProperty("media_filter_classes").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [Theory]
    [InlineData(" /api/media/file")]
    [InlineData("/api/media/../secret")]
    [InlineData("//other.example/media")]
    [InlineData("http://[")]
    [InlineData("relative/media")]
    [InlineData("file:///var/media/file.mp3")]
    [InlineData("data:audio/mpeg;base64,AA==")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://provider.example/media/file.mp3")]
    [InlineData("https://user:secret@provider.example/media/file.mp3")]
    public async Task MediaResolveRejectsMalformedOrNoncanonicalUrls(string url)
    {
        using var server = new TestHomeAssistantServer
        {
            ResolvedMediaResponseJson = JsonSerializer.Serialize(new { url, mime_type = "audio/mpeg" })
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Media.ResolveAsync("media-source://media_source/local/test"));
    }

    [Theory]
    [InlineData("/api/media/file?authSig=signed")]
    [InlineData("https://provider.example/media/file?token=signed")]
    public async Task MediaResolveAcceptsCanonicalProviderUrls(string url)
    {
        using var server = new TestHomeAssistantServer
        {
            ResolvedMediaResponseJson = JsonSerializer.Serialize(new { url, mime_type = "audio/mpeg" })
        };
        using var client = TestClientFactory.Create(server);

        Assert.Equal(url, (await client.Media.ResolveAsync("media-source://media_source/local/test")).Url);
    }

    [Fact]
    public async Task ResolvedMediaStringValidationObservesCancellation()
    {
        var longValue = new string('a', 16_000_000);
        using (var cancellation = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = Task.Run(() =>
            {
                started.TrySetResult(true);
                return HomeAssistantMediaBrowserClient.IsValidResolvedUrl(
                    "/api/media/" + longValue,
                    cancellation.Token);
            });
            await started.Task;
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        }

        using (var cancellation = new CancellationTokenSource())
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var operation = Task.Run(() =>
            {
                started.TrySetResult(true);
                return HomeAssistantMediaBrowserClient.IsValidMediaType(
                    "audio/" + longValue,
                    cancellation.Token);
            });
            await started.Task;
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        }
    }

    [Fact]
    public async Task CameraAndAutomationBulkReadsRejectMalformedServerEntityIds()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        server.SetStates("[{\"entity_id\":\"camera.front.extra\",\"state\":\"idle\",\"attributes\":{}}]");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetAsync());

        server.SetStates("[{\"entity_id\":\"automation.Morning\",\"state\":\"on\",\"attributes\":{}}]");
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Automations.GetAsync());

        server.SetStates("[{\"entity_id\":null,\"state\":\"idle\",\"attributes\":{}}]");
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetAsync());

        server.SetStates("[{\"entity_id\":\" automation.morning\",\"state\":\"on\",\"attributes\":{}}]");
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Automations.GetAsync());
    }

    [Fact]
    public async Task AutomationStatusTreatsNegativeCurrentRunsAsUnavailable()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"automation.morning\",\"state\":\"on\",\"attributes\":{\"current\":-1}}]");
        using var client = TestClientFactory.Create(server);

        Assert.Null(Assert.Single(await client.Automations.GetAsync()).CurrentRuns);
    }

    [Fact]
    public async Task CameraAndAutomationListingsRejectDuplicateEntityIdentities()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        server.SetStates("["
            + "{\"entity_id\":\"camera.front\",\"state\":\"idle\",\"attributes\":{}},"
            + "{\"entity_id\":\"camera.front\",\"state\":\"streaming\",\"attributes\":{}}]");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetAsync());

        server.SetStates("["
            + "{\"entity_id\":\"automation.morning\",\"state\":\"on\",\"attributes\":{}},"
            + "{\"entity_id\":\"automation.morning\",\"state\":\"off\",\"attributes\":{}}]");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Automations.GetAsync());
    }

    [Fact]
    public async Task MediaBrowserPreservesProviderFieldsAndExactWireContracts()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var root = await client.Media.BrowseSourcesAsync();
        Assert.True(root.CanSearch);
        Assert.True(Assert.Single(root.Children).AdditionalData["future_media_field"].GetBoolean());
        var searched = await client.Media.SearchPlayerAsync("media_player.kitchen", "dinner", mediaClasses: new[] { "music" });
        Assert.Equal("Dinner", Assert.Single(searched).Title);
        using (var search = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("media_player/search_media"))))
        {
            Assert.Equal("dinner", search.RootElement.GetProperty("search_query").GetString());
            Assert.False(search.RootElement.TryGetProperty("media_search_query", out _));
            Assert.Equal("music", search.RootElement.GetProperty("media_filter_classes")[0].GetString());
        }
        var detailedSearch = await client.Media.SearchPlayerResponseAsync("media_player.kitchen", "dinner");
        Assert.Equal("Dinner", Assert.Single(detailedSearch.Items).Title);
        Assert.Equal("kept", detailedSearch.AdditionalData["future_search_metadata"].GetProperty("provider").GetString());
        var resolved = await client.Media.ResolveAsync("media-source://media_source/local/dinner.mp3", TimeSpan.FromMinutes(5));
        Assert.Equal("audio/mpeg", resolved.MimeType);
        Assert.True(resolved.AdditionalData["future_resolve"].GetBoolean());

        await Assert.ThrowsAsync<ArgumentException>(() => client.Media.BrowsePlayerAsync("media_player."));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Media.SearchPlayerAsync("media_player.kitchen.extra", "dinner"));

    }

    [Fact]
    public async Task CameraAndAutomationGettersRejectMismatchedResponseEntities()
    {
        using var server = new TestHomeAssistantServer
        {
            ExactStateResponseJson = "{\"entity_id\":\"camera.back\",\"state\":\"idle\",\"attributes\":{}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetAsync("camera.front"));
        server.ExactStateResponseJson = "{\"entity_id\":\"automation.evening\",\"state\":\"on\",\"attributes\":{}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Automations.GetAsync("automation.morning"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    public async Task MediaBrowseRejectsMalformedChildCollections(string childrenJson)
    {
        using var server = new TestHomeAssistantServer
        {
            MediaBrowseResponseJson = "{\"title\":\"Music\",\"children\":" + childrenJson + "}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
    }

    [Fact]
    public async Task MediaSearchRejectsItemsWithoutRequiredTitles()
    {
        using var server = new TestHomeAssistantServer { MediaSearchResponseJson = "{\"result\":[{}]}" };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.SearchSourcesAsync("dinner"));
    }

    [Theory]
    [InlineData("{\"title\":\"Music\",\"can_expand\":true}")]
    [InlineData("{\"title\":\"Music\",\"can_play\":false}")]
    [InlineData("{\"title\":\"Music\",\"can_play\":0,\"can_expand\":true}")]
    public async Task MediaBrowseRejectsMissingOrInvalidActionabilityFlags(string response)
    {
        using var server = new TestHomeAssistantServer { MediaBrowseResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
    }

    [Fact]
    public async Task MediaSearchRejectsMissingActionabilityFlags()
    {
        using var server = new TestHomeAssistantServer { MediaSearchResponseJson = "{\"result\":[{\"title\":\"Music\"}]}" };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.SearchSourcesAsync("music"));
    }

    [Fact]
    public async Task DashboardsExposeReadModelsAndGuardStorageMutations()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var panel = Assert.Single(await client.Dashboards.GetPanelsAsync());
        Assert.Equal("lovelace", panel.UrlPath);
        Assert.True(panel.DefaultVisible);
        Assert.True(panel.AdditionalData["future_panel"].GetBoolean());
        Assert.Equal("storage", (await client.Dashboards.GetInfoAsync()).ResourceMode);
        Assert.True(Assert.Single(await client.Dashboards.GetDashboardsAsync()).AdditionalData["future_dashboard"].GetBoolean());
        Assert.True((await client.Dashboards.GetConfigurationAsync()).GetProperty("future_config").GetBoolean());
        Assert.True(Assert.Single(await client.Dashboards.GetResourcesAsync()).AdditionalData["future_resource"].GetBoolean());

        await client.Dashboards.CreateDashboardAsync(new HomeAssistantDashboardCreate { UrlPath = "house-main", Title = "House" });
        await client.Dashboards.UpdateDashboardAsync("house-main", new HomeAssistantDashboardUpdate { RemoveIcon = true });
        using (var update = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("lovelace/dashboards/update"))))
            Assert.Equal(JsonValueKind.Null, update.RootElement.GetProperty("icon").ValueKind);
        await client.Dashboards.CreateResourceAsync("/local/card.js", HomeAssistantDashboardResourceType.Module);
        using (var resource = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("lovelace/resources/create"))))
            Assert.Equal("module", resource.RootElement.GetProperty("res_type").GetString());
        await client.Dashboards.UpdateResourceAsync("resource-1", url: "/local/card.js");

        server.ClearLastWebSocketCommand("lovelace/dashboards/create");
        await Assert.ThrowsAsync<ArgumentException>(() => client.Dashboards.CreateDashboardAsync(new HomeAssistantDashboardCreate { UrlPath = "house", Title = "House" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Dashboards.CreateDashboardAsync(new HomeAssistantDashboardCreate { UrlPath = "house-main", Title = "House", Icon = "home" }));
        using var emptyConfiguration = JsonDocument.Parse("{}");
        foreach (var invalidPath in new[] { "House-main", "house--main", "house-main-", "house main" })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => client.Dashboards.GetConfigurationAsync(invalidPath));
            await Assert.ThrowsAsync<ArgumentException>(() => client.Dashboards.SaveConfigurationAsync(emptyConfiguration.RootElement, invalidPath));
            await Assert.ThrowsAsync<ArgumentException>(() => client.Dashboards.DeleteConfigurationAsync(invalidPath));
        }
        Assert.Null(server.GetLastWebSocketCommand("lovelace/dashboards/create"));
        Assert.Null(server.GetLastWebSocketCommand("lovelace/config/save"));
        Assert.Null(server.GetLastWebSocketCommand("lovelace/config/delete"));
    }

    [Fact]
    public async Task DashboardResponsesRejectMissingRequiredIdentifiersAndFields()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        server.DashboardListResponseJson = "[{\"id\":\"\",\"url_path\":\"house-main\",\"title\":\"House\",\"mode\":\"storage\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());

        server.DashboardListResponseJson = "[{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"mode\":\"storage\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());

        server.DashboardListResponseJson = "[{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":false,\"require_admin\":0,\"mode\":\"storage\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());

        server.DashboardListResponseJson = "[{\"url_path\":\"yaml-home\",\"title\":\"YAML Home\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"yaml\",\"filename\":\"ui-lovelace.yaml\"}]";
        Assert.Empty(Assert.Single(await client.Dashboards.GetDashboardsAsync()).Id);

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\" \",\"title\":\"House\",\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateDashboardAsync(
            new HomeAssistantDashboardCreate { UrlPath = "house-main", Title = "House" }));

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Updated\",\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate { Title = "Updated" }));

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"yaml\",\"filename\":\"ui-lovelace.yaml\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateDashboardAsync(
            new HomeAssistantDashboardCreate { UrlPath = "house-main", Title = "House", ShowInSidebar = true }));

        server.DashboardResourceListResponseJson = "[{\"url\":\"/local/storage-card.js\",\"type\":\"module\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetResourcesAsync());

        server.LovelaceInfoResponseJson = "{\"resource_mode\":\"yaml\"}";
        server.DashboardResourceListResponseJson = "[{\"url\":\"/local/yaml-card.js\",\"type\":\"module\"}]";
        Assert.Empty(Assert.Single(await client.Dashboards.GetResourcesAsync()).Id);

        server.LovelaceInfoResponseJson = "{\"resource_mode\":\"storage\"}";
        server.DashboardResourceListResponseJson = "[{\"url\":\" \",\"type\":\"module\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetResourcesAsync());

        server.DashboardResourceMutationResponseJson = "{\"id\":\"\",\"url\":\"/local/card.js\",\"type\":\"module\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateResourceAsync(
            "/local/card.js",
            HomeAssistantDashboardResourceType.Module));

        server.DashboardResourceMutationResponseJson = "{\"id\":\" resource-1 \",\"url\":\"/local/card.js\",\"type\":\"module\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateResourceAsync(
            "/local/card.js",
            HomeAssistantDashboardResourceType.Module));

        server.FrontendPanelsResponseJson = "{\"lovelace\":{\"component_name\":\" \"}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.FrontendPanelsResponseJson = "{\"lovelace\":{\"component_name\":\" lovelace \"}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.FrontendPanelsResponseJson = "{\"lovelace\":{\"component_name\":\"lovelace\",\"show_in_sidebar\":true}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.FrontendPanelsResponseJson = "{\"lovelace\":{\"component_name\":\"lovelace\",\"show_in_sidebar\":true,\"require_admin\":0}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.LovelaceInfoResponseJson = "{}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetInfoAsync());
    }

    [Fact]
    public async Task DashboardResourcesRetryWhenResourceModeChangesDuringTheRead()
    {
        using var server = new TestHomeAssistantServer
        {
            DashboardResourceListResponseJson = "[{\"url\":\"/local/card.js\",\"type\":\"module\"}]"
        };
        server.LovelaceInfoResponses.Enqueue("{\"resource_mode\":\"storage\"}");
        server.LovelaceInfoResponses.Enqueue("{\"resource_mode\":\"yaml\"}");
        server.LovelaceInfoResponses.Enqueue("{\"resource_mode\":\"yaml\"}");
        server.LovelaceInfoResponses.Enqueue("{\"resource_mode\":\"yaml\"}");
        using var client = TestClientFactory.Create(server);

        var resource = Assert.Single(await client.Dashboards.GetResourcesAsync());

        Assert.Empty(resource.Id);
    }

    [Fact]
    public async Task DashboardResourcesFailWhenResourceModeNeverStabilizes()
    {
        using var server = new TestHomeAssistantServer();
        for (var index = 0; index < 3; index++)
        {
            server.LovelaceInfoResponses.Enqueue("{\"resource_mode\":\"storage\"}");
            server.LovelaceInfoResponses.Enqueue("{\"resource_mode\":\"yaml\"}");
        }
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantConnectionException>(() => client.Dashboards.GetResourcesAsync());
    }

    [Fact]
    public void AtomicExportsPreserveUnixDestinationPermissions()
    {
        if (OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "homeassistantx-permissions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "destination.bin");
        var temporary = Path.Combine(directory, "temporary.bin");
        try
        {
            File.WriteAllBytes(destination, new byte[] { 1 });
            File.WriteAllBytes(temporary, new byte[] { 2 });
            File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.SetUnixFileMode(
                temporary,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

            HomeAssistantAtomicFile.PreserveDestinationPermissions(destination, temporary);

            Assert.Equal(File.GetUnixFileMode(destination), File.GetUnixFileMode(temporary));

            File.SetUnixFileMode(
                temporary,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            HomeAssistantAtomicFile.PreserveDestinationPermissions(destination, temporary, useManagedApis: false);
            Assert.Equal(File.GetUnixFileMode(destination), File.GetUnixFileMode(temporary));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Linux", "X64", 24)]
    [InlineData("Linux", "Ppc64le", 24)]
    [InlineData("Linux", "S390x", 24)]
    [InlineData("Linux", "Arm64", 16)]
    [InlineData("Linux", "RiscV64", 16)]
    [InlineData("OSX", "Arm64", 4)]
    public void AtomicExportsSelectTheNativeStatModeOffsetByAbi(
        string operatingSystem,
        string architecture,
        int expectedOffset)
    {
        Assert.Equal(expectedOffset, HomeAssistantAtomicFile.UnixModeOffset(operatingSystem, architecture));
    }

    [Fact]
    public void AtomicExportsRejectUnknownNativeStatLayouts()
    {
        Assert.Throws<PlatformNotSupportedException>(() => HomeAssistantAtomicFile.UnixModeOffset("Linux", "FutureCpu"));
    }

    [Fact]
    public void AtomicExportsDoNotCommitAfterCancellation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "homeassistantx-canceled-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "destination.bin");
        var temporary = Path.Combine(directory, "temporary.bin");
        try
        {
            File.WriteAllBytes(destination, new byte[] { 1 });
            File.WriteAllBytes(temporary, new byte[] { 2 });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() => HomeAssistantAtomicFile.CommitTemporaryFile(
                temporary,
                destination,
                overwrite: true,
                cancellation.Token));

            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(destination));
            Assert.True(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("House-main")]
    [InlineData("house--main")]
    [InlineData(" house-main ")]
    public async Task DashboardResponsesRejectNonCanonicalRoutes(string urlPath)
    {
        using var server = new TestHomeAssistantServer
        {
            DashboardListResponseJson = "[{\"id\":\"house-main\",\"url_path\":\"" + urlPath + "\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());
    }

    [Theory]
    [InlineData("[{\"id\":\"one\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"},{\"id\":\"two\",\"url_path\":\"house-main\",\"title\":\"House 2\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}]")]
    [InlineData("[{\"id\":\"same\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"},{\"id\":\"same\",\"url_path\":\"garden-main\",\"title\":\"Garden\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}]")]
    public async Task DashboardResponsesRejectDuplicateRoutesAndStorageIdentifiers(string response)
    {
        using var server = new TestHomeAssistantServer { DashboardListResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());
    }

    [Theory]
    [InlineData("[{\"id\":\" same \",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}]")]
    [InlineData("[{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"Storage\"}]")]
    [InlineData("[{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"future\"}]")]
    [InlineData("[{\"url_path\":\"yaml-home\",\"title\":\"YAML\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"yaml\",\"filename\":\" ui-lovelace.yaml \"}]")]
    public async Task DashboardResponsesRejectNonCanonicalSelectorsAndModes(string response)
    {
        using var server = new TestHomeAssistantServer { DashboardListResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());
    }

    [Fact]
    public async Task DashboardResponsesRejectNoncanonicalIconsAndResourceFields()
    {
        using var server = new TestHomeAssistantServer
        {
            DashboardListResponseJson =
                "[{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"icon\":\"home\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());

        server.DashboardMutationResponseJson =
            "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Updated\",\"icon\":\"home\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate { Title = "Updated" }));

        foreach (var response in new[]
        {
            "[{\"id\":\"resource-1\",\"url\":\" /local/card.js \",\"type\":\"module\"}]",
            "[{\"id\":\"resource-1\",\"url\":\"/local/card.js\",\"type\":\" module \"}]"
        })
        {
            server.DashboardResourceListResponseJson = response;
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetResourcesAsync());
        }
    }

    [Theory]
    [InlineData("{\" House \":{\"component_name\":\"lovelace\"}}")]
    [InlineData("{\"house\":{\"component_name\":\"lovelace\"},\"house\":{\"component_name\":\"lovelace\"}}")]
    public async Task PanelResponsesRejectNonCanonicalAndDuplicateRoutes(string response)
    {
        using var server = new TestHomeAssistantServer { FrontendPanelsResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());
    }

    [Fact]
    public async Task PanelResponsesPreserveValidNonDashboardRoutes()
    {
        using var server = new TestHomeAssistantServer
        {
            FrontendPanelsResponseJson = "{\"config/integrations\":{\"component_name\":\"config\",\"default_visible\":false,\"require_admin\":true,\"show_in_sidebar\":true}}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Equal("config/integrations", Assert.Single(await client.Dashboards.GetPanelsAsync()).UrlPath);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\" \"")]
    public async Task PanelResponsesRejectPresentInvalidEmbeddedRoutes(string embeddedRoute)
    {
        using var server = new TestHomeAssistantServer
        {
            FrontendPanelsResponseJson =
                "{\"lovelace\":{\"url_path\":" + embeddedRoute
                + ",\"component_name\":\"lovelace\",\"default_visible\":true,\"require_admin\":false,\"show_in_sidebar\":true}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());
    }

    [Theory]
    [InlineData("home")]
    [InlineData(" mdi:home ")]
    public async Task PanelResponsesRejectNoncanonicalIcons(string icon)
    {
        using var server = new TestHomeAssistantServer
        {
            FrontendPanelsResponseJson =
                "{\"lovelace\":{\"component_name\":\"lovelace\",\"icon\":\"" + icon
                + "\",\"default_visible\":true,\"require_admin\":false,\"show_in_sidebar\":true}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("true")]
    public async Task DashboardConfigurationReadsRejectNonObjectResponses(string response)
    {
        using var server = new TestHomeAssistantServer { LovelaceConfigurationResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetConfigurationAsync());
    }

    [Fact]
    public async Task DashboardConfigurationSaveHonorsCancellationBeforeInspectingOrCopyingJson()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        JsonElement configuration;
        using (var document = JsonDocument.Parse("{}"))
        {
            configuration = document.RootElement;
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Dashboards.SaveConfigurationAsync(configuration, cancellationToken: cancellation.Token));

        Assert.Null(server.GetLastWebSocketCommand("lovelace/config/save"));
    }

    [Fact]
    public async Task DashboardConfigurationSaveRejectsDuplicatePropertiesBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var configuration = JsonDocument.Parse("{\"views\":[],\"nested\":{\"cards\":[],\"cards\":[{}]}}");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Dashboards.SaveConfigurationAsync(configuration.RootElement));

        Assert.Null(server.GetLastWebSocketCommand("lovelace/config/save"));
    }

    [Theory]
    [InlineData("[{\"id\":\" resource-1 \",\"url\":\"/local/card.js\",\"type\":\"module\"}]")]
    [InlineData("[{\"id\":\"resource-1\",\"url\":\"/local/a.js\",\"type\":\"module\"},{\"id\":\"resource-1\",\"url\":\"/local/b.js\",\"type\":\"module\"}]")]
    public async Task StorageResourceResponsesRejectNonCanonicalAndDuplicateIdentifiers(string response)
    {
        using var server = new TestHomeAssistantServer { DashboardResourceListResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetResourcesAsync());
    }

    [Fact]
    public async Task DashboardResponsesCorrelatePanelAndMutationIdentities()
    {
        using var server = new TestHomeAssistantServer
        {
            FrontendPanelsResponseJson = "{\"lovelace\":{\"url_path\":\"other\",\"component_name\":\"lovelace\"}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.DashboardMutationResponseJson = "{\"id\":\"other\",\"url_path\":\"house-main\",\"title\":\"House\",\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate { Title = "Updated" }));

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"other\",\"title\":\"House\",\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateDashboardAsync(
            new HomeAssistantDashboardCreate { UrlPath = "house-main", Title = "House" }));

        server.DashboardResourceMutationResponseJson = "{\"id\":\"resource-other\",\"url\":\"/local/card.js\",\"type\":\"module\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateResourceAsync(
            "resource-1",
            url: "/local/card-v2.js"));

        server.DashboardResourceMutationResponseJson = "{\"id\":\"resource-2\",\"url\":\"/local/other.js\",\"type\":\"css\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateResourceAsync(
            "/local/card.js",
            HomeAssistantDashboardResourceType.Module));
    }

    [Fact]
    public async Task DashboardMutationResponsesRejectDuplicatePropertiesBeforeProjection()
    {
        using var server = new TestHomeAssistantServer
        {
            DashboardMutationResponseJson =
                "{\"id\":\"other\",\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate { Title = "House" }));

        server.DashboardResourceMutationResponseJson =
            "{\"id\":\"other\",\"id\":\"resource-1\",\"url\":\"/local/card.js\",\"type\":\"module\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateResourceAsync(
            "resource-1",
            url: "/local/card.js"));
    }

    [Fact]
    public async Task DashboardReadsRejectDuplicatePropertiesBeforeProjection()
    {
        using var server = new TestHomeAssistantServer
        {
            FrontendPanelsResponseJson =
                "{\"lovelace\":{\"component_name\":\"lovelace\",\"component_name\":\"other\",\"default_visible\":true,\"require_admin\":false,\"show_in_sidebar\":true}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.LovelaceInfoResponseJson = "{\"resource_mode\":\"storage\",\"resource_mode\":\"yaml\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetInfoAsync());

        server.DashboardListResponseJson =
            "[{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"title\":\"Other\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetDashboardsAsync());

        server.LovelaceInfoResponseJson = "{\"resource_mode\":\"storage\"}";
        server.DashboardResourceListResponseJson =
            "[{\"id\":\"resource-1\",\"url\":\"/local/card.js\",\"url\":\"/local/other.js\",\"type\":\"module\"}]";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetResourcesAsync());
    }

    [Fact]
    public async Task DashboardMutationsCorrelateEverySuppliedField()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Old\",\"icon\":\"mdi:old\",\"show_in_sidebar\":false,\"require_admin\":false,\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate
            {
                Title = "Updated",
                Icon = "mdi:home",
                ShowInSidebar = true,
                RequireAdmin = true
            }));

        foreach (var response in new[]
        {
            "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Updated\",\"icon\":\"mdi:old\",\"show_in_sidebar\":true,\"require_admin\":true,\"mode\":\"storage\"}",
            "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Updated\",\"icon\":\"mdi:home\",\"show_in_sidebar\":false,\"require_admin\":true,\"mode\":\"storage\"}",
            "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Updated\",\"icon\":\"mdi:home\",\"show_in_sidebar\":true,\"require_admin\":false,\"mode\":\"storage\"}"
        })
        {
            server.DashboardMutationResponseJson = response;
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
                "house-main",
                new HomeAssistantDashboardUpdate
                {
                    Title = "Updated",
                    Icon = "mdi:home",
                    ShowInSidebar = true,
                    RequireAdmin = true
                }));
        }

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"icon\":\"mdi:home\",\"show_in_sidebar\":false,\"require_admin\":false,\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate { RemoveIcon = true }));

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"Wrong\",\"icon\":\"mdi:home\",\"show_in_sidebar\":false,\"require_admin\":true,\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateDashboardAsync(
            new HomeAssistantDashboardCreate
            {
                UrlPath = "house-main",
                Title = "House",
                Icon = "mdi:home",
                ShowInSidebar = true,
                RequireAdmin = false
            }));

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\"house-main\",\"title\":\"House\",\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.UpdateDashboardAsync(
            "house-main",
            new HomeAssistantDashboardUpdate { ShowInSidebar = false }));
    }

    [Fact]
    public async Task AutomationConfigurationIsSeparateFromRuntimeExecution()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"automation.morning\",\"state\":\"on\",\"attributes\":{\"friendly_name\":\"Morning\",\"last_triggered\":\"2026-08-26T06:00:00Z\",\"mode\":\"single\",\"current\":0}}]");
        using var client = TestClientFactory.Create(server);
        Assert.True(Assert.Single(await client.Automations.GetAsync()).IsEnabled);
        await Assert.ThrowsAsync<ArgumentException>(() => client.Automations.GetAsync("automation.morning.extra"));
        var definition = await client.Automations.GetConfigurationAsync("morning-routine");
        Assert.True(definition.Definition.GetProperty("future_automation").GetBoolean());
        using var updated = JsonDocument.Parse("{\"alias\":\"Morning\",\"triggers\":[],\"actions\":[]}");
        await client.Automations.SaveConfigurationAsync("morning-routine", updated.RootElement);
        Assert.Contains("\"alias\":\"Morning\"", server.LastRequestBody);
        await client.Automations.TriggerAsync(HomeAssistantTarget.ForEntity("automation.morning"), skipConditions: false);
        using var runtime = JsonDocument.Parse(server.LastServiceCallBody!);
        Assert.Equal("trigger", runtime.RootElement.GetProperty("service").GetString());
        Assert.False(runtime.RootElement.GetProperty("service_data").GetProperty("skip_condition").GetBoolean());
        await client.Automations.DeleteConfigurationAsync("morning-routine");
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("off", false)]
    [InlineData("unavailable", null)]
    [InlineData("unknown", null)]
    public async Task AutomationEnablementRepresentsOnlyNativeOnAndOffStates(string state, bool? expected)
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"automation.morning\",\"state\":\"" + state + "\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        Assert.Equal(expected, Assert.Single(await client.Automations.GetAsync()).IsEnabled);
    }

    [Theory]
    [InlineData("{\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":\"other-routine\",\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":42,\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":\"other-routine\",\"id\":\"morning-routine\",\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":\"morning-routine\",\"alias\":\"First\",\"alias\":\"Second\"}")]
    [InlineData("{\"id\":\"morning-routine\",\"actions\":[{\"service\":\"light.turn_on\",\"service\":\"light.turn_off\"}]}")]
    public async Task AutomationConfigurationCorrelatesReturnedIdentifier(string response)
    {
        using var server = new TestHomeAssistantServer { AutomationConfigurationResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Automations.GetConfigurationAsync("morning-routine"));
    }

    [Theory]
    [InlineData("camera.front")]
    [InlineData("automation.morning")]
    public async Task TypedCameraAndAutomationReadsRejectMissingStateValues(string entityId)
    {
        using var server = new TestHomeAssistantServer
        {
            ExactStateResponseJson = "{\"entity_id\":\"" + entityId + "\",\"state\":null,\"attributes\":{}}"
        };
        using var client = TestClientFactory.Create(server);

        if (entityId.StartsWith("camera", StringComparison.Ordinal))
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Cameras.GetAsync(entityId));
        else
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Automations.GetAsync(entityId));
    }

    [Fact]
    public async Task AutomationTriggerRejectsWrongDomainEntityTargetsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Automations.TriggerAsync(
            HomeAssistantTarget.ForEntity("script.morning")));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Automations.TriggerAsync(
            HomeAssistantTarget.ForEntity("automation.morning.extra")));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Automations.TriggerAsync(
            HomeAssistantTarget.Create()));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task AutomationTargetNormalizationHonorsPreCancellation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var target = new HomeAssistantTarget
        {
            EntityIds = new[] { "automation.morning", "not-an-entity" }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Automations.TriggerAsync(target, cancellationToken: cancellation.Token));

        Assert.Null(server.LastServiceCallBody);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AutomationConfigurationIdsFailBeforeDispatch(string automationId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var definition = JsonDocument.Parse("{\"alias\":\"Morning\",\"triggers\":[],\"actions\":[]}");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Automations.SaveConfigurationAsync(automationId, definition.RootElement));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Automations.DeleteConfigurationAsync(automationId));
        Assert.Null(server.LastRequestBody);
    }

    [Theory]
    [InlineData("{\"id\":\"other-routine\",\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":42,\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":\" morning-routine \",\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":\"morning-routine\",\"id\":\"morning-routine\",\"alias\":\"Morning\"}")]
    [InlineData("{\"id\":\"morning-routine\",\"alias\":\"First\",\"alias\":\"Second\"}")]
    [InlineData("{\"id\":\"morning-routine\",\"actions\":[{\"service\":\"light.turn_on\",\"service\":\"light.turn_off\"}]}")]
    public async Task AutomationConfigurationSaveRejectsAmbiguousDefinitionsBeforeDispatch(string json)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var definition = JsonDocument.Parse(json);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Automations.SaveConfigurationAsync("morning-routine", definition.RootElement));

        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public void AutomationConfigurationIdentityScanHonorsCancellationForEmptyDefinitions()
    {
        using var definition = JsonDocument.Parse("{}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantAutomationIdentifier.ValidateDefinitionForSave(
                "morning-routine",
                definition.RootElement,
                "definition",
                cancellation.Token));
    }

    private static IEnumerable<string> CancelAfterFirstStreamType(CancellationTokenSource cancellation)
    {
        yield return "hls";
        cancellation.Cancel();
        yield return "mjpeg";
    }
}
#endif
