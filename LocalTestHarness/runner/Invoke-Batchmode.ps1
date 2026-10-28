param(
    [ValidateSet("all", "box", "box_guard", "equip", "save_box", "idle", "level", "mail", "bag_full", "skill", "skill_action", "recycle", "task", "task_reset", "battle", "home_afk", "partner", "boss", "tower", "exp_pool", "passive", "store", "skill_advance", "equip_enhance", "equip_star", "gem", "config_audit", "compose", "gem_all", "gem_dungeon", "daily_map", "guard_island", "relic", "relic_reward", "pet", "pet_up", "pet_award", "partner_item", "partner_equip", "partner_gem", "partner_upgrade", "partner_reset", "partner_master", "player_master", "sign")]
    [string]$Scenario = "all",
    # 跳过编译，复用 Hidden~ 里已有的 dll（dll 由外部先编好时用，例如受限环境里 cmd 被禁用）
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$harnessRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $harnessRoot
$projectPath = Join-Path $repoRoot "client\JE_WLY_Clinet"
$dllPath = Join-Path $projectPath "Assets\HotUpdateResources\Main\Dll\Hidden~\HotUpdateScripts.dll"
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
# 注意：这里**不要** Remove-Item 旧报告。
# 某些宿主有「单轮批量删除闸门」，一旦爆配额，本轮之后每次删除都被拒，脚本会直接崩。
# 改为用时间戳判定「报告是不是本次新写的」。
[System.IO.File]::WriteAllText($scenarioFile, $Scenario, (New-Object System.Text.UTF8Encoding($false)))

if (-not $SkipBuild) {
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
}
else {
    Write-Host "== skip build, reuse existing dll =="
    if (-not (Test-Path $dllPath)) {
        throw "dll not found: $dllPath (cannot skip build)"
    }
    $existing = Get-Item $dllPath
    Write-Host "DLL: $($existing.FullName)"
    Write-Host "DLL time: $($existing.LastWriteTime)"
}

Write-Host "== batchmode play =="

# Unity 的 Bee 构建系统（ScriptCompilationBuildProgram）用区分大小写的 Hashtable 读环境变量，
# 若同时存在 HTTPS_PROXY 与 https_proxy，会抛
#   System.ArgumentException: Item has already been added. Key in dictionary: 'HTTPS_PROXY'
# 然后 "Scripts have compiler errors" 直接退出（return code 1）。
# 某些宿主会同时注入大小写两套代理变量，这里在起 Unity 前把小写那份摘掉。
foreach ($lower in @("http_proxy", "https_proxy", "all_proxy", "no_proxy")) {
    $upper = $lower.ToUpper()
    if ((Test-Path "Env:$lower") -and (Test-Path "Env:$upper")) {
        Remove-Item "Env:$lower" -ErrorAction SilentlyContinue
        Write-Host "[fix] duplicate env removed: $lower"
    }
}

# 某些宿主会剥掉系统目录类环境变量。Bee/Stevedore 靠 LOCALAPPDATA 推断 cache-folder，
# 缺了会抛 "'cache-folder' could not be inferred from environment"，然后编译失败退出。
$envDefaults = @{
    "LOCALAPPDATA"     = Join-Path $env:USERPROFILE "AppData\Local"
    "APPDATA"          = Join-Path $env:USERPROFILE "AppData\Roaming"
    "ProgramData"      = "C:\ProgramData"
    "ProgramFiles"     = "C:\Program Files"
    "ProgramFiles(x86)" = "C:\Program Files (x86)"
    # UPM 是 Node 进程，缺 HOME 会报
    #   Failed to resolve packages: The "path" argument must be of type string. Received undefined
    "HOME"             = $env:USERPROFILE
}
foreach ($key in $envDefaults.Keys) {
    if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($key))) {
        [Environment]::SetEnvironmentVariable($key, $envDefaults[$key])
        Write-Host "[fix] env restored: $key"
    }
}

$unityArgs = @(
    "-batchmode",
    # 注意：不要加 -noUpm。ugui 等是 UPM 包，关掉 Package Manager 会缺 UnityEngine.UI，
    # 表现为一堆 "type or namespace 'UI' does not exist" 编译错误。
    "-projectPath", $projectPath,
    "-executeMethod", "LocalTestBatchRunner.Run",
    "-localTest=$Scenario",
    "-logFile", $logFile
)
$started = Get-Date
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
$timeout = 180
while (((Get-Date) - $started).TotalSeconds -lt $timeout) {
    # 报告"本次新写"才算数：不能用「文件存在」，历史报告会立刻误判成功
    if ((Test-Path $report) -and ((Get-Item $report).LastWriteTime -gt $started)) {
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

if ((Test-Path $report) -and ((Get-Item $report).LastWriteTime -gt $started)) {
    Write-Host ""
    Write-Host "== report =="
    Get-Content $report -Encoding UTF8
    exit 0
}

Write-Host "== report missing, see log =="
Write-Host $logFile
exit 1
