---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantAction
## SYNOPSIS
Lists Home Assistant actions and their runtime-provided field descriptions.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantAction [-Domain <string>] [-Action <string>] [-Entity <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Lists Home Assistant actions and their runtime-provided field descriptions.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantAction -Entity 'Kitchen light'
```


## PARAMETERS

### -Action
Action name within the selected domain. Service is an alias.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: Service
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

### -Domain
Action domain, such as light or climate.

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
Entity friendly name or native ID whose domain should be inspected.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: EntityId
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

- `HomeAssistantX.Services.HomeAssistantActionDefinition`

## RELATED LINKS

- None
