param(
    [ValidateSet("box", "box_guard", "equip", "save_box", "idle", "level", "mail", "bag_full", "skill", "skill_action", "recycle", "task", "task_reset", "battle", "home_afk", "partner", "boss", "tower", "exp_pool", "passive", "store", "skill_advance", "equip_enhance", "equip_star", "gem", "config_audit", "compose", "gem_all", "gem_dungeon", "daily_map", "guard_island", "relic", "relic_reward", "pet", "pet_up", "pet_award", "partner_item", "all")]
    [string]$Scenario = "all"
)

$ErrorActionPreference = "Stop"

$harnessRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $harnessRoot
$buildBat = Get-ChildItem -Path (Join-Path $repoRoot "client") -Filter "*DLL*.bat" |
    Select-Object -First 1
$dllPath = Join-Path $repoRoot "client\JE_WLY_Clinet\Assets\HotUpdateResources\Main\Dll\Hidden~\HotUpdateScripts.dll"
$scenarioFile = Join-Path $harnessRoot "active_scenario.txt"

if (-not $buildBat) {
    throw "build bat not found under client"
}
$buildBat = $buildBat.FullName

$unity = Get-Process -Name Unity -ErrorAction SilentlyContinue
if ($unity) {
    throw "Unity Editor is running. Close Unity Editor before building the hotfix DLL."
}

[System.IO.File]::WriteAllText($scenarioFile, $Scenario, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "== build test dll =="
& cmd.exe /c "`"$buildBat`" --test --no-pause"
if ($LASTEXITCODE -ne 0) {
    throw "build failed, exit=$LASTEXITCODE"
}
if (-not (Test-Path $dllPath)) {
    throw "dll not found after build: $dllPath"
}

$dll = Get-Item $dllPath
Write-Host ""
Write-Host "DLL: $($dll.FullName)"
Write-Host "DLL time: $($dll.LastWriteTime)"
Write-Host ""
Write-Host "Next: open Unity, enter Play, wait for login."
Write-Host "Report: $harnessRoot\reports\last_report.json"
Write-Host "Console: [LocalTest][PASS] or [LocalTest][FAIL]"
