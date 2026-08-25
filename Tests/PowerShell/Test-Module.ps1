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
    'Get-HomeAssistantApp',
    'Get-HomeAssistantBackup',
    'Get-HomeAssistantEntity',
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
    'New-HomeAssistantBackup',
    'Receive-HomeAssistantEvent',
    'Restart-HomeAssistant',
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
    'Restart-HomeAssistant'       = @('App', 'Core', 'Host', 'Integration', 'Supervisor')
}
foreach ($entry in $parameterSetContracts.GetEnumerator()) {
    $sets = @((Get-Command -Name $entry.Key).ParameterSets.Name | Sort-Object)
    if (($sets -join '|') -ne ($entry.Value -join '|')) {
        throw "Unexpected parameter sets for $($entry.Key): $($sets -join ', ')"
    }
}

foreach ($name in 'Install-HomeAssistantUpdate', 'Invoke-HomeAssistantAction', 'Invoke-HomeAssistantApp', 'New-HomeAssistantBackup', 'Restart-HomeAssistant') {
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
    $info = $connection | Get-HomeAssistantInfo
    $entities = @($connection | Get-HomeAssistantEntity)
    $logs = @($connection | Get-HomeAssistantLog)
    $integrations = @($connection | Get-HomeAssistantIntegration)
    $apps = @($connection | Get-HomeAssistantApp)
    $backups = @($connection | Get-HomeAssistantBackup)
    $configuration = $connection | Test-HomeAssistantConfiguration

    if ($info.Version -ne '2026.8.3') { throw 'Core information was not returned.' }
    if ($entities.Count -ne 2) { throw 'Entity enumeration did not use the live loopback contract.' }
    if ($logs.Count -ne 1) { throw 'Structured system logs were not returned.' }
    if ($integrations.Count -ne 1) { throw 'Configuration entries were not returned.' }
    if ($apps.Count -ne 1) { throw 'Supervisor apps were not returned.' }
    if ($backups.Count -ne 1) { throw 'Supervisor backups were not returned.' }
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
        $eventConnection = Connect-HomeAssistant -Uri $HomeAssistantUri -AccessToken 'test-access-token'
        try {
            $eventConnection | Receive-HomeAssistantEvent -EntityId light.kitchen -Count 1 -TimeoutSeconds 10
        } finally {
            $eventConnection | Disconnect-HomeAssistant
        }
    } -ArgumentList $resolvedModulePath, $uri.AbsoluteUri
    try {
        $server.StandardInput.WriteLine('WAIT_FOR_PAUSED_SUBSCRIPTION')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SUBSCRIPTION_PAUSED') {
            throw 'The event receiver did not establish a WebSocket subscription.'
        }
        $server.StandardInput.WriteLine('RELEASE_PAUSED_SUBSCRIPTION')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'SUBSCRIPTION_RELEASED') {
            throw 'The event subscription could not be released.'
        }
        $server.StandardInput.WriteLine('PUBLISH_STATE_CHANGE')
        $server.StandardInput.Flush()
        if ($server.StandardOutput.ReadLine() -ne 'STATE_CHANGE_PUBLISHED') {
            throw 'The loopback state change was not published.'
        }
        $null = Wait-Job -Job $eventJob -Timeout 15
        if ($eventJob.State -ne 'Completed') {
            throw "The event receiver did not complete after one matching event. State: $($eventJob.State)"
        }
        $receivedEvents = @(Receive-Job -Job $eventJob -ErrorAction Stop)
        if ($receivedEvents.Count -ne 1 -or $receivedEvents[0].EventType -ne 'state_changed') {
            throw 'Receive-HomeAssistantEvent did not emit the matching state-change event.'
        }
    } finally {
        Stop-Job -Job $eventJob -ErrorAction SilentlyContinue
        Remove-Job -Job $eventJob -Force -ErrorAction SilentlyContinue
    }

    "PASS $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion)"
} finally {
    if ($null -ne $connection) {
        $connection | Disconnect-HomeAssistant
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
