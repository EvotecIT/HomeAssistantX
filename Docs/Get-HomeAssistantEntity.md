---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantEntity
## SYNOPSIS
Gets joined entities by name, identifier, domain, device, area, or floor.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantEntity [[-Entity] <string[]>] [-Name <string>] [-Domain <string>] [-Device <string>] [-Area <string>] [-Floor <string>] [-AvailableOnly] [-IncludeDisabled] [-IncludeHidden] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Gets joined entities by name, identifier, domain, device, area, or floor.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantEntity -Area Kitchen -Domain light
```


## PARAMETERS

### -Area
Area name, alias, or native ID. Room is an alias.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Room
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AvailableOnly
Returns only entities that currently have a state other than unavailable.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
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
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Device
Device friendly name or native ID.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Domain
Entity domain, such as light or sensor.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Entity
One or more exact entity friendly names or native IDs.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: EntityId
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Floor
Floor name, alias, or native ID.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeDisabled
Includes registry entries disabled by Home Assistant, an integration, or a user.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeHidden
Includes registry entries hidden by Home Assistant, an integration, or a user.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Text contained in the entity friendly name.

```yaml
Type: String
Parameter Sets: __AllParameterSets
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

- `HomeAssistantX.Inventory.HomeAssistantEntityInfo`

## RELATED LINKS

- None
