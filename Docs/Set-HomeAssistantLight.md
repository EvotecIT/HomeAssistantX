---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantLight
## SYNOPSIS
Controls lights with typed power, brightness, color, effect, and transition parameters.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantLight [-Entity] <string[]> -Power <HomeAssistantPowerAction> [-BrightnessPercent <Double>] [-ColorTemperatureKelvin <Int32>] [-RgbColor <int[]>] [-Effect <string>] [-TransitionSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantLight -Power <HomeAssistantPowerAction> -InputObject <HomeAssistantEntityInfo[]> [-BrightnessPercent <Double>] [-ColorTemperatureKelvin <Int32>] [-RgbColor <int[]>] [-Effect <string>] [-TransitionSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantLight [-Area] <string[]> -Power <HomeAssistantPowerAction> [-BrightnessPercent <Double>] [-ColorTemperatureKelvin <Int32>] [-RgbColor <int[]>] [-Effect <string>] [-TransitionSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantLight [-Device] <string[]> -Power <HomeAssistantPowerAction> [-BrightnessPercent <Double>] [-ColorTemperatureKelvin <Int32>] [-RgbColor <int[]>] [-Effect <string>] [-TransitionSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantLight [-Floor] <string[]> -Power <HomeAssistantPowerAction> [-BrightnessPercent <Double>] [-ColorTemperatureKelvin <Int32>] [-RgbColor <int[]>] [-Effect <string>] [-TransitionSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Controls lights with typed power, brightness, color, effect, and transition parameters.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -WhatIf
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

### -BrightnessPercent
Brightness from 0 through 100 percent.

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

### -ColorTemperatureKelvin
Color temperature in kelvin.

```yaml
Type: Int32
Parameter Sets: Entity, InputObject, Area, Device, Floor
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

### -Effect
Effect name exposed by the selected lights.

```yaml
Type: String
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

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

### -Power
Turns the selected lights on, off, or toggles their current power.

```yaml
Type: HomeAssistantPowerAction
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: On, Off, Toggle

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RgbColor
Red, green, and blue values, each from 0 through 255.

```yaml
Type: Int32[]
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TransitionSeconds
Transition duration from 0 through 6553 seconds.

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
