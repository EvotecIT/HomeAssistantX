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
        case "PAUSE_NEXT_SUBSCRIPTION":
            server.PauseNextSubscription();
            Console.WriteLine("PAUSE_CONFIGURED");
            break;
        case "WAIT_FOR_PAUSED_SUBSCRIPTION":
            await server.WaitForPausedSubscriptionAsync();
            Console.WriteLine("SUBSCRIPTION_PAUSED");
            break;
        case "RELEASE_PAUSED_SUBSCRIPTION":
            server.ReleasePausedSubscription();
            Console.WriteLine("SUBSCRIPTION_RELEASED");
            break;
        case "WAIT_FOR_UNSUBSCRIBE":
            await server.WaitForUnsubscribeAsync();
            Console.WriteLine("UNSUBSCRIBED");
            break;
        case "EXIT":
            return 0;
        default:
            Console.WriteLine("UNKNOWN");
            break;
    }
}

return 0;
