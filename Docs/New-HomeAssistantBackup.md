---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# New-HomeAssistantBackup
## SYNOPSIS
Creates a full Supervisor backup with optional compression, location, and database exclusion.

## SYNTAX
### __AllParameterSets
```powershell
New-HomeAssistantBackup [[-Name] <string>] [-Password <securestring>] [-Compressed <bool>] [-Location <string>] [-ExcludeDatabase] [-Background] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates a full Supervisor backup with optional compression, location, and database exclusion.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | New-HomeAssistantBackup -Name 'Before update' -WhatIf
```

Shows the backup request without creating it.

## PARAMETERS

### -Background
Starts backup creation in the background and returns the job response.

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

### -Compressed
Controls compression. The default is true.

```yaml
Type: Boolean
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

### -ExcludeDatabase
Excludes the Home Assistant database from the backup.

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

### -Location
Optional Supervisor backup-location identifier.

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

### -Name
Optional display name for the full backup.

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

### -Password
Optional backup password as a secure string.

```yaml
Type: SecureString
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

- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
