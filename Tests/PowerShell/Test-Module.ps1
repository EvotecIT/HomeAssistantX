param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Assembly')]
    [string] $AssemblyPath,

    [Parameter(Mandatory = $true, ParameterSetName = 'Manifest')]
    [string] $ModuleManifest,

    [string] $TestServerPath
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($TestServerPath)) {
    $TestServerPath = Join-Path $PSScriptRoot '..\..\HomeAssistantX.TestServer\bin\Release\net10.0\HomeAssistantX.TestServer.exe'
}
$resolvedModulePath = if ($PSCmdlet.ParameterSetName -eq 'Manifest') {
    (Resolve-Path -LiteralPath $ModuleManifest).Path
} else {
    (Resolve-Path -LiteralPath $AssemblyPath).Path
}
$resolvedTestServerPath = (Resolve-Path -LiteralPath $TestServerPath).Path

Import-Module -Name $resolvedModulePath -Force -ErrorAction Stop

$expectedCommands = @(
    'Connect-HomeAssistant',
    'Disconnect-HomeAssistant',
    'Export-HomeAssistantCameraSnapshot',
    'Export-HomeAssistantDiagnostic',
    'Get-HomeAssistantAction',
    'Get-HomeAssistantApp',
    'Get-HomeAssistantArea',
    'Get-HomeAssistantAutomation',
    'Get-HomeAssistantBackup',
    'Get-HomeAssistantCalendar',
    'Get-HomeAssistantCalendarEvent',
    'Get-HomeAssistantCamera',
    'Get-HomeAssistantCategory',
    'Get-HomeAssistantConnection',
    'Get-HomeAssistantDashboard',
    'Get-HomeAssistantDevice',
    'Get-HomeAssistantEnergy',
    'Get-HomeAssistantEntity',
    'Get-HomeAssistantFloor',
    'Get-HomeAssistantHistory',
    'Get-HomeAssistantInfo',
    'Get-HomeAssistantIntegration',
    'Get-HomeAssistantIssue',
    'Get-HomeAssistantJob',
    'Get-HomeAssistantLabel',
    'Get-HomeAssistantLog',
    'Get-HomeAssistantLogbook',
    'Get-HomeAssistantMedia',
    'Get-HomeAssistantNotification',
    'Get-HomeAssistantStatistic',
    'Get-HomeAssistantTrace',
    'Get-HomeAssistantUpdate',
    'Get-HomeAssistantWeather',
    'Install-HomeAssistantUpdate',
    'Invoke-HomeAssistantAction',
    'Invoke-HomeAssistantApp',
    'Invoke-HomeAssistantAutomation',
    'Invoke-HomeAssistantRecorderMaintenance',
    'Invoke-HomeAssistantRemote',
    'New-HomeAssistantBackup',
    'Receive-HomeAssistantCalendarEvent',
    'Receive-HomeAssistantEvent',
    'Receive-HomeAssistantNotification',
    'Receive-HomeAssistantWeatherForecast',
    'Remove-HomeAssistantAutomation',
    'Remove-HomeAssistantCalendarEvent',
    'Remove-HomeAssistantCategory',
    'Remove-HomeAssistantDashboard',
    'Remove-HomeAssistantLabel',
    'Remove-HomeAssistantNotification',
    'Remove-HomeAssistantStatistic',
    'Restart-HomeAssistant',
    'Send-HomeAssistantNotification',
    'Set-HomeAssistantAutomation',
    'Set-HomeAssistantCalendarEvent',
    'Set-HomeAssistantCamera',
    'Set-HomeAssistantCategory',
    'Set-HomeAssistantClimate',
    'Set-HomeAssistantCover',
    'Set-HomeAssistantDashboard',
    'Set-HomeAssistantEnergy',
    'Set-HomeAssistantLabel',
    'Set-HomeAssistantLight',
    'Set-HomeAssistantLock',
    'Set-HomeAssistantMediaPlayer',
    'Set-HomeAssistantStatistic',
    'Set-HomeAssistantSwitch',
    'Test-HomeAssistantConfiguration',
    'Test-HomeAssistantStatistic'
)
$importedModuleName = (Get-Command -Name Connect-HomeAssistant -ErrorAction Stop).ModuleName
$actualCommands = @(Get-Command -Module $importedModuleName | Sort-Object -Property Name | Select-Object -ExpandProperty Name)
if (($actualCommands -join '|') -ne ($expectedCommands -join '|')) {
    throw "Unexpected command surface: $($actualCommands -join ', ')"
}

$parameterSetContracts = @{
    'Get-HomeAssistantLog'        = @('App', 'Core', 'Host', 'Legacy', 'Supervisor', 'SystemLog')
    'Get-HomeAssistantInfo'       = @('Capabilities', 'Health', 'Overview', 'Supervisor')
    'Get-HomeAssistantEnergy'     = @('FossilConsumption', 'Info', 'Preferences', 'SolarForecast', 'Validation')
    'Get-HomeAssistantStatistic'  = @('Catalog', 'Metadata', 'Values')
    'Get-HomeAssistantWeather'    = @('Current', 'Forecast', 'Units')
    'Get-HomeAssistantCamera'     = @('Capabilities', 'Preferences', 'SignedImage', 'SignedMjpeg', 'Status', 'Stream')
    'Get-HomeAssistantDashboard'  = @('Configuration', 'Dashboards', 'Info', 'Panels', 'Resources')
    'Get-HomeAssistantMedia'      = @('Player', 'PlayerSearch', 'Resolve', 'Sources', 'SourceSearch')
    'Get-HomeAssistantAutomation' = @('Configuration', 'Status')
    'Install-HomeAssistantUpdate' = @('App', 'Core', 'Entity', 'OperatingSystem', 'Supervisor')
    'Invoke-HomeAssistantAction'  = @('Area', 'Data', 'Device', 'Entity', 'Floor', 'Label')
    'Invoke-HomeAssistantRemote'  = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
    'Invoke-HomeAssistantRecorderMaintenance' = @('Disable', 'Enable', 'Purge', 'PurgeEntities', 'RefreshStatisticsIssues')
    'Remove-HomeAssistantNotification' = @('All', 'Id')
    'Send-HomeAssistantNotification' = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label', 'Persistent')
    'Set-HomeAssistantCalendarEvent' = @('CreateAllDay', 'CreateTimed', 'UpdateAllDay', 'UpdateTimed')
    'Set-HomeAssistantCategory'   = @('Create', 'Update')
    'Set-HomeAssistantLabel'      = @('Create', 'Update')
    'Set-HomeAssistantStatistic'  = @('AdjustSum', 'Import', 'Metadata', 'Unit')
    'Set-HomeAssistantDashboard'  = @('Configuration', 'Create', 'ResourceCreate', 'ResourceUpdate', 'Update')
    'Remove-HomeAssistantDashboard' = @('Configuration', 'Dashboard', 'Resource')
    'Restart-HomeAssistant'       = @('App', 'Core', 'Host', 'Integration', 'Supervisor')
    'Set-HomeAssistantClimate'    = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
    'Set-HomeAssistantCover'      = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
    'Set-HomeAssistantLight'      = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
    'Set-HomeAssistantLock'       = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
    'Set-HomeAssistantMediaPlayer' = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
    'Set-HomeAssistantSwitch'     = @('Area', 'Device', 'Entity', 'Floor', 'InputObject', 'Label')
}
foreach ($entry in $parameterSetContracts.GetEnumerator()) {
    $sets = @((Get-Command -Name $entry.Key).ParameterSets.Name | Sort-Object)
    if (($sets -join '|') -ne ($entry.Value -join '|')) {
        throw "Unexpected parameter sets for $($entry.Key): $($sets -join ', ')"
    }
}

$cameraEntityParameter = (Get-Command -Name Get-HomeAssistantCamera -ErrorAction Stop).Parameters['EntityId']
if ($cameraEntityParameter.ParameterSets['Status'].IsMandatory) {
    throw 'Get-HomeAssistantCamera -EntityId must remain optional in the Status parameter set.'
}
foreach ($parameterSetName in 'Capabilities', 'Preferences', 'SignedImage', 'SignedMjpeg', 'Stream') {
    if (-not $cameraEntityParameter.ParameterSets[$parameterSetName].IsMandatory) {
        throw "Get-HomeAssistantCamera -EntityId must be mandatory in the $parameterSetName parameter set."
    }
}

foreach ($command in Get-Command -Module $importedModuleName) {
    foreach ($parameter in $command.Parameters.Values) {
        $isMandatorySelector = $parameter.ParameterType -eq [System.Management.Automation.SwitchParameter] -and
            @($parameter.ParameterSets.Values | Where-Object IsMandatory).Count -gt 0
        if ($isMandatorySelector -and -not ($parameter.Attributes | Where-Object { $_.GetType().Name -eq 'ValidateSwitchPresentAttribute' })) {
            throw "$($command.Name) -$($parameter.Name) is a mandatory selector switch without false-value validation."
        }
    }
}

