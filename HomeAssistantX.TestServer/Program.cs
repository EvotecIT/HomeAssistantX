using HomeAssistantX.Tests.Infrastructure;

using var server = new TestHomeAssistantServer
{
    SendStateChangeBeforeSnapshot = true,
    RecorderMetadataResponseJson =
        "[{\"statistic_id\":\"sensor.grid_energy\",\"source\":\"recorder\",\"name\":\"Grid energy\",\"unit_of_measurement\":\"Wh\",\"statistics_unit_of_measurement\":\"kWh\",\"unit_class\":\"energy\",\"has_mean\":false,\"has_sum\":true}]"
};
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
        case "SET_LABEL_REGISTRY_UNAVAILABLE":
            server.LabelRegistryErrorCode = "unauthorized";
            Console.WriteLine("LABEL_REGISTRY_UNAVAILABLE");
            break;
        case "SET_LABEL_REGISTRY_AVAILABLE":
            server.LabelRegistryErrorCode = null;
            Console.WriteLine("LABEL_REGISTRY_AVAILABLE");
            break;
        case "SET_CASE_DISTINCT_LABELS":
            server.LabelRegistryResponseJson = "[{\"label_id\":\"security\",\"name\":\"Lower security\"},{\"label_id\":\"Security\",\"name\":\"Upper security\"}]";
            Console.WriteLine("CASE_DISTINCT_LABELS_SET");
            break;
        case "SET_DEFAULT_LABELS":
            server.LabelRegistryResponseJson = "[{\"label_id\":\"security\",\"name\":\"Safety\",\"color\":\"red\",\"description\":\"Safety devices\",\"icon\":\"mdi:shield\",\"created_at\":1787731200,\"modified_at\":1787731300},{\"label_id\":\"security-name\",\"name\":\"Security\",\"color\":null,\"description\":\"Identifier collision fixture\",\"icon\":null,\"created_at\":1787731200,\"modified_at\":1787731300}]";
            Console.WriteLine("DEFAULT_LABELS_SET");
            break;
        case "SET_STABLE_CONTROL_STATES":
            server.SetStates("[" + TestHomeAssistantServer.KitchenTemperatureStateJson + "," + TestHomeAssistantServer.KitchenLightOffStateJson + "," + TestHomeAssistantServer.StableControlStatesJson + "]");
            Console.WriteLine("STABLE_CONTROL_STATES_SET");
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
        case "GET_LAST_LABEL_LIST":
            Console.WriteLine(server.GetLastWebSocketCommand("config/label_registry/list") ?? "LABEL_LIST_NONE");
            break;
        case "CLEAR_LAST_LABEL_LIST":
            server.ClearLastWebSocketCommand("config/label_registry/list");
            Console.WriteLine("LABEL_LIST_CLEARED");
            break;
        case "GET_LAST_CATEGORY_LIST":
            Console.WriteLine(server.GetLastWebSocketCommand("config/category_registry/list") ?? "CATEGORY_LIST_NONE");
            break;
        case "CLEAR_LAST_CATEGORY_LIST":
            server.ClearLastWebSocketCommand("config/category_registry/list");
            Console.WriteLine("CATEGORY_LIST_CLEARED");
            break;
        case "GET_LAST_RECORDER_IMPORT":
            Console.WriteLine(server.GetLastWebSocketCommand("recorder/import_statistics") ?? "RECORDER_IMPORT_NONE");
            break;
        case "GET_LAST_RECORDER_METADATA_UPDATE":
            Console.WriteLine(server.GetLastWebSocketCommand("recorder/update_statistics_metadata") ?? "RECORDER_METADATA_NONE");
            break;
        case "GET_LAST_RECORDER_METADATA_LIST":
            Console.WriteLine(server.GetLastWebSocketCommand("recorder/get_statistics_metadata") ?? "RECORDER_METADATA_LIST_NONE");
            break;
        case "CLEAR_LAST_RECORDER_METADATA_LIST":
            server.ClearLastWebSocketCommand("recorder/get_statistics_metadata");
            Console.WriteLine("RECORDER_METADATA_LIST_CLEARED");
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
