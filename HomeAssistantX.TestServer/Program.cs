using HomeAssistantX.Tests.Infrastructure;

using var server = new TestHomeAssistantServer { SendStateChangeBeforeSnapshot = true };
Console.WriteLine("READY " + server.BaseUri.AbsoluteUri);
while (await Console.In.ReadLineAsync() is { } command)
{
    switch (command)
    {
        case "SET_RECONNECT_STATES":
            server.SetStates("[" + TestHomeAssistantServer.KitchenLightOnStateJson + "]");
            Console.WriteLine("STATES_SET");
            break;
        case "DROP":
            await server.DropWebSocketsAsync();
            Console.WriteLine("DROPPED");
            break;
        case "EXIT":
            return 0;
        default:
            Console.WriteLine("UNKNOWN");
            break;
    }
}

return 0;
