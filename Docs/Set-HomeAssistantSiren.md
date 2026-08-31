---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantSiren
## SYNOPSIS
Controls sirens with typed tone, volume, and duration options.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantSiren [-Entity] <string[]> -Action <HomeAssistantSirenAction> [-Tone <string>] [-ToneId <Int32>] [-VolumePercent <Double>] [-Duration <TimeSpan>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantSiren -Action <HomeAssistantSirenAction> -InputObject <HomeAssistantEntityInfo[]> [-Tone <string>] [-ToneId <Int32>] [-VolumePercent <Double>] [-Duration <TimeSpan>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantSiren [-Area] <string[]> -Action <HomeAssistantSirenAction> [-Tone <string>] [-ToneId <Int32>] [-VolumePercent <Double>] [-Duration <TimeSpan>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantSiren [-Device] <string[]> -Action <HomeAssistantSirenAction> [-Tone <string>] [-ToneId <Int32>] [-VolumePercent <Double>] [-Duration <TimeSpan>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantSiren [-Floor] <string[]> -Action <HomeAssistantSirenAction> [-Tone <string>] [-ToneId <Int32>] [-VolumePercent <Double>] [-Duration <TimeSpan>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Set-HomeAssistantSiren [-Label] <string[]> -Action <HomeAssistantSirenAction> [-Tone <string>] [-ToneId <Int32>] [-VolumePercent <Double>] [-Duration <TimeSpan>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Controls sirens with typed tone, volume, and duration options.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantSiren -Entity siren.house -Action TurnOn -Tone alarm -VolumePercent 40 -Duration '00:00:10' -WhatIf
```


## PARAMETERS

### -Action
Turns the siren on, off, or toggles it.

```yaml
Type: HomeAssistantSirenAction
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values: TurnOn, TurnOff, Toggle

Required: True
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

### -Duration
Positive whole-second duration for TurnOn.

```yaml
Type: TimeSpan
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
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

### -Tone
Named tone supported by the target siren.

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

### -ToneId
Numeric tone identifier supported by the target siren.

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

### -VolumePercent
Volume from 0 through 100 percent.

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
