param([string]$UnityEditor = 'G:\Development\Unity\6000.0.75f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$artifacts = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
$version = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $PSScriptRoot 'bin\Release\ImprovedMagicShield.dll')).Version.ToString(3)
$result = Join-Path $artifacts "visual-checks-$version.txt"
if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
$project = Join-Path $PSScriptRoot 'tests\VisualChecks'
$log = Join-Path $artifacts 'visual-unity-checks.log'
# Graphics initialization is needed to exercise the production client-only guard.
$arguments = @('-batchmode','-quit','-projectPath',('"' + $project + '"'),'-executeMethod','ShieldVisualChecks.Run','-logFile',('"' + $log + '"'))
$process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Wait for this editor, not its persistent shared licensing helpers.
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $result)) { throw "Unity component checks failed. Read $log" }
Get-Content -LiteralPath $result
