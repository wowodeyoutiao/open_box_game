# LocalTestHarness

本地自动化测试编排目录。测试代码不进入正式包。

## 目录职责

```text
LocalTestHarness/
  scenarios/       测试场景标识
  runner/          编译与测试准备脚本
  reports/         测试报告（不提交）
  active_scenario.txt  当前测试场景（不提交）
```

真正的热更内测试探针放在：

```text
client/JE_WLY_Clinet/HotUpdateScripts/LocalServer/Testing/
```

并且只在 `LOCAL_TEST` 构建中编译。

## 使用

### 无 GUI batchmode（推荐日常回归）

不需要打开 Unity 编辑器 GUI：

```powershell
powershell -ExecutionPolicy Bypass -File .\LocalTestHarness\runner\Invoke-Batchmode.ps1 -Scenario all
```

会执行：

```text
编译测试版 DLL
→ Unity CLI 打开工程
→ 自动进入 Play
→ 本地登录入口自动跑测试
→ 写报告
→ 退出 Unity
```

### 编辑器 Play（大节点验收）

关闭 Unity Editor 后执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\LocalTestHarness\runner\Invoke-LocalTests.ps1 -Scenario all
```

可用场景：

```text
—— 基础 / 背包 / 存档 ——
box        开箱 -> 待领取 -> 存档字段 -> 领取入包
box_guard  待领取重复开箱 / 重复领取 / 次数不足
save_box   宝箱状态存档往返
equip      职业 / 部位校验 + 属性计算
equip_enhance 装备强化：槽位绑定 / 材料校验 / 属性加成 / 等级上限
equip_star 装备升星：成功率 / 失败掉星 / 材料不退 / 星级上限
recycle    装备分解（批量/堆叠/穿戴中）/ 销毁 / 自动分解设置与出厂开关
bag_full   背包满转系统邮件
mail       系统邮件发送 / 列表 / 领取 / 重复领取
idle       挂机时间结算 / 事件奖励
home_afk   大厅挂机时间自然日重置
level      经验池上限 / 连续升级 / 升级礼包
exp_pool   经验池：主角升级 / 伙伴升级 / 等级上限

—— 战斗 / 副本 ——
battle     PVE-01 普通战役最小闭环
boss       首领副本：次数重置 / 战力门槛 / 通关扣次数
tower      试炼塔：跳层拦截 / 逐层推进 / 战力门槛
relic      遗迹：首关推导 / 跳关拦截 / 星级 / 进度 / 次数
relic_reward 遗迹章节星级宝箱：10/20/30 星档 / 不重复领 / InstanceInfo 下发
guard_island 守卫海岛：挂机刷怪 / 倍率 / 每日时长
daily_map  每日副本：次数 / 结算奖励

—— 养成 ——
partner    伙伴获得 / 上阵 / 下阵 / 升星
partner_item 伙伴整卡道具（Type 8）拦截 / 重复转碎片
partner_equip 伙伴装备：穿脱 / 一键 / 按伙伴等级校验 / 下阵脱装备 / 防误分解 / 存档
partner_gem  伙伴魂石镶嵌：槽位独立 / 槽位数按伙伴等级 / 拆卸回包
partner_upgrade 伙伴装备强化升星：上限按伙伴等级 / 槽位进度存档
partner_reset 伙伴合成 / 分解 / 洗练：碎片换整卡、按品质星级门槛重置并退料、免费 70% 与付费全额
partner_master 伙伴装备四段套装：魂石自然加成 / 魂石大师 / 强化大师 / 升星大师
skill      技能自动学习 / 槽位
skill_action 技能升级 / 进阶 / 换槽
skill_advance 技能进阶：档位配置 / 材料校验 / 伤害加成
passive    被动技能属性：闪避 / 韧性 / 生命百分比 / 自动学习门槛
gem        魂石副本 / 合成 / 镶嵌
gem_all    魂石一键镶嵌
gem_dungeon 魂石副本产出
compose    合成系统
pet        战宠获取 / 上阵 / 背包上限
pet_up     战宠升级 / 升星 / 移动 / 喂养 / 合成
pet_award  战宠道具发放 / 挂机产出 / 累计上限

