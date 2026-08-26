---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Export-HomeAssistantDiagnostic
## SYNOPSIS
Downloads Home Assistant-redacted diagnostics for a configuration entry or one device.

## SYNTAX
### ConfigEntry (Default)
```powershell
Export-HomeAssistantDiagnostic [-EntryId] <string> [-Path] <string> [-Force] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Export-HomeAssistantDiagnostic [-EntryId] <string> [-Path] <string> -DeviceId <string> [-Force] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Downloads Home Assistant-redacted diagnostics for a configuration entry or one device.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Export-HomeAssistantDiagnostic -EntryId 'entry-id' -Path './diagnostic.json'
```

Writes the diagnostic atomically after Home Assistant applies its redaction.

## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: ConfigEntry, Device
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -DeviceId
Optional device identifier within the selected configuration entry.

```yaml
Type: String
Parameter Sets: Device
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntryId
Configuration-entry identifier for the diagnostic export.

```yaml
Type: String
Parameter Sets: ConfigEntry, Device
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Force
Overwrite an existing destination file.

```yaml
Type: SwitchParameter
Parameter Sets: ConfigEntry, Device
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Path
Destination file path. Diagnostic bundles can contain sensitive installation data.

```yaml
Type: String
Parameter Sets: ConfigEntry, Device
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `System.IO.FileInfo`

## RELATED LINKS

- None
