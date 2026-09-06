param([string]$EditorPath='C:\Program Files\Unity\Hub\Editor\6000.5.9f1-x86_64\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$projectDir=$PSScriptRoot
New-Item -ItemType Directory -Force (Join-Path $projectDir 'work') | Out-Null
$buildProcess=Start-Process -FilePath $editorPath -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$projectDir+'\Unity"'),'-executeMethod','BuildArchitecture.Build','-logFile',('"'+$projectDir+'\work\unity_build.log"')) -WindowStyle Hidden -PassThru
$buildProcess.WaitForExit()
if($buildProcess.ExitCode -ne 0){throw 'Falló la compilación; revisar work/unity_build.log'}
