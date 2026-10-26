using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大厅挂机怪物外观目录。
///
/// 只描述“怎么显示”，不参与掉落或战斗数值。
/// 当前使用复制自 moester 的 UGUI SkeletonGraphic 预制体，
/// 替换对应 Monster_x 的 SkeletonData，保留普通小怪与 BOSS 的体型差异。
/// </summary>
public static class HallMonsterCatalog
{
    public sealed class Visual
    {
        public string Key;
        public string Path;
        public float MinScale;
        public float MaxScale;
        public bool DefaultFlipX;
        public string IdleAnim;
        public string AttackAnim;
        public string DeadAnim;
        public int Weight;
        public bool IsBoss;
    }

    private static readonly List<Visual> Visuals = new List<Visual>
    {
        new Visual
        {
            Key = "Monster_1_UI",
            Path = "Role/Prefabs/Monster_1_UI",
            MinScale = 0.36f,
            MaxScale = 0.42f,
            DefaultFlipX = false,
            IdleAnim = "stand",
            AttackAnim = "attack",
            DeadAnim = "dead",
            Weight = 30
        },
        new Visual
        {
            Key = "Monster_2_UI",
            Path = "Role/Prefabs/Monster_2_UI",
            MinScale = 0.40f,
            MaxScale = 0.46f,
            DefaultFlipX = false,
            IdleAnim = "stand",
            AttackAnim = "attack",
            DeadAnim = "dead",
            Weight = 30
        },
        new Visual
        {
            Key = "Monster_3_UI",
            Path = "Role/Prefabs/Monster_3_UI",
            MinScale = 0.38f,
            MaxScale = 0.44f,
            DefaultFlipX = false,
            IdleAnim = "stand",
            AttackAnim = "attack",
            DeadAnim = "dead",
            Weight = 30
        },
        new Visual
        {
            Key = "moester",
            Path = "Role/Prefabs/moester",
            MinScale = 0.26f,
            MaxScale = 0.30f,
            DefaultFlipX = false,
            IdleAnim = "stand",
            AttackAnim = "attackH",
            DeadAnim = "dead",
            Weight = 10,
            IsBoss = true
        }
    };

    public static Visual Pick()
    {
        int total = 0;
        for (int i = 0; i < Visuals.Count; i++)
            total += Mathf.Max(0, Visuals[i].Weight);

        if (total <= 0)
            return Visuals[0];

        int roll = Random.Range(0, total);
        int current = 0;
        for (int i = 0; i < Visuals.Count; i++)
        {
            current += Mathf.Max(0, Visuals[i].Weight);
            if (roll < current)
                return Visuals[i];
        }
        return Visuals[Visuals.Count - 1];
    }
}
