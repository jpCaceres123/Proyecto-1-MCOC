param([string]$PythonExe='', [string]$Overrides='')
$ErrorActionPreference='Stop'
if (-not $PythonExe) {
    $localPython=Join-Path $PSScriptRoot '..\.venv\Scripts\python.exe'
    if (Test-Path -LiteralPath $localPython) { $PythonExe=$localPython }
    else { $PythonExe=(Get-Command python -ErrorAction Stop).Source }
}
Push-Location -LiteralPath $PSScriptRoot
try {
    if ($Overrides) { & $PythonExe -u (Join-Path $PSScriptRoot 'python\run_project.py') --overrides $Overrides }
    else { & $PythonExe -u (Join-Path $PSScriptRoot 'python\run_project.py') }
    if ($LASTEXITCODE -ne 0) { throw 'El análisis falló. Los resultados anteriores no se sustituyen por una corrida fallida.' }
    & $PythonExe (Join-Path $PSScriptRoot 'python\test_project.py')
    if ($LASTEXITCODE -ne 0) { throw 'Una comprobación no pasó; revisar la salida antes de usar los resultados.' }
} finally { Pop-Location }