$outputTypeContracts = @{
    'Get-HomeAssistantEnergy' = @('HomeAssistantEnergyInfo', 'HomeAssistantEnergyPreferences', 'HomeAssistantFossilEnergyPeriod', 'IReadOnlyDictionary`2', 'JsonElement')
    'Get-HomeAssistantStatistic' = @('HomeAssistantStatisticMetadata', 'HomeAssistantStatisticSeries')
    'Get-HomeAssistantWeather' = @('HomeAssistantWeatherForecastUpdate', 'HomeAssistantWeatherObservation', 'IReadOnlyDictionary`2')
    'Install-HomeAssistantUpdate' = @('HomeAssistantServiceCallResult', 'JsonElement')
    'Invoke-HomeAssistantApp' = @('JsonElement')
    'Restart-HomeAssistant' = @('HomeAssistantIntegrationOperationResult', 'JsonElement')
    'Set-HomeAssistantAutomation' = @('JsonElement')
    'Set-HomeAssistantDashboard' = @('HomeAssistantDashboard', 'HomeAssistantDashboardResource', 'JsonElement')
    'Test-HomeAssistantStatistic' = @('JsonElement')
}
foreach ($entry in $outputTypeContracts.GetEnumerator()) {
    $actualTypes = @((Get-Command -Name $entry.Key).OutputType.Type.Name | Sort-Object)
    $expectedTypes = @($entry.Value | Sort-Object)
    if (($actualTypes -join '|') -ne ($expectedTypes -join '|')) {
        throw "Unexpected output types for $($entry.Key): $($actualTypes -join ', ')"
    }
}

$recorderIssuesSet = (Get-Command -Name Invoke-HomeAssistantRecorderMaintenance).ParameterSets |
    Where-Object Name -EQ 'RefreshStatisticsIssues'
if ($recorderIssuesSet.Parameters.Name -contains 'PassThru') {
    throw 'Recorder statistics-issue refresh advertises PassThru without producing an operation result.'
}

$mediaParameters = (Get-Command -Name Set-HomeAssistantMediaPlayer).Parameters
foreach ($name in 'Power', 'Playback', 'VolumePercent', 'VolumeStep', 'Muted', 'Source', 'SoundMode', 'Shuffle', 'Repeat', 'SeekSeconds', 'ClearPlaylist', 'JoinMember', 'Unjoin', 'MediaContentId', 'MediaContentType', 'Enqueue', 'Announce', 'MediaExtra') {
    if (-not $mediaParameters.ContainsKey($name)) {
        throw "Set-HomeAssistantMediaPlayer is missing the $name parameter."
    }
}

$remoteParameters = (Get-Command -Name Invoke-HomeAssistantRemote).Parameters
foreach ($name in 'Action', 'Activity', 'Command', 'RemoteDevice', 'RepeatCount', 'DelaySeconds', 'HoldSeconds', 'CommandType', 'Alternative', 'TimeoutSeconds') {
    if (-not $remoteParameters.ContainsKey($name)) {
        throw "Invoke-HomeAssistantRemote is missing the $name parameter."
    }
}

foreach ($name in 'Export-HomeAssistantCameraSnapshot', 'Install-HomeAssistantUpdate', 'Invoke-HomeAssistantAction', 'Invoke-HomeAssistantApp', 'Invoke-HomeAssistantAutomation', 'Invoke-HomeAssistantRecorderMaintenance', 'Invoke-HomeAssistantRemote', 'New-HomeAssistantBackup', 'Remove-HomeAssistantAutomation', 'Remove-HomeAssistantCalendarEvent', 'Remove-HomeAssistantCategory', 'Remove-HomeAssistantDashboard', 'Remove-HomeAssistantLabel', 'Remove-HomeAssistantNotification', 'Remove-HomeAssistantStatistic', 'Restart-HomeAssistant', 'Send-HomeAssistantNotification', 'Set-HomeAssistantAutomation', 'Set-HomeAssistantCalendarEvent', 'Set-HomeAssistantCamera', 'Set-HomeAssistantCategory', 'Set-HomeAssistantClimate', 'Set-HomeAssistantCover', 'Set-HomeAssistantDashboard', 'Set-HomeAssistantEnergy', 'Set-HomeAssistantLabel', 'Set-HomeAssistantLight', 'Set-HomeAssistantLock', 'Set-HomeAssistantMediaPlayer', 'Set-HomeAssistantStatistic', 'Set-HomeAssistantSwitch') {
    if (-not (Get-Command -Name $name).Parameters.ContainsKey('WhatIf')) {
        throw "$name must support ShouldProcess/WhatIf."
    }
}

