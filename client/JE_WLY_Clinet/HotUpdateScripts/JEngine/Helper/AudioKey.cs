using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using JEngine.Helper;

public class AudioKey
{

    public enum Sound
    {
        BGM_Slect,
        BGM_Creat,
        BGM_Hall,
        BGM_Fight,
        Button,

        Hurt_1,
        Hurt_2,
        Hurt_3,

        //Boss 攻击音效
        BossAttack,

        //近战怪物攻击
        JZAttack,

        //远程战怪物攻击
        YCAttack,
    }
    public static Dictionary<Sound, string> SoundDic = new Dictionary<Sound, string>
    {
        [Sound.Button] = "button.ogg",
        [Sound.BGM_Slect] = "BG_Select.mp3",
        [Sound.BGM_Hall] = "BG_Hall.mp3",
        [Sound.BGM_Creat] = "Bg_Creat.mp3",
        [Sound.BGM_Fight] = "Bg_Creat.mp3",

        [Sound.Hurt_1] = "Skill/Zs_tongyongshouji.mp3",
        [Sound.Hurt_2] = "Skill/Fs_tongyongshouji.mp3",
        [Sound.Hurt_3] = "Skill/Js_tongyongshouji.mp3",

        [Sound.BossAttack] = "Monster/BossAttack.mp3",
        [Sound.JZAttack] = "Monster/JZAttack.mp3",
        [Sound.YCAttack] = "Monster/YCAttack.mp3",
    };

}
