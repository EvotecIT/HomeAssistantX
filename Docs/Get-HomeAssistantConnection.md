---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantConnection
## SYNOPSIS
Gets the default Home Assistant connection for the current PowerShell runspace.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantConnection [<CommonParameters>]
```

## DESCRIPTION
Gets the default Home Assistant connection for the current PowerShell runspace.

## EXAMPLES

### EXAMPLE 1
```powershell
$home = Get-HomeAssistantConnection
```


## PARAMETERS

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `None`

## OUTPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## RELATED LINKS

- None
