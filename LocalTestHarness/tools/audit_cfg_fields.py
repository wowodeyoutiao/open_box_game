# -*- coding: utf-8 -*-
"""
配置表字段对账：json 有的 key、C# 配置类里有没有。

背景：HotUpdateScripts/Config/CFG_*.cs 是手写的解析类，多次出现
「json 里有字段、类里没定义」的情况（CFG_EquipStar.FailLevel、
CFG_Compose.Produced、CFG_Map.MonsterIds.point），表现为运行时悄悄拿到
默认值 0 / null，排查成本很高。这个脚本把所有表的缺口一次列出来。

用法：
    python audit_cfg_fields.py            # 打印全部缺口
    python audit_cfg_fields.py --used     # 只打印「当前游戏已接/即将接」的表

判定口径：
- json 的 key 取所有行并集（有些字段只在部分行出现）
- 类侧的字段名取 CFGItem_* 类体内所有 public 字段 / 属性名（含嵌套类，不区分层级）
- 只报「json 有、类没有」；「类有、json 没有」只作提示（属于冗余，不影响正确性）
"""

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TEXT_ASSETS = os.path.join(
    ROOT, "client", "JE_WLY_Clinet", "Assets", "HotUpdateResources",
    "Main", "Other", "TextAssets")
CONFIG_DIR = os.path.join(
    ROOT, "client", "JE_WLY_Clinet", "HotUpdateScripts", "Config")

# 当前游戏已经接进本地服务层 / 下一阶段要接的表（其余表按需再看）
ACTIVE_TABLES = {
    "item", "compose", "equipmaster", "equipstar", "equipstrengthen",
    "map", "monster", "room", "battle", "role", "task", "store",
    "partner", "skill", "skilleffect", "pet",
}

# 已知缺口：json 有、类里没定义，但「当前这个游戏版本不需要」或「已用别的字段等价实现」。
# value = 处理时机 / 原因。做对应模块时把条目删掉，脚本就会重新报出来当待办。
KNOWN_GAPS = {
    "compose": {
        "Return": "失败返还，只有宠物合成在用（player.go:8154），配置当前全为 null",
        "key": "Return 的嵌套结构（KeyValueEx2），依赖 Return",
        "values": "同上",
    },
    "item": {
        "RandomVariable": "原服只加载不使用（init.go:577 读入后没有任何读取点）",
    },
    "monster": {
        "AI": "当前怪物用 Chase/ViewRange 做被动仇恨，AI 字段原服未在本游戏分支使用",
        "AttackRange": "同上",
        "Trigger": "同上",
    },
    "partner": {"AI": "同上"},
    "battle": {"MapMark": "battle 表地图标记，原服战斗流程未读取"},
    "room": {"TotalTables": "房间分表标记，原服未读取"},
    "skill": {
        "Buffround": "技能 buff 相关，当前技能效果走 SkillEffect",
        "Bufftime": "同上",
        "Cooldown": "冷却读的是别的字段（SkillEffect/技能表既有字段）",
        "Effectnum": "同上",
        "Effecttrigger": "同上",
        "HPDam": "同上",
        "Object": "同上",
        "Powerfactor": "同上",
        "ReducedCD": "同上",
        "RoundMark": "同上",
        "Skillbuff": "同上",
        "Skilltrigger": "同上",
    },
    "skilleffect": {"TargetName": "描述性字段，逻辑不用"},
    "task": {
        "ActivID": "活动任务，当前只做主线/日常",
        "UUID": "任务表主键别名，按 ID 索引即可",
        "limit": "次数上限，当前任务系统未用",
    },
    "pet": {
        # 55 节已接入宠物数据/属性/养成/获取，只剩「天赋 + buff 时长」没做：
        "Bufftime": "宠物 buff 时长，当前不走 buff 系统（宠物只做属性加成）",
        "Talent": "宠物天赋（id + rate + value[]），原服宠物不参战，天赋未参与任何计算",
        "rate": "Talent 的嵌套字段，依赖 Talent",
    },
    "activity": {"ResetTime": "活动系统未做", "StartTime": "活动系统未做"},
    "buffeffect": {"*": "buff 系统未做（技能 buff 走技能表）"},
    "actionset": {"*": "动作集未做"},
    "lamp": {"Receptor": "跑马灯接收者，单机不需要"},
    "privilege": {"Energystate": "特权卡体力状态，单机不做体力"},
    "prizewheel": {"Priority": "转盘未做"},
    "randomdistrib": {"*": "随机分配未做"},
    "summon": {"*": "抽卡未做"},
    "guildexp": {"*": "公会未做"},
    "endlesstrials": {"Monster": "试炼塔怪物走 battle 表", "gap": "同上",
                      "max": "同上", "point": "同上"},
}

FIELD_RE = re.compile(r"public\s+[\w\.\<\>\[\]\?,\s]+?\s+(\w+)\s*(?:=|;)")
PROP_RE = re.compile(r"public\s+[\w\.\<\>\[\]\?,\s]+?\s+(\w+)\s*\{\s*get")


