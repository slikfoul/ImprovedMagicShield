param([string]$Profile = 'Default', [string]$DataRoot = (Join-Path $env:APPDATA 'r2modmanPlus-local\Valheim'))
$ErrorActionPreference = 'Stop'
function AssertClosed {
    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(valheim|r2modman.*|Thunderstore.*)$' })
    if ($running.Count) { throw ('Close Valheim, r2modman and Thunderstore Mod Manager before profile changes. Still running: ' + (($running.ProcessName | Sort-Object -Unique) -join ', ')) }
}
function AssertChild([string]$Path, [string]$Parent) {
    if (-not $Path.StartsWith($Parent.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Resolved path escaped its intended directory.' }
}
AssertClosed
if ($Profile -notmatch '^[A-Za-z0-9 _-]+$' -or -not [IO.Path]::IsPathRooted($DataRoot)) { throw 'Specify a valid profile and absolute data root.' }
$DataRoot = [IO.Path]::GetFullPath($DataRoot)
$profileRoot = [IO.Path]::GetFullPath((Join-Path $DataRoot ('profiles\' + $Profile)))
$registryPath = Join-Path $profileRoot 'mods.yml'
if (-not (Test-Path -LiteralPath $registryPath -PathType Leaf)) { throw 'Selected r2modman profile does not exist. No fallback is allowed.' }
$manifestPath = Join-Path $PSScriptRoot 'packaging\manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.website_url -notmatch '^https://github\.com/slikfoul/ImprovedMagicShield/?$') { throw 'Installation blocked: the package requires the real, verified GitHub repository for this mod.' }
$version = $manifest.version_number
if ($manifest.name -cne 'ImprovedMagicShield' -or $version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package identity/version.' }
$archive = Join-Path $PSScriptRoot "artifacts\Slikfoul-ImprovedMagicShield-$version.zip"
if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'Build and verify the manager package first.' }
$originalRegistry = [IO.File]::ReadAllBytes($registryPath)
$registryText = [Text.Encoding]::UTF8.GetString($originalRegistry).TrimStart([char]0xFEFF)
$blocks = [regex]::Matches($registryText, '(?ms)^- manifestVersion:.*?(?=^- manifestVersion:|\z)')
$existing = @($blocks | Where-Object { $_.Value -match '(?m)^  name: Slikfoul-ImprovedMagicShield\r?$' })
if ($existing.Count -gt 1) { throw 'Duplicate registry entries; profile untouched.' }
$enabled = $existing.Count -eq 0 -or $existing[0].Value -notmatch '(?m)^  enabled: false\r?$'
$trusted = $existing.Count -eq 1 -and $existing[0].Value -match '(?m)^  trustedPackage: true\r?$'
if ($existing.Count) {
    $oldVersion = [regex]::Match($existing[0].Value, '(?m)^  versionNumber:\r?\n    major: (\d+)\r?\n    minor: (\d+)\r?\n    patch: (\d+)')
    if (-not $oldVersion.Success) { throw 'Cannot determine installed version.' }
    $installedVersion = [version]('{0}.{1}.{2}' -f $oldVersion.Groups[1].Value,$oldVersion.Groups[2].Value,$oldVersion.Groups[3].Value)
    if ($installedVersion -gt [version]$version) { throw 'Refusing to downgrade a newer local build.' }
}
$pluginsRoot = [IO.Path]::GetFullPath((Join-Path $profileRoot 'BepInEx\plugins'))
$managerDirectory = [IO.Path]::GetFullPath((Join-Path $pluginsRoot 'Slikfoul-ImprovedMagicShield'))
$cacheRoot = [IO.Path]::GetFullPath((Join-Path $DataRoot 'cache\Slikfoul-ImprovedMagicShield'))
$cacheDirectory = [IO.Path]::GetFullPath((Join-Path $cacheRoot $version))
AssertChild $managerDirectory $pluginsRoot
AssertChild $cacheDirectory $cacheRoot
$duplicates = @(Get-ChildItem -LiteralPath $pluginsRoot -Recurse -File | Where-Object { $_.Name -in @('ImprovedMagicShield.dll','ImprovedMagicShield.dll.old') -and -not $_.FullName.StartsWith($managerDirectory + '\', [StringComparison]::OrdinalIgnoreCase) })
if ($duplicates.Count) { throw ('Review unmanaged duplicate copies before installing: ' + ($duplicates.FullName -join ', ')) }
$cacheTrusted = $trusted
$cacheMetadata = Join-Path $cacheDirectory 'mm_v2_manifest.json'
if (Test-Path -LiteralPath $cacheMetadata) {
    $oldMetadata = Get-Content -LiteralPath $cacheMetadata -Raw | ConvertFrom-Json
    if ($oldMetadata.PSObject.Properties['trustedPackage']) {
        if ($oldMetadata.trustedPackage -isnot [bool]) { throw 'Invalid existing cache trust state.' }
        $cacheTrusted = $oldMetadata.trustedPackage
    }
}
$config = Join-Path $profileRoot 'BepInEx\config\Slikfoul.ImprovedMagicShield.cfg'
$configHash = if (Test-Path -LiteralPath $config) { (Get-FileHash -LiteralPath $config).Hash } else { $null }
$backup = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('backups\before-import-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))))
AssertChild $backup ([IO.Path]::GetFullPath($PSScriptRoot))
$staging = Join-Path $backup 'new-cache'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$required = @('manifest.json','README.md','CHANGELOG.md','icon.png','LICENSE','BepInEx/plugins/ImprovedMagicShield/ImprovedMagicShield.dll')
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    if ($zip.Entries.Count -ne $required.Count) { throw 'Unexpected package contents.' }
    foreach ($name in $required) { if ($null -eq $zip.GetEntry($name)) { throw "Missing package entry: $name" } }
} finally { $zip.Dispose() }
New-Item -ItemType Directory -Path $backup | Out-Null
[IO.File]::WriteAllBytes((Join-Path $backup 'mods.yml'), $originalRegistry)
if ($configHash) { Copy-Item -LiteralPath $config -Destination $backup }
[IO.Compression.ZipFile]::ExtractToDirectory($archive, $staging)
if ((Get-FileHash -LiteralPath (Join-Path $staging 'manifest.json')).Hash -cne (Get-FileHash -LiteralPath $manifestPath).Hash) { throw 'Package manifest differs from verified source.' }
$payloadRelative = 'BepInEx\plugins\ImprovedMagicShield\ImprovedMagicShield.dll'
$builtDll = Join-Path $PSScriptRoot 'bin\Release\ImprovedMagicShield.dll'
if ((Get-FileHash -LiteralPath (Join-Path $staging $payloadRelative)).Hash -cne (Get-FileHash -LiteralPath $builtDll).Hash) { throw 'Package DLL differs from build.' }
if ([Reflection.AssemblyName]::GetAssemblyName($builtDll).Version.ToString(3) -cne $version) { throw 'DLL version differs from manifest.' }
$parts = $version.Split('.')
$installedAt = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$managerWebsite = 'https://thunderstore.io/c/valheim/p/Slikfoul/ImprovedMagicShield/'
$metadata = [ordered]@{
    manifestVersion=1; name='Slikfoul-ImprovedMagicShield'; authorName='Slikfoul'; websiteUrl=$managerWebsite
    displayName='Improved Magic Shield'; description=$manifest.description; gameVersion='0'; networkMode='both'
    packageType='other'; installMode='managed'; installedAtTime=$installedAt; loaders=@(); dependencies=@($manifest.dependencies)
    incompatibilities=@(); optionalDependencies=@(); versionNumber=@{ major=[int]$parts[0]; minor=[int]$parts[1]; patch=[int]$parts[2] }
    enabled=$enabled; icon=(Join-Path $cacheDirectory 'icon.png'); onlineSource=$true; trustedPackage=$cacheTrusted
}
[IO.File]::WriteAllText((Join-Path $staging 'mm_v2_manifest.json'), ($metadata | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding $false))
$description = $manifest.description.Replace("'", "''")
$entry = @"
- manifestVersion: 1
  name: Slikfoul-ImprovedMagicShield
  authorName: Slikfoul
  websiteUrl: '$managerWebsite'
  displayName: Improved Magic Shield
  description: '$description'
  gameVersion: '0'
  networkMode: both
  packageType: other
  installMode: managed
  installedAtTime: $installedAt
  loaders: []
  dependencies:
    - denikson-BepInExPack_Valheim-5.4.2351
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: $($parts[0])
    minor: $($parts[1])
    patch: $($parts[2])
  enabled: $($enabled.ToString().ToLowerInvariant())
  onlineSource: true
  trustedPackage: $($trusted.ToString().ToLowerInvariant())
"@
if ($existing.Count) {
    $match = $existing[0]
    $newRegistry = $registryText.Substring(0,$match.Index) + $entry + [Environment]::NewLine + $registryText.Substring($match.Index+$match.Length)
} else { $newRegistry = $registryText.TrimEnd() + [Environment]::NewLine + $entry + [Environment]::NewLine }
$priorManager = Join-Path $backup 'previous-managed'
$priorCache = Join-Path $backup 'previous-cache'
$newManager = $false; $newCache = $false; $registryChanged = $false
$tempRegistry = Join-Path $profileRoot ('mods.yml.improvedmagicshield-' + [guid]::NewGuid().ToString('N'))
try {
    AssertClosed
    if ((Get-FileHash -LiteralPath $registryPath).Hash -cne (Get-FileHash -LiteralPath (Join-Path $backup 'mods.yml')).Hash) { throw 'Registry changed while preparing installation.' }
    # All recursive move targets were resolved and checked against this mod's roots above.
    if (Test-Path -LiteralPath $managerDirectory) { Move-Item -LiteralPath $managerDirectory -Destination $priorManager }
    if (Test-Path -LiteralPath $cacheDirectory) { Move-Item -LiteralPath $cacheDirectory -Destination $priorCache }
    New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
    Move-Item -LiteralPath $staging -Destination $cacheDirectory
    $newCache = $true
    New-Item -ItemType Directory -Path $managerDirectory | Out-Null
    $newManager = $true
    foreach ($document in @('manifest.json','README.md','CHANGELOG.md','icon.png','LICENSE')) { Copy-Item -LiteralPath (Join-Path $cacheDirectory $document) -Destination $managerDirectory }
    Copy-Item -LiteralPath (Join-Path $cacheDirectory 'BepInEx\plugins\ImprovedMagicShield') -Destination (Join-Path $managerDirectory 'ImprovedMagicShield') -Recurse
    if (-not $enabled) { foreach ($file in Get-ChildItem -LiteralPath $managerDirectory -Recurse -File) { Rename-Item -LiteralPath $file.FullName -NewName ($file.Name + '.old') } }
    [IO.File]::WriteAllText($tempRegistry,$newRegistry,(New-Object Text.UTF8Encoding $false))
    [IO.File]::Replace($tempRegistry,$registryPath,(Join-Path $backup 'atomic-registry-backup.yml'))
    $registryChanged = $true
    $copies = @(Get-ChildItem -LiteralPath $pluginsRoot -Recurse -File | Where-Object { $_.Name -in @('ImprovedMagicShield.dll','ImprovedMagicShield.dll.old') })
    if ($copies.Count -ne 1 -or (Get-FileHash -LiteralPath $copies[0].FullName).Hash -cne (Get-FileHash -LiteralPath $builtDll).Hash) { throw 'Installed DLL verification failed.' }
    $savedRegistry = [IO.File]::ReadAllText($registryPath)
    $othersBefore = @($blocks | Where-Object { $_.Value -notmatch '(?m)^  name: Slikfoul-ImprovedMagicShield\r?$' } | ForEach-Object { $_.Value.TrimEnd() })
    $othersAfter = @([regex]::Matches($savedRegistry,'(?ms)^- manifestVersion:.*?(?=^- manifestVersion:|\z)') | Where-Object { $_.Value -notmatch '(?m)^  name: Slikfoul-ImprovedMagicShield\r?$' } | ForEach-Object { $_.Value.TrimEnd() })
    if (($othersBefore -join "`n") -cne ($othersAfter -join "`n")) { throw 'Another registry entry changed.' }
    if ($configHash -and (Get-FileHash -LiteralPath $config).Hash -cne $configHash) { throw 'Existing configuration changed.' }
    [ordered]@{ version=$version; profile=$Profile; enabled=$enabled; onlineSource=$true; dll=$copies[0].FullName; dllSha256=(Get-FileHash -LiteralPath $builtDll).Hash; archiveSha256=(Get-FileHash -LiteralPath $archive).Hash; backup=$backup } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot "artifacts\installation-$version.json") -Encoding utf8
    Write-Output "Registered Slikfoul-ImprovedMagicShield $version in r2modman profile $Profile."
} catch {
    if ($registryChanged) { [IO.File]::WriteAllBytes($registryPath,$originalRegistry) }
    if ($newManager) { Move-Item -LiteralPath $managerDirectory -Destination (Join-Path $backup 'failed-managed') }
    if ($newCache) { Move-Item -LiteralPath $cacheDirectory -Destination (Join-Path $backup 'failed-cache') }
    if (Test-Path -LiteralPath $priorManager) { Move-Item -LiteralPath $priorManager -Destination $managerDirectory }
    if (Test-Path -LiteralPath $priorCache) { Move-Item -LiteralPath $priorCache -Destination $cacheDirectory }
    throw
} finally { if (Test-Path -LiteralPath $tempRegistry) { Remove-Item -LiteralPath $tempRegistry } }
