---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantIssue
## SYNOPSIS
Gets Core repairs issues or Supervisor resolution issues.

## SYNTAX
### Core (Default)
```powershell
Get-HomeAssistantIssue -Connection <HomeAssistantConnection> [-Core] [-IncludeIgnored] [<CommonParameters>]
```

### Supervisor
```powershell
Get-HomeAssistantIssue -Supervisor -Connection <HomeAssistantConnection> [<CommonParameters>]
```

## DESCRIPTION
Gets Core repairs issues or Supervisor resolution issues.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantIssue
```

Returns non-ignored Core Repairs issues by default.

## PARAMETERS

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Core, Supervisor
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Core
Returns Core Repairs issues. This is the default source.

```yaml
Type: SwitchParameter
Parameter Sets: Core
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IncludeIgnored
Includes Repairs issues currently marked as ignored.

```yaml
Type: SwitchParameter
Parameter Sets: Core
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Supervisor
Returns Supervisor resolution issues.

```yaml
Type: SwitchParameter
Parameter Sets: Supervisor
Aliases: None
Possible values:

Required: True
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

- `HomeAssistantX.Operations.HomeAssistantRepairIssue`
- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
