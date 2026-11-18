param(
    [ValidateSet('Open', 'Verify', 'Build', 'Test', 'Smoke', 'PlayBuild', 'Trainyard')]
    [string]$Action = 'Open',
    [string]$EditorPath
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $EditorPath) {
    $versionLine = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1
    $editorVersion = ($versionLine -split ':', 2)[1].Trim()
    $EditorPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Unity Editor not found: $EditorPath. Pass -EditorPath with your Editor executable." }
$logRoot = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$quotedRoot = '"' + $projectRoot + '"'
if ($Action -eq 'Open') {
    Start-Process -FilePath $EditorPath -WindowStyle Hidden -ArgumentList @('-projectPath', $quotedRoot, '-openfile', '"Assets/IGIRebuilt/Scenes/Trainyard.unity"') | Out-Null
    return
}
$build = Join-Path $projectRoot 'Build\Windows\IGIRebuilt.exe'
if ($Action -eq 'PlayBuild') {
    if (-not (Test-Path -LiteralPath $build)) { throw 'No player build yet. Run this script with -Action Build first.' }
    Start-Process -FilePath $build -WindowStyle Hidden -WorkingDirectory $projectRoot | Out-Null
    return
}
if ($Action -eq 'Smoke') {
    if (-not (Test-Path -LiteralPath $build)) { throw 'Run -Action Build before the standalone smoke test.' }
    $capture = Join-Path $logRoot 'Smoke'
    New-Item -ItemType Directory -Force -Path $capture | Out-Null
    $process = Start-Process -FilePath $build -WorkingDirectory $projectRoot -WindowStyle Hidden -ArgumentList @('--smoke-test', ('"' + $capture + '"'), '-logFile', ('"' + (Join-Path $logRoot 'smoke-player.log') + '"'), '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0') -PassThru
    $process.WaitForExit()
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "Smoke test failed ($($process.ExitCode)). See Logs/smoke-player.log." }
    $result = Get-Content -LiteralPath (Join-Path $capture 'smoke-result.json') -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw 'The player ran but gameplay verification failed.' }
    Write-Host "Smoke test passed. Results and captures: $capture"
    return
}
$arguments = @('-batchmode', '-nographics', '-projectPath', $quotedRoot, '-logFile', ('"' + (Join-Path $logRoot ($Action.ToLowerInvariant() + '-editor.log')) + '"'))
switch ($Action) {
    'Verify' { $arguments += @('-quit', '-executeMethod', 'IGIRebuilt.Editor.ProjectSetup.VerifyBatch') }
    'Trainyard' { $arguments += @('-quit', '-executeMethod', 'IGIRebuilt.Editor.TrainyardSetup.BuildWindows') }
    'Build'  { $arguments += @('-quit', '-executeMethod', 'IGIRebuilt.Editor.ProjectSetup.BuildWindows') }
    'Test'   { $arguments += @('-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + (Join-Path $logRoot 'editmode-results.xml') + '"')) }
}
$process = Start-Process -FilePath $EditorPath -WorkingDirectory $projectRoot -WindowStyle Hidden -ArgumentList $arguments -PassThru
$process.WaitForExit()
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Unity $Action failed ($($process.ExitCode)). See Logs/$($Action.ToLowerInvariant())-editor.log. Licensing exit 198 must be resolved in Unity Hub." }
Write-Host "Unity $Action finished successfully. See $logRoot."


