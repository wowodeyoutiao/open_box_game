using HotUpdateScripts;
using HotUpdateScripts.ProtoM;
using JEngine.LitJson;
using JEngine.Singleton;
using MetaFramework.UI;
using pb;
using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;
using UnityEngine.UI;

namespace JEngine.Helper
{
    public class GameUtil : SingletonTemplate<GameUtil>
    {
        // Unix 纪元的 DateTime 对象 (1970 年 1 月 1 日)
        private DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public long GetCurrentTimeSeconds()
        {
            // 计算两个 DateTime 对象之间的时间差
            TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - epoch;
            // 将 TimeSpan 对象转换为秒
            long TotalSeconds = (long)timeSpan.TotalSeconds - GameM.Instance.SeverTimeOff;

            return TotalSeconds;
        }

        /// 获取当前时间到指定至时间总毫秒秒数
        public double GetCurrentMSTimeSeconds()
        {
            // 计算两个 DateTime 对象之间的时间差
            TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - epoch;
            return timeSpan.TotalMilliseconds - GameM.Instance.SeverTimeOff * 1000;
        }

        public string GetTimeToYMD(long timestampInSeconds)
        {
            DateTime dateTimeUtc = DateTimeOffset.FromUnixTimeSeconds(timestampInSeconds).LocalDateTime;
            // 转换为"年-月-日 时：分"格式的字符串
            return dateTimeUtc.ToString("yyyy-MM-dd HH:mm");
        }

        public DateTime GetDataTime(long timestampInSeconds)
        {
            // 转换为"年-月-日 时：分"格式的字符串
            return DateTimeOffset.FromUnixTimeSeconds(timestampInSeconds).LocalDateTime;
        }

        /// <summary>
        /// 获取当前时间到指定至时间总秒数
        /// </summary>
        /// <returns></returns>
        public long GetNowToTimeSecoud(int day, double houre,int min = 0,int seound = 0)
        {
            DateTime dateTimeLocal = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;

            DateTime nextDayMidnight = dateTimeLocal.Date;
            if (day > 0)
                nextDayMidnight += TimeSpan.FromDays(day);

            if (houre > 0)
                nextDayMidnight += TimeSpan.FromHours(houre);

            if (min > 0)
                nextDayMidnight += TimeSpan.FromMinutes(min);

            if (seound > 0)
                nextDayMidnight += TimeSpan.FromSeconds(min);

            TimeSpan timeSpanToEndOfTheDay = nextDayMidnight - dateTimeLocal;

            return (timeSpanToEndOfTheDay.Ticks / TimeSpan.TicksPerSecond);
        }

        /// <summary>
        /// 计算截至时间到当前时间的显示
        /// </summary>
        /// <returns></returns>
        public string GetTimeStrFromHMS(long endTime)
        {
            TimeSpan timeSpan = DateTime.Now.ToUniversalTime() - epoch;
            var totalSeconds = timeSpan.TotalSeconds - endTime;
            timeSpan = TimeSpan.FromSeconds(totalSeconds);

            if (timeSpan.Days > 0)
                return $"{timeSpan.Days}天前";
            else if(timeSpan.Hours > 0)
                return $"{timeSpan.Hours}小时前";
            else if (timeSpan.Minutes > 0)
                return $"{timeSpan.Minutes}分钟前";

            return $"{timeSpan.Seconds}秒前";
        }

        /// <summary>
        /// 计算当前时间到截至时间的显示 hh:mm:ss
        /// </summary>
        /// <returns></returns>
        public string GetTimeSToHMS(long endTime)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(endTime);

            return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }

        /// <summary>
        /// 计算当前时间到截至时间的显示 天 hh:mm:ss
        /// </summary>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public string GetTimeSToDHOrHMS(long endTime)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(endTime);

