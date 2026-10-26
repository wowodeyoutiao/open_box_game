using JEngine.Core;
using JEngine.Singleton;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace JEngine.Helper
{
    public class ResouseHelper : SingletonTemplate<ResouseHelper>
    {
        //相对路径
        static protected string BaseResousePath = "Assets/HotUpdateResources/";

        private Dictionary<string, SpriteAtlas> spriteAtlasDict = null;
        public ResouseHelper()
        {
            spriteAtlasDict = new Dictionary<string, SpriteAtlas>();
        }

        public void Clear()
        {
            spriteAtlasDict.Clear();
        }

        public Material LoadMaterial(string abname, string packge = "Main")
        {
            abname = BaseResousePath + abname;
            if (!abname.Contains(".mat"))
            {
                abname = abname + ".mat";
            }
            return AssetMgr.Load<Material>(abname, packge);
        }

        public async Task LoadGameObj(string path,Action<GameObject> ObjAction, string packge = "Main")
        {
            path = BaseResousePath + packge + "/" + path + ".prefab";
            //Debug.Log("LoadGameObj path:" + path);
            //return AssetMgr.Load<GameObject>(path, packge);
            var obj = await AssetMgr.LoadAsync<GameObject>(path, packge); ;
            ObjAction?.Invoke(obj);
        }


        public Texture2D LoadTexture(string abname, string packge = "Main")
        {
            return AssetMgr.Load<Texture2D>(abname, packge);
        }

        public async Task LoadTextAssetAsync(string path,Action<String> action, string packge = "Main")
        {
            path = BaseResousePath + packge + "/" + path;
            var obj = await AssetMgr.LoadAsync<TextAsset>(path, packge);
            action?.Invoke(obj.text);
        }
       

        public T LoadAsset<T>(string path, string packge = "Main") where T : UnityEngine.Object
        {
            path = BaseResousePath + packge + "/" + path;
            return AssetMgr.Load<T>(path, packge);
        }

        public async Task LoadAudioClipAssetAsync(string path, Action<AudioClip> action, string packge = "Main")
        {
            path = BaseResousePath + packge + "/" + path;
            var obj = await AssetMgr.LoadAsync<AudioClip>(path, packge);
            action?.Invoke(obj);
        }


        //public async Task<T> LoadAssetAsync<T>(string path, string packge = "Main") where T : UnityEngine.Object
        //{
        //    path = BaseResousePath + packge + "/" + path;
        //    var textAsset = await AssetMgr.LoadAsync<T>(path, packge);
        //    return textAsset;
        //}


        public async Task LoadSprite(string path, Image img, string packge = "Main")
        {
            path = BaseResousePath + packge + "/" + path;
            img.sprite = await AssetMgr.LoadAsync<Sprite>(path, packge);
        }

        public async Task LoadSprite(string path, SpriteRenderer img, string packge = "Main")
        {
            path = BaseResousePath + packge + "/" + path;
            img.sprite = await AssetMgr.LoadAsync<Sprite>(path, packge);
        }

        public async void LoadSpriteByAtlas(string atlas, string iconName,Image img, string packge = "Main")
        {
            if (atlas == "" || atlas == null || iconName == "")
            {
                img.sprite = null;
            }
            var _atls = BaseResousePath + packge + "/" + atlas+ "/_Atlas";
            if (!_atls.Contains(".spriteatlas"))
            {
                _atls = _atls + ".spriteatlas";
            }
            if (!spriteAtlasDict.ContainsKey(atlas))
            {
                
                var sprite = await AssetMgr.LoadAsync<SpriteAtlas>(_atls, packge);
                if (!spriteAtlasDict.ContainsKey(atlas))
                {
                    spriteAtlasDict.Add(atlas, sprite);
                }
                
            }
            img.sprite = spriteAtlasDict[atlas].GetSprite(iconName);
        }
    
        public FileStream loadFileStream(string path, string packge = "Main")
        {
            path = BaseResousePath + packge + "/" + path;

            if (!File.Exists(path))
                return null;

            FileStream filestream = new FileStream(path, FileMode.Open);

            return filestream;
        }
    }
}
