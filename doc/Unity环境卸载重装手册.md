# Unity 环境卸载与重装操作手册

> 目标：干净卸载 `I:\UnityHub` 旧环境，重装 Unity 2022.3.14f1c1
> 环境实测时间：2026-10-22
> 操作方式：全部手动，可逐步执行、随时暂停

---

## 第 0 步：ProjectGroup 处置（已确认放弃）

> **状态：2026-10-22 Boss 已确认 —— `I:\UnityHub\ProjectGroup` 这批 2023 年的项目不再需要，可一并删除。**

原本这里列了 13 项共 15.5 GB 的项目与压缩包（BackupProject / rab / rab_0821 /
TestGame1 系列 + 7 个 zip/rar）。既然确认放弃，**整个 `I:\UnityHub` 最后可以整体删除**，
流程反而简化了。

### 删大目录的实用技巧（Unity 项目必踩）

**问题**：Unity 项目的 `Library` 里常有超过 260 字符的路径，资源管理器删除时会报
「文件名太长」、「找不到该项目」或删到一半卡住。

**解决**：用 robocopy 拿空目录镜像覆盖，再删空壳。管理员 PowerShell 执行：

```powershell
# 1. 建一个空目录当"镜像源"
New-Item -ItemType Directory -Path "C:\_empty_temp" -Force | Out-Null

# 2. 用空目录镜像覆盖目标（/MIR = 让目标变得和源一样空）
robocopy "C:\_empty_temp" "I:\UnityHub\ProjectGroup" /MIR /NFL /NDL /NJH /NJS /NP

# 3. 删除已清空的目录
Remove-Item "I:\UnityHub\ProjectGroup" -Recurse -Force

# 4. 清理临时空目录
Remove-Item "C:\_empty_temp" -Force
```

> `robocopy /MIR` 会删掉目标里所有源里没有的文件。**务必确认目标路径写的是 ProjectGroup**，
> 写错路径会误删别的东西。执行前把第 2 行再读一遍。

如果照样失败（极少见），重启进安全模式再删，或用 `takeown` + `icacls` 取得所有权后删。

---

## 第 1 步：先判断 —— 真的需要全卸吗？

**先说结论：你的情况其实可以不卸 Hub，只补装 2022.3.14f1c1 就行。**

理由：

| 事实 | 说明 |
|---|---|
| Hub 3.3.0 是正常版本 | 装新编辑器就靠它，卸了还得重装 |
| 支持多版本共存 | 2022.3.3 和 2022.3.14 可以并存，Hub 里一键切换 |
| I 盘有 324.9 GB 空闲 | 多留一个 8 GB 的旧版本完全无压力 |
| 没有污染风险 | 这台机器没跑过 maochao 工程（`Library` 是随工程拷来的），不存在"环境脏了"的问题 |

**所以有两条路：**

### 路 A（推荐，约 25 分钟，只做一件事）
直接装 `2022.3.14f1c1` → 激活许可证 → 收工。
旧版 2022.3.3 留着，以后在 Hub 里点一下就能卸。

### 路 B（你问的方案，彻底卸载后重装，约 40 分钟）
按下面第 2–9 步走完，环境回到全新状态。

> 只有这三种情况需要走路 B：① 怀疑旧环境有诡异问题 ② 想省掉 8 GB ③ 习惯干净。
> 都不是的话，路 A 更省事。

---

## 第 2 步（路 B）：关闭所有相关进程

**必须全部关掉，否则文件占用会卸不干净。**

任务管理器（Ctrl+Shift+Esc）→ 详细信息 → 结束以下进程（现在实测是一个都没在跑，但卸之前再确认一次）：

```
Unity.exe
Unity Hub.exe
UnityPackageManager.exe
UnityShaderCompiler.exe
UnityHelper.exe
UnityLicensingClient.exe
```

命令行方式（管理员 PowerShell）：

```powershell
Get-Process | Where-Object { $_.ProcessName -match "Unity" } | Stop-Process -Force
```

---

## 第 3 步（路 B）：卸载 Unity 2022.3.3f1c1 编辑器

**方式一（GUI，推荐）**
```
资源管理器打开：I:\UnityHub\Editor\2022.3.3f1c1\Editor\
双击运行：Uninstall.exe
```

**方式二（命令行静默）**
```powershell
& "I:\UnityHub\Editor\2022.3.3f1c1\Editor\Uninstall.exe" /S
```

卸载完检查：`I:\UnityHub\Editor\2022.3.3f1c1` 应该消失或只剩空目录。

---

## 第 4 步（路 B）：卸载 Unity Hub

