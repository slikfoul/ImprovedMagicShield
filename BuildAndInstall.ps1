param([string]$Profile = 'Default', [string]$ValheimDir = 'G:\Games\SteamLibrary\steamapps\common\Valheim', [string]$DataRoot = (Join-Path $env:APPDATA 'r2modmanPlus-local\Valheim'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath (Join-Path $DataRoot ('profiles\' + $Profile + '\mods.yml')) -PathType Leaf)) { throw 'Selected r2modman profile does not exist. No fallback is allowed.' }
& dotnet build (Join-Path $PSScriptRoot 'ImprovedMagicShield.csproj') -c Release "-p:ValheimDir=$ValheimDir" "-p:ModProfile=$(Join-Path $DataRoot ('profiles\' + $Profile))"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& (Join-Path $PSScriptRoot 'Test.ps1') -ValheimDir $ValheimDir
& (Join-Path $PSScriptRoot 'BuildPackage.ps1') -SkipBuild
& (Join-Path $PSScriptRoot 'RegisterLocalMod.ps1') -Profile $Profile -DataRoot $DataRoot
