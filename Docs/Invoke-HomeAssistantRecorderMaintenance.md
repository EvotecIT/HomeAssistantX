---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Invoke-HomeAssistantRecorderMaintenance
## SYNOPSIS
Runs a bounded Recorder maintenance task.

## SYNTAX
### Purge
```powershell
Invoke-HomeAssistantRecorderMaintenance -Purge [-KeepDays <Int32>] [-Repack] [-ApplyFilter] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### PurgeEntities
```powershell
Invoke-HomeAssistantRecorderMaintenance -PurgeEntities [-KeepDays <Int32>] [-EntityId <string[]>] [-Domain <string[]>] [-EntityGlob <string[]>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Enable
```powershell
Invoke-HomeAssistantRecorderMaintenance -Enable [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Disable
```powershell
Invoke-HomeAssistantRecorderMaintenance -Disable [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RefreshStatisticsIssues
```powershell
Invoke-HomeAssistantRecorderMaintenance -RefreshStatisticsIssues [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Runs a bounded Recorder maintenance task.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HomeAssistantRecorderMaintenance -Purge -KeepDays 30 -WhatIf
```


## PARAMETERS

### -ApplyFilter
Specifies the apply filter switch.

```yaml
Type: SwitchParameter
Parameter Sets: Purge
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
Parameter Sets: Purge, PurgeEntities, Enable, Disable, RefreshStatisticsIssues
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Disable
Specifies the disable switch.

```yaml
Type: SwitchParameter
Parameter Sets: Disable
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Domain
Specifies one or more values for domain.

```yaml
Type: String[]
Parameter Sets: PurgeEntities
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Enable
Specifies the enable switch.

```yaml
Type: SwitchParameter
Parameter Sets: Enable
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityGlob
Specifies one or more values for entity glob.

```yaml
Type: String[]
Parameter Sets: PurgeEntities
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Specifies one or more values for entity id.

```yaml
Type: String[]
Parameter Sets: PurgeEntities
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -KeepDays
Specifies a value for keep days.

```yaml
Type: Int32
Parameter Sets: Purge, PurgeEntities
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Specifies the pass thru switch.

```yaml
Type: SwitchParameter
Parameter Sets: Purge, PurgeEntities, Enable, Disable
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Purge
Specifies the purge switch.

```yaml
Type: SwitchParameter
Parameter Sets: Purge
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PurgeEntities
Specifies the purge entities switch.

```yaml
Type: SwitchParameter
Parameter Sets: PurgeEntities
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RefreshStatisticsIssues
Specifies the refresh statistics issues switch.

```yaml
Type: SwitchParameter
Parameter Sets: RefreshStatisticsIssues
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repack
Specifies the repack switch.

```yaml
Type: SwitchParameter
Parameter Sets: Purge
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

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`

## RELATED LINKS

- None
