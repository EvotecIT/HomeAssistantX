---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantClimate
## SYNOPSIS
Sets common climate values with typed parameters instead of raw action data.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantClimate [-Entity] <string[]> [-Temperature <Double>] [-TargetTemperatureLow <Double>] [-TargetTemperatureHigh <Double>] [-HvacMode <string>] [-FanMode <string>] [-PresetMode <string>] [-Humidity <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantClimate -InputObject <HomeAssistantEntityInfo[]> [-Temperature <Double>] [-TargetTemperatureLow <Double>] [-TargetTemperatureHigh <Double>] [-HvacMode <string>] [-FanMode <string>] [-PresetMode <string>] [-Humidity <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantClimate [-Area] <string[]> [-Temperature <Double>] [-TargetTemperatureLow <Double>] [-TargetTemperatureHigh <Double>] [-HvacMode <string>] [-FanMode <string>] [-PresetMode <string>] [-Humidity <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantClimate [-Device] <string[]> [-Temperature <Double>] [-TargetTemperatureLow <Double>] [-TargetTemperatureHigh <Double>] [-HvacMode <string>] [-FanMode <string>] [-PresetMode <string>] [-Humidity <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantClimate [-Floor] <string[]> [-Temperature <Double>] [-TargetTemperatureLow <Double>] [-TargetTemperatureHigh <Double>] [-HvacMode <string>] [-FanMode <string>] [-PresetMode <string>] [-Humidity <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Set-HomeAssistantClimate [-Label] <string[]> [-Temperature <Double>] [-TargetTemperatureLow <Double>] [-TargetTemperatureHigh <Double>] [-HvacMode <string>] [-FanMode <string>] [-PresetMode <string>] [-Humidity <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sets common climate values with typed parameters instead of raw action data.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantClimate -Entity climate.downstairs -Temperature 21.5 -HvacMode heat -WhatIf
```


## PARAMETERS

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

### -FanMode
Fan mode supported by the target.

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

### -Humidity
Target humidity from 0 through 100 percent.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HvacMode
HVAC mode supported by the target, such as heat, cool, or auto.

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

### -PresetMode
Preset mode supported by the target.

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

### -TargetTemperatureHigh
Upper target temperature for range-based HVAC modes.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TargetTemperatureLow
Lower target temperature for range-based HVAC modes.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Temperature
Target temperature in the entity's configured unit.

```yaml
Type: Double
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
