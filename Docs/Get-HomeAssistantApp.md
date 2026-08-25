---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantApp
## SYNOPSIS
Gets installed Supervisor-managed Home Assistant apps.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantApp [[-App] <string>] -Connection <HomeAssistantConnection> [<CommonParameters>]
```

## DESCRIPTION
Gets installed Supervisor-managed Home Assistant apps.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantApp
```

Returns installed Supervisor apps/add-ons.

## PARAMETERS

### -App
Supervisor app/add-on slug. Omit it to return all installed apps.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Supervisor.HomeAssistantApp`

## RELATED LINKS

- None
