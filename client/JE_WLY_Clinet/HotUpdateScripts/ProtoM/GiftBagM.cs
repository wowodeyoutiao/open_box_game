using JEngine.Singleton;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using UnityEngine;

namespace HotUpdateScripts.ProtoM
{
    public class GiftBagM : SingletonTemplate<GiftBagM>
    {
        private List<CFGItem_Activity> showActivityList; // 活动表


        #region 每日礼包、每日特价、伙伴礼包、宠物礼包、魂石礼包
        public int GetGiftRedNum(int cfgId)
        {
            showActivityList = CFG_Activity.GetActiveListByType((CFG_Activity.Type)cfgId);
            if (showActivityList == null || showActivityList.Count == 0)
                return 0;

            var count = 0;
            for(int i = 0; i < showActivityList.Count; i++)
            {
                int shopId = showActivityList[i].PreLimit;
                var storeInfo = CFG_Store.GetById(shopId);
                bool soldOut = storeInfo.Limite.limittype != 999 && StoreM.Instance.GetBuyNumByID(shopId) >= storeInfo.Limite.limitnum;
                if(!soldOut)
                {
                    var useAd = storeInfo.Limite.needmoney == 101;
                    if(useAd)
                    {
                        count++;
                        continue;
                    }

                    // 判断总次数，购买总次数为X次后免费
                    var isFree = (storeInfo.Limite.Num == 0) || (storeInfo.Limite.times != 0 && storeInfo.Limite.times <= StoreM.Instance.GetBuyAllNum(shopId));
                    if(isFree)
                        count++;
                }
            }

            return count;
        }
        #endregion

        #region 成长基金可领取奖励数量

        List<CFGItem_Activity> cfg_GrowthFundItems;

        public void GetGrowthFundItems()
        {
            cfg_GrowthFundItems = CFG_Activity.GetActiveListByType(CFG_Activity.Type.GrowthFund);
            cfg_GrowthFundItems.Sort((a, b) => a.Content.CompareTo(b.Content));
        }

        public int GetGrowthFundCanGetAwardCount(HTTP_GrowthFundResponse response)
        {
            GetGrowthFundItems();
            int count = 0;

            var roleLevel = RoleM.Instance.Level;
            var isBuy = response.buy == 2;
            var growthFundInfos = response.data;
            for (int i = 0; i < cfg_GrowthFundItems.Count; i++)
            {
                if (cfg_GrowthFundItems[i].PreLimit > roleLevel)
                    break;

                var isLeftGeted = false;
                var isRightGeted = false;
                if (growthFundInfos != null && growthFundInfos.Length > i)
                {
                    isLeftGeted = growthFundInfos[i].status == 2;
                    isRightGeted = growthFundInfos[i].status == 2 && growthFundInfos[i].buy == 2;
                }

                if (!isLeftGeted)
                    count++;

                if (!isRightGeted && isBuy)
                    count++;
            }

            return count;
        }

        #endregion
    }
}