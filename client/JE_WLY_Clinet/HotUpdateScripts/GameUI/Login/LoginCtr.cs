using JEngine.Core;
using JEngine.Event;
using JEngine.MyNet;
using MetaFramework.UI;
using pb;
using System.Configuration;
using UnityEngine;

namespace HotUpdateScripts
{
    public class LoginCtr : MonoBehaviour
    {
        public void Awake()
        {
            UITool.Open<UILogin>();
        }
    }
}


