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
    'Export-HomeAssistantDiagnostic',
    'Get-HomeAssistantAction',
    'Get-HomeAssistantApp',
    'Get-HomeAssistantArea',
    'Get-HomeAssistantBackup',
    'Get-HomeAssistantConnection',
    'Get-HomeAssistantDevice',
    'Get-HomeAssistantEntity',
    'Get-HomeAssistantFloor',
    'Get-HomeAssistantHistory',
    'Get-HomeAssistantInfo',
    'Get-HomeAssistantIntegration',
    'Get-HomeAssistantIssue',
    'Get-HomeAssistantJob',
    'Get-HomeAssistantLog',
    'Get-HomeAssistantTrace',
    'Get-HomeAssistantUpdate',
    'Install-HomeAssistantUpdate',
    'Invoke-HomeAssistantAction',
    'Invoke-HomeAssistantApp',
    'Invoke-HomeAssistantRemote',
    'New-HomeAssistantBackup',
    'Receive-HomeAssistantEvent',
    'Restart-HomeAssistant',
    'Set-HomeAssistantClimate',
    'Set-HomeAssistantCover',
    'Set-HomeAssistantLight',
    'Set-HomeAssistantLock',
    'Set-HomeAssistantMediaPlayer',
    'Set-HomeAssistantSwitch',
    'Test-HomeAssistantConfiguration'
)
$importedModuleName = (Get-Command -Name Connect-HomeAssistant -ErrorAction Stop).ModuleName
$actualCommands = @(Get-Command -Module $importedModuleName | Sort-Object -Property Name | Select-Object -ExpandProperty Name)
if (($actualCommands -join '|') -ne ($expectedCommands -join '|')) {
    throw "Unexpected command surface: $($actualCommands -join ', ')"
}

$parameterSetContracts = @{
    'Get-HomeAssistantLog'        = @('App', 'Core', 'Host', 'Legacy', 'Supervisor', 'SystemLog')
    'Get-HomeAssistantInfo'       = @('Capabilities', 'Health', 'Overview', 'Supervisor')
    'Install-HomeAssistantUpdate' = @('App', 'Core', 'Entity', 'OperatingSystem', 'Supervisor')
    'Invoke-HomeAssistantAction'  = @('Area', 'Data', 'Device', 'Entity', 'Floor', 'Label')
    'Invoke-HomeAssistantRemote'  = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
    'Restart-HomeAssistant'       = @('App', 'Core', 'Host', 'Integration', 'Supervisor')
    'Set-HomeAssistantClimate'    = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
    'Set-HomeAssistantCover'      = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
    'Set-HomeAssistantLight'      = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
    'Set-HomeAssistantLock'       = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
    'Set-HomeAssistantMediaPlayer' = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
    'Set-HomeAssistantSwitch'     = @('Area', 'Device', 'Entity', 'Floor', 'InputObject')
}
foreach ($entry in $parameterSetContracts.GetEnumerator()) {
    $sets = @((Get-Command -Name $entry.Key).ParameterSets.Name | Sort-Object)
    if (($sets -join '|') -ne ($entry.Value -join '|')) {
        throw "Unexpected parameter sets for $($entry.Key): $($sets -join ', ')"
    }
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

foreach ($name in 'Install-HomeAssistantUpdate', 'Invoke-HomeAssistantAction', 'Invoke-HomeAssistantApp', 'Invoke-HomeAssistantRemote', 'New-HomeAssistantBackup', 'Restart-HomeAssistant', 'Set-HomeAssistantClimate', 'Set-HomeAssistantCover', 'Set-HomeAssistantLight', 'Set-HomeAssistantLock', 'Set-HomeAssistantMediaPlayer', 'Set-HomeAssistantSwitch') {
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
    $server.StandardInput.WriteLine('GET_LAST_SERVICE_CALL')
    $server.StandardInput.Flush()
    if ($server.StandardOutput.ReadLine() -ne 'SERVICE_CALL_NONE') {
        throw 'A disposed provenance connection invoked a Home Assistant action.'
    }

    $info = Get-HomeAssistantInfo
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

    foreach ($invalidMedia in @(
        { Set-HomeAssistantMediaPlayer -Area Kitchen -MediaContentId test -MediaContentType music -Enqueue Add -Announce -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent 30 -VolumeStep Up -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -MediaExtra @{ provider = 'value' } -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumePercent ([double]::NaN) -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -JoinMember light.kitchen -WhatIf -ErrorAction Stop },
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

    foreach ($invalidControl in @(
        { Set-HomeAssistantLight -Area Kitchen -ColorTemperatureKelvin 3000 -RgbColor 10, 20, 30 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantLock -Area Kitchen -Action 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantCover -Area Kitchen -Action 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Power 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Playback 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -VolumeStep 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Repeat 99 -WhatIf -ErrorAction Stop },
        { Set-HomeAssistantMediaPlayer -Area Kitchen -Enqueue 99 -MediaContentId test -MediaContentType music -WhatIf -ErrorAction Stop },
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
        } catch {
            $invalidEnumRejected = $true
        }
        if (-not $invalidEnumRejected) {
            throw 'A typed operation accepted invalid input under WhatIf.'
        }
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
