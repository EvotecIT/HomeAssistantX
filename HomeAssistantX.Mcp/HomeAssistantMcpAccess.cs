namespace HomeAssistantX.Mcp;

public sealed record HomeAssistantMcpAccess(bool AllowChanges)
{
    internal void RequireChanges()
    {
        if (!AllowChanges)
        {
            throw new InvalidOperationException(
                "Automation changes are disabled. Restart the MCP server with HOMEASSISTANTX_MCP_ALLOW_CHANGES=true to enable them.");
        }
    }
}
