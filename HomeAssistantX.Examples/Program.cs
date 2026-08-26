using HomeAssistantX;

var baseUrl = Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL");
var accessToken = Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN");
if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) || string.IsNullOrWhiteSpace(accessToken))
{
    Console.Error.WriteLine("Set HOME_ASSISTANT_URL and HOME_ASSISTANT_TOKEN before running this sample.");
    return 1;
}

using var client = HomeAssistantClient.Create(baseUri, accessToken);
var api = await client.Rest.CheckApiAsync();
var configuration = await client.Rest.GetConfigurationAsync();
var inventory = await client.Inventory.GetSnapshotAsync();
var availableLights = inventory.Entities
    .Where(entity => entity.Domain == "light" && entity.IsAvailable)
    .ToArray();

Console.WriteLine($"{api.Message} Home Assistant {configuration.Version}");
Console.WriteLine($"{inventory.Floors.Count} floors, {inventory.Areas.Count} areas, {inventory.Devices.Count} devices, {inventory.Entities.Count} entities");
Console.WriteLine($"{availableLights.Length} available lights and {inventory.Actions.Count} registered actions");
return 0;
