param([Parameter(Mandatory=$true)][string]$Address,[int]$Port=8765,[string]$PythonInterpreter)
$ErrorActionPreference='Stop'
if(-not $PythonInterpreter){$taskConfiguredPython=Join-Path $env:USERPROFILE '.mcoc-honors/Python312/Scripts/python.exe';$PythonInterpreter=if(Test-Path -LiteralPath $taskConfiguredPython){$taskConfiguredPython}else{'python'}}
$parsed=[System.Net.IPAddress]::Parse($Address)
$bytes=$parsed.GetAddressBytes()
if($bytes.Length -ne 4 -or -not ($bytes[0] -eq 10 -or ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or ($bytes[0] -eq 192 -and $bytes[1] -eq 168) -or $Address -eq '127.0.0.1')) { throw 'Indica una IPv4 privada del PC, no 0.0.0.0 ni una dirección pública.' }
$taskTokenBytes=New-Object byte[] 32
$rng=[System.Security.Cryptography.RandomNumberGenerator]::Create()
try {$rng.GetBytes($taskTokenBytes)} finally {$rng.Dispose()}
$env:MCOC_API_TOKEN=[Convert]::ToBase64String($taskTokenBytes)
Write-Host "Servidor: http://${Address}:${Port}"
Write-Host "Token temporal (copiar en el teléfono; no subir a GitHub): $env:MCOC_API_TOKEN"
try { & $PythonInterpreter (Join-Path $PSScriptRoot 'server.py') --host $Address --port $Port; if($LASTEXITCODE -ne 0){throw 'El servidor terminó con error.'} }
finally { Remove-Item Env:MCOC_API_TOKEN -ErrorAction SilentlyContinue }
