---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Connect-HomeAssistant
## SYNOPSIS
Creates, verifies, and optionally stores the runspace's default Home Assistant connection.

## SYNTAX
### Token (Default)
```powershell
Connect-HomeAssistant [-Uri] <uri> -AccessToken <string> [-Name <string>] [-NoDefault] [<CommonParameters>]
```

### Provider
```powershell
Connect-HomeAssistant [-Uri] <uri> -AccessTokenProvider <IHomeAssistantAccessTokenProvider> [-Name <string>] [-NoDefault] [<CommonParameters>]
```

## DESCRIPTION
Creates, verifies, and optionally stores the runspace's default Home Assistant connection.

## EXAMPLES

### EXAMPLE 1
```powershell
Connect-HomeAssistant -Uri 'https://home.example.net' -AccessToken $token -Name 'Home' | Out-Null
```

Validates REST and WebSocket access and stores the connection as the runspace default.

## PARAMETERS

### -AccessToken
Long-lived or OAuth access token. Prefer a variable or secret store over a command-line literal.

```yaml
Type: String
Parameter Sets: Token
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -AccessTokenProvider
Token provider that owns retrieval or refresh without exposing the token to the cmdlet.

```yaml
Type: IHomeAssistantAccessTokenProvider
Parameter Sets: Provider
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Friendly connection name used in output and confirmation messages.

```yaml
Type: String
Parameter Sets: Token, Provider
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoDefault
Returns the connection without replacing the current runspace default.

```yaml
Type: SwitchParameter
Parameter Sets: Token, Provider
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Uri
Home Assistant base URI, for example https://home.example.net.

```yaml
Type: Uri
Parameter Sets: Token, Provider
Aliases: Url
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

- `None`

## OUTPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## RELATED LINKS

- None
