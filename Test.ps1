param([string]$ValheimDir = 'G:\Games\SteamLibrary\steamapps\common\Valheim')
$ErrorActionPreference = 'Stop'
$gameAssembly = Join-Path $ValheimDir 'valheim_Data\Managed\assembly_valheim.dll'
$modAssembly = Join-Path $PSScriptRoot 'bin\Release\ImprovedMagicShield.dll'
$version = [Reflection.AssemblyName]::GetAssemblyName($modAssembly).Version.ToString(3)
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'artifacts') -Force | Out-Null
$result = & dotnet run --project (Join-Path $PSScriptRoot 'tests\ShieldChecks\ShieldChecks.csproj') -c Release -- $gameAssembly $modAssembly
$testExit = $LASTEXITCODE
$result | Set-Content -LiteralPath (Join-Path $PSScriptRoot "artifacts\checks-$version.txt") -Encoding utf8
$result | Write-Output
if ($testExit -ne 0) { throw 'Shield checks failed.' }
[ordered]@{ version=$version; gameAssemblySha256=(Get-FileHash -LiteralPath $gameAssembly).Hash; dllSha256=(Get-FileHash -LiteralPath $modAssembly).Hash; result=($result -join "`n"); liveUnityTested=$false; liveMultiplayerTested=$false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot "artifacts\checks-$version.json") -Encoding utf8