**方式一（GUI）**
```
I:\UnityHub\Unity Hub\Uninstall Unity Hub.exe
```
（也可以从 设置 → 应用 → 已安装的应用 → Unity Hub 3.3.0-c9 卸载）

**方式二（命令行）**
```powershell
& "I:\UnityHub\Unity Hub\Uninstall Unity Hub.exe" /allusers
```

> 注意：Electron 应用（Hub 就是）的卸载器有时会留下整个目录，属正常现象，第 5 步手删即可。

---

## 第 5 步（路 B）：清理残留目录

**因为 ProjectGroup 已确认放弃，`I:\UnityHub` 可以整个删掉** —— 跑完第 3、4 步的卸载程序后，
直接删这个根目录即可：

```powershell
Remove-Item "I:\UnityHub" -Recurse -Force
```

如果删不动（长路径/占用），先按第 0 步的 robocopy 技巧清空，再删。

其余散落在系统盘的目录（这张表是按本机实测列的，不是通用清单）：

| 路径 | 实测大小 | 处置 | 说明 |
|---|---|---|---|
| `C:\Users\Administrator\AppData\Roaming\UnityHub` | 59.4 MB | ✅ 删 | Hub 配置、登录缓存、项目列表 |
| `C:\Users\Administrator\AppData\Roaming\Unity` | 39.5 MB | ✅ 删 | 编辑器配置、Asset Store 缓存 |
| `C:\Users\Administrator\AppData\Local\Unity` | 4.3 MB | ✅ 删 | GI Cache、日志 |
| `C:\ProgramData\Unity` | 极小 | ✅ 删 | **含过期许可证 `Unity_lic.ulf`**，删了重装后重新激活更干净 |
| `C:\Users\Administrator\AppData\LocalLow\Unity` | — | 不存在，跳过 | |

清理后 `I:\UnityHub` 应该完全消失。

> 💡 `C:\ProgramData\Unity` 是隐藏目录，资源管理器要开「显示隐藏项目」才看得到。

---

## 第 6 步（路 B）：清理注册表

**先备份，再删。** 管理员 PowerShell：

```powershell
reg export "HKLM\SOFTWARE\Unity Technologies" "C:\unity_reg_backup_HKLM.reg" /y
reg export "HKCU\SOFTWARE\Unity Technologies" "C:\unity_reg_backup_HKCU1.reg" /y
reg export "HKCU\SOFTWARE\Unity" "C:\unity_reg_backup_HKCU2.reg" /y
```

实测存在的分支，逐个删除：

```
HKLM\SOFTWARE\Unity Technologies          ← 存在（含 Hub 安装信息）
HKCU\SOFTWARE\Unity Technologies          ← 存在
HKCU\SOFTWARE\Unity                       ← 存在
HKLM\SOFTWARE\WOW6432Node\Unity Technologies   ← 不存在，跳过
```

命令：
```powershell
reg delete "HKLM\SOFTWARE\Unity Technologies" /f
reg delete "HKCU\SOFTWARE\Unity Technologies" /f
reg delete "HKCU\SOFTWARE\Unity" /f
```

> ⚠️ 卸载器通常已经清了一部分，报"找不到"就跳过，不影响。

---

## 第 7 步（路 B）：删快捷方式

实测位置（在**公共桌面**，不是你的个人桌面）：

```
C:\Users\Public\Desktop\Unity 2022.3.3f1c1.lnk
C:\Users\Public\Desktop\Unity Hub.lnk
```

直接删这两个文件。

> 开始菜单里没找到 Unity 项（卸载器会自己清），不用管。

---

## 第 8 步（路 B）：重启一次

**为什么要重启**：释放被占用的句柄、让注册表变更生效、清理内存中的 Unity 相关服务。

重启后验证（管理员 PowerShell）：

```powershell
# 1. 目录应不存在
Test-Path "I:\UnityHub\Editor"
Test-Path "I:\UnityHub\Unity Hub"

# 2. 注册表应不存在
Test-Path "HKLM:\SOFTWARE\Unity Technologies"
Test-Path "HKCU:\SOFTWARE\Unity"
```

都为 `False` 即卸载干净。

---

## 第 9 步：安装 Unity 2022.3.14f1c1

安装包已经在手，不用下载：

```
I:\AINewWork\maochao_game\game_machao\tools\UnitySetup64-2022.3.14f1c1.exe    (2.5 GB)
```

**安装时注意：**

1. 双击运行，安装路径改成 **`I:\UnityHub\Editor\2022.3.14f1c1`**（沿用你原来的目录习惯，别再往 C 盘塞）
2. 组件勾选（默认勾了 Unity Editor 本体）：

