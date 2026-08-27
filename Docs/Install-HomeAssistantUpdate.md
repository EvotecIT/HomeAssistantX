---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Install-HomeAssistantUpdate
## SYNOPSIS
Installs an update entity or a Supervisor-managed Core, OS, Supervisor, or app update.

## SYNTAX
### Entity (Default)
```powershell
Install-HomeAssistantUpdate [-EntityId] <string> [-Version <string>] [-Backup <Boolean>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Core
```powershell
Install-HomeAssistantUpdate -Core [-Version <string>] [-Backup <Boolean>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Supervisor
```powershell
Install-HomeAssistantUpdate -Supervisor [-Version <string>] [-Backup <Boolean>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### OperatingSystem
```powershell
Install-HomeAssistantUpdate -OperatingSystem [-Version <string>] [-Backup <Boolean>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### App
```powershell
Install-HomeAssistantUpdate -App <string> [-Version <string>] [-Backup <Boolean>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Installs an update entity or a Supervisor-managed Core, OS, Supervisor, or app update.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Install-HomeAssistantUpdate -Core -WhatIf
```

Shows the high-impact operation without installing the update.

## PARAMETERS

### -App
Supervisor app/add-on slug to update.

```yaml
Type: String
Parameter Sets: App
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Backup
Requests a backup before installation when the target supports it.

```yaml
Type: Boolean
Parameter Sets: Entity, Core, Supervisor, OperatingSystem, App
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Entity, Core, Supervisor, OperatingSystem, App
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Core
Installs a Home Assistant Core update through Supervisor.

```yaml
Type: SwitchParameter
Parameter Sets: Core
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Home Assistant update entity to install.

```yaml
Type: String
Parameter Sets: Entity
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OperatingSystem
Installs a Home Assistant OS update.

```yaml
Type: SwitchParameter
Parameter Sets: OperatingSystem
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Writes the action result to the pipeline.

```yaml
Type: SwitchParameter
Parameter Sets: Entity, Core, Supervisor, OperatingSystem, App
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Supervisor
Installs a Supervisor update.

```yaml
Type: SwitchParameter
Parameter Sets: Supervisor
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Version
Specific target version. Omit it to install the advertised latest version.

```yaml
Type: String
Parameter Sets: Entity, Core, Supervisor, OperatingSystem, App
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`
- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
