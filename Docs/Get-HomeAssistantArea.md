---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantArea
## SYNOPSIS
Lists Home Assistant areas (rooms), optionally within a floor.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantArea [[-Area] <string>] [-Floor <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Lists Home Assistant areas (rooms), optionally within a floor.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantArea -Floor 'Ground Floor'
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
Position: 0
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

### -Floor
Floor name, alias, or native ID used to filter areas.

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

- `HomeAssistantX.Inventory.HomeAssistantAreaInfo`

## RELATED LINKS

- None
