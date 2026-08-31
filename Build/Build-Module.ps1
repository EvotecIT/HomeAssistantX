param(
    [Alias('ConfigurationGateMode')]
    [ValidateSet('Manifest', 'Build', 'Publish')]
    [string] $RunMode = 'Build',

    [bool] $SignModule = $false,

    [string] $PowerShellGalleryApiKeyPath = 'C:\Support\Important\PowerShellGalleryAPI.txt',

    [string] $GitHubApiKeyPath = 'C:\Support\Important\GitHubAPI.txt'
)

Import-Module PSPublishModule -MinimumVersion 3.0.55 -Force -ErrorAction Stop

Build-Module -ModuleName 'HomeAssistantX' -RunMode $RunMode {
    $manifest = [ordered] @{
        PowerShellVersion    = '5.1'
        CompatiblePSEditions = @('Desktop', 'Core')
        GUID                 = '9c949e39-7bcb-41a2-ab01-ca4e6fe1dc27'
        ModuleVersion        = '0.1.X'
        Author               = 'Przemyslaw Klys'
        CompanyName          = 'Evotec'
        Copyright            = "(c) 2026 - $((Get-Date).Year) Przemyslaw Klys @ Evotec. All rights reserved."
        Description          = 'Task-oriented PowerShell access to Home Assistant Core and Supervisor through HomeAssistantX.'
        Tags                 = @('HomeAssistant', 'SmartHome', 'Automation', 'REST', 'WebSocket', 'Windows', 'macOS', 'Linux')
        ProjectUri           = 'https://github.com/EvotecIT/HomeAssistantX'
        LicenseUri           = 'https://github.com/EvotecIT/HomeAssistantX/blob/main/LICENSE'
    }
    New-ConfigurationManifest @manifest
    New-ConfigurationModule -Type ExternalModule -Name 'Microsoft.PowerShell.Management', 'Microsoft.PowerShell.Utility'
    New-ConfigurationDocumentation -Enable -PathReadme 'Docs\README.md' -Path 'Docs' -SyncExternalHelpToProjectRoot
    New-ConfigurationImportModule -ImportSelf

    $build = @{
        Enable                                  = $true
        SignModule                              = $SignModule
        MergeModuleOnBuild                      = $true
        ResolveBinaryConflicts                  = $true
        ResolveBinaryConflictsName              = 'HomeAssistantX.PowerShell'
        NETProjectName                          = 'HomeAssistantX.PowerShell'
        NETProjectPath                          = 'HomeAssistantX.PowerShell\HomeAssistantX.PowerShell.csproj'
        NETConfiguration                        = 'Release'
        NETFramework                            = 'netstandard2.0', 'net10.0', 'net472'
        NETHandleAssemblyWithSameName           = $true
        NETAssemblyLoadContext                  = $true
        NETAssemblyTypeAcceleratorMode          = 'AllowList'
        NETAssemblyTypeAccelerators             = @(
            'HomeAssistantX.PowerShell.HomeAssistantConnection'
            'HomeAssistantX.PowerShell.HomeAssistantLogLine'
            'HomeAssistantX.PowerShell.HomeAssistantAppAction'
            'HomeAssistantX.Inventory.HomeAssistantEntityInfo'
            'HomeAssistantX.Services.HomeAssistantActionDefinition'
            'HomeAssistantX.Controls.HomeAssistantPowerAction'
            'HomeAssistantX.Controls.HomeAssistantCoverAction'
            'HomeAssistantX.Controls.HomeAssistantMediaPlaybackAction'
            'HomeAssistantX.Controls.HomeAssistantMediaVolumeStepAction'
            'HomeAssistantX.Controls.HomeAssistantMediaRepeatMode'
            'HomeAssistantX.Controls.HomeAssistantMediaEnqueueMode'
            'HomeAssistantX.Controls.HomeAssistantRemoteCommandType'
            'HomeAssistantX.PowerShell.HomeAssistantRemoteAction'
            'HomeAssistantX.Controls.HomeAssistantLockAction'
            'HomeAssistantX.Controls.HomeAssistantAlarmAction'
            'HomeAssistantX.Controls.HomeAssistantButtonDomain'
            'HomeAssistantX.Controls.HomeAssistantFanAction'
            'HomeAssistantX.Controls.HomeAssistantFanDirection'
            'HomeAssistantX.Controls.HomeAssistantHelperDomain'
            'HomeAssistantX.Controls.HomeAssistantHumidifierAction'
            'HomeAssistantX.Controls.HomeAssistantLawnMowerAction'
            'HomeAssistantX.Controls.HomeAssistantSirenAction'
            'HomeAssistantX.Controls.HomeAssistantVacuumAction'
            'HomeAssistantX.Controls.HomeAssistantValveAction'
            'HomeAssistantX.Controls.HomeAssistantWaterHeaterAction'
            'HomeAssistantX.PowerShell.HomeAssistantRoutineAction'
            'HomeAssistantX.Authentication.IHomeAssistantAccessTokenProvider'
            'HomeAssistantX.Authentication.StaticAccessTokenProvider'
            'HomeAssistantX.Operations.HomeAssistantCapabilityAvailability'
            'HomeAssistantX.Energy.HomeAssistantEnergyPeriod'
            'HomeAssistantX.Recorder.HomeAssistantStatisticKind'
            'HomeAssistantX.Recorder.HomeAssistantStatisticPeriod'
            'HomeAssistantX.Recorder.HomeAssistantStatisticType'
            'HomeAssistantX.Recorder.HomeAssistantStatisticMeanType'
            'HomeAssistantX.Recorder.HomeAssistantStatisticImportMetadata'
            'HomeAssistantX.Recorder.HomeAssistantStatisticImportRow'
            'HomeAssistantX.Weather.HomeAssistantWeatherForecastType'
            'HomeAssistantX.Weather.HomeAssistantWeatherFeature'
            'HomeAssistantX.Cameras.HomeAssistantCameraFeature'
            'HomeAssistantX.Cameras.HomeAssistantCameraOrientation'
            'HomeAssistantX.Dashboards.HomeAssistantDashboardResourceType'
        )
        DeleteTargetModuleBeforeBuild           = $true
        NETBinaryModuleDocumentation            = $true
        NETDevelopmentBinaries                  = $true
        NETDevelopmentBinariesMode              = 'Auto'
        NETDevelopmentBinariesPath              = 'HomeAssistantX.PowerShell\bin'
        NETDevelopmentSourceBootstrapperMode    = 'ReplaceSingleFile'
    }
    New-ConfigurationBuild @build

    New-ConfigurationProjectBuild -Name 'HomeAssistantX' -ConfigPath 'Build\project.build.json' -Enabled:$true -BuildBeforeModule -UseAsReleaseVersionSource -ProvideLocalNuGetFeed -PublishNuget
    New-ConfigurationRelease -StageRoot 'Artefacts\UploadReady' -VersionSource ProjectBuild -PrimaryProject 'HomeAssistantX' -SynchronizeModuleVersion -BuildOrder 'Packages', 'Module' -PublishOrder 'NuGet', 'PowerShellGallery', 'GitHub'

    New-ConfigurationArtefact -Type Unpacked -Enable -Path 'Artefacts\Unpacked' -ModulesPath 'Modules' -RequiredModulesPath 'Modules' -AddRequiredModules -CopyFilesRelative
    New-ConfigurationArtefact -Type Packed -Enable -Path 'Artefacts\Packed' -ModulesPath 'Modules' -RequiredModulesPath 'Modules' -AddRequiredModules -ArtefactName 'HomeAssistantX-PowerShellModule.<TagModuleVersionWithPreRelease>.zip' -IncludeTagName

    New-ConfigurationPublish -Type PowerShellGallery -FilePath $PowerShellGalleryApiKeyPath -Enabled:$false -UseAsDependencyVersionSource
    New-ConfigurationPublish -Type GitHub -FilePath $GitHubApiKeyPath -UserName 'EvotecIT' -RepositoryName 'HomeAssistantX' -Enabled:$false -GenerateReleaseNotes
    New-ConfigurationGate -Mode $RunMode
} -ExitCode
