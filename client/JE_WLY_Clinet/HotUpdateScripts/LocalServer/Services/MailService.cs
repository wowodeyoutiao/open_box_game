using System;
using System.Collections.Generic;
using JEngine.Core;
using JEngine.LitJson;
using JEngine.Singleton;
using pb;

namespace HotUpdateScripts.LocalNet
{
    /// <summary>
    /// 单机系统邮件。
    ///
    /// 只替代原服“系统信/背包满转邮件”这一条链路，不做玩家邮件与多人通知。
    /// 邮件附件在领取时进入背包；背包放不下时拒绝领取，邮件保留。
    /// </summary>
    public class MailService : SingletonTemplate<MailService>
    {
        public sealed class MailAttachment
        {
            public int CfgId;
            public long Num;
        }

        private const int DefaultMailMax = 200;
        private const int MailExpireDays = 30;

        public HotUpdateScripts.ProtoM.HTTP_MailListResponse GetList()
        {
            var response = new HotUpdateScripts.ProtoM.HTTP_MailListResponse
            {
                code = 0,
                total = 0,
                data = new List<HotUpdateScripts.ProtoM.HTTP_MailInfo>(),
            };
            LocalRole role = LocalData.Instance.Current;
            if (role == null || role.Mails == null)
                return response;

            response.total = role.Mails.Count;
            for (int i = 0; i < role.Mails.Count; i++)
                response.data.Add(ToHttpMail(role.Mails[i]));
            return response;
        }

        public HotUpdateScripts.ProtoM.Http_MailDetailResponse GetDetail(string body)
        {
            var response = new HotUpdateScripts.ProtoM.Http_MailDetailResponse
            {
                code = -1,
                data = default(HotUpdateScripts.ProtoM.HTTP_MailInfo),
            };
            LocalRole role = LocalData.Instance.Current;
            LocalMail mail = FindMail(role, GetMailId(body));
            if (mail == null)
                return response;
            response.code = 0;
            response.data = ToHttpMail(mail);
            return response;
        }

        public HotUpdateScripts.ProtoM.Http_MailOperateResponse Extract(string body)
        {
            var response = new HotUpdateScripts.ProtoM.Http_MailOperateResponse { code = -1, data = "" };
            LocalRole role = LocalData.Instance.Current;
            LocalMail mail = FindMail(role, GetMailId(body));
            if (mail == null)
            {
                response.data = "邮件不存在";
                return response;
            }
            return ExtractOne(role, mail);
        }

        public HotUpdateScripts.ProtoM.Http_MailOperateResponse ExtractAll()
        {
            var response = new HotUpdateScripts.ProtoM.Http_MailOperateResponse { code = 0, data = "成功" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null || role.Mails == null)
                return response;

            for (int i = 0; i < role.Mails.Count; i++)
            {
                LocalMail mail = role.Mails[i];
                if (mail == null || mail.IsAttachments == 0 || mail.IsExtract != 0)
                    continue;
                var one = ExtractOne(role, mail);
                if (one.code != 0)
                    return one;
            }
            SaveCoordinator.Instance.MarkDirty();
            return response;
        }

        public HotUpdateScripts.ProtoM.Http_MailOperateResponse Delete(string body)
        {
            var response = new HotUpdateScripts.ProtoM.Http_MailOperateResponse { code = -1, data = "" };
            LocalRole role = LocalData.Instance.Current;
            if (role == null || role.Mails == null)
            {
                response.data = "没有角色";
                return response;
            }

            string id = GetMailId(body);
            for (int i = 0; i < role.Mails.Count; i++)
            {
                if (role.Mails[i] != null && role.Mails[i].MailId == id)
                {
                    role.Mails.RemoveAt(i);
                    SaveCoordinator.Instance.MarkDirty();
                    response.code = 0;
                    response.data = "成功";
                    return response;
                }
            }
            response.data = "邮件不存在";
            return response;
        }

        public HotUpdateScripts.ProtoM.Http_MailOperateResponse DeleteAll()
        {
            LocalRole role = LocalData.Instance.Current;
            if (role != null && role.Mails != null)
            {
                role.Mails.Clear();
                SaveCoordinator.Instance.MarkDirty();
            }
            return new HotUpdateScripts.ProtoM.Http_MailOperateResponse { code = 0, data = "成功" };
        }

