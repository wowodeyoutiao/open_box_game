using BestHTTP.JSON;
using JEngine.Helper;
using JEngine.LitJson;
using pb;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

namespace HotUpdateScripts.Game
{

    //[System.Serializable]
    //public class MapData
    //{
    //    public int x;
    //    public int z;
    //    public float posY;

    //}

    //[System.Serializable]
    //public class mapJson
    //{
    //    public int mapWidth;
    //    public int mapHeight;
    //    public List<MapData> mapPosList;
    //}

    public static class SceneManager
    {

        //private static Dictionary<int, Dictionary<Vector2, float>> mapPos; //地图可行走坐标
        //private static Dictionary<int, Vector2> mapWH; //地图宽高
        public static float Block = 1;

        public static int Wight = 0;
        public static int Height = 0;

        public static int MinX = 0;
        public static int MinY = 0;
        public static int MaxX = 0;
        public static int MaxY = 0;

        private static int OffX = 6;
        private static int OffY = 13;

        public static int EffOffZ = -3;
        public static void loadMapInfo(int mapID)
        {
            //if (mapPos == null)
            //    mapPos = new Dictionary<int, Dictionary<Vector2, float>>();
            //if (mapWH == null)
            //    mapWH = new Dictionary<int, Vector2>();

            //if (mapPos.ContainsKey(mapID))
            //    return;

            //_ = loadMap(mapID);

            Wight = CFG_Map.GetById(mapID).Wide;
            Height = CFG_Map.GetById(mapID).High;
            MinX = OffX;
            MaxX = Wight - OffX;

            MinY = OffY;
            MaxY = Height - OffY;
            if (MinY > MaxY)
            {
                MinY = Height / 2 - 1;
                MaxY = Height / 2 - 1;
            }
        }

        public static Vector3 MapPos()
        {
            return new Vector3(Wight / 2 - 1, Height / 2 - 1, 0 );
        }

        //private static async Task loadMap(int mapFileName)
        //{
        //   // Dictionary<Point, float> mapPosList = new Dictionary<Point, float>();

        //    if (!mapPos.ContainsKey(mapFileName))
        //    {
        //        Dictionary<Vector2, float> _mapPosList = new Dictionary<Vector2, float>();
        //        mapPos.Add(mapFileName, _mapPosList);
        //    }

        //    await ResouseHelper.Instance.LoadTextAssetAsync("Other/TextAssets/mapPos/map_"+ mapFileName + ".json", (jsonStr) => {

        //        if (jsonStr == "")
        //        {
        //            Debug.Log("jerryc json is null");
        //        }
        //        mapJson _mapJson = JsonMapper.ToObject<mapJson>(jsonStr);

        //        int mapwid = _mapJson.mapWidth;
        //        Debug.Log("jerryc json wid:" + mapwid);

        //        int maphei = _mapJson.mapHeight;
        //        Debug.Log("jerryc json hei:" + maphei);

        //        if (!mapWH.ContainsKey(mapFileName))//地图宽 高
        //        {
        //            Vector2 _point = new Vector2();
        //            _point.x = mapwid;
        //            _point.y = maphei;
        //            mapWH.Add(mapFileName, _point);

        //        }

        //        foreach (var VARIABLE in _mapJson.mapPosList)
        //        {
        //            float x = VARIABLE.x;
        //            float z = VARIABLE.z;
        //            //float y = Convert.ToSingle(VARIABLE.posY);
        //            float y = VARIABLE.posY;
        //            Vector2 _pointPos = new Vector2();
        //            _pointPos.x = x;
        //            _pointPos.y = z;
        //            if (!mapPos[mapFileName].ContainsKey(_pointPos))
        //            {
        //                mapPos[mapFileName].Add(_pointPos, y);
        //            }
        //        }
        //    });

        //   // string resultString = $"{{\"jsonDatas\":{json.text}}}";
        //  //  Debug.Log("jerryc json" + json.text);


        //}


        public static float GetOffZ(double X, double Y)
        {
            if (X < 0 || X >= Wight || Y < 0 || Y >= Height)
                return Block;
            return  (float)(200 - Y) * - 0.01f;
        }

        //public static double getPosY(int mapID, double x, double z)
        //{
        //    Vector2 point = new Vector2();
        //    point.x = (float)x;
        //    point.y = (float)z;
        //    if (mapPos.ContainsKey(mapID))
        //    {
        //        //foreach(var pos in mapPos[mapID])
        //        //{
        //        //    Debug.Log("jerryc test pos key:" + pos.Key.x);
        //        //    Debug.Log("jerryc test pos key:" + pos.Key.y);
        //        //    Debug.Log("jerryc test pos  value:" + pos.Value);

        //        //    if (pos.Key.x == 50 && pos.Key.y == 47)
        //        //    {
        //        //        Debug.Log("aaaaa2222");
        //        //    }

        //        //}
        //        if (mapPos[mapID].ContainsKey(point))
        //        {
        //            return mapPos[mapID][point];
        //        }
        //    }
        //    else
        //        Debug.Log("mapID miss");

        //    return Block;
        //}

    }
}
