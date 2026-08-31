---
Module Name: HomeAssistantX
Module Guid: 9c949e39-7bcb-41a2-ab01-ca4e6fe1dc27
Download Help Link: https://github.com/EvotecIT/HomeAssistantX
Help Version: 0.1.0
Locale: en-US
---
# HomeAssistantX Module
## Description
Task-oriented PowerShell access to Home Assistant Core and Supervisor through HomeAssistantX.

## HomeAssistantX Cmdlets
### [Connect-HomeAssistant](Connect-HomeAssistant.md)
Creates, verifies, and optionally stores the runspace's default Home Assistant connection.

### [Disconnect-HomeAssistant](Disconnect-HomeAssistant.md)
Closes the supplied connection or the current runspace default.

### [Export-HomeAssistantDiagnostic](Export-HomeAssistantDiagnostic.md)
Downloads Home Assistant-redacted diagnostics for a configuration entry or one device.

### [Get-HomeAssistantAction](Get-HomeAssistantAction.md)
Lists Home Assistant actions and their runtime-provided field descriptions.

### [Get-HomeAssistantApp](Get-HomeAssistantApp.md)
Gets installed Supervisor-managed Home Assistant apps.

### [Get-HomeAssistantArea](Get-HomeAssistantArea.md)
Lists Home Assistant areas (rooms), optionally within a floor.

### [Get-HomeAssistantBackup](Get-HomeAssistantBackup.md)
Gets backups from a Supervisor-managed Home Assistant installation.

### [Get-HomeAssistantCalendar](Get-HomeAssistantCalendar.md)
Lists Home Assistant calendar entities.

### [Get-HomeAssistantCalendarEvent](Get-HomeAssistantCalendarEvent.md)
Gets events from one Home Assistant calendar over an explicit time range.

### [Get-HomeAssistantCategory](Get-HomeAssistantCategory.md)
Lists Home Assistant categories within an explicit registry scope.

### [Get-HomeAssistantConnection](Get-HomeAssistantConnection.md)
Gets the default Home Assistant connection for the current PowerShell runspace.

### [Get-HomeAssistantDevice](Get-HomeAssistantDevice.md)
Lists Home Assistant devices, optionally filtered by area or floor.

### [Get-HomeAssistantEntity](Get-HomeAssistantEntity.md)
Gets joined entities by name, identifier, domain, device, area, or floor.

### [Get-HomeAssistantFloor](Get-HomeAssistantFloor.md)
Lists Home Assistant floors with their joined areas, devices, and entities.

### [Get-HomeAssistantHistory](Get-HomeAssistantHistory.md)
Gets recorder history for one or more entity identifiers.

### [Get-HomeAssistantInfo](Get-HomeAssistantInfo.md)
Gets Core configuration, discovered capabilities, system health, or Supervisor information.

### [Get-HomeAssistantIntegration](Get-HomeAssistantIntegration.md)
Gets Home Assistant configuration entries by identifier, domain, or all integrations.

### [Get-HomeAssistantIssue](Get-HomeAssistantIssue.md)
Gets Core repairs issues or Supervisor resolution issues.

### [Get-HomeAssistantJob](Get-HomeAssistantJob.md)
Gets all Supervisor jobs or one job by identifier.

### [Get-HomeAssistantLabel](Get-HomeAssistantLabel.md)
Lists Home Assistant labels, optionally selecting one by name or ID.

### [Get-HomeAssistantLog](Get-HomeAssistantLog.md)
Gets structured system-log entries or bounded Core, Supervisor, host, and app log lines.

### [Get-HomeAssistantNotification](Get-HomeAssistantNotification.md)
Gets persistent notifications currently stored by Home Assistant.

### [Get-HomeAssistantTrace](Get-HomeAssistantTrace.md)
Gets automation or script trace summaries, or one complete trace run.

### [Get-HomeAssistantUpdate](Get-HomeAssistantUpdate.md)
Gets update entities or Supervisor component and app updates.

### [Install-HomeAssistantUpdate](Install-HomeAssistantUpdate.md)
Installs an update entity or a Supervisor-managed Core, OS, Supervisor, or app update.

### [Invoke-HomeAssistantAction](Invoke-HomeAssistantAction.md)
Invokes any Home Assistant action with one target-oriented set of parameters.

### [Invoke-HomeAssistantApp](Invoke-HomeAssistantApp.md)
Runs one explicit lifecycle operation for a Supervisor-managed Home Assistant app.

### [Invoke-HomeAssistantRemote](Invoke-HomeAssistantRemote.md)
Controls a Home Assistant remote, including sending, learning, and deleting commands.

### [New-HomeAssistantBackup](New-HomeAssistantBackup.md)
Creates a full Supervisor backup with optional compression, location, and database exclusion.

### [Receive-HomeAssistantCalendarEvent](Receive-HomeAssistantCalendarEvent.md)
Streams refreshed event lists for one calendar without polling.

### [Receive-HomeAssistantEvent](Receive-HomeAssistantEvent.md)
Streams Home Assistant events without polling until canceled.

### [Receive-HomeAssistantNotification](Receive-HomeAssistantNotification.md)
Streams persistent-notification changes without polling.

### [Remove-HomeAssistantCalendarEvent](Remove-HomeAssistantCalendarEvent.md)
Deletes a Home Assistant calendar event or recurring occurrence.

### [Remove-HomeAssistantCategory](Remove-HomeAssistantCategory.md)
Deletes a Home Assistant category from an explicit scope.

### [Remove-HomeAssistantLabel](Remove-HomeAssistantLabel.md)
Deletes a Home Assistant label.

### [Remove-HomeAssistantNotification](Remove-HomeAssistantNotification.md)
Dismisses one or all persistent Home Assistant notifications.

### [Restart-HomeAssistant](Restart-HomeAssistant.md)
Restarts Core, Supervisor, host, an app, or reloads one integration.

### [Send-HomeAssistantNotification](Send-HomeAssistantNotification.md)
Sends a persistent notification or a message to selected notify entities.

### [Set-HomeAssistantCalendarEvent](Set-HomeAssistantCalendarEvent.md)
Creates or updates a timed or all-day Home Assistant calendar event.

### [Set-HomeAssistantCategory](Set-HomeAssistantCategory.md)
Creates or updates a Home Assistant category within an explicit scope.

### [Set-HomeAssistantClimate](Set-HomeAssistantClimate.md)
Sets common climate values with typed parameters instead of raw action data.

### [Set-HomeAssistantCover](Set-HomeAssistantCover.md)
Moves covers with a typed action, position, or tilt position.

### [Set-HomeAssistantLabel](Set-HomeAssistantLabel.md)
Creates or updates a Home Assistant label while allowing nullable fields to be explicitly cleared.

### [Set-HomeAssistantLight](Set-HomeAssistantLight.md)
Controls lights with typed power, brightness, color, effect, and transition parameters.

### [Set-HomeAssistantLock](Set-HomeAssistantLock.md)
Locks, unlocks, or opens a lock with high-impact confirmation.

### [Set-HomeAssistantMediaPlayer](Set-HomeAssistantMediaPlayer.md)
Controls media-player power, playback, volume, source, grouping, queueing, and content.

### [Set-HomeAssistantSwitch](Set-HomeAssistantSwitch.md)
Turns switches on, off, or toggles them through a resolved Home Assistant target.

### [Test-HomeAssistantConfiguration](Test-HomeAssistantConfiguration.md)
Validates the active Home Assistant configuration without restarting Core.
