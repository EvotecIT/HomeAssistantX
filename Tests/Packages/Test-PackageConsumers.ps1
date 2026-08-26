param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath
)

$ErrorActionPreference = 'Stop'
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$packageName = [IO.Path]::GetFileName($resolvedPackage)
if ($packageName -notmatch '^HomeAssistantX\.(?<version>\d+\.\d+\.\d+(?:[-+][^.]+)?)\.nupkg$') {
    throw "Could not determine the HomeAssistantX package version from '$packageName'."
}

$version = $Matches.version
$packageDirectory = Split-Path -Parent $resolvedPackage
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::Combine([IO.Path]::GetTempPath(), 'HomeAssistantX-PackageConsumer-' + [Guid]::NewGuid().ToString('N')))
$isolatedPackages = Join-Path $temporaryRoot 'packages'
$expectedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $temporaryRoot.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The package-consumer test directory escaped the system temporary directory.'
}

try {
    $null = New-Item -ItemType Directory -Path $temporaryRoot
    $nugetConfig = Join-Path $temporaryRoot 'NuGet.config'
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="HomeAssistantXArtifact" value="$packageDirectory" />
    <add key="NuGet.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath $nugetConfig -Encoding UTF8

    foreach ($framework in 'net472', 'netstandard2.0', 'net10.0') {
        $projectDirectory = Join-Path $temporaryRoot $framework
        $null = New-Item -ItemType Directory -Path $projectDirectory
        $projectPath = Join-Path $projectDirectory 'Consumer.csproj'
        $sourcePath = Join-Path $projectDirectory 'Contract.cs'
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="HomeAssistantX" Version="$version" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding UTF8
        @'
using System;
using System.Threading;
using System.Threading.Tasks;
using HomeAssistantX;
using HomeAssistantX.Operations;
using HomeAssistantX.Supervisor;

public static class PackageContract
{
    public static async Task<HomeAssistantCapabilityReport> ReadAsync(
        Uri uri,
        string token,
        CancellationToken cancellationToken)
    {
        using (var client = HomeAssistantClient.Create(uri, token))
        {
            var capabilities = await client.Operations.GetCapabilitiesAsync(cancellationToken);
            await client.Supervisor.GetAppsAsync(cancellationToken);
            return capabilities;
        }
    }
}
'@ | Set-Content -LiteralPath $sourcePath -Encoding UTF8

        dotnet restore $projectPath --packages $isolatedPackages --configfile $nugetConfig
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed for $framework." }
        dotnet build $projectPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Package consumer build failed for $framework." }
    }

    "PASS package consumers net472, netstandard2.0, net10.0"
} finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        $verifiedRoot = [IO.Path]::GetFullPath($temporaryRoot)
        if ($verifiedRoot.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $verifiedRoot -Recurse -Force
        }
    }
}
