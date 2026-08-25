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
var states = await client.States.GetAllAsync();
await client.WebSocket.ConnectAsync();
await client.WebSocket.PingAsync();

Console.WriteLine($"{api.Message} Home Assistant {configuration.Version}; {states.Count} entity states; WebSocket healthy.");
return 0;