$server = New-Object System.Diagnostics.Process
$server.StartInfo = New-Object System.Diagnostics.ProcessStartInfo
$server.StartInfo.FileName = $resolvedTestServerPath
$server.StartInfo.WorkingDirectory = Split-Path -Parent $resolvedTestServerPath
$server.StartInfo.UseShellExecute = $false
$server.StartInfo.CreateNoWindow = $true
$server.StartInfo.RedirectStandardInput = $true
$server.StartInfo.RedirectStandardOutput = $true
$server.StartInfo.RedirectStandardError = $true
$server.StartInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$null = $server.Start()
$connection = $null
try {
    $ready = $server.StandardOutput.ReadLine()
    if ([string]::IsNullOrWhiteSpace($ready) -or -not $ready.StartsWith('READY ')) {
        throw "The loopback Home Assistant server did not start: $ready"
    }

    $uri = [Uri] $ready.Substring(6)
    $server.StandardInput.WriteLine('PING')
    $server.StandardInput.Flush()
    $ping = $server.StandardOutput.ReadLine()
    if ($ping -ne 'PONG') {
        $server.StandardInput.WriteLine('PING')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'PONG') {
            throw 'The loopback Home Assistant server input channel is not ready.'
        }
    }

    $server.StandardInput.WriteLine('SET_REMOTE_STATES')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'REMOTE_STATES_SET') {
        throw 'Could not load the provenance control state fixture.'
    }

    $connection = Connect-HomeAssistant -Uri $uri -AccessToken 'test-access-token'
    $defaultConnection = Get-HomeAssistantConnection
    $secondaryConnection = Connect-HomeAssistant -Uri $uri -AccessToken 'test-access-token' -Name Secondary -NoDefault
    try {
        if (-not [object]::ReferenceEquals($connection, (Get-HomeAssistantConnection))) {
            throw 'Connect-HomeAssistant -NoDefault replaced the runspace default.'
        }

        $defaultLights = @($connection | Get-HomeAssistantEntity -Area Kitchen -Domain light)
        $secondaryLights = @($secondaryConnection | Get-HomeAssistantEntity -Area Kitchen -Domain light)
        $confirmationTargetMethod = [HomeAssistantX.PowerShell.HomeAssistantCmdlet].GetMethod(
            'ConfirmationTarget',
            [Reflection.BindingFlags]'NonPublic, Static')
        $confirmationConnection = $secondaryConnection.PSObject.BaseObject
        $confirmationOutput = $confirmationTargetMethod.Invoke($null, @($confirmationConnection, 'light.kitchen'))
        $null = $secondaryLights | Set-HomeAssistantLight -Power On -WhatIf
        if ($confirmationOutput -match [regex]::Escape($secondaryConnection.Name) -or
            $confirmationOutput -match [regex]::Escape($secondaryConnection.Uri.GetLeftPart([UriPartial]::Authority)) -or
            $confirmationOutput -notmatch '^Home Assistant target \[[0-9A-F]{8}\] on Home Assistant connection \[[0-9A-F]{8}\]$') {
            throw 'WhatIf confirmation exposed an explicit connection name or failed to provide a privacy-safe connection tag.'
        }
        $secondaryRemotes = @($secondaryConnection | Get-HomeAssistantEntity -Domain remote)
        $server.StandardInput.WriteLine('CLEAR_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_CLEARED') {
            throw 'Could not reset the mixed-connection action baseline.'
        }
        $mixedConnectionsRejected = $false
        try {
            $null = @($defaultLights + $secondaryLights) | Set-HomeAssistantLight -Power Off -Confirm:$false -ErrorAction Stop
        } catch {
            $mixedConnectionsRejected = $true
        }
        if (-not $mixedConnectionsRejected) {
            throw 'A typed-control pipeline accepted entities from different Home Assistant connections.'
        }
        $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
            throw 'A mixed-connection pipeline invoked a Home Assistant action before rejection.'
        }

        $connection | Disconnect-HomeAssistant
        $connection = $null
        $null = $secondaryLights | Set-HomeAssistantLight -Power Off -Confirm:$false
        $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        $provenanceCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
        if ($provenanceCall.service -ne 'turn_off' -or @($provenanceCall.target.entity_id)[0] -ne 'light.kitchen') {
            throw 'Piped entities did not retain their non-default source connection.'
        }

        $null = $secondaryRemotes | Invoke-HomeAssistantRemote -Action TurnOn -Activity ' Watch TV ' -Confirm:$false
        $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        $remoteProvenanceCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
        if ($remoteProvenanceCall.service -ne 'turn_on' -or @($remoteProvenanceCall.target.entity_id)[0] -ne 'remote.living_room') {
            throw 'The remote cmdlet did not bind pipeline connection provenance before reading client options.'
        }
        if ($remoteProvenanceCall.service_data.activity -cne ' Watch TV ') {
            throw 'The remote cmdlet did not preserve integration-defined activity text.'
        }

        $server.StandardInput.WriteLine('SET_DEFAULT_STATES')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'DEFAULT_STATES_SET') {
            throw 'Could not restore the default state fixture after the provenance regression.'
        }

        $connection = Connect-HomeAssistant -Uri $uri -AccessToken 'test-access-token'
        $defaultConnection = $connection
        $server.StandardInput.WriteLine('CLEAR_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_CLEARED') {
            throw 'Could not reset the connection-provenance action baseline.'
        }

        $mismatchedConnectionRejected = $false
        try {
            $null = $secondaryLights | Set-HomeAssistantLight -Connection $connection -Power Off -Confirm:$false -ErrorAction Stop
        } catch {
            $mismatchedConnectionRejected = $true
        }
        if (-not $mismatchedConnectionRejected) {
            throw 'A piped entity accepted a different explicit Home Assistant connection.'
        }
        $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
            throw 'A mismatched piped entity connection invoked a Home Assistant action.'
        }
    } finally {
        if (-not $secondaryConnection.IsDisposed) {
            $secondaryConnection | Disconnect-HomeAssistant
        }
    }
    $disposedProvenanceRejected = $false
    try {
        $null = $secondaryLights | Set-HomeAssistantLight -Power Off -WhatIf -ErrorAction Stop
    } catch {
        $disposedProvenanceRejected = $true
    }
    if (-not $disposedProvenanceRejected) {
        throw 'A typed-control WhatIf preview accepted a disposed source connection.'
    }
    $disposedConfirmationRejected = $false
    try {
        $null = Invoke-HomeAssistantAction -Connection $secondaryConnection light turn_on -EntityId light.kitchen -WhatIf -ErrorAction Stop
    } catch {
        $disposedConfirmationRejected = $true
    }
    if (-not $disposedConfirmationRejected) {
        throw 'A ShouldProcess preview accepted a disposed explicit Home Assistant connection.'
    }
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
        throw 'A disposed provenance connection invoked a Home Assistant action.'
    }

    $info = Get-HomeAssistantInfo

    $falseSelectorRejected = $false
    try {
        Remove-HomeAssistantNotification -All:$false -WhatIf -ErrorAction Stop
    } catch {
        $falseSelectorRejected = $true
    }
    if (-not $falseSelectorRejected) {
        throw 'A destructive parameter set accepted its mandatory selector switch as an explicit false value.'
    }

    foreach ($invalidEnergyJson in '[null]', '[1]', '["sensor.energy"]', '[{"type":"grid","type":"solar"}]') {
        $invalidEnergyRejected = $false
        try {
            Set-HomeAssistantEnergy -DeviceConsumptionJson $invalidEnergyJson -WhatIf -ErrorAction Stop
        } catch {
            $invalidEnergyRejected = $true
        }
        if (-not $invalidEnergyRejected) {
            throw 'The Energy cmdlet accepted a non-object preference entry before WhatIf confirmation.'
        }
    }

    $blankLogbookFilterRejected = $false
    try {
        Get-HomeAssistantLogbook -EntityId ' ' -ErrorAction Stop
    } catch {
        $blankLogbookFilterRejected = $true
    }
    if (-not $blankLogbookFilterRejected) {
        throw 'The logbook cmdlet broadened a blank entity filter to the whole installation.'
    }

    $endOnlyLogbook = @(Get-HomeAssistantLogbook -EndTime '2026-08-24T12:00:01Z' -ErrorAction Stop)
    $endOnlyHistory = @(Get-HomeAssistantHistory -EntityId sensor.kitchen_temperature -EndTime '2026-08-24T12:00:00Z' -ErrorAction Stop)
    if ($endOnlyLogbook.Count -ne 1 -or $endOnlyLogbook[0].Message -ne 'turned on') {
        throw 'The logbook cmdlet did not preserve Home Assistant default-start behavior for an end-only query.'
    }
    if ($endOnlyHistory.Count -ne 1 -or $endOnlyHistory[0].EntityId -ne 'sensor.kitchen_temperature') {
        throw 'The history cmdlet did not preserve Home Assistant default-start behavior for an end-only query.'
    }

    foreach ($automationCommand in @(
        { Set-HomeAssistantAutomation -AutomationId ' ' -ConfigurationJson '{"alias":"Morning","triggers":[],"actions":[]}' -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantAutomation -AutomationId ' ' -WhatIf -ErrorAction Stop }
    )) {
        $blankAutomationIdRejected = $false
        try {
            & $automationCommand
        } catch {
            $blankAutomationIdRejected = $true
        }
        if (-not $blankAutomationIdRejected) {
            throw 'An automation configuration cmdlet accepted a blank identifier before WhatIf confirmation.'
        }
    }

    $floors = @(Get-HomeAssistantFloor)
    $areas = @(Get-HomeAssistantArea -Floor Ground)
    $devices = @(Get-HomeAssistantDevice -Area Kitchen)
    $entities = @(Get-HomeAssistantEntity)
    $kitchenLights = @(Get-HomeAssistantEntity -Area Kitchen -Domain light)
    $lightActions = @(Get-HomeAssistantAction -Entity 'Kitchen light')
    $logs = @($connection | Get-HomeAssistantLog)
    $integrations = @($connection | Get-HomeAssistantIntegration)
    $apps = @($connection | Get-HomeAssistantApp)
    $backups = @($connection | Get-HomeAssistantBackup)
    $supervisorOverview = $connection | Get-HomeAssistantInfo -Supervisor
    $configuration = $connection | Test-HomeAssistantConfiguration
    $notifications = @(Get-HomeAssistantNotification)
    $notificationUpdates = @(Receive-HomeAssistantNotification -Count 1 -TimeoutSeconds 5)
    $calendars = @(Get-HomeAssistantCalendar)
    $calendarEvents = @(Get-HomeAssistantCalendarEvent -EntityId calendar.home -StartTime '2026-08-26T00:00:00Z' -EndTime '2026-08-28T00:00:00Z')
    $calendarUpdates = @(Receive-HomeAssistantCalendarEvent -EntityId calendar.home -StartTime '2026-08-26T00:00:00Z' -EndTime '2026-08-28T00:00:00Z' -Count 1 -TimeoutSeconds 5)
    $labels = @(Get-HomeAssistantLabel)
    $labelByNativeId = @(Get-HomeAssistantLabel -Label Security)
    $labelByPaddedNativeId = @(Get-HomeAssistantLabel -Label ' security ')
    $categories = @(Get-HomeAssistantCategory -Scope automation)
    $categoryByPaddedName = @(Get-HomeAssistantCategory -Scope automation -Category ' Comfort ')
    $energyPreferences = Get-HomeAssistantEnergy
    $energyInfo = Get-HomeAssistantEnergy -Info
    $energyValidation = Get-HomeAssistantEnergy -Validation
    $solarForecast = Get-HomeAssistantEnergy -SolarForecast
    $fossilEnergy = @(Get-HomeAssistantEnergy -FossilConsumption -StartTime '2026-08-26T10:00:00Z' -EndTime '2026-08-26T12:00:00Z' -EnergyStatisticId sensor.grid_energy -Co2StatisticId sensor.co2_intensity -Period Hour)
    $statistics = @(Get-HomeAssistantStatistic -Kind Sum)
    $statisticValues = @(Get-HomeAssistantStatistic -StatisticId sensor.grid_energy -StartTime '2026-08-26T00:00:00Z' -EndTime '2026-08-27T00:00:00Z' -Period Hour -Type Change, Sum)
    $statisticIssues = Test-HomeAssistantStatistic
    $weatherForecast = Get-HomeAssistantWeather -EntityId weather.home -Forecast -ForecastType Daily
    $weatherUnits = Get-HomeAssistantWeather -ConvertibleUnits
    $weatherUpdates = @(Receive-HomeAssistantWeatherForecast weather.home -ForecastType Hourly -Count 1 -TimeoutSeconds 5)
    $logbook = @(Get-HomeAssistantLogbook -StartTime '2026-08-24T00:00:00Z' -EndTime '2026-08-26T00:00:00Z' -EntityId light.kitchen)
    $cameraCapabilities = Get-HomeAssistantCamera camera.front -Capabilities
    $cameraStream = Get-HomeAssistantCamera camera.front -Stream
    $cameraPreferences = Get-HomeAssistantCamera camera.front -Preferences
    $mediaRoot = Get-HomeAssistantMedia
    $mediaSearch = @(Get-HomeAssistantMedia -PlayerEntityId media_player.kitchen -Search dinner -MediaClass music)
    $resolvedMedia = Get-HomeAssistantMedia -Resolve -MediaContentId 'media-source://media_source/local/dinner.mp3' -ExpiresInSeconds 300
    $dashboardPanels = @(Get-HomeAssistantDashboard -Panels)
    $dashboards = @(Get-HomeAssistantDashboard)
    $dashboardConfiguration = Get-HomeAssistantDashboard -Configuration
    $dashboardResources = @(Get-HomeAssistantDashboard -Resources)
    $automationConfiguration = Get-HomeAssistantAutomation morning-routine -Configuration

    if ($info.Version -ne '2026.8.3') { throw 'Core information was not returned.' }
    if (-not [object]::ReferenceEquals($connection, $defaultConnection)) { throw 'Connect-HomeAssistant did not establish the runspace default.' }
    if ($floors.Count -ne 1 -or $floors[0].Name -ne 'Ground') { throw 'Floor discovery did not return the joined floor.' }
    if ($areas.Count -ne 1 -or $areas[0].Name -ne 'Kitchen') { throw 'Area discovery did not resolve the floor name.' }
    if ($devices.Count -ne 1 -or $devices[0].Name -ne 'Kitchen Sensor') { throw 'Device discovery did not resolve the area name.' }
    if ($entities.Count -ne 2) { throw 'Entity enumeration did not use the live loopback contract.' }
    if ($kitchenLights.Count -ne 1 -or $kitchenLights[0].EntityId -ne 'light.kitchen') { throw 'Joined entity discovery did not find the kitchen light.' }
    if (-not ($lightActions | Where-Object Action -EQ turn_on)) { throw 'Action discovery did not return the light action catalog.' }
    if ($logs.Count -ne 1) { throw 'Structured system logs were not returned.' }
    if ($integrations.Count -ne 1) { throw 'Configuration entries were not returned.' }
    if ($apps.Count -ne 1) { throw 'Supervisor apps were not returned.' }
    if ($backups.Count -ne 1) { throw 'Supervisor backups were not returned.' }
    if ($supervisorOverview.CoreVersion -ne '2026.8.3') { throw 'Supervisor installation overview was not returned.' }
    if (-not $configuration.IsValid) { throw 'Configuration validation failed.' }
    if ($notifications.Count -ne 1 -or $notifications[0].NotificationId -ne 'notice-1') { throw 'Persistent notifications were not returned.' }
    if ($notificationUpdates.Count -ne 1 -or $notificationUpdates[0].RawType -ne 'current') { throw 'Persistent notification streaming did not return its current snapshot.' }
    if ($calendars.Count -ne 1 -or $calendars[0].EntityId -ne 'calendar.home') { throw 'Calendar discovery was not returned.' }
    if ($calendarEvents.Count -ne 1 -or $calendarEvents[0].Summary -ne 'Dinner') { throw 'Calendar events were not returned.' }
    if ($calendarUpdates.Count -ne 1 -or $calendarUpdates[0].Events[0].Uid -ne 'event-1') { throw 'Calendar event streaming did not return an event list.' }
    if ($labels.Count -ne 2 -or -not ($labels | Where-Object LabelId -EQ 'security')) { throw 'Label discovery was not returned.' }
    if ($labelByNativeId.Count -ne 1 -or $labelByNativeId[0].LabelId -ne 'security') { throw 'A native label ID did not take precedence over a colliding friendly name.' }
    if ($labelByPaddedNativeId.Count -ne 1 -or $labelByPaddedNativeId[0].LabelId -ne 'security') { throw 'A padded label filter was not normalized.' }
    if ($categories.Count -ne 1 -or $categories[0].CategoryId -ne 'comfort') { throw 'Scoped category discovery was not returned.' }
    if ($categoryByPaddedName.Count -ne 1 -or $categoryByPaddedName[0].CategoryId -ne 'comfort') { throw 'A padded category filter was not normalized.' }

    $server.StandardInput.WriteLine('CLEAR_LAST_LABEL_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'LABEL_LIST_CLEARED') { throw 'The label-list dispatch fixture did not clear.' }
    $server.StandardInput.WriteLine('CLEAR_LAST_CATEGORY_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'CATEGORY_LIST_CLEARED') { throw 'The category-list dispatch fixture did not clear.' }
    foreach ($invalidRegistryFilter in @(
        { Get-HomeAssistantLabel -Label ' ' -ErrorAction Stop },
        { Get-HomeAssistantCategory -Scope automation -Category ' ' -ErrorAction Stop }
    )) {
        $rejected = $false
        try { & $invalidRegistryFilter } catch { $rejected = $true }
        if (-not $rejected) { throw 'A whitespace-only registry filter was accepted.' }
    }
    $server.StandardInput.WriteLine('GET_LAST_LABEL_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'LABEL_LIST_NONE') { throw 'A whitespace-only label filter dispatched before validation.' }
    $server.StandardInput.WriteLine('GET_LAST_CATEGORY_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'CATEGORY_LIST_NONE') { throw 'A whitespace-only category filter dispatched before validation.' }

    $server.StandardInput.WriteLine('SET_LABEL_REGISTRY_UNAVAILABLE')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'LABEL_REGISTRY_UNAVAILABLE') { throw 'The label-registry availability fixture did not activate.' }
    try {
        Set-HomeAssistantLight -Label security -Power On -Confirm:$false
        $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
        $server.StandardInput.Flush()
        $nativeLabelCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
        if ($nativeLabelCall.target.label_id.Count -ne 1 -or $nativeLabelCall.target.label_id[0] -ne 'security') {
            throw 'A native label target was not retained when label-registry enrichment was unavailable.'
        }
    } finally {
        $server.StandardInput.WriteLine('SET_LABEL_REGISTRY_AVAILABLE')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'LABEL_REGISTRY_AVAILABLE') { throw 'The label-registry availability fixture did not reset.' }
    }
    if ($energyPreferences.EnergySources[0].GetProperty('type').GetString() -ne 'solar') { throw 'Energy preferences were not returned.' }
    if (@($energyInfo.SolarForecastDomains)[0] -ne 'forecast_solar') { throw 'Energy capabilities were not returned.' }
    if (-not $energyValidation.GetProperty('future_validation').GetProperty('valid').GetBoolean()) { throw 'Energy validation was not returned.' }
    if (-not $solarForecast.ContainsKey('entry-solar')) { throw 'Solar provider forecasts were not returned.' }
    if ($fossilEnergy.Count -ne 2 -or $fossilEnergy[0].EnergyKiloWattHours -ne 0.42) { throw 'Fossil energy periods were not returned.' }
    if ($statistics.Count -ne 1 -or $statistics[0].StatisticId -ne 'sensor.grid_energy') { throw 'Recorder statistics were not listed.' }
    if ($statisticValues.Count -ne 1 -or $statisticValues[0].Rows[0].Sum -ne 10.5) { throw 'Recorder statistic values were not returned.' }
    if ($statisticIssues.GetProperty('sensor.grid_energy').GetProperty('issue').GetString() -ne 'unit_changed') { throw 'Recorder statistics validation was not returned.' }
    if ($weatherForecast.Forecast[0].Condition -ne 'sunny') { throw 'Weather forecast was not returned.' }
    if (-not $weatherUnits.ContainsKey('temperature_unit')) { throw 'Weather convertible units were not returned.' }
    if ($weatherUpdates.Count -ne 1 -or $weatherUpdates[0].Forecast[0].Condition -ne 'rainy') { throw 'Weather forecast streaming was not returned.' }
    if ($logbook.Count -ne 1 -or $logbook[0].Message -ne 'turned on') { throw 'Recorder logbook activity was not returned.' }
    if (-not @($cameraCapabilities.FrontendStreamTypes).Contains('web_rtc')) { throw 'Camera capabilities were not returned.' }
    if (-not $cameraStream.Path.EndsWith('master_playlist.m3u8')) { throw 'Camera HLS stream was not returned.' }
    if (-not $cameraPreferences.PreloadStream) { throw 'Camera preferences were not returned.' }
    if ($mediaRoot.Title -ne 'Music' -or $mediaRoot.Children[0].Title -ne 'Dinner') { throw 'Media-source browsing was not returned.' }
    if ($mediaSearch.Count -ne 1 -or $mediaSearch[0].Title -ne 'Dinner') { throw 'Media-player search was not returned.' }
    if ($resolvedMedia.MimeType -ne 'audio/mpeg') { throw 'Media resolution was not returned.' }
    if ($dashboardPanels.Count -ne 1 -or $dashboardPanels[0].UrlPath -ne 'lovelace') { throw 'Frontend panels were not returned.' }
    if ($dashboards.Count -ne 1 -or $dashboards[0].Id -ne 'house-main') { throw 'Lovelace dashboards were not returned.' }
    if ($dashboardConfiguration.GetProperty('views').GetArrayLength() -ne 1) { throw 'Lovelace configuration was not returned.' }
    if ($dashboardResources.Count -ne 1 -or $dashboardResources[0].Id -ne 'resource-1') { throw 'Lovelace resources were not returned.' }
    if ($automationConfiguration.AutomationId -ne 'morning-routine' -or $automationConfiguration.Definition.GetProperty('alias').GetString() -ne 'Morning') { throw 'Editable automation configuration was not returned.' }

    $server.StandardInput.WriteLine('SET_CASE_DISTINCT_LABELS')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'CASE_DISTINCT_LABELS_SET') { throw 'The case-distinct label fixture did not activate.' }
    try {
        $null = Set-HomeAssistantLight -Label security -Power On -WhatIf -ErrorAction Stop
    } finally {
        $server.StandardInput.WriteLine('SET_DEFAULT_LABELS')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'DEFAULT_LABELS_SET') { throw 'The default label fixture did not reset.' }
    }

    $diagnosticPath = Join-Path ([IO.Path]::GetTempPath()) ('HomeAssistantX-Diagnostic-' + [Guid]::NewGuid().ToString('N') + '.json')
    try {
        [IO.File]::WriteAllText($diagnosticPath, 'existing diagnostic')
        $diagnosticFile = $connection | Export-HomeAssistantDiagnostic -EntryId entry-1 -Path $diagnosticPath -Force -Confirm:$false
        if (-not $diagnosticFile.Exists -or -not ([IO.File]::ReadAllText($diagnosticPath).Contains('REDACTED'))) {
            throw 'The diagnostic export did not atomically replace the destination with the server response.'
        }
        $temporaryPattern = '.' + [IO.Path]::GetFileName($diagnosticPath) + '.*.tmp'
        if (@(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($diagnosticPath)) -Filter $temporaryPattern).Count -ne 0) {
            throw 'The diagnostic export left a temporary file behind.'
        }
    } finally {
        if (Test-Path -LiteralPath $diagnosticPath) {
            Remove-Item -LiteralPath $diagnosticPath -Force
        }
    }

    $server.StandardInput.WriteLine('CLEAR_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    $serviceBaseline = $server.StandardOutput.ReadLine()
    if ($serviceBaseline -ne 'SERVICE_CALL_CLEARED') {
        throw "Could not establish the action-call baseline. Received: $serviceBaseline"
    }

    $server.StandardInput.WriteLine('CLEAR_LAST_LABEL_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'LABEL_LIST_CLEARED') { throw 'The notification preflight fixture did not clear label discovery.' }
    foreach ($invalidNotification in @(
        { Send-HomeAssistantNotification -Label security -Message ' ' -WhatIf -ErrorAction Stop },
        { Send-HomeAssistantNotification -Persistent -Message Valid -NotificationId ' ' -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantNotification -NotificationId ' ' -WhatIf -ErrorAction Stop }
    )) {
        $rejected = $false
        try { & $invalidNotification } catch { $rejected = $true }
        if (-not $rejected) { throw 'A whitespace-only notification value was accepted before confirmation.' }
    }
    $server.StandardInput.WriteLine('GET_LAST_LABEL_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'LABEL_LIST_NONE') { throw 'A whitespace-only notification message dispatched inventory discovery.' }
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') { throw 'A whitespace-only notification value dispatched an action.' }

    $cameraPath = Join-Path ([IO.Path]::GetTempPath()) ('HomeAssistantX-Camera-' + [Guid]::NewGuid().ToString('N') + '.jpg')
    try {
        [IO.File]::WriteAllText($cameraPath, 'old image')
        $cameraFile = Export-HomeAssistantCameraSnapshot camera.front $cameraPath -Width 640 -Height 360 -Force -Confirm:$false
        if (-not $cameraFile.Exists -or [IO.File]::ReadAllText($cameraPath) -ne 'test-image-bytes') { throw 'Camera snapshot export did not atomically replace the destination.' }
        $temporaryPattern = '.' + [IO.Path]::GetFileName($cameraPath) + '.*.tmp'
        if (@(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($cameraPath)) -Filter $temporaryPattern).Count -ne 0) { throw 'Camera snapshot export left a temporary file behind.' }
    } finally {
        if (Test-Path -LiteralPath $cameraPath) { Remove-Item -LiteralPath $cameraPath -Force }
    }

    $null = Send-HomeAssistantNotification -Persistent -Message 'Garage is open' -Title Security -WhatIf
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
        throw 'Notification WhatIf invoked a Home Assistant action.'
    }

    $null = Send-HomeAssistantNotification -Persistent -Message 'Garage is open' -Title Security -NotificationId garage-open -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    $notificationCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($notificationCall.domain -ne 'persistent_notification' -or $notificationCall.service -ne 'create' -or $notificationCall.service_data.notification_id -ne 'garage-open') {
        throw 'The notification cmdlet produced the wrong action payload.'
    }

    $updatedLabel = Set-HomeAssistantLabel -LabelId ' security ' -ClearColor -Description 'Safety devices' -Confirm:$false
    if ($updatedLabel.LabelId -ne 'security' -or $null -ne $updatedLabel.Color) { throw 'The label update contract was not returned.' }
    $updatedCategory = Set-HomeAssistantCategory -Scope ' automation ' -CategoryId ' comfort ' -ClearIcon -Confirm:$false
    if ($updatedCategory.CategoryId -ne 'comfort' -or $null -ne $updatedCategory.Icon) { throw 'The category update contract was not returned.' }
    $null = Set-HomeAssistantCategory -Scope automation -Name Comfort -ClearIcon:$false -WhatIf
    $null = Set-HomeAssistantLabel -Name Security -ClearColor:$false -ClearDescription:$false -ClearIcon:$false -WhatIf
    $null = Set-HomeAssistantCalendarEvent -EntityId calendar.home -Summary Holiday -StartDate 2026-08-27 -EndDate 2026-08-28 -Confirm:$false
    $null = Set-HomeAssistantEnergy -DeviceConsumptionJson '[]' -WhatIf
    $null = Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitOfMeasurement MWh -WhatIf
    $null = Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum 1.5 -StartTime '2026-08-26T00:00:00Z' -Unit kWh -WhatIf
    $null = Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitOfMeasurement '' -WhatIf
    $null = Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -ChangeUnit -OldUnit '' -NewUnit kWh -WhatIf
    $null = Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum 1.5 -StartTime '2026-08-26T00:00:00Z' -Unit '' -WhatIf
    $null = Remove-HomeAssistantStatistic sensor.grid_energy -WhatIf
    $null = Invoke-HomeAssistantRecorderMaintenance -Purge -KeepDays 30 -WhatIf
    $null = Invoke-HomeAssistantRecorderMaintenance -RefreshStatisticsIssues -Confirm:$false
    $null = Set-HomeAssistantCamera camera.front -PreloadStream $false -WhatIf
    $null = Set-HomeAssistantDashboard -ConfigurationJson '{"views":[]}' -WhatIf
    $null = Set-HomeAssistantAutomation morning-routine '{"alias":"Morning","triggers":[],"actions":[]}' -WhatIf
    $null = Remove-HomeAssistantAutomation morning-routine -WhatIf
    $null = Remove-HomeAssistantDashboard -Configuration -UrlPath house-main -WhatIf

    $null = Set-HomeAssistantStatistic -StatisticId ' sensor.grid_energy ' -UnitOfMeasurement MWh -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_RECORDER_METADATA_UPDATE')
    $server.StandardInput.Flush()
    $metadataUpdate = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($metadataUpdate.unit_class -ne 'energy' -or $metadataUpdate.unit_of_measurement -ne 'MWh') {
        throw 'The statistics metadata update did not preserve the existing unit class.'
    }

    $null = Set-HomeAssistantStatistic -StatisticId ' sensor.grid_energy ' -UnitClass power -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_RECORDER_METADATA_UPDATE')
    $server.StandardInput.Flush()
    $metadataUpdate = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($metadataUpdate.unit_class -ne 'power' -or $metadataUpdate.unit_of_measurement -ne 'kWh') {
        throw 'The statistics metadata update did not preserve the existing unit of measurement.'
    }

    $importMetadata = [HomeAssistantX.Recorder.HomeAssistantStatisticImportMetadata]::new()
    $importMetadata.StatisticId = ' external:daily_energy '
    $importMetadata.Source = ' external '
    $importMetadata.HasMean = $false
    $importMetadata.HasSum = $true
    $importMetadata.MeanType = [HomeAssistantX.Recorder.HomeAssistantStatisticMeanType]::None
    $importMetadata.UnitClass = 'energy'
    $importMetadata.UnitOfMeasurement = 'kWh'
    $importRows = 1, 2 | ForEach-Object {
        $row = [HomeAssistantX.Recorder.HomeAssistantStatisticImportRow]::new()
        $row.Start = [DateTimeOffset]::Parse("2026-08-26T0$($_):00:00Z")
        $row.Sum = [double] $_
        $row
    }
    $importRows | Set-HomeAssistantStatistic -ImportMetadata $importMetadata -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_RECORDER_IMPORT')
    $server.StandardInput.Flush()
    $importCommand = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($importCommand.stats.Count -ne 2 -or $importCommand.metadata.has_mean -ne $false -or $importCommand.metadata.statistic_id -ne 'external:daily_energy' -or $importCommand.metadata.source -ne 'external') {
        throw 'Piped statistics rows were not imported as one complete batch.'
    }

    $reusedImportRow = [HomeAssistantX.Recorder.HomeAssistantStatisticImportRow]::new()
    & {
        $reusedImportRow.Start = [DateTimeOffset]::Parse('2026-08-26T03:00:00Z')
        $reusedImportRow.Sum = 3
        Write-Output $reusedImportRow
        $reusedImportRow.Start = [DateTimeOffset]::Parse('2026-08-26T04:00:00Z')
        $reusedImportRow.Sum = 4
        Write-Output $reusedImportRow
    } | Set-HomeAssistantStatistic -ImportMetadata $importMetadata -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_RECORDER_IMPORT')
    $server.StandardInput.Flush()
    $reusedImportCommand = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($reusedImportCommand.stats.Count -ne 2 -or $reusedImportCommand.stats[0].sum -ne 3 -or $reusedImportCommand.stats[1].sum -ne 4) {
        throw 'Piped statistics rows were not snapshotted when each pipeline record arrived.'
    }

    $recorderPreview = @(
        Remove-HomeAssistantStatistic sensor.grid_energy -WhatIf 6>&1
        Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitOfMeasurement MWh -WhatIf 6>&1
        $importRows | Set-HomeAssistantStatistic -ImportMetadata $importMetadata -WhatIf 6>&1
    ) | Out-String
    if ($recorderPreview.Contains('sensor.grid_energy') -or $recorderPreview.Contains('external:daily_energy')) {
        throw 'Recorder WhatIf output exposed statistic identifiers.'
    }

    $invalidImportSource = [HomeAssistantX.Recorder.HomeAssistantStatisticImportMetadata]::new()
    $invalidImportSource.StatisticId = 'external:source_mismatch'
    $invalidImportSource.Source = 'homeassistantx'
    $invalidImportSource.HasSum = $true
    $invalidImportSource.MeanType = [HomeAssistantX.Recorder.HomeAssistantStatisticMeanType]::None
    $invalidImportTime = [HomeAssistantX.Recorder.HomeAssistantStatisticImportMetadata]::new()
    $invalidImportTime.StatisticId = 'external:unaligned'
    $invalidImportTime.Source = 'external'
    $invalidImportTime.HasSum = $true
    $invalidImportTime.MeanType = [HomeAssistantX.Recorder.HomeAssistantStatisticMeanType]::None
    $alignedImportRow = [HomeAssistantX.Recorder.HomeAssistantStatisticImportRow]::new()
    $alignedImportRow.Start = [DateTimeOffset]::Parse('2026-08-26T01:00:00Z')
    $alignedImportRow.Sum = 1
    $unalignedImportRow = [HomeAssistantX.Recorder.HomeAssistantStatisticImportRow]::new()
    $unalignedImportRow.Start = [DateTimeOffset]::Parse('2026-08-26T01:01:00Z')
    $unalignedImportRow.Sum = 1
    $invalidRangeMetadata = [HomeAssistantX.Recorder.HomeAssistantStatisticImportMetadata]::new()
    $invalidRangeMetadata.StatisticId = 'external:invalid_range'
    $invalidRangeMetadata.Source = 'external'
    $invalidRangeMetadata.HasMean = $true
    $invalidRangeMetadata.MeanType = [HomeAssistantX.Recorder.HomeAssistantStatisticMeanType]::Arithmetic
    $invalidRangeRow = [HomeAssistantX.Recorder.HomeAssistantStatisticImportRow]::new()
    $invalidRangeRow.Start = [DateTimeOffset]::Parse('2026-08-26T01:00:00Z')
    $invalidRangeRow.Mean = 5
    $invalidRangeRow.Minimum = 10
    $invalidRangeRow.Maximum = 1

    foreach ($invalidPlatformData in @(
        { Set-HomeAssistantLabel -LabelId security -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCategory -Scope automation -CategoryId comfort -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantLabel -LabelId ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantLabel -LabelId ' ' -Description test -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantCategory -Scope ' ' -CategoryId comfort -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantCategory -Scope automation -CategoryId ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCategory -Scope ' ' -Name Comfort -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCategory -Scope automation -CategoryId ' ' -Icon mdi:test -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantLabel -Name Security -ClearColor -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCategory -Scope automation -Name Comfort -ClearIcon -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCalendarEvent -EntityId calendar.home -Summary Invalid -StartDate 2026-08-27 -EndDate 2026-08-27 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCalendarEvent -EntityId calendar.home -Uid event-1 -RecurrenceRange THISANDFUTURE -Summary Invalid -StartDate 2026-08-27 -EndDate 2026-08-28 -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantCalendarEvent -EntityId calendar.home -Uid event-1 -RecurrenceId 20260827 -RecurrenceRange THIS -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCalendarEvent -EntityId calendar.Home -Summary Invalid -StartDate 2026-08-27 -EndDate 2026-08-28 -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantCalendarEvent -EntityId calendar.Home -Uid event-1 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantEnergy -DeviceConsumptionJson '{}' -WhatIf -ErrorAction Stop },
        { $alignedImportRow | Set-HomeAssistantStatistic -ImportMetadata $invalidImportSource -WhatIf -ErrorAction Stop },
        { $unalignedImportRow | Set-HomeAssistantStatistic -ImportMetadata $invalidImportTime -WhatIf -ErrorAction Stop },
        { $invalidRangeRow | Set-HomeAssistantStatistic -ImportMetadata $invalidRangeMetadata -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum ([double]::NaN) -StartTime '2026-08-26T00:00:00Z' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.missing -UnitOfMeasurement kWh -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.missing -UnitClass energy -UnitOfMeasurement kWh -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitOfMeasurement kWh -WhatIf -ErrorAction Stop },
        { Remove-HomeAssistantStatistic ' ' -WhatIf -ErrorAction Stop },
        { Invoke-HomeAssistantRecorderMaintenance -PurgeEntities -WhatIf -ErrorAction Stop }
        { Invoke-HomeAssistantRecorderMaintenance -PurgeEntities -EntityId sensor.Kitchen -WhatIf -ErrorAction Stop }
        { Invoke-HomeAssistantRecorderMaintenance -PurgeEntities -Domain SENSOR -WhatIf -ErrorAction Stop }
        { Invoke-HomeAssistantRecorderMaintenance -PurgeEntities -Domain ' ' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantCamera camera.front -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantCamera camera.Front -PreloadStream $true -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -ConfigurationJson '[]' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -ConfigurationJson '{"views":[],"views":[{}]}' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -New -UrlPath house -Title House -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -New -UrlPath house-main -Title ' ' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -New -UrlPath ' ' -Title House -AllowSingleWord -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -New -UrlPath house-main -Title House -Icon home -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -DashboardId ' ' -Title House -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -DashboardId house-main -Title ' ' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -DashboardId house-main -Icon home -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -NewResource -ResourceUrl ' ' -ResourceType Module -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -ResourceId ' ' -ResourceUrl /local/card.js -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantDashboard -ConfigurationJson '{}' -UrlPath ' ' -WhatIf -ErrorAction Stop }
        { Remove-HomeAssistantDashboard ' ' -WhatIf -ErrorAction Stop }
        { Remove-HomeAssistantDashboard -ResourceId ' ' -WhatIf -ErrorAction Stop }
        { Remove-HomeAssistantDashboard -Configuration -UrlPath ' ' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantAutomation morning-routine '[]' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantAutomation morning-routine '{"id":"other-routine","alias":"Morning"}' -WhatIf -ErrorAction Stop }
        { Set-HomeAssistantAutomation morning-routine '{"id":"morning-routine","alias":"First","alias":"Second"}' -WhatIf -ErrorAction Stop }
    )) {
        $rejected = $false
        try { $null = & $invalidPlatformData } catch { $rejected = $true }
        if (-not $rejected) { throw 'Invalid platform-data input was accepted under WhatIf.' }
    }

    $server.StandardInput.WriteLine('CLEAR_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_CLEARED') {
        throw 'Could not reset the action baseline after notification validation.'
    }

    $actionCmdlet = [HomeAssistantX.PowerShell.InvokeHomeAssistantActionCommand]::new()
    $actionCmdlet.Connection = $connection.PSObject.BaseObject
    $describeAction = $actionCmdlet.GetType().GetMethod('DescribeAction', [Reflection.BindingFlags]'NonPublic, Instance')
    $serviceCallType = $describeAction.GetParameters()[0].ParameterType
    $createServiceCall = $serviceCallType.GetMethod('Create', [Reflection.BindingFlags]'Public, Static')
    $standardAction = $describeAction.Invoke($actionCmdlet, @($createServiceCall.Invoke($null, @('light', 'turn_on'))))
    $customAction = $describeAction.Invoke($actionCmdlet, @($createServiceCall.Invoke($null, @('private_domain', 'private_action'))))
    if (-not $standardAction.Contains('light.turn_on')) {
        throw 'WhatIf output did not identify a validated standard Home Assistant action.'
    }
    if ($customAction.Contains('private_domain') -or $customAction.Contains('private_action')) {
        throw 'WhatIf output exposed a custom Home Assistant service name.'
    }
    $null = $connection | Invoke-HomeAssistantAction -Domain light -Action turn_on -EntityId light.kitchen -WhatIf
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
        throw 'WhatIf invoked the Home Assistant action.'
    }

    $null = $connection | Invoke-HomeAssistantAction -Domain light -Action turn_on -EntityId light.kitchen -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    $serviceCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($serviceCall.domain -ne 'light' -or $serviceCall.service -ne 'turn_on') {
        throw 'The generic action cmdlet produced the wrong Home Assistant action.'
    }
    if (@($serviceCall.target.entity_id)[0] -ne 'light.kitchen') {
        throw 'The entity target parameter set produced the wrong target.'
    }

    $server.StandardInput.WriteLine('CLEAR_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_CLEARED') {
        throw 'Could not reset the typed-control action baseline.'
    }
    $null = Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -WhatIf
    $null = Set-HomeAssistantLight -Label Security -Power On -WhatIf
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
        throw 'WhatIf invoked the typed light control.'
    }

    $invalidClimateRejected = $false
    try {
        $null = Set-HomeAssistantClimate -Area Kitchen -TargetTemperatureLow 18 -Confirm:$false -ErrorAction Stop
    } catch {
        $invalidClimateRejected = $true
    }
    if (-not $invalidClimateRejected) {
        throw 'The climate cmdlet accepted an incomplete target temperature range.'
    }

    $nonFiniteClimateRejected = $false
    try {
        $null = Set-HomeAssistantClimate -Area Kitchen -Temperature ([double]::NaN) -WhatIf -ErrorAction Stop
    } catch {
        $nonFiniteClimateRejected = $true
    }
    if (-not $nonFiniteClimateRejected) {
        throw 'The climate cmdlet accepted a non-finite temperature under WhatIf.'
    }

    $invalidMediaRejected = $false
    try {
        $null = Set-HomeAssistantMediaPlayer -Area Kitchen -Power Off -Playback Play -Confirm:$false -ErrorAction Stop
    } catch {
        $invalidMediaRejected = $true
    }
    if (-not $invalidMediaRejected) {
        throw 'The media-player cmdlet accepted contradictory power and playback operations.'
    }

    try {
        $null = Set-HomeAssistantMediaPlayer -Area Kitchen -MediaContentId test -MediaContentType music -Enqueue Add -Announce:$false -WhatIf -ErrorAction Stop
    } catch {
        if ($_.Exception.Message -notlike "*no 'media_player' entities*") { throw }
    }
    try {
        $null = Invoke-HomeAssistantRemote -Area Kitchen -Action LearnCommand -Command Power -WhatIf -ErrorAction Stop
    } catch {
        if ($_.Exception.Message -notlike "*no 'remote' entities*") { throw }
    }

    foreach ($invalidMedia in @(
        { Set-HomeAssistantMediaPlayer -Area Kitchen -MediaContentId test -MediaContentType music -Enqueue Add -Announce -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent 30 -VolumeStep Up -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -MediaExtra @{ provider = 'value' } -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent 30 -MediaContentId ' ' -MediaContentType music -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent 30 -MediaContentId test -MediaContentType ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent ([double]::NaN) -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -JoinMember light.kitchen -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -JoinMember 'media_player.' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -JoinMember 'media_player.kitchen.extra' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -JoinMember 'media_player.Kitchen' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -JoinMember 'MEDIA_PLAYER.kitchen' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent 30 -SeekSeconds ([TimeSpan]::MaxValue.TotalSeconds) -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -MediaContentId test -MediaContentType music -MediaExtra @{ 1 = 'value' } -WhatIf -ErrorAction Stop }
    )) {
        $invalidMediaShapeRejected = $false
        try {
            $null = & $invalidMedia
        } catch {
            $invalidMediaShapeRejected = $true
        }
        if (-not $invalidMediaShapeRejected) {
            throw 'The media-player cmdlet accepted an invalid typed operation shape under WhatIf.'
        }
    }

    foreach ($invalidRemote in @(
        { Invoke-HomeAssistantRemote -Area Kitchen -Action 99 -WhatIf -ErrorAction Stop },
        { Invoke-HomeAssistantRemote -Area Kitchen -Action SendCommand -WhatIf -ErrorAction Stop },
        { Invoke-HomeAssistantRemote -Area Kitchen -Action LearnCommand -Command Power -TimeoutSeconds 1e-10 -WhatIf -ErrorAction Stop },
        { Invoke-HomeAssistantRemote -Area Kitchen -Action DeleteCommand -Command Power -TimeoutSeconds 10 -WhatIf -ErrorAction Stop },
        { Invoke-HomeAssistantRemote -Area Kitchen -Action LearnCommand -Command Power -TimeoutSeconds 30 -WhatIf -ErrorAction Stop }
    )) {
        $invalidRemoteShapeRejected = $false
        try {
            $null = & $invalidRemote
        } catch {
            $invalidRemoteShapeRejected = $true
        }
        if (-not $invalidRemoteShapeRejected) {
            throw 'The remote cmdlet accepted an invalid typed operation shape under WhatIf.'
        }
    }

    $server.StandardInput.WriteLine('CLEAR_LAST_RECORDER_METADATA_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'RECORDER_METADATA_LIST_CLEARED') {
        throw 'Could not establish the Recorder metadata-list command baseline.'
    }

    foreach ($invalidControl in @(
        { Set-HomeAssistantLight -Area Kitchen -ColorTemperatureKelvin 3000 -RgbColor 10, 20, 30 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantLock -Area Kitchen -Action 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCover -Area Kitchen -Action 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Power 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Playback 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumeStep 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Repeat 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Source ' ' -VolumeStep Up -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -SoundMode ' ' -VolumeStep Up -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Enqueue 99 -MediaContentId test -MediaContentType music -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantClimate -Area Kitchen -Temperature 21 -HvacMode ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantClimate -Area Kitchen -FanMode ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -ChangeUnit -OldUnit ' ' -NewUnit MWh -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -ChangeUnit -OldUnit kWh -NewUnit ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum 1 -StartTime ([DateTimeOffset]::UtcNow) -Unit ' ' -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum 0 -StartTime ([DateTimeOffset]::UtcNow) -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitClass ' ' -UnitOfMeasurement kWh -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitClass Energy -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -UnitClass ' energy ' -WhatIf -ErrorAction Stop },
        { $invalidImportUnit = [HomeAssistantX.Recorder.HomeAssistantStatisticImportMetadata]::new(); $invalidImportUnit.StatisticId = 'external:blank_unit'; $invalidImportUnit.Source = 'external'; $invalidImportUnit.HasSum = $true; $invalidImportUnit.UnitOfMeasurement = ' '; $alignedImportRow | Set-HomeAssistantStatistic -ImportMetadata $invalidImportUnit -WhatIf -ErrorAction Stop },
        { Install-HomeAssistantUpdate -EntityId light.kitchen -WhatIf -ErrorAction Stop }
        { Install-HomeAssistantUpdate -EntityId update.home_assistant_core_update -Version ' ' -WhatIf -ErrorAction Stop }
        { Install-HomeAssistantUpdate -Core -Version ' ' -WhatIf -ErrorAction Stop }
        { Install-HomeAssistantUpdate -EntityId update.Kitchen -WhatIf -ErrorAction Stop }
        { Install-HomeAssistantUpdate -EntityId UPDATE.kitchen -WhatIf -ErrorAction Stop }
        { Install-HomeAssistantUpdate -App '..' -WhatIf -ErrorAction Stop }
        { Invoke-HomeAssistantApp -App '../bad' -Action Restart -WhatIf -ErrorAction Stop }
        { Invoke-HomeAssistantApp -App test_app -Action 99 -WhatIf -ErrorAction Stop }
        { Restart-HomeAssistant -App '../bad' -WhatIf -ErrorAction Stop }
        { Invoke-HomeAssistantAction -Domain light -Action turn_on -EntityId _light.kitchen -WhatIf -ErrorAction Stop }
        { Receive-HomeAssistantEvent -EntityId LIGHT.kitchen -Count 1 -TimeoutSeconds 1 -ErrorAction Stop }
    )) {
        $invalidEnumRejected = $false
        try {
            $null = & $invalidControl
        } catch [System.Management.Automation.CommandNotFoundException] {
            throw "A validation contract referenced an unavailable command: $($_.Exception.Message)"
        } catch {
            $invalidEnumRejected = $true
        }
        if (-not $invalidEnumRejected) {
            throw 'A typed operation accepted invalid input under WhatIf.'
        }
    }

    $server.StandardInput.WriteLine('GET_LAST_RECORDER_METADATA_LIST')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'RECORDER_METADATA_LIST_NONE') {
        throw 'Invalid Recorder metadata input queried Home Assistant before local preflight completed.'
    }

    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
        throw 'Invalid typed-control input invoked a Home Assistant action.'
    }

    $null = Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    $typedLightCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if ($typedLightCall.domain -ne 'light' -or $typedLightCall.service -ne 'turn_on' -or $typedLightCall.service_data.brightness_pct -ne 45) {
        throw 'The typed light cmdlet produced the wrong action payload.'
    }
    if (@($typedLightCall.target.area_id)[0] -ne 'kitchen') {
        throw 'The typed light cmdlet did not resolve the friendly area name.'
    }

    $null = $kitchenLights | Set-HomeAssistantLight -Power Off -Confirm:$false
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    $pipelineLightCall = $server.StandardOutput.ReadLine() | ConvertFrom-Json
    if (@($pipelineLightCall.target.entity_id)[0] -ne 'light.kitchen' -or $pipelineLightCall.service -ne 'turn_off') {
        throw 'The typed light cmdlet did not accept joined entity pipeline input.'
    }

    $server.StandardInput.WriteLine('CLEAR_LAST_SUPERVISOR_COMMAND')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SUPERVISOR_COMMAND_CLEARED') {
        throw 'Could not establish the Supervisor command baseline.'
    }

    $null = $connection | Restart-HomeAssistant -Core -WhatIf
    $server.StandardInput.WriteLine('GET_LAST_SUPERVISOR_COMMAND')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SUPERVISOR_COMMAND_NONE') {
        throw 'WhatIf invoked the Core restart.'
    }

    $server.StandardInput.WriteLine('PAUSE_NEXT_SUBSCRIPTION')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'PAUSE_CONFIGURED') {
        throw 'Could not configure the event-subscription proof.'
    }
    $eventJob = Start-Job -ScriptBlock {
        param($ModuleAssembly, $HomeAssistantUri)
        Import-Module -Name $ModuleAssembly -Force -ErrorAction Stop
        $unexpectedDefault = $false
        try {
            $null = Get-HomeAssistantConnection -ErrorAction Stop
            $unexpectedDefault = $true
        } catch {
        }
        if ($unexpectedDefault) {
            throw 'A default Home Assistant connection leaked into another runspace.'
        }
        $eventConnection = Connect-HomeAssistant -Uri $HomeAssistantUri -AccessToken 'test-access-token'
        try {
            Receive-HomeAssistantEvent -EntityId light.kitchen -Count 1 -TimeoutSeconds 10
        } finally {
            Disconnect-HomeAssistant
        }
    } -ArgumentList $resolvedModulePath, $uri.AbsoluteUri
    try {
        $server.StandardInput.WriteLine('WAIT_FOR_PAUSED_SUBSCRIPTION')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SUBSCRIPTION_PAUSED') {
            throw 'The event receiver did not establish a WebSocket subscription.'
        }
        $releaseTimer = [Diagnostics.Stopwatch]::StartNew()
        $server.StandardInput.WriteLine('RELEASE_PAUSED_SUBSCRIPTION')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SUBSCRIPTION_RELEASED') {
            throw 'The event subscription could not be released.'
        }
        $server.StandardInput.WriteLine('GET_LAST_EVENT_SUBSCRIPTION')
        $server.StandardInput.Flush()
        $subscriptionCommand = $server.StandardOutput.ReadLine() | ConvertFrom-Json
        if ($subscriptionCommand.type -ne 'subscribe_events' -or $subscriptionCommand.event_type -ne 'state_changed') {
            throw 'The event cmdlet did not establish the expected state-change subscription.'
        }
        $server.StandardInput.WriteLine('PUBLISH_STATE_CHANGE')
        $server.StandardInput.Flush()
        $publishResult = $server.StandardOutput.ReadLine()
        if ($publishResult -ne 'STATE_CHANGE_PUBLISHED 1') {
            $server.StandardInput.WriteLine('GET_UNSUBSCRIBE_COUNT')
            $server.StandardInput.Flush()
            $unsubscribeCount = $server.StandardOutput.ReadLine()
            throw "The loopback state change did not reach exactly one active subscription. Received: $publishResult; unsubscribe count: $unsubscribeCount; release-to-publish: $($releaseTimer.ElapsedMilliseconds) ms"
        }
        $null = Wait-Job -Job $eventJob -Timeout 15
        if ($eventJob.State -ne 'Completed') {
            throw "The event receiver did not complete after one matching event. State: $($eventJob.State)"
        }
        $receivedEvents = @(Receive-Job -Job $eventJob -ErrorAction Stop)
        if ($receivedEvents.Count -ne 1 -or $receivedEvents[0].EventType -ne 'state_changed') {
            $eventTypes = @($receivedEvents | ForEach-Object { $_.EventType }) -join ', '
            throw "Receive-HomeAssistantEvent returned $($receivedEvents.Count) records with event types [$eventTypes]."
        }
    } finally {
        Stop-Job -Job $eventJob -ErrorAction SilentlyContinue
        Remove-Job -Job $eventJob -Force -ErrorAction SilentlyContinue
    }

    $cleanupJob = Start-Job -ScriptBlock {
        param($ModuleAssembly, $HomeAssistantUri)
        Import-Module -Name $ModuleAssembly -Force -ErrorAction Stop
        $replacedConnection = Connect-HomeAssistant -Uri $HomeAssistantUri -AccessToken 'test-access-token'
        $cleanupConnection = Connect-HomeAssistant -Uri $HomeAssistantUri -AccessToken 'test-access-token'
        if (-not $replacedConnection.IsDisposed -or -not [object]::ReferenceEquals($cleanupConnection, (Get-HomeAssistantConnection))) {
            throw 'Replacing the runspace default did not dispose the previous default cleanly.'
        }
        $moduleName = (Get-Command -Name Connect-HomeAssistant -ErrorAction Stop).ModuleName
        Remove-Module -Name $moduleName -Force -ErrorAction Stop
        $cleanupConnection.IsDisposed
    } -ArgumentList $resolvedModulePath, $uri.AbsoluteUri
    try {
        $null = Wait-Job -Job $cleanupJob -Timeout 15
        if ($cleanupJob.State -ne 'Completed') {
            throw "The module cleanup contract did not complete. State: $($cleanupJob.State)"
        }
        $cleanupResult = @(Receive-Job -Job $cleanupJob -ErrorAction Stop)
        if ($cleanupResult.Count -ne 1 -or $cleanupResult[0] -ne $true) {
            throw 'Removing the binary module did not dispose its runspace default connection.'
        }
    } finally {
        Stop-Job -Job $cleanupJob -ErrorAction SilentlyContinue
        Remove-Job -Job $cleanupJob -Force -ErrorAction SilentlyContinue
    }

    "PASS $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)"
} finally {
    if ($null -ne $connection) {
        Disconnect-HomeAssistant
    }
    if (-not $server.HasExited) {
        $server.StandardInput.WriteLine('EXIT')
        $server.StandardInput.Flush()
        if (-not $server.WaitForExit(5000)) {
            $server.Kill()
        }
    }
    $server.Dispose()
}
