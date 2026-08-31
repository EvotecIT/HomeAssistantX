using HomeAssistantX.Tests.Infrastructure;

using var server = new TestHomeAssistantServer { SendStateChangeBeforeSnapshot = true };
Console.WriteLine("READY " + server.BaseUri.AbsoluteUri);
while (await Console.In.ReadLineAsync() is { } command)
{
    switch (command.TrimStart('\uFEFF'))
    {
        case "SET_RECONNECT_STATES":
            server.SetStates("[" + TestHomeAssistantServer.KitchenLightOnStateJson + "]");
            Console.WriteLine("STATES_SET");
            break;
        case "SET_REMOTE_STATES":
            server.SetStates("[" + TestHomeAssistantServer.LivingRoomRemoteStateJson + "]");
            Console.WriteLine("REMOTE_STATES_SET");
            break;
        case "SET_DEFAULT_STATES":
            server.SetStates(TestHomeAssistantServer.DefaultStatesJson);
            Console.WriteLine("DEFAULT_STATES_SET");
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
        case "PING":
            Console.WriteLine("PONG");
            break;
        case "GET_LAST_SERVICE_CALL":
            Console.WriteLine(server.LastServiceCallBody ?? "SERVICE_CALL_NONE");
            break;
        case "CLEAR_LAST_SERVICE_CALL":
            server.ClearLastServiceCall();
            Console.WriteLine("SERVICE_CALL_CLEARED");
            break;
        case "GET_LAST_SUPERVISOR_COMMAND":
            Console.WriteLine(server.GetLastWebSocketCommand("supervisor/api") ?? "SUPERVISOR_COMMAND_NONE");
            break;
        case "GET_LAST_EVENT_SUBSCRIPTION":
            Console.WriteLine(server.GetLastWebSocketCommand("subscribe_events") ?? "EVENT_SUBSCRIPTION_NONE");
            break;
        case "GET_UNSUBSCRIBE_COUNT":
            Console.WriteLine(server.UnsubscribeCommandCount);
            break;
        case "PUBLISH_STATE_CHANGE":
            var recipients = 0;
            for (var index = 0; index < 3; index++)
            {
                recipients = await server.PublishStateChangeAsync(
                    "light.kitchen",
                    TestHomeAssistantServer.KitchenLightOffStateJson,
                    TestHomeAssistantServer.KitchenLightOnStateJson);
            }
            Console.WriteLine("STATE_CHANGE_PUBLISHED " + recipients);
            break;
        case "CLEAR_LAST_SUPERVISOR_COMMAND":
            server.ClearLastWebSocketCommand("supervisor/api");
            Console.WriteLine("SUPERVISOR_COMMAND_CLEARED");
            break;
        case "EXIT":
            return 0;
        default:
            Console.WriteLine("UNKNOWN");
            break;
    }
}

return 0;
