---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Restart-HomeAssistant
## SYNOPSIS
Restarts Core, Supervisor, host, an app, or reloads one integration.

## SYNTAX
### Core (Default)
```powershell
Restart-HomeAssistant -Connection <HomeAssistantConnection> [-Core] [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Supervisor
```powershell
Restart-HomeAssistant -Supervisor -Connection <HomeAssistantConnection> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Host
```powershell
Restart-HomeAssistant -HostSystem -Connection <HomeAssistantConnection> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### App
```powershell
Restart-HomeAssistant -App <string> -Connection <HomeAssistantConnection> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Integration
```powershell
Restart-HomeAssistant -IntegrationId <string> -Connection <HomeAssistantConnection> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Restarts Core, Supervisor, host, an app, or reloads one integration.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Restart-HomeAssistant -Core -WhatIf
```

Shows the restart target and requires no change while WhatIf is present.

## PARAMETERS

### -App
Restarts the specified Supervisor app/add-on.

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

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Core, Supervisor, Host, App, Integration
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Core
Restarts Home Assistant Core. This is the default target.

```yaml
Type: SwitchParameter
Parameter Sets: Core
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HostSystem
Reboots the Home Assistant host system. Host is an alias.

```yaml
Type: SwitchParameter
Parameter Sets: Host
Aliases: Host
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IntegrationId
Reloads the specified Home Assistant configuration entry.

```yaml
Type: String
Parameter Sets: Integration
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Writes the operation result to the pipeline.

```yaml
Type: SwitchParameter
Parameter Sets: Core, Supervisor, Host, App, Integration
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Supervisor
Restarts Supervisor.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `None`

## RELATED LINKS

- None
