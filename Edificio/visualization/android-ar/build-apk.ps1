param([string]$ToolRoot = (Join-Path $env:USERPROFILE '.codex/android-ar-tools'))
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$jdkDirectory = (Get-ChildItem (Join-Path $ToolRoot 'jdk') -Directory | Select-Object -First 1).FullName
if (-not $jdkDirectory) { throw 'Ejecuta tools/bootstrap_toolchain.py primero, o indica -ToolRoot.' }
$env:JAVA_HOME = $jdkDirectory
$sdkDirectory = Join-Path $ToolRoot 'sdk'
$env:ANDROID_HOME = $sdkDirectory
$gradleBat = Join-Path $ToolRoot 'gradle/gradle-8.10.2/bin/gradle.bat'
$sdkLine = 'sdk.dir=' + $sdkDirectory.Replace('\','/')
Set-Content -LiteralPath (Join-Path $projectPath 'local.properties') -Value $sdkLine -Encoding ascii
Push-Location $projectPath
try {
    & $gradleBat --no-daemon --console=plain assembleDebug lintDebug
    if ($LASTEXITCODE -ne 0) { throw 'Falló compilación/lint Android; consulta el log.' }
    New-Item -ItemType Directory -Force (Join-Path $projectPath 'dist') | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectPath 'app/build/outputs/apk/debug/app-debug.apk') -Destination (Join-Path $projectPath 'dist/EdificioAR.apk') -Force
    & (Join-Path $sdkDirectory 'build-tools/35.0.0/apksigner.bat') verify --verbose (Join-Path $projectPath 'dist/EdificioAR.apk')
    if ($LASTEXITCODE -ne 0) { throw 'La firma del APK no es válida.' }
    & python (Join-Path $projectPath 'tools/verify_apk.py')
    if ($LASTEXITCODE -ne 0) { throw 'APK con instantánea o marcadores desactualizados.' }
    Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $projectPath 'dist/EdificioAR.apk') | Format-List
} finally { Pop-Location }
