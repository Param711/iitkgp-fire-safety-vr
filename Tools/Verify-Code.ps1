param(
    [string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.21f1/Editor/Data',
    [string]$DependencyRoot = ''
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($DependencyRoot)) { $DependencyRoot = $projectRoot }
$compileDir = Join-Path $projectRoot 'tmp/analysis/compile'
New-Item -ItemType Directory -Force $compileDir | Out-Null
$monoExe = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$compiler = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
$unityRefs = Get-ChildItem -LiteralPath "$UnityData/Managed/UnityEngine" -Filter '*.dll'
function Compile-Assembly([string]$Name, [string[]]$Sources, [string[]]$ExtraRefs, [string]$Target = 'library') {
    $compilerArgs = [System.Collections.Generic.List[string]]::new()
    $compilerArgs.Add('/nologo')
    $compilerArgs.Add('/target:' + $Target)
    $compilerArgs.Add('/langversion:9')
    $compilerArgs.Add('/out:"' + (Join-Path $compileDir $Name) + '"')
    foreach ($ref in $unityRefs) { $compilerArgs.Add('/reference:"' + $ref.FullName + '"') }
    $compilerArgs.Add('/reference:"' + $UnityData + '/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll"')
    foreach ($ref in $ExtraRefs) { $compilerArgs.Add('/reference:"' + $ref + '"') }
    foreach ($source in $Sources) { $compilerArgs.Add('"' + $source + '"') }
    $responseFile = Join-Path $compileDir ($Name + '.rsp')
    $compilerArgs | Set-Content -LiteralPath $responseFile
    & $monoExe $compiler "@$responseFile"
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $Name" }
    Write-Output "PASS: $Name compiled"
}
$runtime = Join-Path $compileDir 'FireSafety.Runtime.dll'
$urp = Join-Path $DependencyRoot 'Library/ScriptAssemblies/Unity.RenderPipelines.Universal.Runtime.dll'
$renderCore = Join-Path $DependencyRoot 'Library/ScriptAssemblies/Unity.RenderPipelines.Core.Runtime.dll'
$inputSystem = Join-Path $DependencyRoot 'Library/ScriptAssemblies/Unity.InputSystem.dll'
$xrCore = Join-Path $DependencyRoot 'Library/ScriptAssemblies/Unity.XR.CoreUtils.dll'
Compile-Assembly 'FireSafety.Runtime.dll' (Get-ChildItem "$projectRoot/Assets/FireSafety/Runtime" -Filter '*.cs').FullName @($urp, $renderCore, $inputSystem, $xrCore)
Compile-Assembly 'FireSafety.Editor.dll' (Get-ChildItem "$projectRoot/Assets/FireSafety/Editor" -Filter '*.cs').FullName @($runtime, $urp, $renderCore, $inputSystem, $xrCore)
$nunit = Join-Path $DependencyRoot 'Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll'
$rules = Join-Path $projectRoot 'Assets/FireSafety/Tests/Editor/AssessmentRulesTests.cs'
Compile-Assembly 'RulesCheck.exe' @($rules, (Join-Path $PSScriptRoot 'RulesCheck.cs')) @($runtime, $nunit) 'exe'
Compile-Assembly 'FireSafety.Tests.dll' (Get-ChildItem "$projectRoot/Assets/FireSafety/Tests/Editor" -Filter '*.cs').FullName @($runtime, (Join-Path $compileDir 'FireSafety.Editor.dll'), $nunit, $inputSystem, $xrCore)
Compile-Assembly 'FireSafety.PlayTests.dll' (Get-ChildItem "$projectRoot/Assets/FireSafety/Tests/PlayMode" -Filter '*.cs').FullName @($runtime, $nunit, $inputSystem, $xrCore, (Join-Path $DependencyRoot 'Library/ScriptAssemblies/UnityEngine.TestRunner.dll'))
Copy-Item -LiteralPath $nunit -Destination $compileDir -Force
$env:MONO_PATH = "$UnityData/Managed/UnityEngine;$compileDir"
& $monoExe (Join-Path $compileDir 'RulesCheck.exe')
if ($LASTEXITCODE -ne 0) { throw 'Assessment rules tests failed' }
Write-Output 'Unity scene/play/headset tests require the licensed editor and are separate from these managed checks.'
