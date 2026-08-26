---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantJob
## SYNOPSIS
Gets all Supervisor jobs or one job by identifier.

## SYNTAX
### All (Default)
```powershell
Get-HomeAssistantJob -Connection <HomeAssistantConnection> [-All] [<CommonParameters>]
```

### Id
```powershell
Get-HomeAssistantJob [-Id] <string> -Connection <HomeAssistantConnection> [<CommonParameters>]
```

## DESCRIPTION
Gets all Supervisor jobs or one job by identifier.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantJob -Id 'job-id'
```

Returns progress and completion metadata for one job.

## PARAMETERS

### -All
Returns recent Supervisor jobs. This is the default behavior.

```yaml
Type: SwitchParameter
Parameter Sets: All
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: All, Id
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Id
Exact Supervisor job identifier.

```yaml
Type: String
Parameter Sets: Id
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Supervisor.HomeAssistantSupervisorJob`

## RELATED LINKS

- None
