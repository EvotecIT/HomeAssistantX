using HomeAssistantX;
using HomeAssistantX.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var address = Environment.GetEnvironmentVariable("HOMEASSISTANTX_URL");
var token = Environment.GetEnvironmentVariable("HOMEASSISTANTX_TOKEN");
if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri)
    || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
    || string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Set HOMEASSISTANTX_URL and HOMEASSISTANTX_TOKEN before starting the MCP server.");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Services.AddSingleton(HomeAssistantClient.Create(baseUri, token));
builder.Services.AddSingleton(new HomeAssistantMcpAccess(
    string.Equals(Environment.GetEnvironmentVariable("HOMEASSISTANTX_MCP_ALLOW_CHANGES"),
        "true", StringComparison.OrdinalIgnoreCase)));
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<HomeAssistantMcpTools>();

await builder.Build().RunAsync();
return 0;
