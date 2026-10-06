param([Parameter(Mandatory=$true)][string]$PythonInterpreter)
$ErrorActionPreference='Stop'
$taskVersion=& $PythonInterpreter -c 'import sys; print(str(sys.version_info.major)+"."+str(sys.version_info.minor))'
if($LASTEXITCODE -ne 0 -or $taskVersion -ne '3.12'){throw 'Selecciona un ejecutable Python 3.12 para reproducir la base histórica.'}
$taskEnvironment=Join-Path $env:USERPROFILE '.mcoc-honors/Python312'
if(-not (Test-Path -LiteralPath (Join-Path $taskEnvironment 'Scripts/python.exe'))){
    & $PythonInterpreter -m venv $taskEnvironment
    if($LASTEXITCODE -ne 0){throw 'No se pudo crear el entorno aislado.'}
}
$taskPython=Join-Path $taskEnvironment 'Scripts/python.exe'
& $taskPython -m pip install -r (Join-Path $PSScriptRoot '../../requirements_honors.txt')
if($LASTEXITCODE -ne 0){throw 'Instalación incompleta de dependencias.'}
Write-Host "Entorno listo: $taskPython"
