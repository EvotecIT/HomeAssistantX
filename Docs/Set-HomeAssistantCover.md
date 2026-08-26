---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantCover
## SYNOPSIS
Moves covers with a typed action, position, or tilt position.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantCover [-Entity] <string[]> [-Action <HomeAssistantCoverAction>] [-PositionPercent <Double>] [-TiltPositionPercent <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantCover -InputObject <HomeAssistantEntityInfo[]> [-Action <HomeAssistantCoverAction>] [-PositionPercent <Double>] [-TiltPositionPercent <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantCover [-Area] <string[]> [-Action <HomeAssistantCoverAction>] [-PositionPercent <Double>] [-TiltPositionPercent <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantCover [-Device] <string[]> [-Action <HomeAssistantCoverAction>] [-PositionPercent <Double>] [-TiltPositionPercent <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantCover [-Floor] <string[]> [-Action <HomeAssistantCoverAction>] [-PositionPercent <Double>] [-TiltPositionPercent <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Moves covers with a typed action, position, or tilt position.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantCover -Entity cover.kitchen -PositionPercent 60 -WhatIf
```


## PARAMETERS

### -Action
Opens, closes, stops, or toggles the selected covers.

```yaml
Type: HomeAssistantCoverAction
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: Open, Close, Stop, Toggle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Area
One or more area names, aliases, or native area IDs. Room is an alias.

```yaml
Type: String[]
Parameter Sets: Area
Aliases: AreaId, Room
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Device
One or more device friendly names or native device IDs.

```yaml
Type: String[]
Parameter Sets: Device
Aliases: DeviceId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Entity
One or more entity friendly names or native entity IDs.

```yaml
Type: String[]
Parameter Sets: Entity
Aliases: EntityId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Floor
One or more floor names, aliases, or native floor IDs.

```yaml
Type: String[]
Parameter Sets: Floor
Aliases: FloorId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Joined entities accepted from Get-HomeAssistantEntity.

```yaml
Type: HomeAssistantEntityInfo[]
Parameter Sets: InputObject
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -PositionPercent
Cover position from 0 through 100 percent.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TiltPositionPercent
Cover tilt position from 0 through 100 percent.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor
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

- `HomeAssistantX.Inventory.HomeAssistantEntityInfo[]`
- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`

## RELATED LINKS

- None
