---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Invoke-HomeAssistantApp
## SYNOPSIS
Runs one explicit lifecycle operation for a Supervisor-managed Home Assistant app.

## SYNTAX
### __AllParameterSets
```powershell
Invoke-HomeAssistantApp [-App] <string> [-Action] <HomeAssistantAppAction> -Connection <HomeAssistantConnection> [-PassThru] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Runs one explicit lifecycle operation for a Supervisor-managed Home Assistant app.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Invoke-HomeAssistantApp -App 'example_app' -Action Restart -WhatIf
```

Uses one lifecycle action enum instead of a cmdlet per app operation.

## PARAMETERS

### -Action
Lifecycle action: Install, Update, Start, Stop, Restart, or Uninstall.

```yaml
Type: HomeAssistantAppAction
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Install, Update, Start, Stop, Restart, Uninstall

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -App
Supervisor app/add-on slug.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
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

### -PassThru
Writes the Supervisor result to the pipeline.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `None`

## RELATED LINKS

- None
