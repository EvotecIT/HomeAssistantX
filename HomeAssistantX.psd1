@{
    AliasesToExport      = @()
    Author               = 'Przemyslaw Klys'
    CmdletsToExport      = @('Connect-HomeAssistant', 'Disconnect-HomeAssistant', 'Export-HomeAssistantDiagnostic', 'Get-HomeAssistantAction', 'Get-HomeAssistantApp', 'Get-HomeAssistantArea', 'Get-HomeAssistantBackup', 'Get-HomeAssistantConnection', 'Get-HomeAssistantDevice', 'Get-HomeAssistantEntity', 'Get-HomeAssistantFloor', 'Get-HomeAssistantHistory', 'Get-HomeAssistantInfo', 'Get-HomeAssistantIntegration', 'Get-HomeAssistantIssue', 'Get-HomeAssistantJob', 'Get-HomeAssistantLog', 'Get-HomeAssistantTrace', 'Get-HomeAssistantUpdate', 'Install-HomeAssistantUpdate', 'Invoke-HomeAssistantAction', 'Invoke-HomeAssistantApp', 'New-HomeAssistantBackup', 'Receive-HomeAssistantEvent', 'Restart-HomeAssistant', 'Set-HomeAssistantClimate', 'Set-HomeAssistantCover', 'Set-HomeAssistantLight', 'Set-HomeAssistantLock', 'Set-HomeAssistantMediaPlayer', 'Set-HomeAssistantSwitch', 'Test-HomeAssistantConfiguration')
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