        /// <summary>背包满时把奖励转为系统邮件。</summary>
        public bool SendSystemMail(LocalRole role, string desc, List<MailAttachment> attachments)
        {
            if (role == null)
                return false;
            if (role.Mails == null)
                role.Mails = new List<LocalMail>();

            int max = GetMailMax();
            if (max > 0 && role.Mails.Count >= max)
            {
                Log.PrintWarning("[MailService] 邮件已满，奖励无法转邮件");
                return false;
            }

            var mail = new LocalMail
            {
                MailId = NowUnix().ToString() + "_" + role.Mails.Count,
                Title = "系统邮件",
                Desc = string.IsNullOrEmpty(desc) ? "背包已满，奖励转邮件" : desc,
                IsRead = 0,
                IsExtract = 0,
                IsAttachments = attachments != null && attachments.Count > 0 ? 1 : 0,
                CreatedAt = NowUnix(),
                ExpiryAt = NowUnix() + MailExpireDays * 86400L,
                Attachments = new List<ItemInfo>(),
            };

            if (attachments != null)
            {
                for (int i = 0; i < attachments.Count; i++)
                {
                    MailAttachment a = attachments[i];
                    if (a == null || a.CfgId <= 0 || a.Num <= 0)
                        continue;
                    mail.Attachments.Add(CreateItem(a.CfgId, a.Num));
                }
            }

            role.Mails.Add(mail);
            SaveCoordinator.Instance.MarkDirty();
            return true;
        }

        private HotUpdateScripts.ProtoM.Http_MailOperateResponse ExtractOne(LocalRole role, LocalMail mail)
        {
            var response = new HotUpdateScripts.ProtoM.Http_MailOperateResponse { code = -1, data = "" };
            if (mail.IsExtract != 0)
            {
                response.data = "该邮件已领取";
                return response;
            }

            if (mail.Attachments != null && mail.Attachments.Count > 0)
            {
                if (!CanAddAttachments(role, mail.Attachments, out string error))
                {
                    response.data = error;
                    return response;
                }
                for (int i = 0; i < mail.Attachments.Count; i++)
                    AddAttachment(role, mail.Attachments[i]);
            }

            mail.IsExtract = 1;
            mail.IsRead = 1;
            SaveCoordinator.Instance.MarkDirty();
            LocalPusher.Instance.PushUserBaseNow();
            LocalPusher.Instance.PushBagInfoNow();
            response.code = 0;
            response.data = "成功";
            return response;
        }

        private static bool CanAddAttachments(LocalRole role, List<ItemInfo> attachments, out string error)
        {
            error = "";
            if (attachments == null)
                return true;
            for (int i = 0; i < attachments.Count; i++)
            {
                ItemInfo item = attachments[i];
                if (item == null || item.CfgId <= 0)
                    continue;
                if (item.CfgId == 1 || item.CfgId == 2 || item.CfgId == 14)
                    continue;
                if (!EquipRuleService.CanAddToBag(role, item, out error))
                    return false;
            }
            return true;
        }

        private static void AddAttachment(LocalRole role, ItemInfo source)
        {
            if (source == null || source.CfgId <= 0)
                return;
            if (source.CfgId == 1)
            {
                role.Coin += source.Num;
                LocalData.Instance.Gold = role.Coin;
                return;
            }
            if (source.CfgId == 2)
            {
                LevelUpService.Instance.AddExp(role, source.Num);
                LocalData.Instance.Exp = role.Exp;
                LocalData.Instance.Level = role.Level;
                return;
            }
            if (source.CfgId == 14)
            {
                role.TreasuerBox += source.Num;
                LocalData.Instance.TreasuerBox = role.TreasuerBox;
                return;
            }

            if (role.BagData == null)
                role.BagData = new Bag { ItemList = new List<ItemInfo>() };
            if (role.BagData.ItemList == null)
                role.BagData.ItemList = new List<ItemInfo>();

            if (!EquipRuleService.IsEquipment(source))
            {
                for (int i = 0; i < role.BagData.ItemList.Count; i++)
                {
                    ItemInfo old = role.BagData.ItemList[i];
                    if (old != null && old.CfgId == source.CfgId && !EquipRuleService.IsEquipment(old))
                    {
                        old.Num += source.Num;
                        return;
                    }
                }
            }

            int count = role.BagData.ItemList.Count;
            var items = new ItemInfo[count + 1];
            for (int i = 0; i < count; i++)
                items[i] = role.BagData.ItemList[i];
            items[count] = source;
            role.BagData.ItemList = System.Linq.Enumerable.ToList(items);
        }

