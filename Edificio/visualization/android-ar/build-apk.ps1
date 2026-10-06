param([string]$ToolRoot = (Join-Path $env:USERPROFILE '.codex/android-ar-tools'),[switch]$Honors)
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
    & $gradleBat --no-daemon --console=plain assembleDebug lintDebug testDebugUnitTest
    if ($LASTEXITCODE -ne 0) { throw 'Falló compilación/lint Android; consulta el log.' }
    New-Item -ItemType Directory -Force (Join-Path $projectPath 'dist') | Out-Null
    $taskApkName=if($Honors){'EdificioAR-Honors.apk'}else{'EdificioAR.apk'}
    $taskReport=if($Honors){'delivery_honors.json'}else{'delivery.json'}
    $taskApk=Join-Path $projectPath "dist/$taskApkName"
    Copy-Item -LiteralPath (Join-Path $projectPath 'app/build/outputs/apk/debug/app-debug.apk') -Destination $taskApk -Force
    $taskVerifyArgs=@((Join-Path $projectPath 'tools/verify_apk.py'),'--apk',$taskApk,'--report',(Join-Path $projectPath "dist/$taskReport"),'--apksigner',(Join-Path $sdkDirectory 'build-tools/36.0.0/apksigner.bat'))
    if($Honors){$taskVerifyArgs+='--honors'}
    & python @taskVerifyArgs
    if ($LASTEXITCODE -ne 0) { throw 'APK con instantánea o marcadores desactualizados.' }
    Get-FileHash -Algorithm SHA256 -LiteralPath $taskApk | Format-List
} finally { Pop-Location }