| 组件 | 是否勾选 | 原因 |
|---|---|---|
| Unity Editor (64-bit) | ✅ 必须 | 本体 |
| **WebGL Build Support** | ✅ **必须** | TapTap 小游戏就靠它，这是命门 |
| Windows Build Support (IL2CPP) | ✅ 建议 | 本地快速验证用 |
| Documentation | ⬜ 可选 | 离线文档，占空间 |
| Android / iOS Build Support | ⬜ 不用勾 | 当前路线不走 APK，需要时再加 |
| Visual Studio Community | ⬜ **不要勾** | 你已有 VS 2022 Enterprise，重复装会冲突 |

3. 装完约 8–9 GB，20–30 分钟

> ⚠️ 因为走了第 4 步卸掉 Hub，需要**重新装 Unity Hub** 才能管理版本和激活许可证：
> 从 https://unity.com/download 下最新 Hub（或用 `I:\UnityHub` 里若还留着的安装包）。
> 如果走路 A（不卸 Hub），这一步就省了。

---

## 第 10 步：装完必做的三件事

### ① 激活许可证（不做这一步编辑器打不开）

打开 Unity Hub → 右上角登录 Unity 账号 → Preferences/Licenses → Add → **Get a free personal license**。
个人版免费，需要联网。

### ② 把 GI Cache 挪到 I 盘（C 盘只剩 7.9 GB，很容易被吃满）

编辑器里：`Edit → Preferences → GI Cache → Cache Folder` 改成
```
I:\UnityCache\GICache
```
同样把 `Preferences → Jobs → Cache Folder` 也改到 I 盘。

### ③ 打开工程前，先处理 Library

`I:\AINewWork\maochao_game\game_machao\client\JE_WLY_Clinet\Library`（1.9 GB）是**别的版本生成的**，
用 2022.3.14 打开时必然全量重导入，还可能带进旧缓存问题。

**建议先重命名而不是删**，留一条退路：
```
Library  →  Library_2022.3.3_old
```
新编辑器打开工程会重建 `Library`。跑通且稳定后，再删掉 `_old`。

---

## 完整检查清单（打印/对照用）

```
[ ] 0. 已确认 ProjectGroup 放弃（2026-10-22 确认）
[ ] 1. 决定：走路 B 彻底卸载（路 A 只补装新版本，不删）
[ ] 2. 所有 Unity 进程已关闭
[ ] 3. 2022.3.3f1c1 编辑器已卸载（Uninstall.exe）
[ ] 4. Unity Hub 已卸载（Uninstall Unity Hub.exe /allusers）
[ ] 5. I:\UnityHub 整个目录已删除（含 ProjectGroup 15.5 GB）
[ ] 6. AppData 三处 + ProgramData\Unity 已清理
[ ] 7. 注册表已备份并清理 3 个分支
[ ] 8. 公共桌面 2 个快捷方式已删
[ ] 9. 已重启，Test-Path 验证全部 False
[ ] 10. 2022.3.14f1c1 已装（勾了 WebGL Build Support，不勾 VS）
[ ] 11. 许可证已激活（个人版免费）
[ ] 12. GI Cache / Jobs Cache 已指到 I:\UnityCache
[ ] 13. 旧 Library 已重命名为 Library_2022.3.3_old
[ ] 14. 重装 Unity Hub 并登录
```

---

## 附：这台机器的环境实测结果（原状态存档）

```
Unity Hub 3.3.0-c9          I:\UnityHub\Unity Hub
Unity Editor                I:\UnityHub\Editor\2022.3.3f1c1  (8.12 GB)
播放器模块                  WebGLSupport ✅ / windowsstandalonesupport ✅ / Android ❌ / iOS ❌
许可证                      C:\ProgramData\Unity\Unity_lic.ulf
                            StartDate 2023-06-14 · StopDate 2023-08-25 🔴 已过期
项目备份                    I:\UnityHub\ProjectGroup  (15.5 GB，13 项)
注册表                      HKLM\SOFTWARE\Unity Technologies
                            HKCU\SOFTWARE\Unity Technologies
                            HKCU\SOFTWARE\Unity
快捷方式                    C:\Users\Public\Desktop\Unity 2022.3.3f1c1.lnk
                            C:\Users\Public\Desktop\Unity Hub.lnk
缓存                        %APPDATA%\UnityHub 59.4 MB
                            %APPDATA%\Unity 39.5 MB
                            %LOCALAPPDATA%\Unity 4.3 MB
磁盘                        I: 324.9 GB 空闲 · C: 仅 7.9 GB 空闲 ⚠️
其它工具                    VS 2022 Enterprise (E:\DevelopTools\...) · dotnet SDK 未装（不影响）
```
