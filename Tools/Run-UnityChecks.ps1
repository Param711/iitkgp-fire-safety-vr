param(
    [ValidateSet('Scene','EditMode','PlayMode','WindowsBuild','WindowsXRBuild','QuestBuild')][string]$Check = 'EditMode',
    [string]$UnityExe = 'C:/Program Files/Unity/Hub/Editor/2022.3.21f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$resultsDir = Join-Path $projectRoot 'tmp/analysis'
New-Item -ItemType Directory -Force $resultsDir | Out-Null
$logPath = Join-Path $resultsDir ("unity-$Check.log")
$unityArgs = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + $logPath + '"'))
switch ($Check) {
    'Scene' { $unityArgs += @('-nographics', '-executeMethod', 'FireSafety.Editor.TrainingSceneBuilder.Build', '-quit') }
    'EditMode' { $unityArgs += @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-assemblyNames', 'FireSafety.Tests', '-testResults', ('"' + (Join-Path $resultsDir 'editmode-results.xml') + '"')) }
    'PlayMode' { $unityArgs += @('-nographics', '-runTests', '-testPlatform', 'PlayMode', '-assemblyNames', 'FireSafety.PlayTests', '-testResults', ('"' + (Join-Path $resultsDir 'playmode-results.xml') + '"')) }
    'WindowsBuild' { $unityArgs += @('-executeMethod', 'FireSafety.Editor.TrainingSceneBuilder.BuildWindows', '-quit') }
    'WindowsXRBuild' { $unityArgs += @('-executeMethod', 'FireSafety.Editor.TrainingControllerSetup.BuildConfiguredWindows', '-quit') }
    'QuestBuild' { $unityArgs += @('-executeMethod', 'FireSafety.Editor.TrainingSceneBuilder.BuildQuest', '-quit') }
}
$unityProcess = Start-Process -FilePath $UnityExe -ArgumentList $unityArgs -PassThru -Wait -WindowStyle Hidden
Write-Output "Unity $Check exited with code $($unityProcess.ExitCode). Log: $logPath"
if ($unityProcess.ExitCode -ne 0) { Get-Content -LiteralPath $logPath -Tail 50; throw "Unity $Check failed. See the log above." }
