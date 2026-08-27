#if NET10_0
using System.Text;
using System.Text.Json;
using HomeAssistantX.Automations;
using HomeAssistantX.Cameras;
using HomeAssistantX.Dashboards;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Media;
using HomeAssistantX.Models;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class CamerasDashboardsAutomationContractTests
{
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

    [Theory]
    [InlineData("{\"title\":\"Music\",\"media_content_type\":\"library\",\"children\":[]}")]
    [InlineData("{\"title\":\"Music\",\"media_content_id\":\"media-source://media_source\",\"children\":[{\"title\":\"Child\",\"media_content_id\":\"child\",\"children\":[]}]}")]
    public async Task MediaBrowseRejectsItemsWithoutContentIdentity(string response)
    {
        using var server = new TestHomeAssistantServer { MediaBrowseResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Media.BrowseSourcesAsync());
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

    [Fact]
    public async Task DashboardsExposeReadModelsAndGuardStorageMutations()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var panel = Assert.Single(await client.Dashboards.GetPanelsAsync());
        Assert.Equal("lovelace", panel.UrlPath);
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

        server.DashboardListResponseJson = "[{\"url_path\":\"yaml-home\",\"title\":\"YAML Home\",\"mode\":\"yaml\",\"filename\":\"ui-lovelace.yaml\"}]";
        Assert.Empty(Assert.Single(await client.Dashboards.GetDashboardsAsync()).Id);

        server.DashboardMutationResponseJson = "{\"id\":\"house-main\",\"url_path\":\" \",\"title\":\"House\",\"mode\":\"storage\"}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.CreateDashboardAsync(
            new HomeAssistantDashboardCreate { UrlPath = "house-main", Title = "House" }));

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

        server.FrontendPanelsResponseJson = "{\"lovelace\":{\"component_name\":\" \"}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetPanelsAsync());

        server.LovelaceInfoResponseJson = "{}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Dashboards.GetInfoAsync());
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
}
#endif
