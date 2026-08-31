@{
    AliasesToExport      = @()
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @('Connect-HomeAssistant', 'Disconnect-HomeAssistant', 'Export-HomeAssistantCameraSnapshot', 'Export-HomeAssistantDiagnostic', 'Find-HomeAssistant', 'Get-HomeAssistantAction', 'Get-HomeAssistantApp', 'Get-HomeAssistantArea', 'Get-HomeAssistantAutomation', 'Get-HomeAssistantBackup', 'Get-HomeAssistantCalendar', 'Get-HomeAssistantCalendarEvent', 'Get-HomeAssistantCamera', 'Get-HomeAssistantCategory', 'Get-HomeAssistantConnection', 'Get-HomeAssistantDashboard', 'Get-HomeAssistantDevice', 'Get-HomeAssistantEnergy', 'Get-HomeAssistantEntity', 'Get-HomeAssistantFloor', 'Get-HomeAssistantHistory', 'Get-HomeAssistantInfo', 'Get-HomeAssistantIntegration', 'Get-HomeAssistantIssue', 'Get-HomeAssistantJob', 'Get-HomeAssistantLabel', 'Get-HomeAssistantLog', 'Get-HomeAssistantLogbook', 'Get-HomeAssistantMedia', 'Get-HomeAssistantNotification', 'Get-HomeAssistantStatistic', 'Get-HomeAssistantTrace', 'Get-HomeAssistantUpdate', 'Get-HomeAssistantWeather', 'Install-HomeAssistantUpdate', 'Invoke-HomeAssistantAction', 'Invoke-HomeAssistantApp', 'Invoke-HomeAssistantAutomation', 'Invoke-HomeAssistantRecorderMaintenance', 'Invoke-HomeAssistantRemote', 'Invoke-HomeAssistantRoutine', 'New-HomeAssistantBackup', 'Receive-HomeAssistantCalendarEvent', 'Receive-HomeAssistantEvent', 'Receive-HomeAssistantNotification', 'Receive-HomeAssistantWeatherForecast', 'Remove-HomeAssistantAutomation', 'Remove-HomeAssistantCalendarEvent', 'Remove-HomeAssistantCategory', 'Remove-HomeAssistantDashboard', 'Remove-HomeAssistantLabel', 'Remove-HomeAssistantNotification', 'Remove-HomeAssistantStatistic', 'Restart-HomeAssistant', 'Send-HomeAssistantNotification', 'Set-HomeAssistantAlarm', 'Set-HomeAssistantAutomation', 'Set-HomeAssistantCalendarEvent', 'Set-HomeAssistantCamera', 'Set-HomeAssistantCategory', 'Set-HomeAssistantClimate', 'Set-HomeAssistantCover', 'Set-HomeAssistantDashboard', 'Set-HomeAssistantEnergy', 'Set-HomeAssistantFan', 'Set-HomeAssistantHelper', 'Set-HomeAssistantHumidifier', 'Set-HomeAssistantLabel', 'Set-HomeAssistantLawnMower', 'Set-HomeAssistantLight', 'Set-HomeAssistantLock', 'Set-HomeAssistantMediaPlayer', 'Set-HomeAssistantSiren', 'Set-HomeAssistantStatistic', 'Set-HomeAssistantSwitch', 'Set-HomeAssistantVacuum', 'Set-HomeAssistantValve', 'Set-HomeAssistantWaterHeater', 'Test-HomeAssistantConfiguration', 'Test-HomeAssistantStatistic')
    CompanyName          = 'Evotec'
    CompatiblePSEditions = @('Desktop', 'Core')
    Copyright            = '(c) 2026 - 2026 Przemyslaw Klys @ Evotec. All rights reserved.'
    Description          = 'Task-oriented PowerShell access to Home Assistant Core and Supervisor through HomeAssistantX.'
    FunctionsToExport    = @()
    GUID                 = '9c949e39-7bcb-41a2-ab01-ca4e6fe1dc27'
    ModuleVersion        = '0.1.0'
    PowerShellVersion    = '5.1'
    PrivateData          = @{
        PSData = @{
            LicenseUri = 'https://github.com/EvotecIT/HomeAssistantX/blob/main/LICENSE'
            ProjectUri = 'https://github.com/EvotecIT/HomeAssistantX'
            Tags       = @('HomeAssistant', 'SmartHome', 'Automation', 'REST', 'WebSocket', 'Windows', 'macOS', 'Linux')
            RequireLicenseAcceptance = $false
            ExternalModuleDependencies = @()
}
    }
    RequiredModules      = @()
    RootModule           = 'HomeAssistantX.psm1'
    ScriptsToProcess     = @()
}
