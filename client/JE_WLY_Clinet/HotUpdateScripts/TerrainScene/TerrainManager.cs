using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace SceneTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager _TerrainManager;
        
        // public int mapWid { get; set; }
        // public int mapHei { get; set; }
        
        public int terrainWid { get; set; }
        public int terrainHei { get; set; }
        
        public int _MapIndexNow { get; set; }
        public int _MapIndexNowDB { get; set; }
        
        public FightSetting safrMap { get; set; }//是否是安全地图

        public string _mapName;

        private Point terrainPos;
        private Dictionary<string, GameObject> terrainList;
        private List<string> terrainLoadList;//异步加载用
        //private Dictionary<string, GameObject> terrainList1;
        private GameObject river1, river2;
        private void Awake()
        {
            _TerrainManager = this;

            // mapWid = 600;
            // mapHei = 800;
            terrainWid = 100;
            terrainHei = 100;
            terrainPos=Point.Empty;
            
            _MapIndexNow = 0;

            _assetBundlenav = null;
            _assetBundleScene = null;
            terrainLoadList = new List<string>();
            //_manorBuildObjList = new Dictionary<string, GameObject>();
            terrainList=new Dictionary<string, GameObject>();
         //   initTerrain1List();  test
        }

        private void OnEnable()
        {
           // throw new NotImplementedException();
           Debug.Log("jerryc OnEnable");
        }

        private void OnDisable()
        {
           // throw new NotImplementedException();
           Debug.Log("jerryc OnDisable");
        }

        // private void initTerrain1List()
        // {
        //     if (terrainList1 == null)
        //     {
        //         terrainList1 = new Dictionary<string, GameObject>();
        //         river1=GameObject.Find("Plane_water_1");
        //         river2=GameObject.Find("Plane_water_2");
        //         river1.SetActive(false);
        //         river2.SetActive(false);
        //     }
        //
        //     int wid = 600 / 100;
        //     int hei = 800 / 100;
        //
        //     for (int i = 0; i <=wid; i++)
        //     {
        //         for (int j = 0; j <=hei; j++)
        //         {
        //             GameObject terrain = GameObject.Find("Terrain" + "_" + i + "_" + j);
        //             if (terrain != null)
        //             {
        //                 terrainList1.Add("Terrain" + "_" + i + "_" + j,terrain);
        //                 terrain.SetActive(false);
        //             }
        //         }
        //     }
        //     
        // }

        private uint addX=0;
        private uint addY=0;
        public void updateTerrain(float x,float z)
        {
            if (_MapIndexNow ==2)
            {
                return;
            }
            
            // if (_MapIndexNow ==3)
            // {
            //     return;
            // }
            
            if (_MapIndexNow >=100)
            {
                return;
            }

            if (terrainList == null)
                return;
            
            
            int xint = (int) ((x+addX) / 100);
            int yint = (int) ((z+(addY)-Gamemanager.game.getMapOffset(_MapIndexNow).Y) / 100);//地图1 terrain竖方向编号从0开始 其他从1开始
            
            if (terrainPos.X==xint&&terrainPos.Y==yint)
                return;

            Debug.Log("updateterrain TERRAIN hide:"+xint+"  "+yint);
            int xNow = terrainPos.X;
            int yNow = terrainPos.Y;
            for (int i = xNow-1; i <= xNow+1; i++)
            {
                for (int j = yNow-1; j <= yNow+1; j++)
                {
                    string keyname = "Terrain" + "_" + i + "_" + (j);
                    Debug.Log("updateterrain keyname hideeeeeee :"+keyname);
                    if (terrainList.ContainsKey(keyname))
                    {
                        Debug.Log("updateterrain terrainList.ContainsKey keyname:"+keyname);

                        if (_MapIndexNow == 5&&i==5&&j==1)//test
                        {
                            continue;
                        }
                        
                        terrainList[keyname].SetActive(false);
                    }
                    else
                    {
                        Debug.Log("updateterrain terrainList.not ContainsKey keyname:"+keyname);
                    }
                    
                }
            }

            xNow = xint;
            yNow = yint;
            Debug.Log("updateterrain TERRAIN hide:"+xint+"  "+yint);
            for (int i = xNow-1; i <= xNow+1; i++)
            {
                for (int j = yNow-1; j <= yNow+1; j++)
                {
                    string keyname = "Terrain" + "_" + i + "_" + (j);
                    Debug.Log("updateterrain keyname showwwwww:"+keyname);
                    if (terrainList.ContainsKey(keyname))
                    {
                        Debug.Log("updateterrain terrainList.ContainsKey keyname:"+keyname);
                        terrainList[keyname].SetActive(true);
                    }
                    else
                    {
                        Debug.Log("updateterrain terrainList not ContainsKey keyname:"+keyname);
                    }
                    
                }
            }

            terrainPos = new Point(xint,yint);
        }
        
        public void updateTerrainForce()
        {
            if (_MapIndexNow ==2)
            {
                return;
            }
            
            // if (_MapIndexNow ==3)
            // {
            //     return;
            // }

            if (terrainList == null)
                return;
            
            
            int xNow = terrainPos.X;
            int  yNow = terrainPos.Y;
            for (int i = xNow-1; i <= xNow+1; i++)
            {
                for (int j = yNow-1; j <= yNow+1; j++)
                {
                    string keyname = "Terrain" + "_" + i + "_" + (j);
                    
                    if (terrainList.ContainsKey(keyname))
                    {
                        //    Debug.Log("jerryc 0816 keyname visible:"+keyname);
                        terrainList[keyname].SetActive(true);
                    }
                    
                }
            }

          //  terrainPos = new Point(xint,yint);
        }

        // public void loadSceneTerrain(int mapID,string terrainName)
        // {
        //     AssetLoader.LoadScene(mapID,terrainName,OnLoadTerrainAsyncDone);
        // }
        //
        // public void loadScene(int mapID)
        // {
        //     string path = Globals.ResourcePath + "/scene1/scenemap1.txt";
        //     AssetBundle _assetBundle = Libraries.LoadAssetBundle(path);
        //     string[] scenePaths=_assetBundle.GetAllScenePaths();
        //     SceneManager.LoadScene(scenePaths[0], LoadSceneMode.Additive);
        // }
        
        // private void OnLoadTerrainAsyncDone(Terrain terrain)
        // {
        //     if (terrain != null)
        //     {
        //         // Material mat=    Resources.Load<Material>("TerrainLit");
        //         // terrain.materialTemplate = mat;
        //         terrain.gameObject.SetActive(true);
        //        
        //     }
        //     else
        //       Debug.Log("jerryC OnLoadTerrainAsyncDone terrain is null:"+terrain.name);
        // }

        // private void initMap1()
        // {
        //      initTerrain1List();
        // }
        public void changeMap(int toMapIndex)
        {
            int mapIndexFact = 1;
            _MapIndexNowDB = toMapIndex;
            MapInfo  Info = Globals.MapInfoList?.Binding.FirstOrDefault(x => x.Index == toMapIndex);
            if (Info != null)
            {
                mapIndexFact = Convert.ToInt32(Info.FileName);
            }

            
            if (_MapIndexNow == mapIndexFact)
                return;

            
            if(!Gamemanager.game._loadingCanvas.gameObject.activeSelf)
              Gamemanager.game._loadingCanvas.gameObject.SetActive(true);
            
            if(MainRole._MainRole!=null)
             MainRole._MainRole.stopAgent();

            // if(MainRole._MainRole!=null)
            //     MainRole._MainRole.resetTargetInfo();
            
            unloadMap(_MapIndexNow);
            loadMap(mapIndexFact);
            _mapName = Info.Description;
        }

        public void unloadCurrentMap()
        {
            unloadMap(_MapIndexNow);
            _MapIndexNow = 0;
        }
        private void unloadMap(int mapIndex)
        {
        //    Debug.Log("unloadMap begin:"+CEnvir.GetTime());
            if (_MapIndexNow == 0)
                return;

            if(mapBig._MapBig!=null&&mapBig._MapBig.gameObject.activeSelf)
                mapBig._MapBig.gameObject.SetActive(false);

            if (Gamemanager.game.MainUI != null)
            {
                Gamemanager.game.MainUI.hideApplyPanel();
                Gamemanager.game.MainUI.forceCloseFightEffect();
            }

            StopCoroutine(checkTransState(1.0f));
            _mapTransTargetList.Clear();

            if (MainRole._MainRole != null)
            {
                MainRole._MainRole.clearPosSplit();
                MainRole._MainRole.clear_calcuateGoToInfo();
            }
            
            if (_MapIndexNow == 2)
            {
                //_manorBuildObjList.Clear();
               
                if (Gamemanager.game.MainUI)
                {
                    Gamemanager.game.MainUI.openMapSelect(false);
                    Gamemanager.game.MainUI.removeManorBuildTitles();
                    
                    AssetLoaderManorBuild.releaseAllBuild();
                }
            }
            
            terrainList.Clear();
            terrainPos=Point.Empty;
         //   if (mapIndex == 1)
         if(false)
            {
                foreach (var VARIABLE in terrainList)
                {
                    VARIABLE.Value.gameObject.SetActive(false);
                }
                terrainPos=Point.Empty;
            }
            else
            {
                if(mapIndex==2)
                    Gamemanager.game._ManorManager.clearManorSceneBuild();
                
                SceneManager.UnloadScene("scene"+mapIndex);
                Libraries.UnLoadAssetBundle(_assetBundlenav,true);
                Libraries.UnLoadAssetBundle(_assetBundleScene,true);
                if (Gamemanager.game.MainUI)
                {
                    AssetLoaderManorBuild.releaseAllBuild();
                }
               
            }


            if (_mapSceneEffectList != null)
            {
                foreach (var VARIABLE in _mapSceneEffectList)
                {

                    foreach (var list in VARIABLE.Value)
                    {
                        AssetLoader.releaseEffectPrefab(list);
                    }
                }
                _mapSceneEffectList.Clear();
            }

            if (isCopyMap(mapIndex))
            {
                Monster_Base[] monsterBases = FindObjectsOfType<Monster_Base>();//副本怪全部清理
                foreach (var VARIABLE in monsterBases)
                {
                    VARIABLE.isDead = true;
                }
            }

            AssetLoader.LoadSprite("miniMap/minimap" + mapIndex.ToString());
          //  Debug.Log("unloadMap end:"+CEnvir.GetTime());
        }

        private AssetBundle _assetBundlenav;
        private AssetBundle _assetBundleScene;
        //private Dictionary<string, GameObject> _manorBuildObjList;
        private void loadMap(int mapIndex)
        {
          //  Debug.Log("loadMap begin:"+CEnvir.GetTime());
            if (mapIndex == 0)
                return;

            Gamemanager.game.mapDataLoadBlock(mapIndex);
            
            // mapWid = Gamemanager.game.getMapWH(mapIndex).X-Gamemanager.game.getMapOffset(mapIndex).X;
            // mapHei = Gamemanager.game.getMapWH(mapIndex).Y-Gamemanager.game.getMapOffset(mapIndex).Y;
           
            
            if (cameraOffsetLoad.cameraOffsetCfg.ContainsKey((uint)mapIndex))
            {
                FollowTarget followTarget = Camera.main.GetComponent<FollowTarget>();
                followTarget.setOffset(cameraOffsetLoad.cameraOffsetCfg[(uint)mapIndex]);
            }
            else
            {
                FollowTarget followTarget = Camera.main.GetComponent<FollowTarget>();
                followTarget.setOffset(cameraOffsetLoad.cameraOffsetCfg[(uint)2]);
            }

         //   if (mapIndex == 1)
         if(false)
            {
               // if(terrainList==null||terrainList.Count<=0)
               //     initMap1();

               //initTerrain1List();
               Gamemanager.game._loadingCanvas.canEnterScene = true;
            }
            else
            {
               
                if (mapIndex == 2)
                {
                    
                     string pathNav = Globals.ResourcePath + "/scene/scenenav"+mapIndex+".txt";
                     _assetBundlenav = Libraries.LoadAssetBundle(pathNav);
                    
                    string path = Globals.ResourcePath + "/scene/scene"+mapIndex+".txt";
                     _assetBundleScene = Libraries.LoadAssetBundle(path);
                    string[] scenePaths=_assetBundleScene.GetAllScenePaths();
                    SceneManager.LoadScene(scenePaths[0], LoadSceneMode.Additive);
                    
                    string[] builds = manorLocationLoad.manorLocationCfg[0].BuildLocation.Split(':');
                    uint buildIndex = 0;
                    terrainList.Clear();
                    string formatStr = "{0}:{1}:{2}";
                    foreach (var VARIABLE in builds)
                    {
                        if (String.Compare(VARIABLE, "", StringComparison.Ordinal) == 0)
                        {
                            buildIndex++;
                            continue;
                        }

                      //  AssetLoaderManorBuild.LoadBuildPrefabAsync(VARIABLE,buildIndex);
                        
                        if (VARIABLE.Contains("Location"))
                        {
                         //   Debug.Log("VARIABLE:"+VARIABLE);
                            GameObject buildObjInit = AssetLoaderManorBuild.LoadBuildPrefab(VARIABLE);
                            if (buildObjInit != null)
                            {
                                GameObject build = Instantiate(buildObjInit);
                                build.name = VARIABLE;
                            //    Debug.Log("build.name:"+build.name+" "+buildIndex);
                                // string[] locations = manorLocationLoad.manorLocationCfg[buildIndex].BuildLocation
                                //     .Split(':');
                                
                                // build.transform.localPosition = new Vector3(x, y, z);

                                float x = build.transform.localPosition.x;
                                 float y = build.transform.localPosition.y;
                                 float z = build.transform.localPosition.z;

                                 manorLocationLoad.manorLocationCfg[buildIndex].BuildLocation =
                                     string.Format(formatStr, x, y, z);
                                 
                                SceneManager.MoveGameObjectToScene(build, SceneManager.GetSceneByName("scene2"));
                        
                                ManorBuilder manorBuilder = build.AddComponent<ManorBuilder>();
                                manorBuilder.buildID = buildIndex;
                        
                                Gamemanager.game._ManorManager.saveManorSceneBuild(buildIndex, build);
                            }
                            //  }
                        }
                        else
                        {
                            // GameObject terr = Resources.Load<GameObject>("sceneManor/"+VARIABLE);
                            // GameObject terrobj= Instantiate(terr.gameObject);
                            GameObject terrainObjInit = AssetLoaderManorBuild.LoadBuildPrefab(VARIABLE);
                            GameObject terrobj= Instantiate(terrainObjInit);
                            terrobj.name = VARIABLE;
                            terrainList.Add(VARIABLE,terrobj);
                            SceneManager.MoveGameObjectToScene(terrobj,SceneManager.GetSceneByName("scene2"));
                        }

                        buildIndex++;
                    }
                    
                    addSecenEffect(mapIndex);
                    
                    if (Gamemanager.game.MainUI != null)
                    {
                        C.GetManorInfo getManorInfo = new C.GetManorInfo
                        {
                        };
                        CEnvir.Enqueue(getManorInfo);
                    }
                    Gamemanager.game._loadingCanvas.canEnterScene = true;
                }
                else
                {
                    string pathNavOther = Globals.ResourcePath + "/scene/"+"scene"+mapIndex+ "/scenenav"+mapIndex+".txt";
                    _assetBundlenav = Libraries.LoadAssetBundle(pathNavOther);
                    terrainLoadList.Clear();
                    string pathOther = Globals.ResourcePath + "/scene/"+"scene"+mapIndex+"/scene"+mapIndex+".txt";
                    _assetBundleScene = Libraries.LoadAssetBundle(pathOther);
                    string[] scenePathsOther=_assetBundleScene.GetAllScenePaths();
                    SceneManager.LoadScene(scenePathsOther[0], LoadSceneMode.Additive);
                    _MapIndexNow = mapIndex;
                    if (terrainLocationLoad.TerrainLocationCfg.ContainsKey((uint)mapIndex))
                    {
                        string range = terrainLocationLoad.TerrainLocationCfg[(uint)mapIndex].range;
                        string other=terrainLocationLoad.TerrainLocationCfg[(uint)mapIndex].other;

                        addX=  terrainLocationLoad.TerrainLocationCfg[(uint)mapIndex].addX;
                        addY = terrainLocationLoad.TerrainLocationCfg[(uint)mapIndex].addY;

                        int wid = Convert.ToInt32(range.Split('x')[0]);
                        int hei = Convert.ToInt32(range.Split('x')[1]);
                        terrainList.Clear();
                      //  Debug.Log("jerryc terrain wid hei:"+wid+"  "+hei);
                        for (int i = 1; i <= wid; i++)
                        {
                            for (int j = 1; j <= hei; j++)
                            {
                                string terrainName = "Terrain_"+ i + "_" + j;
                                
                                string filePath = "scene{0}/{1}";
                                filePath = string.Format(filePath, mapIndex, terrainName.ToLower());
                                
                                string path = Globals.ResourcePath + "/scene/" +filePath+".txt";


                                if (mapIndex == 5)
                                {
                                    
                                    if(i==1&&j==1)
                                        continue;
                                    if(i==1&&j==3)
                                        continue;
                                    // if(i==3&&j==5)
                                    //     continue;
                                    if(i==4&&j==1)
                                        continue;
                                    // if(i==5&&j==1)
                                    //     continue;
                                }
                                
                            //    Debug.Log("jerryc loadMap:"+path);
                                if (File.Exists(path))
                                {
                                    if (false)
                                    {
                                        GameObject terrainObjInit =
                                            AssetLoaderManorBuild.LoadBuildPrefab(filePath);
                                        GameObject terrobj = Instantiate(terrainObjInit);
                                        terrobj.name = terrainName;

                                        SceneManager.MoveGameObjectToScene(terrobj,
                                            SceneManager.GetSceneByName("scene" + mapIndex));

                                        if (mapIndex != 3)
                                        {
                                            terrobj.SetActive(false);

                                            terrainList.Add("Terrain" + "_" + i + "_" + j, terrobj);
                                        }
                                    }
                                    else
                                    {
                                        terrainLoadList.Add(terrainName);
                                    }
                                    //  AssetLoaderManorBuild.LoadBuildPrefabAsync(filePath,0);
                                }
                                
                                
                            }
                        }
//("jerryc terrainLoadList.Count:"+terrainLoadList.Count);
                        if (terrainLoadList.Count > 0)
                        {

                            string terrainName = terrainLoadList[0];
                            string filePath = "scene{0}/{1}";
                            filePath = string.Format(filePath, mapIndex, terrainName);
                            
                            AssetLoaderManorBuild.LoadBuildPrefabAsync(filePath,0);
                        }
                        
                        string[] others = other.Split(':');
                        foreach (var VARIABLE in others)
                        {
                            if(String.Compare(VARIABLE, "", StringComparison.Ordinal)==0)
                                continue;
                            GameObject landObjInit = AssetLoaderManorBuild.LoadBuildPrefab("scene"+mapIndex+ "/"+VARIABLE);
                            GameObject landobj= Instantiate(landObjInit);
                            landobj.name = VARIABLE;
                        
                            SceneManager.MoveGameObjectToScene(landobj,SceneManager.GetSceneByName("scene"+mapIndex));
                        }
                    }
                    
                    //loadSceneTerrain(mapIndex, "terrain1_1");
                }
            }

            StartCoroutine(checkTransState(2.0f));
            if(MainRole._MainRole!=null)
              MainRole._MainRole.resetTargetInfo();
            _MapIndexNow = mapIndex;
            loadSourceRegion(_MapIndexNowDB);
           // Gamemanager.game._loadingCanvas.canEnterScene = true;
            if (Gamemanager.game.MainUI != null)
            {
                Gamemanager.game.MainUI.closeChangeMapPanel();
                Gamemanager.game.MainUI.setMiniMap(mapIndex,_MapIndexNowDB);

                if (isCopyMap(mapIndex))
                {
                    Gamemanager.game.MainUI.openCruadeCountDown(true,mapIndex);
                    if(PanelMyTeam._panelMyTeam!=null&&PanelMyTeam._panelMyTeam.gameObject.activeSelf)
                        PanelMyTeam._panelMyTeam.gameObject.SetActive(false);
                }
                else
                {
                    Gamemanager.game.MainUI.openCruadeCountDown(false,mapIndex);
                }
            }

           // Debug.Log("loadMap end:"+CEnvir.GetTime());
        }


        public void loadTerrainDone(GameObject terrobj)
        {
            if(terrainLoadList.Count<=0)
                return;
            
            string terrainName = terrainLoadList[0];
            terrobj.name = terrainName;
             SceneManager.MoveGameObjectToScene(terrobj,
                 SceneManager.GetSceneByName("scene" + _MapIndexNow));
            
            
           // if (_MapIndexNow != 3)
           // {
                terrobj.SetActive(false);
            
                terrainList.Add(terrainName, terrobj);
           // }
            
           // Debug.Log("jerryc load terrain:"+terrainName+" ok");
            terrainLoadList.RemoveAt(0);
            
            Gamemanager.game._loadingCanvas.canEnterScene = true;

            if (terrainLoadList.Count <= 0)
            {
               updateTerrainForce();
                return;
            }

            terrainName = terrainLoadList[0];
            string filePath = "scene{0}/{1}";
            filePath = string.Format(filePath, _MapIndexNow, terrainName);
                            
            AssetLoaderManorBuild.LoadBuildPrefabAsync(filePath,0);
            
        }
        
        // public void setMapWH(int mapFileName)
        // {
        //     mapWid = Gamemanager.game.getMapWH(mapFileName).X-Gamemanager.game.getMapOffset(mapFileName).X;
        //     mapHei = Gamemanager.game.getMapWH(mapFileName).Y-Gamemanager.game.getMapOffset(mapFileName).Y;
        // }

        public void reLoadSourceRegion()
        {
            _mapTransTargetList.Clear();
            for (int i = 0; i < Globals.MovementInfoList.Count; i++)
            {
                MapInfo mapInfo = Globals.MovementInfoList[i].SourceRegion.Map;
                if (mapInfo.FileName.CompareTo(_MapIndexNow.ToString()) == 0)
                {
                    safrMap = mapInfo.Fight;
                    Point pos = Globals.MovementInfoList[i].SourceRegion.Centre;
                   
                    _TransFormdiastance = Globals.MovementInfoList[i].SourceRegion.Distance;
                    //MapInfo mapInfodes = Globals.MovementInfoList[i].DestinationRegion.Map;
                    if (!_mapTransTargetList.ContainsKey(pos))
                    {
                        _mapTransTargetList.Add(pos,Globals.MovementInfoList[i]);
                    }
                }
            }
        }
        
        private void loadSourceRegion(int mapindxDB)//加载传送点
        {
            int index = 1;

            if (_mapTransSourceList == null)
                _mapTransSourceList = new Dictionary<int, GameObject>();
            if (_mapTransTargetList == null)
                _mapTransTargetList = new Dictionary<Point, MovementInfo>();
            
            if (_mapTransSourceList != null)
            {
                foreach (var VARIABLE in _mapTransSourceList)
                {
                    VARIABLE.Value.gameObject.SetActive(false);
                }
            }
            
            for (int i = 0; i < Globals.MovementInfoList.Count; i++)
            {
                MapInfo mapInfo = Globals.MovementInfoList[i].SourceRegion.Map;
                if (mapInfo.FileName.CompareTo(_MapIndexNow.ToString()) == 0)
                {
                    if (Globals.MovementInfoList[i].SourceRegion.Type == MapRegion.RegionType.Telport)
                    {
                        safrMap = mapInfo.Fight;
                        Point pos = Globals.MovementInfoList[i].SourceRegion.Centre;
                        
                        _TransFormdiastance = Globals.MovementInfoList[i].SourceRegion.Distance;
                        //MapInfo mapInfodes = Globals.MovementInfoList[i].DestinationRegion.Map;
                        if (!_mapTransTargetList.ContainsKey(pos))
                        {
                            createRegionEffect(pos, index);
                            _mapTransTargetList.Add(pos, Globals.MovementInfoList[i]);
                        }
                    }


                    index++;
                }
            }
        }

        private Dictionary<int, GameObject> _mapTransSourceList;
        private Dictionary<Point, MovementInfo> _mapTransTargetList;//传送点坐标， 传送点信息
        private GameObject _mapTransSource;
        private int _TransFormdiastance;
        private void createRegionEffect(Point pos,int index)
        {
            if (_mapTransSourceList.ContainsKey(index))
            {
                float y = CEnvir.testPosY(pos.X, pos.Y);
                _mapTransSourceList[index].transform.localPosition = new Vector3(pos.X, y, pos.Y);
                _mapTransSourceList[index].gameObject.SetActive(true);
            }
            else
            {
                if (_mapTransSource == null)
                {
                   // _mapTransSource = Resources.Load<GameObject>("Effect/Prefab/DaoJU/Fx_ChuanSongMen");
                   _mapTransSource = AssetLoader.LoadEffectPrefab("scene/Fx_ChuanSongMen");
                }

                GameObject mapTrans = Instantiate(_mapTransSource);
                mapTrans.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                float y = CEnvir.testPosY(pos.X, pos.Y);
                mapTrans.transform.localPosition = new Vector3(pos.X, y-1, pos.Y);
                mapTrans.name = "mapTrans" + index;
                _mapTransSourceList.Add(index,mapTrans);
            }
        }

        public void resetRegionEffectPos()
        {
            foreach (var VARIABLE in _mapTransSourceList)
            {
                float x = VARIABLE.Value.transform.localPosition.x;
                float z=  VARIABLE.Value.transform.localPosition.z;
                float y = CEnvir.testPosY((int)x, (int)z);
                VARIABLE.Value.transform.localPosition = new Vector3(x, y, z);
            }
        }

        public Point getMapOffser()
        {
            return Gamemanager.game.getMapOffset(_MapIndexNow);
        }

        public bool isCopyMap(int mapIndex)//是否是副本
        {
            if (mapIndex == 100)
                return true;
            
            return false;
        }
        
        public bool isInCopyMap()//是否是副本
        {
            if (_MapIndexNow == 100)
                return true;
            
            return false;
        }
        
        #region 庄园传送门逻辑

        public void resetcheckTransState()
        {
            // StopCoroutine(checkTransState(0));
            // StartCoroutine(checkTransState(2.0f));
        }
        IEnumerator checkTransState(float time)
        {
            while (true)
            {
                yield return new WaitForSeconds(time);
                isCloseToTransFormGate();
            }
        }
        public void isCloseToTransFormGate()//是否靠近传送们 区域
        {
            if (Gamemanager.game.MainUI == null)
                return;
            if (MainRole._MainRole == null)
                return;
            
            foreach (var VARIABLE in _mapTransTargetList)
            {
                   Vector2 posTrans = new Vector2(VARIABLE.Key.X,VARIABLE.Key.Y);

                    Vector2 role = new Vector2(MainRole._MainRole.posNow.X, MainRole._MainRole.posNow.Y);
                    if (Vector2.Distance(posTrans, role) <= _TransFormdiastance)
                    {
                        if(isManorMap())
                          Gamemanager.game.MainUI.openMapSelect(true);
                        else
                        {
                            MovementInfo movementInfo = VARIABLE.Value;
                           
                            string content = string.Format(Gamemanager.GetMsg(1368), movementInfo.DestinationRegion.Map.Description);
                            Gamemanager.game.MainUI.showChangeMapPanel(movementInfo.DestinationRegion.Map.Index, movementInfo.DestinationRegion.Centre,content);
                        }
                        return;
                    }
              
            }

            Gamemanager.game.MainUI.openMapSelect(false);
        }

        public bool isManorMap()
        {
            return _MapIndexNow == 2;
        }

        private Dictionary<uint, List<string>> _mapSceneEffectList;//建筑等级特效
        private void addSecenEffect(int mapIndex)
        {
            if (_mapSceneEffectList == null)
                _mapSceneEffectList = new Dictionary<uint, List<string>>();

            if (_mapSceneEffectList.ContainsKey((uint)mapIndex))
                return;

            List<string> effectNameList = new List<string>();
            _mapSceneEffectList.Add((uint)mapIndex,effectNameList);
            
            string effectList = terrainLocationLoad.TerrainLocationCfg[(uint)mapIndex].effect;
            string[] effects = effectList.Split(':');
            foreach (var VARIABLE in effects)
            {
                string name = "manor/" + VARIABLE;
                GameObject inst = AssetLoader.LoadEffectPrefab(name);
                GameObject effecObj;
                if (inst != null)
                {
                    effecObj = Instantiate(inst);
                    SceneManager.MoveGameObjectToScene(effecObj,SceneManager.GetSceneByName("scene"+mapIndex));
                    effecObj.transform.localScale = inst.transform.localScale;
                    effecObj.transform.localPosition = inst.transform.localPosition;
                    effecObj.transform.localRotation = inst.transform.localRotation;
                    effectNameList.Add(name);
                }
                
               
            }
        }

        #endregion

        public void setTerrainVisible(bool isVisible)
        {
            foreach (var VARIABLE in terrainList)
            {
                Debug.Log("jerryc terrainname:"+VARIABLE.Key);
                VARIABLE.Value.SetActive(isVisible);
            }
        }
    }
}