        private static LocalMail FindMail(LocalRole role, string mailId)
        {
            if (role == null || role.Mails == null || string.IsNullOrEmpty(mailId))
                return null;
            for (int i = 0; i < role.Mails.Count; i++)
            {
                if (role.Mails[i] != null && role.Mails[i].MailId == mailId)
                    return role.Mails[i];
            }
            return null;
        }

        private static HotUpdateScripts.ProtoM.HTTP_MailInfo ToHttpMail(LocalMail mail)
        {
            var info = new HotUpdateScripts.ProtoM.HTTP_MailInfo
            {
                mail_id = mail.MailId,
                title = mail.Title,
                desc = mail.Desc,
                is_read = mail.IsRead,
                is_extract = mail.IsExtract,
                is_attachments = mail.IsAttachments,
                created_at = mail.CreatedAt,
                expiry_at = mail.ExpiryAt,
                attachments = new HotUpdateScripts.ProtoM.Http_AttachmentInfo[0],
            };
            if (mail.Attachments != null && mail.Attachments.Count > 0)
            {
                var arr = new HotUpdateScripts.ProtoM.Http_AttachmentInfo[mail.Attachments.Count];
                for (int i = 0; i < mail.Attachments.Count; i++)
                {
                    ItemInfo item = mail.Attachments[i];
                    if (item == null) continue;
                    arr[i] = new HotUpdateScripts.ProtoM.Http_AttachmentInfo
                    {
                        user_props_id = item.CfgId,
                        num = (int)item.Num,
                    };
                }
                info.attachments = arr;
            }
            return info;
        }

        private static string GetMailId(string body)
        {
            if (string.IsNullOrEmpty(body))
                return "";
            try
            {
                Dictionary<string, object> dict =
                    JsonMapper.ToObject<Dictionary<string, object>>(body);
                if (dict != null && dict.ContainsKey("mail_id"))
                    return dict["mail_id"] == null ? "" : dict["mail_id"].ToString();
            }
            catch { }

            string marker = "\"mail_id\"";
            int index = body.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0) return "";
            int colon = body.IndexOf(':', index + marker.Length);
            if (colon < 0) return "";
            int first = body.IndexOf('"', colon + 1);
            if (first < 0) return "";
            int second = body.IndexOf('"', first + 1);
            if (second < 0) return "";
            return body.Substring(first + 1, second - first - 1);
        }

        private static ItemInfo CreateItem(int cfgId, long num)
        {
            CFGItem_Item cfg;
            try { cfg = CFG_Item.GetById(cfgId); }
            catch { return null; }
            if (cfg == null)
                return null;

            int type = EquipRuleService.ToInt(cfg.Type);
            var item = new ItemInfo
            {
                CfgId = cfgId,
                Uid = LocalData.Instance.AllocateItemUid(),
                Num = num,
                CreateTime = NowUnix(),
                Type = (ItemType)type,
            };
            if (type == 5)
            {
                item.Equip = new EquipInfo
                {
                    Quality = ToQuality(EquipRuleService.ToInt(cfg.Quality)),
                    Attributes = new List<Stats>(),
                };
            }
            return item;
        }

        private static ItemQuality ToQuality(int value)
        {
            if (value < (int)ItemQuality.WHITE) return ItemQuality.WHITE;
            if (value > (int)ItemQuality.Rainbow) return ItemQuality.Rainbow;
            return (ItemQuality)value;
        }

        private static int GetMailMax()
        {
            try
            {
                if (CFG_XMLConfig.xmlConfig != null
                    && CFG_XMLConfig.xmlConfig.BaseValue != null
                    && CFG_XMLConfig.xmlConfig.BaseValue.mailMaxNum > 0)
                    return CFG_XMLConfig.xmlConfig.BaseValue.mailMaxNum;
            }
            catch { }
            return DefaultMailMax;
        }

        private static long NowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
