# HomeAssistantX MCP server

The MCP server lets an AI client inspect a Home Assistant installation, find
entities and actions, diagnose common issues, and prepare automations through
the same HomeAssistantX library used by C# and PowerShell. It runs locally over
stdio. The host targets .NET 10; the shared library still supports `net472`,
`netstandard2.0`, and `net10.0`.

## Start from source

Build `HomeAssistantX.slnx` with the .NET 10 SDK. Give the MCP process the URL
and a Home Assistant long-lived access token through its environment:

```powershell
$env:HOMEASSISTANTX_URL = 'http://homeassistant.local:8123/'
$env:HOMEASSISTANTX_TOKEN = '<token from your local secret store>'
dotnet run --project HomeAssistantX.Mcp --configuration Release
```

Configure your MCP client to start the built `HomeAssistantX.Mcp` executable
with those environment variables. Keep the token in your local secret store or
the MCP client's private configuration; do not commit it to a project file.
The server writes protocol messages to stdout and diagnostics to stderr.

The first public NuGet and PowerShell Gallery releases are pending. The MCP
host is currently built from source. Once `HomeAssistantX.Mcp` is published as a
.NET tool, users can install it with `dotnet tool install --global HomeAssistantX.Mcp`
and configure their MCP client to run `homeassistantx-mcp`.

## Ask for a diagnosis

Use `get_home_overview` to see installation capabilities and counts, then
`get_home_issues`, `get_home_updates`, and `get_home_errors` for details. An
unavailable integration API can fail its own tool call; the other tools remain
usable. The overview returns a null issue count when Repairs is not installed.
The issues tool lists active, nonignored issues. Log entries may contain private
information from the home, so review them before sharing an AI transcript.

Use `find_home_entities` to resolve a room, device, or entity and
`get_home_actions` to inspect the actions and fields the connected installation
actually offers. These tools also work with custom integrations.

## Prepare an automation

1. Find the entities and action fields the automation will use.
2. Write a complete Home Assistant automation JSON definition with `triggers`
   and `actions` (or the singular `trigger` and `action` forms).
3. Call `validate_home_automation_draft` and inspect Home Assistant's trigger,
   condition, and action validation response. This does not save the draft.
4. For a new automation, choose a configuration ID and use revision `new`.
   For an existing automation, call `get_home_automation_definition` and use
   its returned `Revision` when saving your edited definition.
5. Start the server with `HOMEASSISTANTX_MCP_ALLOW_CHANGES=true` to enable
   `save_home_automation_definition`. Review the draft and target before
   calling the save tool.

The revision check catches an edit made between reading and submitting a
definition. Home Assistant's configuration API does not provide an atomic
compare-and-swap, so another edit made during the save can still race. The
save tool applies to administrator-managed automation definitions; it does not
edit YAML files or install integrations. Validation checks the individual
trigger, condition, and action fragments and is not a guarantee that Home
Assistant will accept the complete definition.

Writes are disabled by default. The opt-in affects only this local MCP
process. Home Assistant must also authorize the access token for automation
configuration changes.

## C# and PowerShell equivalents

Applications can use `HomeAssistantClient.Inventory`, `.Services`,
`.Operations`, `.System.ValidateConfigAsync`, and `.Automations` directly. The
PowerShell module exposes `Get-HomeAssistantEntity`,
`Get-HomeAssistantAction`, `Get-HomeAssistantIssue`, `Get-HomeAssistantUpdate`,
`Get-HomeAssistantLog`, `Get-HomeAssistantAutomation`, and
`Set-HomeAssistantAutomation`. PowerShell's write command supports `-WhatIf`
and `-Confirm`; it does not use the MCP revision guard.
