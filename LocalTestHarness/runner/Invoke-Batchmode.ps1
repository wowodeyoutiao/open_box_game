param(
    [ValidateSet("all", "box", "box_guard", "equip", "save_box", "idle", "level", "mail", "bag_full", "skill", "skill_action", "recycle", "task", "task_reset", "battle", "home_afk", "partner", "boss", "tower", "exp_pool", "passive", "store", "skill_advance", "equip_enhance", "equip_star", "gem", "config_audit", "compose", "gem_all", "gem_dungeon", "daily_map", "guard_island", "relic", "relic_reward", "pet", "pet_up", "pet_award", "partner_item")]
    [string]$Scenario = "all"
)

$ErrorActionPreference = "Stop"

$harnessRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $harnessRoot
$projectPath = Join-Path $repoRoot "client\JE_WLY_Clinet"
$unity = "I:\Unity\Unity 2022.3.14f1c1\Editor\Unity.exe"
$logFile = Join-Path $harnessRoot "reports\batchmode.log"
$report = Join-Path $harnessRoot "reports\last_report.json"
$scenarioFile = Join-Path $harnessRoot "active_scenario.txt"
$jengineProj = Join-Path $projectPath "Assets\JEngine.proj"
$jengineProjBytes = $null
if (Test-Path $jengineProj) {
    $jengineProjBytes = [System.IO.File]::ReadAllBytes($jengineProj)
}

if (-not (Test-Path $unity)) {
    throw "Unity not found: $unity"
}
if (-not (Test-Path $projectPath)) {
    throw "Unity project not found: $projectPath"
}
$unityProc = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($unityProc) {
    throw "Unity Editor is running. Close it before batchmode."
}

New-Item -ItemType Directory -Force -Path (Join-Path $harnessRoot "reports") | Out-Null
if (Test-Path $report) {
    Remove-Item $report -Force
}
[System.IO.File]::WriteAllText($scenarioFile, $Scenario, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "== build test dll =="
$buildBat = Get-ChildItem -Path (Join-Path $repoRoot "client") -Filter "*DLL*.bat" |
    Select-Object -First 1
if (-not $buildBat) {
    throw "build bat not found under client"
}
& cmd.exe /c "`"$($buildBat.FullName)`" --test --no-pause"
if ($LASTEXITCODE -ne 0) {
    throw "test dll build failed, exit=$LASTEXITCODE"
}

Write-Host "== batchmode play =="
$unityArgs = @(
    "-batchmode",
    "-projectPath", $projectPath,
    "-executeMethod", "LocalTestBatchRunner.Run",
    "-localTest=$Scenario",
    "-logFile", $logFile
)
$started = Get-Date
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
$timeout = 180
while (((Get-Date) - $started).TotalSeconds -lt $timeout) {
    if (Test-Path $report) {
        break
    }
    if ($proc -and $proc.HasExited) {
        break
    }
    Start-Sleep -Milliseconds 500
}

if ($proc -and -not $proc.HasExited) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    $proc.WaitForExit(5000) | Out-Null
}
Start-Sleep -Milliseconds 500

# batchmode 会清空 JEngine.proj，跑完恢复原内容，避免污染工作区。
if ($jengineProjBytes -and (Test-Path $jengineProj)) {
    for ($i = 0; $i -lt 20; $i++) {
        try {
            [System.IO.File]::WriteAllBytes($jengineProj, $jengineProjBytes)
            break
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }
}

if (Test-Path $report) {
    Write-Host ""
    Write-Host "== report =="
    Get-Content $report -Encoding UTF8
    exit 0
}

Write-Host "== report missing, see log =="
Write-Host $logFile
exit 1