def strip_comments(src):
    """
    去掉 // 行注释与 /* */ 块注释（粗略处理：字符串内的 // 不处理，实际配置类里没有这种情况）。
    只为了让字段抽取不看注释里的字段声明。
    """
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    out = []
    for line in src.splitlines():
        # 逐行去掉 // 之后的内容（引号内的 // 极少见，配置类里没有 URL 之类）
        pos = line.find("//")
        if pos >= 0 and line.count('"', 0, pos) % 2 == 0:
            line = line[:pos]
        out.append(line)
    return "\n".join(out)


def load_class_fields():
    """
    返回 {(表基名小写): (类名, set(字段名))}。

    ⚠️ 按**文件**收集字段，不按 class 块切：CFG_Item.cs 这类文件里
    CFGItem_Item 后面还跟着嵌套类 / 同表辅助类（CFGItem_ItemTimeliness），
    按 class 切会把主类后面的字段切丢（第一版就踩了这个坑，误报了 24 个字段）。
    """
    result = {}
    for name in os.listdir(CONFIG_DIR):
        if not name.startswith("CFG_") or not name.endswith(".cs"):
            continue
        base = name[len("CFG_"):-len(".cs")].lower()
        path = os.path.join(CONFIG_DIR, name)
        with open(path, encoding="utf-8-sig", errors="replace") as fp:
            src = fp.read()

        m = re.search(r"class\s+(CFGItem_\w+)", src)
        if not m:
            continue
        # ⚠️ 先剥注释再抽字段：CFG_Pet 里 `//public AttributeItem.Item[] PetKind;`
        #    这种注释掉的字段会被正则当成真字段，导致「类里没有」被漏报成「已有」
        #    （第一版就踩了，pet.PetKind 明明没实现却没报缺口）。
        code = strip_comments(src)
        fields = set(FIELD_RE.findall(code)) | set(PROP_RE.findall(code))
        fields.discard("GetIdx")
        result[base] = (m.group(1), fields)
    return result


def table_to_class_key(json_base):
    """item -> item（用基名去匹配 CFGItem_item，大小写不敏感）"""
    return json_base.lower()


def collect_keys(node, out, depth=0):
    """
    收集 json 里的 key，**递归进嵌套对象**。

    map.json 的 MonsterIds[].point 这类嵌套字段，只看顶层是查不出来的
    （第一版就是这么漏掉 point 的）。但递归要挡住两类噪音：
      - 纯数字 key（item.json 的 Decompose 是 {"12": 3} 这种 map）
      - 深度超过 2 层（再深就是运行时数据，不是表结构）
    """
    if depth > 2:
        return
    if isinstance(node, dict):
        for k, v in node.items():
            if isinstance(k, str) and not k.isdigit():
                out.add(k)
            collect_keys(v, out, depth + 1)
    elif isinstance(node, list):
        for v in node:
            collect_keys(v, out, depth + 1)


def audit(only_used=False):
    class_fields = load_class_fields()

    report = []
    for base in sorted(class_fields):
        name = base + ".json"
        if name not in os.listdir(TEXT_ASSETS):
            continue
        cls, fields = class_fields[base]
        if only_used and base not in ACTIVE_TABLES:
            continue

        path = os.path.join(TEXT_ASSETS, name)
        try:
            with open(path, encoding="utf-8-sig") as fp:
                rows = json.load(fp)
        except Exception as exc:                      # noqa: BLE001
            report.append((base, cls, ["<json 解析失败: %s>" % exc], []))
            continue
        if not isinstance(rows, list) or not rows:
            continue

        keys = set()
        for row in rows:
            if isinstance(row, dict):
                collect_keys(row, keys)

        missing = sorted(k for k in keys if k not in fields)
        extra = sorted(k for k in fields if k not in keys)
        if missing:
            report.append((base, cls, missing, extra))

    if not report:
        print("OK: 没有发现字段缺口")
        return 0

    unexpected = 0
    known_lines = []
    for base, cls, missing, extra in report:
        gaps = KNOWN_GAPS.get(base, {})
        todo = []
        for f in missing:
            reason = gaps.get(f, gaps.get("*"))
            if reason:
                known_lines.append("  %-16s %-22s %s  ← %s" % (base, f, cls, reason))
            else:
                todo.append(f)
        if todo:
            unexpected += 1
            print("  %-16s %-26s **未预期缺口**: %s" % (base, cls, ", ".join(todo)))
        if extra:
            print("  %-16s %-26s 冗余(可忽略): %s" % ("", "", ", ".join(extra)))

    if known_lines:
        print("\n已知缺口（当前游戏不需要，做对应模块时再补）：")
        for line in known_lines:
            print(line)

    print("\n未预期缺口表数: %d" % unexpected)
    return unexpected


if __name__ == "__main__":
    only_used = "--used" in sys.argv
    audit(only_used)