—— 其它 ——
task       任务初始化 / 目标推进 / 领奖
task_reset 每日/每周任务重置
store      商城：列表 / 货币不足 / 购买扣费发货 / 限购拦截
config_audit 配置字段一致性审计
all        全部场景
```

然后打开 Unity，进入 Play。

本地模式下测试会在配置加载完成后自动执行，不需要登录或点 UI；batchmode 会直接运行并退出 Unity。

## 坑位（都是实测过的，别再踩）

```text
① 源码改了 ≠ 生效
   HotUpdateScripts 在 Assets 之外，Unity 不会编译它（Library/ScriptAssemblies 里没有这个 dll）。
   必须先用 runner 编译出 Dll/Hidden~/HotUpdateScripts.dll，否则 batchmode 测的是**旧 dll**，
   报告照样 PASS —— 这是假绿。判据：比对 dll 与源码的 mtime。

② 永远不要手搓 Unity 命令
   runner 已经做了：写 active_scenario.txt / 编译 / 起 Unity / 轮询报告(180s) / 杀进程 / 恢复 JEngine.proj。
   自己拼命令最容易漏掉「先编译」和「JEngine.proj 恢复」这两步。

③ Unity 可能跑完不退出
   LocalTestBatchRunner 在 delayCall 里调 EditorApplication.Exit，batchmode 下可能卡死不退。
   报告已写出但进程还活着 ⇒ 表现是「测试早就 PASS 了，脚本却一直等着」。
   runner 里已有 Stop-Process 兜底；若手工跑，看到报告就手动杀 Unity。

④ 热更代码里别用 lambda / 委托
   例：list.Sort((a, b) => ...) 会抛
   KeyNotFoundException: Cannot find Delegate Adapter for: Xxx/<>c.<Method>b__N_M(...)
   ILRuntime 需要 AppDomain.DelegateManager 预注册适配器。改成手写排序/显式循环。

⑤ 测试用例必须自己 catch
   只写 try/finally 不写 catch 时，异常会冒到 TryRun 的外层 catch，
   那里会新建一个空 CaseResult ⇒ **本次已通过的断言全部丢失**，报告里只剩一条 FAIL。
   另外 ④ 那类委托异常在这个位置常常表现为
   「System.StackOverflowException at RuntimeStack.InitializeFrame」，看着像配置加载递归，
   实际是 CLRMethod.Invoke 处理异常时把栈耗尽 —— 别顺着栈溢出方向查。
   排查手法：在用例里插 Log.Print("[xx] stepN") 逐步打点。

⑥ runner/*.ps1 不要用脚本批量改写
   文件是「无 BOM UTF-8 + CRLF」。用 `io.open(p, encoding='utf-8')` 读再 `newline=''` 写，
   universal newlines 会把 CRLF 统一成 LF；PowerShell 5.1 再按 ANSI 解码中文注释，
   就会出现「字符串缺少终止符 / 表达式或语句中有意外的标记 }」这类看不懂的解析错误。
   改 ps1 请用编辑工具逐行改，或显式 `newline='\r\n'` + `encoding='utf-8'` 写回。
```

## 正式包

正式构建不要加 `--test`：

```bat
编译热更DLL.bat --no-pause
```

这样 `LOCAL_TEST` 未定义，测试目录不会产生任何类型，业务代码中的测试挂点也不会编译。

跑完测试后**一定要重打正式包**，否则 `Hidden~` 里留着带 LOCAL_TEST 的 dll。
（runner 的 `Invoke-Batchmode.ps1` 跑的是 `--test` 版本，不会自动恢复。）

结果查看：

```text
LocalTestHarness/reports/last_report.json
Console: [LocalTest][PASS] / [LocalTest][FAIL]
```
