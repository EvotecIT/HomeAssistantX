---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantHelper
## SYNOPSIS
Sets common boolean, number, select, text, date, time, and date-time helpers.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantHelper [-Entity] <string[]> -Domain <HomeAssistantHelperDomain> [-Boolean <Boolean>] [-Number <Double>] [-Increment] [-Decrement] [-Text <string>] [-Option <string>] [-Options <string[]>] [-Next] [-Previous] [-Cycle <bool>] [-Date <DateTime>] [-Time <TimeSpan>] [-DateTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantHelper -Domain <HomeAssistantHelperDomain> -InputObject <HomeAssistantEntityInfo[]> [-Boolean <Boolean>] [-Number <Double>] [-Increment] [-Decrement] [-Text <string>] [-Option <string>] [-Options <string[]>] [-Next] [-Previous] [-Cycle <bool>] [-Date <DateTime>] [-Time <TimeSpan>] [-DateTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantHelper [-Area] <string[]> -Domain <HomeAssistantHelperDomain> [-Boolean <Boolean>] [-Number <Double>] [-Increment] [-Decrement] [-Text <string>] [-Option <string>] [-Options <string[]>] [-Next] [-Previous] [-Cycle <bool>] [-Date <DateTime>] [-Time <TimeSpan>] [-DateTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantHelper [-Device] <string[]> -Domain <HomeAssistantHelperDomain> [-Boolean <Boolean>] [-Number <Double>] [-Increment] [-Decrement] [-Text <string>] [-Option <string>] [-Options <string[]>] [-Next] [-Previous] [-Cycle <bool>] [-Date <DateTime>] [-Time <TimeSpan>] [-DateTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantHelper [-Floor] <string[]> -Domain <HomeAssistantHelperDomain> [-Boolean <Boolean>] [-Number <Double>] [-Increment] [-Decrement] [-Text <string>] [-Option <string>] [-Options <string[]>] [-Next] [-Previous] [-Cycle <bool>] [-Date <DateTime>] [-Time <TimeSpan>] [-DateTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Set-HomeAssistantHelper [-Label] <string[]> -Domain <HomeAssistantHelperDomain> [-Boolean <Boolean>] [-Number <Double>] [-Increment] [-Decrement] [-Text <string>] [-Option <string>] [-Options <string[]>] [-Next] [-Previous] [-Cycle <bool>] [-Date <DateTime>] [-Time <TimeSpan>] [-DateTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sets common boolean, number, select, text, date, time, and date-time helpers.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantHelper -Entity input_number.volume -Domain InputNumber -Number 15
```


### EXAMPLE 2
```powershell
Set-HomeAssistantHelper -Entity input_select.house_mode -Domain InputSelect -Next
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

### -Boolean
Value for an input_boolean helper.

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

### -Cycle
Allows next/previous selection to wrap around. The default is true.

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

### -Date
Date value for a date or input_datetime entity.

```yaml
Type: DateTime
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DateTime
Date and time value for a datetime or input_datetime entity.

```yaml
Type: DateTimeOffset
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Decrement
Decrements an input_number helper.

```yaml
Type: SwitchParameter
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
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

### -Domain
Native Home Assistant helper domain; it must match the selected entities.

```yaml
Type: HomeAssistantHelperDomain
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values: InputBoolean, Number, InputNumber, Select, InputSelect, Text, InputText, Date, Time, DateTime, InputDateTime

Required: True
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

### -Increment
Increments an input_number helper.

```yaml
Type: SwitchParameter
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

### -Next
Selects the next option.

```yaml
Type: SwitchParameter
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Number
Finite value for a number or input_number entity.

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

### -Option
Option to select on a select or input_select entity.

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

### -Options
Replacement option list for an input_select helper.

```yaml
Type: String[]
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Previous
Selects the previous option.

```yaml
Type: SwitchParameter
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Text
Value for a text or input_text entity; an empty string is allowed.

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

### -Time
Time-of-day value for a time or input_datetime entity.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.Inventory.HomeAssistantEntityInfo[]`
- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`

## RELATED LINKS

- None
