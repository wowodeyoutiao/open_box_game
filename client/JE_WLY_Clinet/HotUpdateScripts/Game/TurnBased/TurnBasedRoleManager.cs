using HotUpdateScripts.ProtoM;

using JEngine.Singleton;

using pb;

using System.Collections.Generic;

using Object = UnityEngine.Object;

namespace HotUpdateScripts.Game
{
    public class TurnBasedRoleManager : SingletonTemplate<TurnBasedRoleManager>
    {
        public Dictionary<int, TurnBasedRole> blueRoleDic = new Dictionary<int, TurnBasedRole>();
        public Dictionary<int, TurnBasedRole> redRoleDic = new Dictionary<int, TurnBasedRole>();

        public List<TurnBasedRole> allRoles = new List<TurnBasedRole>();

        public void AddBuleRole(TurnBasedRole role, int position)
        {
            if (role == null) return;
            blueRoleDic[position] = role;
            allRoles.Add(role);
        }

        public void AddRedRole(TurnBasedRole role, int position)
        {
            if (role == null) return;
            redRoleDic[position] = role;
            allRoles.Add(role);
        }

        public bool GetBlueRoleByPosition(int position, out TurnBasedRole role)
        {
            bool hasRole = blueRoleDic.TryGetValue(position, out TurnBasedRole _role);
            role = _role;
            return hasRole;
        }

        public bool GetRedRoleByPosition(int position, out TurnBasedRole role)
        {
            bool hasRole = redRoleDic.TryGetValue(position, out TurnBasedRole _role);
            role = _role;
            return hasRole;
        }

        public bool GetBlueRoleByUid(long uid, out TurnBasedRole role)
        {
            foreach (var roleObj in blueRoleDic.Values)
            {
                if (!Object.Equals(roleObj, null) && roleObj.uid == uid)
                {
                    role = roleObj;
                    return true;
                }
            }
            role = null;
            return false;
        }

        public bool GetRedRoleByUid(long uid, out TurnBasedRole role)
        {
            foreach (var roleObj in redRoleDic.Values)
            {
                if (!Object.Equals(roleObj, null) && roleObj.uid == uid)
                {
                    role = roleObj;
                    return true;
                }
            }
            role = null;
            return false;
        }

        public TurnBasedM.CampType GetRoleCampType(long uid)
        {
            if(GetBlueRoleByUid(uid, out var _))
            {
                return TurnBasedM.CampType.BLUE_CAMP;
            }
            else if(GetRedRoleByUid(uid, out var _))
            {
                return TurnBasedM.CampType.RED_CAMP;
            }
            else
            {
                return TurnBasedM.CampType.INVALID_CAMP;
            }
        }

        public void ClearAllRole()
        {
            blueRoleDic.Clear();
            redRoleDic.Clear();

            foreach (var role in allRoles)
            {
                if (!Object.Equals(role, null))
                {
                    Object.Destroy(role.gameObject);
                }
            }
            allRoles.Clear();
        }

        public void StopAllRoleAttack()
        {
            foreach (var role in allRoles)
            {
                if (!Object.Equals(role, null))
                    role.StopAttack();
            }
        }

        void OnDestroy()
        {
            StopAllRoleAttack();
            ClearAllRole();
        }
    }
}
