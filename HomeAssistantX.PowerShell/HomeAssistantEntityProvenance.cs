using System.Runtime.CompilerServices;
using HomeAssistantX.Inventory;

namespace HomeAssistantX.PowerShell;

internal static class HomeAssistantEntityProvenance
{
    private static readonly ConditionalWeakTable<HomeAssistantEntityInfo, HomeAssistantConnection> Connections = new();

    public static void Set(HomeAssistantEntityInfo entity, HomeAssistantConnection connection)
    {
        Connections.Remove(entity);
        Connections.Add(entity, connection);
    }

    public static bool TryGet(HomeAssistantEntityInfo entity, out HomeAssistantConnection connection)
    {
        return Connections.TryGetValue(entity, out connection!);
    }
}