            if (timeSpan.Days > 0)
                return $"{timeSpan.Days}天{timeSpan.Hours}小时";
            else if(timeSpan.Hours > 0)
                return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
            else
                return $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }

        /// <summary>
        /// 计算当前时间到截至时间的显示 天 hh:mm:ss
        /// </summary>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public string GetTimeSToDHMS(long endTime)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(endTime);

            if (timeSpan.Days > 0)
                return $"{timeSpan.Days}天 {timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
            else
                return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }

        /// <summary>
        /// 转换为 "天 时" 或 "时 分" 格式的字符串。
        /// 小于一天的时间只显示时和分，大于等于一天的时间显示天和时。
        /// </summary>
        /// <param name="timestampInSeconds"></param>
        /// <returns></returns>
        public string GetTimeToDHorHM(long timestampInSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(timestampInSeconds);
            if (timeSpan.Days > 0)
                return $"{timeSpan.Days}天{timeSpan.Hours}时";
            else
                return $"{timeSpan.Hours}时{timeSpan.Minutes}分";
        }

        /// <summary>
        /// 转换为"天 时 分"格式的字符串
        /// </summary>
        /// <param name="timestampInSeconds"></param>
        /// <returns></returns>
        public string GetTimeToDHM(long timestampInSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(timestampInSeconds);
            return $"{timeSpan.Days}天{timeSpan.Hours}时{timeSpan.Minutes}分";
        }

        /// <summary>
        /// 转换为"天 h：m"格式的字符串
        /// </summary>
        /// <param name="timestampInSeconds"></param>
        /// <returns></returns>
        public string GetTimeToDHM2(long timestampInSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(timestampInSeconds);
            // 转换为"年-月-日 时：分"格式的字符串
            return $"{timeSpan.Days}天 {timeSpan.Hours}:{timeSpan.Minutes}";
        }

        /// <summary>
        /// 转换为"天 hh:mm"格式的字符串
        /// </summary>
        /// <param name="timestampInSeconds"></param>
        /// <returns></returns>
        public string GetTimeToDHM3(long timestampInSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(timestampInSeconds);
            return $"{timeSpan.Days}天 {timeSpan.Hours:00}:{timeSpan.Minutes:00}";
        }

        /// <summary>
        /// 获取时间天数
        /// </summary>
        /// <param name="timestampInSeconds"></param>
        /// <returns></returns>
        public int GetTimeToD(long timestampInSeconds)
        {
            return TimeSpan.FromSeconds(timestampInSeconds).Days;
        }

        /// <summary>
        /// 转换为"年-月-日 时：分"格式的字符串
        /// </summary>
        /// <param name="timestampInSeconds"></param>
        /// <returns></returns>
        public TimeSpan GetTimeSpan(long timestampInSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(timestampInSeconds);
            return timeSpan;
        }

        /// <summary>
        /// 转换为 "时 分 秒" ， "分 秒" 或 "秒" 格式的字符串。
        /// </summary>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public string GetTimeToHMS(long endTime)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(endTime);

            if (timeSpan.TotalHours >= 1)
                return $"{Math.Floor(timeSpan.TotalHours)}时{timeSpan.Minutes}分{timeSpan.Seconds}秒";
            else if (timeSpan.Minutes > 0)
                return $"{timeSpan.Minutes}分{timeSpan.Seconds}秒";
            else
                return $"{timeSpan.Seconds}秒";
        }

        //检测是否在开始结束时间
        public bool CheakCurTime(string strTime,string endTime)
        {
            DateTime nowTime = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;
            DateTime startParseTime = DateTime.Parse(strTime);
            DateTime endParseTime = DateTime.Parse(endTime);
            startParseTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, startParseTime.Hour, startParseTime.Minute, startParseTime.Second) ;
            endParseTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, endParseTime.Hour, endParseTime.Minute, endParseTime.Second);

            if (startParseTime.Ticks <= nowTime.Ticks && nowTime.Ticks < endParseTime.Ticks)
                //if (DateTime.Parse(strTime).Hour <= nowTime.Hour && nowTime.Hour <= DateTime.Parse(endTime).Hour)
                return true;
            return false;
        }

        //检测是否在开始结束时间
        public bool CheakCurTimeDay(string strTime, string endTime)
        {
            DateTime nowTime = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;
            DateTime compareTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day);
            DateTime startParseTime = DateTime.Parse(strTime);
            DateTime endParseTime = DateTime.Parse(endTime);
            DateTime _startTime = compareTime.AddHours(startParseTime.Hour).AddMinutes(startParseTime.Minute).AddSeconds(startParseTime.Second);
            DateTime _endTime = compareTime.AddHours(endParseTime.Hour).AddMinutes(endParseTime.Minute).AddSeconds(endParseTime.Second);

            if (_startTime.Date <= nowTime.Date && nowTime.Date <= _endTime.Date)
                return true;
            return false;
        }

        /// <summary>
        /// 检查是否在活动时间
        /// </summary>
        /// <param name="strTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <param name="dayOfWeek">周几？</param>
        /// <returns></returns>
        public bool CheakCurTime(string strTime, string endTime, int dayOfWeek)
        {
            DateTime nowTime = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;
            DateTime compareTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day);
            DateTime startParseTime = DateTime.Parse(strTime);
            DateTime endParseTime = DateTime.Parse(endTime);
            DateTime _startTime = compareTime.AddHours(startParseTime.Hour).AddMinutes(startParseTime.Minute).AddSeconds(startParseTime.Second);
            DateTime _endTime = compareTime.AddHours(endParseTime.Hour).AddMinutes(endParseTime.Minute).AddSeconds(endParseTime.Second);

            int _dayOfWeek = dayOfWeek == 7 ? 0 : dayOfWeek;
            if (_startTime.Ticks <= nowTime.Ticks && nowTime.Ticks < _endTime.Ticks && _endTime.DayOfWeek == (DayOfWeek)_dayOfWeek)
                return true;
            return false;
        }

        public int CheckNextOpenTime(string strTime, string endTime)
        {
            DateTime _nowTime = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;
            DateTime _compareTime = new DateTime(_nowTime.Year, _nowTime.Month, _nowTime.Day);
            DateTime _startParseTime = DateTime.Parse(strTime);
            DateTime _endParseTime = DateTime.Parse(endTime);
            DateTime _startTime = _compareTime.AddHours(_startParseTime.Hour).AddMinutes(_startParseTime.Minute).AddSeconds(_startParseTime.Second);
            DateTime _endTime = _compareTime.AddHours(_endParseTime.Hour).AddMinutes(_endParseTime.Minute).AddSeconds(_endParseTime.Second);

            if (_startTime <= _nowTime && _nowTime < _endTime)
                return 0;
            else
            {
                if (_nowTime < _startTime)
                {
                    TimeSpan timeSpan = _startTime - _nowTime;
                    return (int)Math.Ceiling(timeSpan.TotalSeconds);
                }
                else
                {
                    DateTime nextStartTime = _startTime.Date.AddDays(1).AddHours(_startTime.Hour).AddMinutes(_startTime.Minute);
                    return (int)(nextStartTime - _nowTime).TotalSeconds;
                }
            }
        }

        public int CheckNextEndTime(string endTime)
        {
            DateTime _nowTime = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;
            DateTime _compareTime = new DateTime(_nowTime.Year, _nowTime.Month, _nowTime.Day);
            DateTime _endParseTime = DateTime.Parse(endTime);
            DateTime _endTime = _compareTime.AddHours(_endParseTime.Hour).AddMinutes(_endParseTime.Minute).AddSeconds(_endParseTime.Second);

            if (_nowTime < _endTime)
            {
                TimeSpan timeSpan = _endTime - _nowTime;
                return (int)Math.Ceiling(timeSpan.TotalSeconds);
            }
            else
            {
                DateTime nextEndTime = _endTime.Date.AddDays(1).AddHours(_endTime.Hour).AddMinutes(_endTime.Minute);
                return (int)Math.Ceiling((nextEndTime - _nowTime).TotalSeconds);
            }
        }

        public int CheckNextOpenTime(string strTime, string endTime, int dayOfWeek)
        {
            DateTime _nowTime = DateTimeOffset.FromUnixTimeSeconds(GetCurrentTimeSeconds()).LocalDateTime;
            DateTime _compareTime = new DateTime(_nowTime.Year, _nowTime.Month, _nowTime.Day);
            DateTime _startParseTime = DateTime.Parse(strTime);
            DateTime _endParseTime = DateTime.Parse(endTime);
            DateTime _startTime = _compareTime.AddHours(_startParseTime.Hour).AddMinutes(_startParseTime.Minute).AddSeconds(_startParseTime.Second);
            DateTime _endTime = _compareTime.AddHours(_endParseTime.Hour).AddMinutes(_endParseTime.Minute).AddSeconds(_endParseTime.Second);

            int _dayOfWeek = dayOfWeek == 7 ? 0 : dayOfWeek;
            if (_startTime <= _nowTime && _nowTime < _endTime && _nowTime.DayOfWeek == (DayOfWeek)_dayOfWeek)
            {
                return 0;
            }
            else
            {
                if (_nowTime < _startTime && _dayOfWeek <= (int)_nowTime.DayOfWeek)
                {
                    TimeSpan timeSpan = _startTime - _nowTime;
                    return (int)Math.Ceiling(timeSpan.TotalSeconds);
                }
                else
                {
                    int addDays = GetTotalDaysToDayOfWeek(_nowTime, (DayOfWeek)_dayOfWeek);
                    DateTime nextStartTime = _startTime.Date.AddDays(addDays).AddHours(_startTime.Hour).AddMinutes(_startTime.Minute);
                    return (int)Math.Ceiling((nextStartTime - _nowTime).TotalSeconds);
                }
            }
        }

        public string GetWeekDayName(int dayOfWeek)
        {
            int _dayOfWeek = dayOfWeek == 7 ? 0 : dayOfWeek;
            return GetWeekDayName((DayOfWeek)_dayOfWeek);
        }

        public string GetWeekDayName(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Sunday => "周日",
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                _ => "",
            };
        }

        /// <summary>
        /// 距离周几还有多少天。
        /// 比如今天是周5，目标是周6，那么返回1天。如果目标是周1，那么返回3天。
        /// </summary>
        /// <param name="dateTime">要计算的日期</param>
        /// <param name="targetDay">目标时间</param>
        /// <returns></returns>
        public int GetTotalDaysToDayOfWeek(DateTime dateTime, DayOfWeek targetDay)
        {
            // 计算从当前日期到目标星期的偏移量
            int daysUntilTarget = ((int)targetDay - (int)dateTime.DayOfWeek + 7) % 7;

            if (daysUntilTarget == 0)
            {
                daysUntilTarget = 7;
            }

            return daysUntilTarget;
        }

        /// <summary>
        /// 数值统一化显示
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public string FormatNumWithUnit(double number)
        {
            string unit = string.Empty;

            if (number >= 100000000) // 大于等于1亿
            {
                number /= 1000000;
                int num1 = (int)Math.Floor(number / 100);
                int num2 = (int)number % 100;
                if (num2 > 0)
                {
                    if (num2 < 10)
                        unit = $"{num1}.0{num2}";
                    else
                        unit = $"{num1}.{num2}";
                }
                else
                {
                    unit = $"{num1}";
                }
                unit += "亿";
            }
            else if (number >= 100000) // 大于等于10万
            {
                number /= 100;

                int num1 = (int)Math.Floor(number / 100);
                unit = $"{num1}";
                unit += "万";
            }
            else
            {
                unit = number.ToString(); // 其他情况，直接转换为字符串
            }

            return unit;
        }

        /// <summary>
        /// 战力数值统一化显示
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public string FormatPowerWithUnit(double number)
        {
            string unit = string.Empty;

            if (number >= 100000000) // 大于等于1亿(保留三位小数)
            {
                number /= 100000;
                int num1 = (int)Math.Floor(number / 1000);
                int num2 = (int)number % 1000;
                if (num2 > 0)
                {
                    if (num2 < 10)
                        unit = $"{num1}.00{num2}";
                    else if (num2 < 100)
                        unit = $"{num1}.0{num2}";
                    else
                        unit = $"{num1}.{num2}";
                }
                else
                {
                    unit = $"{num1}";
                }
                unit += "亿";
            }
            else if (number >= 10000000) // 大于等于1000万
            {
                int num1 = (int)Math.Floor(number / 10000);
                unit += $"{num1}万";
            }
            else
            {
                unit = number.ToString(); // 其他情况，直接转换为字符串
            }

            return unit;
        }


        /// <summary>
        /// 数值转为百分比保留一位小数显示
        /// </summary>
        /// <param name="avalue"></param>
        /// <returns></returns>
        public string GetPercentStr(float avalue)
        {
            return $"{(float)(Math.Truncate(avalue * 1000) /10)}%"; // 格式化为保留一位小数
        }

        /// <summary>
        /// 检测道具是否充足
        /// </summary>
        /// <param name="avalue"></param>
        /// <returns></returns>
        public string GetCheakNumStr(int id ,long num,string str = null)
        {
            if (str == null)
            {
                str = FormatNumWithUnit(num);
            }
            return ComCoinToCheck(id, num, null, false) ? str : "<color=#DA4E38>" + str + "</color>";
        }

        /// <summary>
        /// 设置人民币icon
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="go"></param>
        public void SetShopItemIcon(int needmoney, Image img = null)
        {
            if (img == null)
                return;

            if(needmoney == 100 ||needmoney == 101)
            {
                ResouseHelper.Instance.LoadSpriteByAtlas(GameConst.CommonAtlas, (needmoney == 100) ? "icon_Activity_common_rmb" : "icon_Activity_common_bq", img);
            }
            else
                SetItemIcon(needmoney, img);
        }


        /// <summary>
        /// 根据道具ID 填充图片
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="go"></param>
        public void SetItemIcon(int itemId,GameObject go)
        {
            if (go.GetComponent<Image>() != null)
                SetItemIcon(itemId, go.GetComponent<Image>());
        }

        public void SetItemIcon(int itemId, Image img = null,Image bgImage = null,Image natureImg = null,int _Quality = -1)
        {
            if (img != null)
            {
                 ResouseHelper.Instance.LoadSpriteByAtlas(CFG_Item.GetById(itemId).Icon, CFG_Item.GetById(itemId).Iconname, img);
            }
            if (bgImage != null)
            {
                SetItemBg((_Quality < 0 ? (int)CFG_Item.GetById(itemId).Quality: _Quality), bgImage);
            }

            if (natureImg != null)
            {
                natureImg.enabled = CFG_Item.GetById(itemId).NatureMark != 0;
                if (CFG_Item.GetById(itemId).NatureMark != 0)
                {
                     ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Common", "nature_icon_" + CFG_Item.GetById(itemId).NatureMark, natureImg);
                }
            }
        }

        public void SetItemJobIcon(int cfgId, Image img)
        {
            SetJobIcon((int)CFG_Item.GetById(cfgId).Occupylimit, img);
        }

        public void SetJobIcon(int job, Image img)
        {
            if (img != null)
            {
                if (job == 0)
                {
                    img.enabled = false;
                    return;
                }
                img.enabled = true;
                ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Common", "img_job"+ job, img);
            }
        }

        /// <summary>
        /// 根据怪物ID填充怪物头像
        /// </summary>
        /// <param name="mId"></param>
        /// <param name="img"></param>
        public void SetMonsterIcon(int mId, Image img = null)
        {
            var bossCFG = CFG_Monster.GetById(mId);
            if (img != null)
            {
                 ResouseHelper.Instance.LoadSpriteByAtlas(bossCFG.Icon, bossCFG.Iconname, img);
            }
        }

        /// <summary>
        /// 玩家头像转载
        /// </summary>
        /// <param name="hUrl"></param>
        /// <param name="img"></param>
        public void SetHeadImg(string hUrl, Image img = null)
        {
            if (string.IsNullOrEmpty(hUrl))
            {
                 ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Common", "Com_img_touxiang_bg2", img);
            }
        }


        /// <summary>
        /// 根据品质填充图品
        /// </summary>
        /// <param name="Quilte"></param>
        /// <param name="image"></param>
        public void SetItemBg(int Quilte, Image image)
        {
             ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Common", "bg_icon" + Quilte, image);
        }

        public void SetItemBg(int Quilte, GameObject image)
        {
            SetItemBg(Quilte, image.GetComponent<Image>());
        }

        /// <summary>
        /// 设置技能图片
        /// </summary>
        /// <param name="skillId"></param>
        /// <param name="image"></param>
        public void SetSkillBg(int skillId, Image image)
        {
            int idx = 1;
            if (skillId != 0 && (CFG_Skill.GetById(skillId).Skilltype == (int)CFG_Skill.SkillType.Super || CFG_Skill.GetById(skillId).Skilltype == (int)CFG_Skill.SkillType.SuperCtr))
            {
                idx = 2 ;
            }
            ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Common", "icon_caowei" + idx, image);
        }

        /// <summary>
        /// 设置伙伴品质底框（圆形底框）
        /// </summary>
        /// <param name="quality">伙伴配置</param>
        /// <param name="image">image</param>
        public void SetPartnetQualityFrame(int quality, Image image)
        {
            string resName = quality switch
            {
                1 => "img_baise",
                2 => "img_lvse",
                3 => "img_lanse",
                4 => "img_zise",
                5 => "img_fense",
                6 => "img_chengse",
                7 => "img_hongse",
                8 => "img_yellow",
                _ => "img_weijiesuo2",
            };

            ResouseHelper.Instance.LoadSpriteByAtlas("Common/Atlas/Common", resName, image);
        }

        public string GetColorString(string str, string colorHex)
        {
            return $"<color=#{colorHex}>{str}</color>";
        }

        public string GetColorString(string str, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{str}</color>";
        }

        public void SetColorText(Text text, string str, string colorHex)
        {
            text.text = GetColorString(str, colorHex);
        }

        public void SetColorText(Text text, string str, Color color)
        {
            text.text = GetColorString(str, color);
        }

        private Dictionary<int, string> QualityColorDic = new Dictionary<int, string> {
            [1] = "B7B5B1", // 白色
            [2] = "4DAE1F", // 绿色
            [3] = "64cdfe", // 蓝色
            [4] = "e597ff", // 紫色
            [5] = "ffa1a1", // 粉色
            [6] = "fd9938", // 橙色
            [7] = "ff5855", // 红色
            [8] = "D2BC00", // 黄色
        };

        private Dictionary<int, string> QualityOutlintColorDic = new Dictionary<int, string>
        {
            [1] = "666666", // 白色
            [2] = "0A5214", // 绿色
            [3] = "36429c", // 蓝色
            [4] = "551577", // 紫色
            [5] = "911414", // 粉色
            [6] = "783b06", // 橙色
            [7] = "6f1c08", // 红色
            [8] = "AB0D10", // 黄色
        };

        /// <summary>
        /// 返回道具名字（不同品质不同颜色）
        /// </summary>
        /// <param name="itemId"></param>
        public string GetItemName(int itemId,int Qulity = -1)
        {
            string equipStr = (CFG_Item.GetById(itemId).Type == 5 && Qulity > 0) ? CFG_XMLConfig.xmlConfig.EquipQualityDic[Qulity]._prefix : "";
            return $"<color=#{QualityColorDic[Qulity < 0 ? (int)CFG_Item.GetById(itemId).Quality : Qulity]}>{equipStr}{CFG_Item.GetById(itemId).Name}</color>";
        }

        public string GetStrByQuality(int qyality, string str)
        {
            return $"<color=#{QualityColorDic[qyality]}>{str}</color>";
        }

        public string GetQualityColorName(int quality)
        {
            return quality switch
            {
                1 => "白色",
                2 => "绿色",
                3 => "蓝色",
                4 => "紫色",
                5 => "粉色",
                6 => "橙色",
                7 => "红色",
                8 => "彩色",
                _ => "",
            };
        }

        private Dictionary<int, string> FightQualityColorDic = new Dictionary<int, string>
        {
            [1] = "fff6f6",
            [2] = "95ff40",
            [3] = "5efcf5",
            [4] = "f287ff",
            [5] = "ffacac",
            [6] = "ff942b",
            [7] = "fe3c3c",
            [8] = "fff712",
        };

        public string GetFightStrByQuality(int qyality, string str)
        {
            return $"<color=#{FightQualityColorDic[qyality]}>{str}</color>";
        }

        public Color GetQualityOutlineColorByItemID(int itemId)
        {
            return GetQualityOutlineColor((int)CFG_Item.GetById(itemId).Quality);
        }

        public Color GetQualityOutlineColor(int quality)
        {
            return GetQualityOutlineColor(quality, new Color(0.6f, 0.5f, 0.4f));
        }

        public Color GetQualityOutlineColor(int quality, Color defaultColor)
        {
            if (quality < 1)
            {
                return defaultColor;
            }

            bool result = ColorUtility.TryParseHtmlString($"#{QualityOutlintColorDic[quality]}", out Color color);
            if (result)
                return color;
            else
                return defaultColor;
        }

        /// <summary>
        /// 游戏统一购买
        /// </summary>
        /// <param name="MainId"></param>
        /// <param name="SusseCallBack"></param>
        public void GameToBuy(long MainId, Action SusseCallBack)
        {
            GameToBuy(MainId, 1, SusseCallBack);
        }

        public void GameToBuy(long MainId,int mailNum, Action SusseCallBack)
        {
            var storeCfg = CFG_Store.GetById((int)MainId);

            var httpInfo = new HTTP_StoreOperateRequset()
            {
                goods_uuid = MainId,
                num = mailNum,
            };

            if (SDKCtr.Instance.CurrentPlatform == SDKCtr.PlatformType.WebGL_WX)
            {
                //广告
                if (storeCfg.Limite.needmoney == 101)
                {
                    SDKCtr.Instance.ShowRewardedVideo(OnBuy,
                        () =>
                        {
                            UITool.ShowTips("广告观看取消");
                        });
                }
                //充值
                else if (storeCfg.Limite.needmoney == 100)
                {
                    httpInfo.platform = PlayerPrefsExtend.GetString(SDKCtr.SaveKey.WX_Platform.ToString());
                    var WXItem = new HTTP_StoreOperateWXItem
                    {
                        mode = "goods",
                        buyQuantity = mailNum,
                        env = 1,
                        currencyType = "CNY",
                        productId = MainId,
                        goodsPrice = storeCfg.Limite.Num * mailNum * 100,
                    };
                    httpInfo.extra = JsonMapper.ToJson(WXItem);
                    if (PlayerPrefsExtend.GetString(SDKCtr.SaveKey.WX_Platform.ToString()) == "wc_ios")
                        OnBuy();
                    else
                        HttpC.OnCheakSeeion((_r) =>
                        {
                            if (_r.code == 200)
                                OnBuy();
                            else
                                SDKCtr.Instance.OnWXWebLogin((code) =>
                                {
                                    GameM.Instance.WXCode = code;
                                    Debug.Log(" GameToBuy  " + GameM.Instance.WXCode);
                                    HttpC.OnGetOpenID(code, (msg) =>
                                    {
                                        Debug.Log(" OnGetOpenID Buy 刷新  " + GameM.Instance.WXCode);
                                        OnBuy();
                                    });
                                }, null);
                        });
                }
                else
                {
                    OnBuy();
                }
            }
            else
            {
                OnBuy();
            }

            void OnBuy()
            {
                HttpC.OnGameToBuy(JsonMapper.ToJson(httpInfo), (_r) =>
                {
                    if (_r.code == 0)
                    {
                        Action winCall = delegate
                        {
                            StoreM.Instance.SetBuyNumAddByID(MainId, mailNum);
                            SusseCallBack?.Invoke();
                        };

                        if (string.IsNullOrEmpty(_r.signData) && string.IsNullOrEmpty(_r.prepay_internal_id))
                        {
                            UITool.ShowTips("购买成功");
                            winCall();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(_r.signData))
                                SDKCtr.Instance.OnWXWebPay(_r.paySig, _r.signData,_r.signature, winCall, delegate { UITool.ShowTips("购买结束"); });
                            else
                                SDKCtr.Instance.OnWXCustomerService(_r.prepay_internal_id,  winCall, delegate { UITool.ShowTips("购买结束"); });
                        }

                    }
                    else
                        UITool.ShowTips(_r.error);
                });
            }
            
        }

        public string SetAndCheckStr(string str,int id ,int num)
        {
            bool isEnough = ComCoinToCheck(id, num, null, false);

            return !isEnough ? $"<color=#DA4E38>{str}</color>" : str;
        }

        //商城ID检测
        public bool ComCoinToCheck(int mailId, Action ClsoeFun, bool NeedJump = true,int mailNum = 1)
        {
            var cfgInfo = CFG_Store.GetById(mailId);

            return ComCoinToCheck(cfgInfo.Limite.needmoney, cfgInfo.Limite.Num * mailNum, ClsoeFun, NeedJump);
        }

        //公用货币不足跳转
        public bool ComCoinToCheck(int ItemId,long Num,Action ClsoeFun,bool NeedJump = true)
        {
            string strTip = $"当前{GameUtil.Instance.GetItemName(ItemId)}不足，是否前往获取?";

            var message = new UIMessageBox.CallBack
            {
                ShowStr = strTip,
            };

            bool isEnough = BagM.Instance.GetNumByCfgID(ItemId) >= Num;
            if (!NeedJump || isEnough)
                return isEnough;

            // 行会贡献 许愿点
            if (ItemId == (int)CFG_Item.ItemID.GuildContribution || ItemId == (int)CFG_Item.ItemID.GuildWish)
            {
                message.YesFun = () =>
                {
                    ClsoeFun?.Invoke();
                    UITool.Open<UIGuildDonation>();
                };
            }

            //点券
            else if (ItemId == (int)CFG_Item.ItemID.DianQuan)
            {
                message.YesFun = () =>
                {
                    ClsoeFun?.Invoke();
                    UITool.Open<UIShop>(null, CFG_Store.StoreType.RMB);
                };
            }

            //人民币(直接返回 拉支付)
            else if (ItemId == 100)
            {
                return true;
            }
            //直升宝石
            else if (ItemId >= 44 && ItemId <= 50)
            {
                message.YesFun = () =>
                {
                    ClsoeFun?.Invoke();
                    UITool.ShowTips("暂未开放");
                    //UITool.Open<UIHall>(null, UIHall.HallType.PVE);
                };
            }
            //战魂
            else if (ItemId == (int)CFG_Item.ItemID.ZhanHun)
            {
                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return false;
                }
                else
                {
                    message.YesFun = () =>
                    {
                        if (CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_PVP))
                            return;
                        ClsoeFun?.Invoke();
                        UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Limit, UIFightChoice.ChanllengeType.PvP);
                    };
                }
            }
            //试炼凭证
            else if (ItemId == (int)CFG_Item.ItemID.SLPZ)
            {
                if (GameRoomM.Instance._GameState != GameRoomM.GameState.No)
                {
                    UITool.ShowTips("已在战斗中");
                    return false;
                }
                else
                {
                    message.YesFun = () =>
                    {
                        if (CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Play_SlTower))
                            return;
                        ClsoeFun?.Invoke();
                        UITool.Open<UIFightChoice>(null, UIFightChoice.SelectType.Challange, UIFightChoice.ChanllengeType.SLTower);
                    };
                }
            }
            else if (CFG_Item.GetById(ItemId).Type == (decimal)CFG_Item.ItemType.PartnerPice)
                message.YesFun = () =>
                {
                    if (CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Role_SkillAndCall))
                        return;

                    ClsoeFun?.Invoke();
                    HttpC.GetPrivilegeDetail(1, (_r) =>
                    {
                        UITool.Open<UIPartnerCall>(null, _r);
                    }, () => { UITool.Close<UILoading>(); });
                };

            //技能卷轴 书页
            else if (ItemId == (int)CFG_Item.ItemID.SkillJuanZhou || ItemId == (int)CFG_Item.ItemID.ShuYe)
                message.YesFun = () =>
                {
                    if (CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne.Shop_SL))
                        return;

                    ClsoeFun?.Invoke();
                    UITool.Open<UIShop>(null, CFG_Store.StoreType.SiLian);
                };
            else if(ItemId == (int)CFG_Item.ItemID.Strengthen || ItemId == (int)CFG_Item.ItemID.UpStar)
            {
                message.YesFun = () =>
                {
                    ClsoeFun?.Invoke();
                    UITool.Open<UIShop>(null, CFG_Store.StoreType.Coin);
                };
            }
            else
            {
                message.YesFun = () =>
                {
                    ClsoeFun?.Invoke();
                    UITool.Open<UIShop>(null, CFG_Store.StoreType.Coupon);
                };
            }
            UITool.Open<UIMessageBox>(null, message);

            return isEnough;
        }


        private float GetArrtValue(pb.EquipInfo eInfo, pb.Attribute attribute)
        {
            float _value = 0;
            if (eInfo != null && eInfo.Attributes != null)
            {
                foreach (var item in eInfo.Attributes)
                {
                    if (item.attr == attribute)
                    {
                        _value = item.Num;
                        break;
                    }
                }
            }
            return _value;
        }

        private float baseValue = 100f;
        private float baseFM = 10000f;
        public int GetEquipPower(pb.ItemInfo ItemInfo,int Job)
        {
            var cfgInfo = CFG_Item.GetById(ItemInfo.CfgId);

            float equipValue = CFG_XMLConfig.xmlConfig.EquipQualityDic[(int)ItemInfo.Equip.Quality]._value ;
            //生命
            float hp = (baseValue + (float)cfgInfo.HP * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.MAX_HEALTH)) * (1 + GetArrtValue(ItemInfo.Equip, pb.Attribute.HEALTH_PERCENTAGE) / baseFM) ;
            
            float wf = (baseValue + (float)cfgInfo.Phydef * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.PHYSICAL_DEFENSE)) * (1 + GetArrtValue(ItemInfo.Equip, pb.Attribute.PHYSICAL_DEFENSE_PERCENTAGE) / baseFM);
            float mf = (baseValue + (float)cfgInfo.Magdef * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.MAGIC_DEFENSE)) * (1 + GetArrtValue(ItemInfo.Equip, pb.Attribute.MAGIC_DEFENSE_PERCENTAGE) / baseFM);
            
            float jsXS = GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_FINAL_REDUCE_PERCENTAGE) + GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_REDUCE_PERCENTAGE_PLAYER) + GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_REDUCE_FIXED_MONSTER) ;
            jsXS = jsXS > 7500 ? 7500 : jsXS;

            float js = GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_REDUCE_FIXED_PLAYER) + GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_REDUCE_FIXED_MONSTER);
            //最终减伤
            float zzjs = GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_FINAL_REDUCE_FIXED);

            //有效值
            float AllHP = (hp + CFG_XMLConfig.xmlConfig.BaseValue.Power.defvalue * (wf + mf) / 2f) / (1f - jsXS / baseFM) + js / 2f + zzjs;
            //主攻击值
            float zgjz = 0;
            if (Job % 10 == 1)
                zgjz = (baseValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.PHYSICAL_ATTACK) + (float)cfgInfo.Phyatk * equipValue) * (1f + GetArrtValue(ItemInfo.Equip, pb.Attribute.PHYSICAL_ATTACK_PERCENTAGE) / baseFM);
            else
                zgjz = (baseValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.MAGIC_ATTACK) + (float)cfgInfo.Magatk * equipValue) * (1f + GetArrtValue(ItemInfo.Equip, pb.Attribute.MAGIC_ATTACK_PERCENTAGE) / baseFM);

            //增伤比
            float zsb = GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_FINAL_INCREASE_PERCENTAGE) + (float)cfgInfo.PVEdamage * equipValue + (float)cfgInfo.PVPdamage * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.PVP_DAMAGE_PERCENTAGE) + GetArrtValue(ItemInfo.Equip, pb.Attribute.PVE_DAMAGE_PERCENTAGE) + GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_FINAL_INCREASE_PERCENTAGE);

            //暴击增伤百分比
            float bjzs = baseValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.CRITICAL_DAMAGE_PERCENTAGE);

            //增伤固定总值
            float zsgdzz = (GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_INCREASE_FIXED_PLAYER) + GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_INCREASE_FIXED_MONSTER)) / 2f + GetArrtValue(ItemInfo.Equip, pb.Attribute.DMT_FINAL_INCREASE_FIXED);

            //有效输出
            float yxsc = zgjz * (1 + zsb / baseFM + bjzs / baseFM) + zsgdzz;

            float allPower = (float)Math.Sqrt(AllHP * CFG_XMLConfig.xmlConfig.BaseValue.Power.lifevalue) * (float)Math.Sqrt(yxsc * CFG_XMLConfig.xmlConfig.BaseValue.Power.attackvalue)
                +( (float)cfgInfo.Hit * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.HIT) )*( 1+ GetArrtValue(ItemInfo.Equip, pb.Attribute.HIT_PERCENTAGE)) * CFG_XMLConfig.xmlConfig.BaseValue.Power.hitvalue
                +((float)cfgInfo.Miss * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.DODGE) ) * (1 + GetArrtValue(ItemInfo.Equip, pb.Attribute.DODGE_PERCENTAGE))*CFG_XMLConfig.xmlConfig.BaseValue.Power.dodgevalue
                +((float)cfgInfo.Crit * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.CRITICAL_STRIKE)) * (1 + GetArrtValue(ItemInfo.Equip, pb.Attribute.CRITICAL_STRIKE_PERCENTAGE)) * CFG_XMLConfig.xmlConfig.BaseValue.Power.critvalue 
                +((float)cfgInfo.Decrit * equipValue + GetArrtValue(ItemInfo.Equip, pb.Attribute.TOUGHNESS)) * (1 + GetArrtValue(ItemInfo.Equip, pb.Attribute.TOUGHNESS_PERCENTAGE)) * CFG_XMLConfig.xmlConfig.BaseValue.Power.toughness;

            return (int)Math.Round(allPower);
        }

        //检测装备是否符合职业
        public bool CheakEquipQccupylimit(int cfgId)
        {
            if (CFG_Item.GetById(cfgId).Occupylimit == 0)
                return true;
            if (CFG_Item.GetById(cfgId).Occupylimit == RoleM.Instance.Job)
                return true;

            for (int i = 1; i <= 4; i++)
            {
                var solt = PartnerM.Instance.GetOnePartnerSlotInfo(i);
                if (solt.PartnerUid != 0)
                {
                    var partner = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid);
                    if ((CFG_Partner.GetById(partner.CfgId).job % 10) == CFG_Item.GetById(cfgId).Occupylimit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

         //检测装备是否符合等级
        public bool CheakEquipLevellimit(int cfgId)
        {
            if (CFG_Item.GetById(cfgId).Levellimit == 0)
                return true;
            if (CFG_Item.GetById(cfgId).Levellimit <= RoleM.Instance.Job)
                return true;

            for (int i = 1; i <= 4; i++)
            {
                var solt = PartnerM.Instance.GetOnePartnerSlotInfo(i);
                if (solt.PartnerUid != 0)
                {
                    var partner = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid);
                    if (partner.Level >= CFG_Item.GetById(cfgId).Occupylimit)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private (int, int) GetEquiopBsetByOnePartner(int soltIdx, pb.ItemInfo ItemInfo)
        {
            var equipCFG = CFG_Item.GetById(ItemInfo.CfgId);
            if (soltIdx == 0)
            {
                if (equipCFG.Occupylimit != 0 && RoleM.Instance.Job != equipCFG.Occupylimit)
                    return (0,-1);

                if (equipCFG.Levellimit > RoleM.Instance.Level)
                    return (0, -1);

                if (RoleM.Instance.CheckIsNullEquip((pb.EquipSlot)equipCFG.Equiptype))
                    return (1, soltIdx);

                return (GetEquipPower(ItemInfo,RoleM.Instance.Job) - GetEquipPower(RoleM.Instance.GetSlotInfoBySolt((pb.EquipSlot)equipCFG.Equiptype).Item, RoleM.Instance.Job),soltIdx);
            }
            else
            {
                var solt = PartnerM.Instance.GetOnePartnerSlotInfo(soltIdx);
                if (solt.PartnerUid != 0)
                {
                    var partner = PartnerM.Instance.GetOnePartnerInfoByUid(solt.PartnerUid);
                    if (equipCFG.Occupylimit != 0 && (CFG_Partner.GetById(partner.CfgId).job % 10) != equipCFG.Occupylimit)
                    {
                        return (0, -1);
                    }
                    if (partner.Level < equipCFG.Levellimit)
                    {
                        return (0, -1);
                    }
                    var _equipInfo = PartnerM.Instance.GetOnePartnerOneEquip(soltIdx, (int)equipCFG.Equiptype);
                    if (_equipInfo == null)
                        return (1, soltIdx);

                    return (GetEquipPower(ItemInfo, CFG_Partner.GetById(partner.CfgId).job) - GetEquipPower(_equipInfo, CFG_Partner.GetById(partner.CfgId).job), soltIdx);
                }
                else
                {
                    return (0, -1);
                }
            }
           
        }

        /// <summary>
        /// 检测装备是否更强(第一个值: >0 up、<0 down, 第二个值:=0 主角、>0 伙伴)
        /// </summary>
        /// <param name="ItemInfo"></param>
        /// <returns></returns>
        public (int,int) GetEquipIsBest(pb.ItemInfo ItemInfo)
        {
            int[] powers = new int[5];
            int[] sords = new int[5];
            //返回一个合适装备战力 +
            for (int i = 0; i <= 4; i++)
            {
                (powers[i], sords[i]) = GetEquiopBsetByOnePartner(i, ItemInfo);
                //返回一个合适装备战力 +
                if (powers[i] > 0)
                    return (powers[i], sords[i]);
            }
            //返回一个合适装备战力 无变动的
            for (int i = 0; i < powers.Length; i++)
            {
                if (powers[i] == 0 && sords[i] >= 0)
                    return (0, sords[i]);
            }
            //返回一个合适装备战力 -
            for (int i = 0; i < powers.Length; i++)
            {
                if (powers[i] < 0)
                    return (powers[i], sords[i]);
            }
            
            //返回都不合适
            return (0,-1);
        }

        public void OnEnegerAdd(Action JumpAction)
        {
            if (RoleM.Instance.Energy >= CFG_XMLConfig.xmlConfig.BaseValue.energy.maxValue)
            {
                UITool.ShowTips("已达到体力值上限！");
                return;

            }

            var itemInfo = CFG_Store.GetById(10001);
            UITool.Open<UIMessageBox>(null, new UIMessageBox.CallBack
            {
                ShowStr = $"是否消耗{itemInfo.Limite.Num}点券购买{itemInfo.Solditems[0].Num}点体力？\n 今日限购次数{itemInfo.Limite.limitnum - StoreM.Instance.GetBuyNumByID(itemInfo.ID)}/{itemInfo.Limite.limitnum}",
                YesFun = () => {
                    if (!GameUtil.Instance.ComCoinToCheck(10001, JumpAction))
                    {
                        return;
                    }
                    GameUtil.Instance.GameToBuy(10001, null);
                },
                BtnYes = "购买",
            });
        }

        public void SetEffectLayer(Transform effect, int layer)
        {
            var ps = effect.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < ps.Length; i++)
            {
                ps[i].sortingOrder = layer;
            }

            var canvas = effect.GetComponentsInChildren<Canvas>();
            for (int i = 0; i < canvas.Length; i++)
            {
                canvas[i].sortingOrder = layer;
            }
        }

        public bool CheakIsLock(CFG_XMLConfig.XML_Config.SwitchOpne switchOpne,bool showTip = true)
        {
            if (CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[switchOpne] > RoleM.Instance.Level)
            {
                if (showTip)
                    UITool.ShowTips(CFG_XMLConfig.xmlConfig.SwitchOpenLvDic[switchOpne] + "级解锁");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 开服后，功能已到开启时间后是否提示过新功能说明？
        /// </summary>
        /// <param name="openDay"></param>
        /// <returns></returns>
        public bool CheckIsTipNewFunction(int openDay)
        {
            return GetPlayerPrefsInt($"NewFunctionOpenTip_{RoleM.Instance.Uid}_{openDay}") == 1;
        }

        /// <summary>
        /// 开服后，功能已到开启时间后，记录提示过新功能说明。
        /// </summary>
        /// <param name="openDay"></param>
        /// <param name="isTip"></param>
        public void SetIsTipNewFunction(int openDay, bool isTip)
        {
            SetPlayerPrefs($"NewFunctionOpenTip_{RoleM.Instance.Uid}_{openDay}", isTip ? 1 : 0);
        }

        /// <summary>
        /// 开服后，功能已到开启时间后，弹出过新功能说明。
        /// </summary>
        /// <param name="openDay"></param>
        public void ShowNewFunctionTip(int openDay)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<size=24>今日开启功能</size>");
            int number = 0;
            foreach (var item in CFG_XMLConfig.xmlConfig.SwitchOpenFunction)
            {
                if (item.Value.openDay == openDay && item.Value.isShow)
                {
                    number++;
                    sb.AppendLine($"{number}.{item.Value.describe}");
                }
            }
            UITool.Open<UINewFunctionOpen>(null, sb.ToString());
        }

        public bool HaveNewFunctionTip(int openDay)
        {
            foreach (var item in CFG_XMLConfig.xmlConfig.SwitchOpenFunction)
            {
                if (item.Value.openDay == openDay && item.Value.isShow)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查开服后功能是否到达开启时间
        /// </summary>
        /// <param name="switchOpne"></param>
        /// <param name="showTip"></param>
        /// <returns></returns>
        public bool CheckOpenServerIsLock(CFG_XMLConfig.XML_Config.SwitchOpne switchOpne,bool showTip = true)
        {
            // 今天零点距离开服时间的秒数
            long sec = GetNowToTimeSecoud(1, 0) + GetCurrentTimeSeconds() - GetServerOpenTime();
            // 没到开服时间
            if(GetNowToTimeSecoud(1, 0) > sec)
            {
                if (showTip)
                    UITool.ShowTips($"未到开服时间");
                return false;
            }

            // 开服第一天0点到开服时间的秒数
            int sec_firstDay0 = (int)(sec % 86400);
            // 开服的第几天
            int dayOpenServer = (int)(sec - sec_firstDay0) / 86400 + 1;
            // 功能在开服后第几天开启。
            int funcOpenDay = GetOpenServerUnlockDay(switchOpne);
            bool isLock = dayOpenServer < funcOpenDay;
            if (isLock && showTip)
                UITool.ShowTips($"{funcOpenDay - dayOpenServer}天后开启");

            return isLock;
        }

        /// <summary>
        /// 开服的第几天
        /// </summary>
        public int GetTheDayOpenServer()
        {
            // 今天零点距离开服时间的秒数
            long sec = GetNowToTimeSecoud(1, 0) + GetCurrentTimeSeconds() - GetServerOpenTime();
            // 没到开服时间
            if (GetNowToTimeSecoud(1, 0) > sec)
            {
                return -1;
            }
            // 开服第一天0点到开服时间的秒数
            int sec_firstDay0 = (int)(sec % 86400);
            // 开服的第几天
            int dayOpenServer = (int)(sec - sec_firstDay0) / 86400 + 1;
            return dayOpenServer;
        }

        /// <summary>
        /// 开服后，距离功能开启还剩多少天？（开服几天后开启功能）
        /// </summary>
        /// <returns>-1：未到开服时间</returns>
        public int RemainingDaysUnlockOpen(CFG_XMLConfig.XML_Config.SwitchOpne switchOpne)
        {
            // 今天零点距离开服时间的秒数
            long sec = GetNowToTimeSecoud(1, 0) + GetCurrentTimeSeconds() - GetServerOpenTime();
            // 没到开服时间
            if (GetNowToTimeSecoud(1, 0) > sec)
            {
                return -1;
            }

            // 开服第一天0点到开服时间的秒数
            int sec_firstDay0 = (int)(sec % 86400);
            // 开服的第几天
            int dayOpenServer = (int)(sec - sec_firstDay0) / 86400 + 1;
            // 功能在开服后第几天开启。
            int funcOpenDay = GetOpenServerUnlockDay(switchOpne);
            return funcOpenDay - dayOpenServer;
        }

        /// <summary>
        /// 开服第几天功能开启？
        /// </summary>
        /// <param name="switchOpne"></param>
        /// <returns></returns>
        public int GetOpenServerUnlockDay(CFG_XMLConfig.XML_Config.SwitchOpne switchOpne)
        {
            if(CFG_XMLConfig.xmlConfig.SwitchOpenDayDic.ContainsKey(switchOpne))
                return CFG_XMLConfig.xmlConfig.SwitchOpenDayDic[switchOpne];

            return 1;
        }

        #region 游戏设置项保存
        public void SetPlayerPrefs(string key, string value)
        {
            string valueString = PlayerPrefsExtend.GetString(GameConst.GameSettingKay);
            Dictionary<string, string> settings = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(valueString))
            {
                settings.Add(key, value);
            }
            else
            {
                settings = JsonMapper.ToObject<Dictionary<string, string>>(valueString);

                if (settings.ContainsKey(key))
                {
                    settings[key] = value;
                }
                else
                {
                    settings.Add(key, value);
                }
            }

            PlayerPrefsExtend.SetString(GameConst.GameSettingKay, JsonMapper.ToJson(settings));
        }

        public string GetPlayerPrefs(string key)
        {
            return GetPlayerPrefs(key, "");
        }

        public string GetPlayerPrefs(string key, string defaultValue)
        {
            string valueString = PlayerPrefsExtend.GetString(GameConst.GameSettingKay);
            if (string.IsNullOrEmpty(valueString))
            {
                return defaultValue;
            }

            Dictionary<string, string> settings = JsonMapper.ToObject<Dictionary<string, string>>(valueString);
            if(settings.TryGetValue(key, out string value))
            {
                return value;
            }
            else
            {
                return defaultValue;
            }
        }

        public void RemovePlayerPrefs(string key)
        {
            string valueString = PlayerPrefsExtend.GetString(GameConst.GameSettingKay);
            if (string.IsNullOrEmpty(valueString))
            {
                return;
            }
            Dictionary<string, string> settings = JsonMapper.ToObject<Dictionary<string, string>>(valueString);
            if (settings.ContainsKey(key))
            {
                settings.Remove(key);
                PlayerPrefsExtend.SetString(GameConst.GameSettingKay, JsonMapper.ToJson(settings));
            }
        }

        public void SetPlayerPrefs(string key, int value)
        {
            SetPlayerPrefs(key, value.ToString());
        }

        public int GetPlayerPrefsInt(string key)
        {
            if (int.TryParse(GetPlayerPrefs(key), out int value))
            {
                return value;
            }
            return 0;
        }

        public int GetPlayerPrefsInt(string key, int defaultValue)
        {
            if (int.TryParse(GetPlayerPrefs(key), out int value))
            {
                return value;
            }
            return defaultValue;
        }

        public void SetPlayerPrefs(string key, float value)
        {
            SetPlayerPrefs(key, value.ToString());
        }

        public float GetPlayerPrefsFloat(string key)
        {
            if (float.TryParse(GetPlayerPrefs(key), out float value))
            {
                return value;
            }
            return 0;
        }
        public float GetPlayerPrefsFloat(string key, int defaultValue)
        {
            if (float.TryParse(GetPlayerPrefs(key), out float value))
            {
                return value;
            }
            return defaultValue;
        }

        #endregion

        #region 开服后功能是否开启
        // 获取当前服务器名称
        public string GetServerName()
        {
            var selectSever = GameM.Instance.SeverInfoDic[GameM.Instance.SeverDicIdx][GameM.Instance.SeverIdx];
            return selectSever.name;
        }

        /// <summary>
        /// 开服时间
        /// </summary>
        /// <returns></returns>
        public long GetServerOpenTime()
        {
            var selectSever = GameM.Instance.SeverInfoDic[GameM.Instance.SeverDicIdx][GameM.Instance.SeverIdx];
            return selectSever.open_by / 1000;
        }

        /// <summary>
        /// 已开服时间
        /// </summary>
        /// <returns></returns>
        public long GetServerOpeningTime()
        {
            return GetCurrentTimeSeconds() - GetServerOpenTime();
        }

        #endregion

        #region TD 相关
        public void SendTDGuideCompleted(int guideStep, string guideName)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add("guide_step", guideStep);
            properties.Add("guide_name", guideName);

            //TDAnalyticsUtil.Track("guide_completed", properties, guideStep == CFG_Guidance.LastGuideId());
        }

        #endregion
    }
}
