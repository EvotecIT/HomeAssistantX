---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantFan
## SYNOPSIS
Sets one fan action, speed, oscillation, direction, or preset.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantFan [-Entity] <string[]> [-Action <HomeAssistantFanAction>] [-Percentage <Int32>] [-Oscillating <Boolean>] [-Direction <HomeAssistantFanDirection>] [-PresetMode <string>] [-PercentageStep <Int32>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantFan -InputObject <HomeAssistantEntityInfo[]> [-Action <HomeAssistantFanAction>] [-Percentage <Int32>] [-Oscillating <Boolean>] [-Direction <HomeAssistantFanDirection>] [-PresetMode <string>] [-PercentageStep <Int32>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantFan [-Area] <string[]> [-Action <HomeAssistantFanAction>] [-Percentage <Int32>] [-Oscillating <Boolean>] [-Direction <HomeAssistantFanDirection>] [-PresetMode <string>] [-PercentageStep <Int32>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantFan [-Device] <string[]> [-Action <HomeAssistantFanAction>] [-Percentage <Int32>] [-Oscillating <Boolean>] [-Direction <HomeAssistantFanDirection>] [-PresetMode <string>] [-PercentageStep <Int32>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantFan [-Floor] <string[]> [-Action <HomeAssistantFanAction>] [-Percentage <Int32>] [-Oscillating <Boolean>] [-Direction <HomeAssistantFanDirection>] [-PresetMode <string>] [-PercentageStep <Int32>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Set-HomeAssistantFan [-Label] <string[]> [-Action <HomeAssistantFanAction>] [-Percentage <Int32>] [-Oscillating <Boolean>] [-Direction <HomeAssistantFanDirection>] [-PresetMode <string>] [-PercentageStep <Int32>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sets one fan action, speed, oscillation, direction, or preset.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantFan -Entity fan.office -Percentage 35 -WhatIf
```


## PARAMETERS

### -Action
Power or relative speed action.

```yaml
Type: HomeAssistantFanAction
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values: TurnOn, TurnOff, Toggle, IncreaseSpeed, DecreaseSpeed

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
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
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

### -Direction
Sets forward or reverse direction.

```yaml
Type: HomeAssistantFanDirection
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values: Forward, Reverse

Required: False
Position: named
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

### -Label
One or more label names or native label IDs.

```yaml
Type: String[]
Parameter Sets: Label
Aliases: LabelId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Oscillating
Enables or disables oscillation.

```yaml
Type: Boolean
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Percentage
Absolute fan speed from 0 through 100 percent.

```yaml
Type: Int32
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PercentageStep
Optional percentage step for IncreaseSpeed or DecreaseSpeed.

```yaml
Type: Int32
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PresetMode
Selects a preset mode supported by the target.

```yaml
Type: String
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
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
