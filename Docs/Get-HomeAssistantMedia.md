---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantMedia
## SYNOPSIS
Browses, searches, or resolves Home Assistant media sources and media-player libraries.

## SYNTAX
### Sources (Default)
```powershell
Get-HomeAssistantMedia [-MediaContentId <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### SourceSearch
```powershell
Get-HomeAssistantMedia -Search <string> [-MediaContentId <string>] [-MediaClass <string[]>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Resolve
```powershell
Get-HomeAssistantMedia [-MediaContentId] <string> -Resolve [-ExpiresInSeconds <Int32>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Player
```powershell
Get-HomeAssistantMedia -PlayerEntityId <string> [-MediaContentId <string>] [-MediaContentType <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### PlayerSearch
```powershell
Get-HomeAssistantMedia -Search <string> -PlayerEntityId <string> [-MediaContentId <string>] [-MediaContentType <string>] [-MediaClass <string[]>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Browses, searches, or resolves Home Assistant media sources and media-player libraries.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantMedia
```


### EXAMPLE 2
```powershell
Get-HomeAssistantMedia -PlayerEntityId media_player.kitchen -Search dinner -MediaClass music
```


## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Sources, SourceSearch, Resolve, Player, PlayerSearch
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ExpiresInSeconds
Specifies a value for expires in seconds.

```yaml
Type: Int32
Parameter Sets: Resolve
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MediaClass
Specifies one or more values for media class.

```yaml
Type: String[]
Parameter Sets: SourceSearch, PlayerSearch
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MediaContentId
Optional media selector for browsing and searching; required and positional only with -Resolve.

```yaml
Type: String
Parameter Sets: Sources, SourceSearch, Resolve, Player, PlayerSearch
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MediaContentType
Specifies a value for media content type.

```yaml
Type: String
Parameter Sets: Player, PlayerSearch
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PlayerEntityId
Specifies a value for player entity id.

```yaml
Type: String
Parameter Sets: Player, PlayerSearch
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Resolve
Specifies the resolve switch.

```yaml
Type: SwitchParameter
Parameter Sets: Resolve
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Search
Specifies a value for search.

```yaml
Type: String
Parameter Sets: SourceSearch, PlayerSearch
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

- `HomeAssistantX.Media.HomeAssistantMediaItem`
- `HomeAssistantX.Media.HomeAssistantResolvedMedia`

## RELATED LINKS

- None
