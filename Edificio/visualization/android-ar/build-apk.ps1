param([string]$ToolRoot = (Join-Path $env:USERPROFILE '.codex/android-ar-tools'),[switch]$Honors)
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$jdkDirectory = (Get-ChildItem (Join-Path $ToolRoot 'jdk') -Directory | Select-Object -First 1).FullName
if (-not $jdkDirectory) { throw 'Ejecuta tools/bootstrap_toolchain.py primero, o indica -ToolRoot.' }
$env:JAVA_HOME = $jdkDirectory
$sdkDirectory = Join-Path $ToolRoot 'sdk'
$taskBuildTools = Get-ChildItem (Join-Path $sdkDirectory 'build-tools') -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' -and [version]$_.Name -ge [version]'35.0.0' } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $taskBuildTools) { throw 'Instala Android build-tools 35.0.0 o superior en el SDK indicado.' }
$env:ANDROID_HOME = $sdkDirectory
$gradleBat = Join-Path $ToolRoot 'gradle/gradle-8.10.2/bin/gradle.bat'
$taskBuildProject = $projectPath
# JDK 17/Gradle test workers can fail to load classes through non-ASCII Windows paths.
# Stage only build inputs; verification and the delivered APK still use the original sources.
if ($projectPath -match '[^\x00-\x7F]') {
    $taskBuildProject = Join-Path ([System.IO.Path]::GetTempPath()) ('MCOC-AR-build-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $taskBuildProject 'app') -Force | Out-Null
    foreach ($taskConfig in @('build.gradle','settings.gradle','gradle.properties')) {
        Copy-Item -LiteralPath (Join-Path $projectPath $taskConfig) -Destination (Join-Path $taskBuildProject $taskConfig)
    }
    Copy-Item -LiteralPath (Join-Path $projectPath 'app/build.gradle') -Destination (Join-Path $taskBuildProject 'app/build.gradle')
    Copy-Item -LiteralPath (Join-Path $projectPath 'app/src') -Destination (Join-Path $taskBuildProject 'app/src') -Recurse
    Write-Host "Compilación aislada: $taskBuildProject"
}
$sdkLine = 'sdk.dir=' + $sdkDirectory.Replace('\','/')
Set-Content -LiteralPath (Join-Path $taskBuildProject 'local.properties') -Value $sdkLine -Encoding ascii
Push-Location $taskBuildProject
try {
    & $gradleBat --no-daemon --console=plain "-PmcocBuildToolsVersion=$($taskBuildTools.Name)" assembleDebug lintDebug testDebugUnitTest
    if ($LASTEXITCODE -ne 0) { throw 'Falló compilación/lint Android; consulta el log.' }
    New-Item -ItemType Directory -Force (Join-Path $projectPath 'dist') | Out-Null
    $taskApkName=if($Honors){'EdificioAR-Honors.apk'}else{'EdificioAR.apk'}
    $taskReport=if($Honors){'delivery_honors.json'}else{'delivery.json'}
    $taskApk=Join-Path $projectPath "dist/$taskApkName"
    Copy-Item -LiteralPath (Join-Path $taskBuildProject 'app/build/outputs/apk/debug/app-debug.apk') -Destination $taskApk -Force
    $taskVerifyArgs=@((Join-Path $projectPath 'tools/verify_apk.py'),'--apk',$taskApk,'--report',(Join-Path $projectPath "dist/$taskReport"),'--apksigner',(Join-Path $taskBuildTools.FullName 'apksigner.bat'))
    if($Honors){$taskVerifyArgs+='--honors'}
    & python @taskVerifyArgs
    if ($LASTEXITCODE -ne 0) { throw 'APK con instantánea o marcadores desactualizados.' }
    Get-FileHash -Algorithm SHA256 -LiteralPath $taskApk | Format-List
} finally { Pop-Location }
