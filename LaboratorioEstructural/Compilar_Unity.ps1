param([string]$UnityExe='C:\Program Files\Unity\Hub\Editor\6000.5.9f1-x86_64\Editor\Unity.exe')
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'work') | Out-Null
$unityProject=Join-Path $PSScriptRoot 'Unity'
$unityLog=Join-Path $PSScriptRoot 'work\unity_build.log'
$buildProcess=Start-Process -FilePath $UnityExe -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$unityProject+'"'),'-executeMethod','BuildLaboratory.Build','-logFile',('"'+$unityLog+'"')) -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
if ($buildProcess.ExitCode -ne 0) { throw "Falló Unity. Revisar $unityLog" }
Write-Output 'Aplicación compilada en Aplicacion\LaboratorioEstructural.exe'
