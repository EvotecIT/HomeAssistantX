---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Invoke-HomeAssistantAction
## SYNOPSIS
Invokes any Home Assistant action with one target-oriented set of parameters.

## SYNTAX
### Data (Default)
```powershell
Invoke-HomeAssistantAction [-Domain] <string> [-Action] <string> -Connection <HomeAssistantConnection> [-Data <hashtable>] [-ReturnResponse] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Entity
```powershell
Invoke-HomeAssistantAction [-Domain] <string> [-Action] <string> -EntityId <string[]> -Connection <HomeAssistantConnection> [-Data <hashtable>] [-ReturnResponse] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Invoke-HomeAssistantAction [-Domain] <string> [-Action] <string> -DeviceId <string[]> -Connection <HomeAssistantConnection> [-Data <hashtable>] [-ReturnResponse] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Invoke-HomeAssistantAction [-Domain] <string> [-Action] <string> -AreaId <string[]> -Connection <HomeAssistantConnection> [-Data <hashtable>] [-ReturnResponse] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Invoke-HomeAssistantAction [-Domain] <string> [-Action] <string> -FloorId <string[]> -Connection <HomeAssistantConnection> [-Data <hashtable>] [-ReturnResponse] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Invoke-HomeAssistantAction [-Domain] <string> [-Action] <string> -LabelId <string[]> -Connection <HomeAssistantConnection> [-Data <hashtable>] [-ReturnResponse] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Invokes any Home Assistant action with one target-oriented set of parameters.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Invoke-HomeAssistantAction light turn_on -AreaId kitchen -Data @{ brightness_pct = 45 } -WhatIf
```

Uses the area parameter set and shows the action without changing devices.

## PARAMETERS

### -Action
Action name within the domain, such as turn_on. Service is an alias.

```yaml
Type: String
Parameter Sets: Data, Entity, Device, Area, Floor, Label
Aliases: Service
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AreaId
Targets one or more area identifiers.

```yaml
Type: String[]
Parameter Sets: Area
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
Parameter Sets: Data, Entity, Device, Area, Floor, Label
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Data
Action-specific data. Keys must be non-empty strings.

```yaml
Type: Hashtable
Parameter Sets: Data, Entity, Device, Area, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DeviceId
Targets one or more device identifiers.

```yaml
Type: String[]
Parameter Sets: Device
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Domain
Home Assistant action domain, such as light or climate.

```yaml
Type: String
Parameter Sets: Data, Entity, Device, Area, Floor, Label
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Targets one or more entity identifiers.

```yaml
Type: String[]
Parameter Sets: Entity
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FloorId
Targets one or more floor identifiers.

```yaml
Type: String[]
Parameter Sets: Floor
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LabelId
Targets one or more label identifiers.

```yaml
Type: String[]
Parameter Sets: Label
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ReturnResponse
Requests response data from actions that support it.

```yaml
Type: SwitchParameter
Parameter Sets: Data, Entity, Device, Area, Floor, Label
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

## RELATED LINKS

- None
