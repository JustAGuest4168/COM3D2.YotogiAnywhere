using BepInEx.Harmony;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using wf;
using Yotogis;

namespace COM3D2.YotogiAnywhere.Plugin.Core
{
    internal static class YotogiAnywhereHooks
    {
        private static bool initialized;
        private static HarmonyLib.Harmony instance;

        public static void Initialize()
        {
            //Copied from examples
            if (YotogiAnywhereHooks.initialized)
                return;

            YotogiAnywhereHooks.instance = HarmonyWrapper.PatchAll(typeof(YotogiAnywhereHooks), "org.guest4168.yotogianywhereplugin.hooks.base");
            YotogiAnywhereHooks.initialized = true;


            UnityEngine.Debug.Log("YotogiAnywhere: Hooks Initialize");
        }

        [HarmonyPatch(typeof(YotogiPlayManager), nameof(YotogiPlayManager.NextSkill))]
        [HarmonyPostfix]
        static void YotogiPlayManager_NextSkill_Postfix()
        {
            UnityEngine.Debug.Log("YotogiAnywhere: NextSkill Postfix");

            //Tell the update it can load in the changed data
            YotogiAnywhere.nextSkillWasCalled = true;
        }

        [HarmonyPatch(typeof(Skill.Data), nameof(Skill.Data.IsExecStage), new Type[] { typeof(YotogiStage.Data) })]
        [HarmonyPostfix]
        static void Skill_Data_IsExecStage(YotogiStage.Data stageData, ref bool __result, Skill.Data __instance)
        {
            for (int i = 0; i < stageData.prefabName.Length; i++)
            {
                string key = stageData.prefabName[i].ToString().Trim().ToLower();

                //Initialize
                if (YotogiAnywhere.defaultAvailableSkills == null)
                {
                    YotogiAnywhere.defaultAvailableSkills = new Dictionary<string, List<int>>();
                }

                //Add Stage to dictionary
                if (!YotogiAnywhere.defaultAvailableSkills.ContainsKey(key))
                {
                    YotogiAnywhere.defaultAvailableSkills[key] = new List<int>();
                }

                //Add skill id to stage's list
                if (__result && !YotogiAnywhere.defaultAvailableSkills[key].Contains(__instance.id))
                {
                    YotogiAnywhere.defaultAvailableSkills[key].Add(__instance.id);
                }

                UnityEngine.Debug.Log("YotogiAnywhere: Skill_Data_IsExecStage Postfix: " + key);
            }

            __result = __result || YotogiAnywhere.newStageIds.Contains(stageData.id) || YotogiAnywhere.enableAllSkillsAllLocations;
        }

        [HarmonyPatch(typeof(YotogiStage), nameof(YotogiStage.CreateData))]
        [HarmonyPostfix]
        static void YotogiStage_CreateData()
        {
            //Get the Dictionary
            Dictionary<int, YotogiStage.Data> basicsData = (Dictionary<int, YotogiStage.Data>)typeof(YotogiStage).GetField("basicDatas", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            //Create folders if they dont exist
            string mainPath = UTY.gameProjectPath + "\\Mod\\[YotogiAnywhere]\\[NewStages]";
            if (!Directory.Exists(mainPath))
            {
                Directory.CreateDirectory(mainPath);
            }

            if (YotogiAnywhere.newStageIds == null)
            {
                YotogiAnywhere.newStageIds = new List<int>();
            }

            //Get the data in folders
            string[] newStagesDirs = Directory.GetDirectories(mainPath);
            for (int i = 0; i < newStagesDirs.Length; i++)
            {
                DirectoryInfo di = new DirectoryInfo(newStagesDirs[i]);
                string stageName = di.Name;

                //Wow this is bad I should have done something better than a bunch of if statements
                if (Directory.Exists(mainPath + "\\" + stageName + "\\[Day]"))
                {
                    if (Directory.Exists(mainPath + "\\" + stageName + "\\[Night]"))
                    {
                        int maxId = -1;
                        YotogiStage.Data stageData = null;

                        //Try to find stage in existing data
                        foreach (KeyValuePair<int, YotogiStage.Data> kvp in basicsData)
                        {
                            if (kvp.Value.uniqueName.Equals(stageName))
                            {
                                stageData = kvp.Value;
                                break;
                            }

                            //Also get the max Id
                            maxId = System.Math.Max(maxId, kvp.Value.id);
                        }

                        //Create new if it does not exist
                        if (stageData == null)
                        {
                            //Increment from max Id
                            maxId += 10;

                            //Add to list of new ones
                            if (!YotogiAnywhere.newStageIds.Contains(maxId))
                            {
                                YotogiAnywhere.newStageIds.Add(maxId);
                                CsvCommonIdManager commonIdManager = (CsvCommonIdManager)typeof(YotogiStage).GetField("commonIdManager", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                                commonIdManager.idMap.Add(maxId, new KeyValuePair<string, string>(stageName, ""));
                                commonIdManager.nameMap.Add(stageName, maxId);
                                typeof(YotogiStage).GetField("commonIdManager", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, commonIdManager);
                            }

                            //CsvParser is a pain, and the YotogiStage.Data constructor is a pain, so reuse yotogi_stage_list
                            //It wont find the id so it will be ok
                            string f_strFileName = "yotogi_stage_list.nei";
                            AFileBase afileBase = GameUty.FileSystem.FileOpen(f_strFileName);
                            CsvParser csvParser = new CsvParser();
                            csvParser.Open(afileBase);
                            stageData = new YotogiStage.Data(-1, csvParser);

                            //Set id, only field we really need right now
                            typeof(YotogiStage.Data).GetField("id", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, maxId);

                            //Cleanup
                            csvParser.Dispose();
                        }

                        //Get BG and Thumbnail from folders
                        string prefab_bg = null;
                        string prefab_bg_night = null;

                        //Day
                        if (Directory.GetFiles(mainPath + "\\" + stageName + "\\[Day]").Where(f => f.EndsWith(".asset_bg")).ToList().Count != 0)
                        {
                            prefab_bg = Path.GetFileNameWithoutExtension(new FileInfo(Directory.GetFiles(mainPath + "\\" + stageName + "\\[Day]").Where(f => f.EndsWith(".asset_bg")).ToList()[0]).Name);
                        }
                        else if (Directory.GetFiles(mainPath + "\\" + stageName + "\\[Day]").Where(f => f.EndsWith(".room")).ToList().Count != 0)
                        {
                            prefab_bg = Path.GetFileNameWithoutExtension(new FileInfo(Directory.GetFiles(mainPath + "\\" + stageName + "\\[Day]").Where(f => f.EndsWith(".room")).ToList()[0]).Name);
                        }
                        else
                        {
                            UnityEngine.Debug.Log("No.asset_bg or .room [Day] file found for " + stageName);
                        }
                        //Night
                        if (Directory.GetFiles(mainPath + "\\" + stageName + "\\[Night]").Where(f => f.EndsWith(".asset_bg")).ToList().Count != 0)
                        {
                            prefab_bg_night = Path.GetFileNameWithoutExtension(new FileInfo(Directory.GetFiles(mainPath + "\\" + stageName + "\\[Night]").Where(f => f.EndsWith(".asset_bg")).ToList()[0]).Name);
                        }
                        else if (Directory.GetFiles(mainPath + "\\" + stageName + "\\[Night]").Where(f => f.EndsWith(".room")).ToList().Count != 0)
                        {
                            prefab_bg_night = Path.GetFileNameWithoutExtension(new FileInfo(Directory.GetFiles(mainPath + "\\" + stageName + "\\[Night]").Where(f => f.EndsWith(".room")).ToList()[0]).Name);
                        }
                        else
                        {
                            UnityEngine.Debug.Log("No.asset_bg or .room [Night] file found for " + stageName);
                        }

                        string thumbnail = null;
                        string thumbnail_night = null;

                        //Day
                        if (Directory.GetFiles(mainPath + "\\" + stageName + "\\[Day]").Where(f => f.EndsWith(".tex")).ToList().Count != 0)
                        {
                            thumbnail = new FileInfo(Directory.GetFiles(mainPath + "\\" + stageName + "\\[Day]").Where(f => f.EndsWith(".tex")).ToList()[0]).Name;
                        }
                        else
                        {
                            UnityEngine.Debug.Log("No .tex [Day] thumbnail file found for " + stageName);
                        }
                        //Night
                        if (Directory.GetFiles(mainPath + "\\" + stageName + "\\[Night]").Where(f => f.EndsWith(".tex")).ToList().Count != 0)
                        {
                            thumbnail_night = new FileInfo(Directory.GetFiles(mainPath + "\\" + stageName + "\\[Night]").Where(f => f.EndsWith(".tex")).ToList()[0]).Name;
                        }
                        else
                        {
                            UnityEngine.Debug.Log("No .tex [Night] thumbnail file found for " + stageName);
                        }

                        if (prefab_bg == null || prefab_bg_night == null || thumbnail == null || thumbnail_night == null)
                        {
                            UnityEngine.Debug.Log("YotogiAnywhere failed to create Location found for " + stageName);
                            break;
                        }


                        //Create a settings json if it doesnt exist
                        if (!File.Exists(mainPath + "\\" + stageName + "/settings.json"))
                        {
                            YotogiStage_Data_Json newSetting = new YotogiStage_Data_Json();
                            File.WriteAllText(mainPath + "\\" + stageName + "/settings.json", Newtonsoft.Json.JsonConvert.SerializeObject(newSetting));
                        }

                        //Settings from Directory
                        typeof(YotogiStage.Data).GetField("uniqueName", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, stageName);
                        typeof(YotogiStage.Data).GetField("drawName", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, stageName);

                        typeof(YotogiStage.Data).GetField("prefabName", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, new string[] { prefab_bg, prefab_bg_night });
                        typeof(YotogiStage.Data).GetField("thumbnailName", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, new string[] { thumbnail, thumbnail_night });

                        //Some Generic settings we do not worry about
                        typeof(YotogiStage.Data).GetField("sortId", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, stageData.id);

                        typeof(YotogiStage.Data).GetField("drawClubGrade", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, 1);
                        typeof(YotogiStage.Data).GetField("requestClubGrade", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, 1);

                        typeof(YotogiStage.Data).GetField("requestFacilityIds", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, new int[] { });

                        //Settings file
                        YotogiStage_Data_Json settings = Newtonsoft.Json.JsonConvert.DeserializeObject<YotogiStage_Data_Json>(File.ReadAllText(mainPath + "\\" + stageName + "/settings.json"));
                        typeof(YotogiStage.Data).GetField("bgmFileName", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, settings.bgm);
                        typeof(YotogiStage.Data).GetField("stageSelectCameraData", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, new YotogiStage.Data.Camera(new UnityEngine.Vector3(settings.camera_pos_x, settings.camera_pos_y, settings.camera_pos_z),
                                                                                                                                                                                   new UnityEngine.Vector2(settings.camera_rot_x, settings.camera_rot_y),
                                                                                                                                                                                   settings.camera_radius));
                        typeof(YotogiStage.Data).GetField("skillSelectcharacterData", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, new YotogiStage.Data.Character(new UnityEngine.Vector3(settings.kasuko_x, settings.kasuko_y, settings.kasuko_z),
                                                                                                                                                                                     new UnityEngine.Vector3(settings.kasuko_rot_x, settings.kasuko_rot_y, settings.kasuko_rot_z),
                                                                                                                                                                                     new UnityEngine.Vector2(settings.skill_x, settings.skill_y)));
                        typeof(YotogiStage.Data).GetField("skillSelectLightData", BindingFlags.Instance | BindingFlags.Public).SetValue(stageData, new YotogiStage.Data.Light(new UnityEngine.Vector3(settings.light_x, settings.light_y, settings.light_z), settings.light_intensity));

                        //Update the entry
                        basicsData[stageData.id] = stageData;
                    }
                    else
                    {
                        UnityEngine.Debug.Log("YotogiAnywhere: YotogiStage_CreateData Missing: " + mainPath + "\\" + stageName + "\\[Night]");
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("YotogiAnywhere: YotogiStage_CreateData Missing: " + mainPath + "\\" + stageName + "\\[Day]");
                }
            }

            //Update entire dictionary as precaution
            typeof(YotogiStage).GetField("basicDatas", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, basicsData);
        }

        [HarmonyPatch(typeof(YotogiStage), nameof(YotogiStage.IsEnabled), new Type[] { typeof(int) })]
        [HarmonyPostfix]
        static void YotogiStage_IsEnabled(int id, ref bool __result)
        {
            __result = __result || YotogiAnywhere.newStageIds.Contains(id);
            UnityEngine.Debug.Log("YotogiAnywhere: YotogiStage_IsEnabled id Postfix: " + id + " " + __result);
        }

        [HarmonyPatch(typeof(YotogiStage), nameof(YotogiStage.IsEnabled), new Type[] { typeof(string) })]
        [HarmonyPostfix]
        static void YotogiStage_IsEnabled(string uniqueName, ref bool __result)
        {
            __result = __result || YotogiAnywhere.newStageIds.Contains(YotogiStage.uniqueNameToId(uniqueName));
            UnityEngine.Debug.Log("YotogiAnywhere: YotogiStage_IsEnabled name Postfix: " + uniqueName + " " + __result);
        }

        [HarmonyPatch(typeof(YotogiStage), nameof(YotogiStage.GetAllDatas), new Type[] { typeof(bool) })]
        [HarmonyPostfix]
        static void YotogiStage_GetAllDatas(bool onlyEnabled, ref List<YotogiStage.Data> __result)
        {
            List<YotogiStage.Data> dataList = __result;

            Dictionary<int, YotogiStage.Data> basicsData = (Dictionary<int, YotogiStage.Data>)typeof(YotogiStage).GetField("basicDatas", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            foreach (KeyValuePair<int, YotogiStage.Data> kvp in basicsData)
            {
                if (YotogiAnywhere.newStageIds.Contains(kvp.Key))
                {
                    bool found = false;
                    foreach (YotogiStage.Data dataItem in __result)
                    {
                        if (dataItem.id == kvp.Key)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        __result.Add(kvp.Value);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BgMgr), nameof(BgMgr.ChangeBg), new Type[] { typeof(string) })]
        [HarmonyPrefix]
        static void BgMgr_ChangeBg_Prefix(string f_strPrefubName, BgMgr __instance)
        {
            UnityEngine.Object original = __instance.CreateAssetBundle(f_strPrefubName);
            if (original == (UnityEngine.Object)null)
            {
                original = Resources.Load("BG/" + f_strPrefubName);
                if (original == (UnityEngine.Object)null)
                {
                    original = Resources.Load("BG/2_0/" + f_strPrefubName);
                }
            }
            if (original == null)
            {
                UnityEngine.Debug.Log("YotogiAnywhere: The following error message MAY be nothing to worry about if you use MyRooms for YotogiAnywhere.");
            }
        }
        [HarmonyPatch(typeof(BgMgr), nameof(BgMgr.ChangeBg), new Type[] { typeof(string) })]
        [HarmonyPostfix]
        static void BgMgr_ChangeBg_Postfix(string f_strPrefubName, BgMgr __instance)
        {
            YotogiAnywhere.roomBGName = null;

            UnityEngine.Object original = __instance.CreateAssetBundle(f_strPrefubName);
            if (original == (UnityEngine.Object)null)
            {
                original = Resources.Load("BG/" + f_strPrefubName);
                if (original == (UnityEngine.Object)null)
                {
                    original = Resources.Load("BG/2_0/" + f_strPrefubName);
                }
            }
            if (original == null)
            {
                UnityEngine.Debug.Log("YotogiAnywhere: Attempting.Room load.");

                //Get the path to NewStages
                string path = UTY.gameProjectPath + "\\Mod\\[YotogiAnywhere]\\[NewStages]";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                //Search for .room files, unfortunately we have to check all stages
                string[] files = Directory.GetFiles(path, f_strPrefubName + ".room", SearchOption.AllDirectories);
                if (files.Length == 0)
                {
                    //Not found
                    UnityEngine.Debug.Log("YotogiAnywhere: No " + f_strPrefubName + ".Room file found.");
                }
                else
                {
                    //Found something
                    if (files.Length > 1)
                    {
                        UnityEngine.Debug.Log("YotogiAnywhere: Multiple .Room files named the same, please fix if there are Day/Night differences.");
                    }

                    //Load the Room file instead using the file's guid
                    object[] parameters = new object[] { files[0], null };

                    MethodInfo deserializeHeader = typeof(MyRoomCustom.CreativeRoomManager).GetMethod("DeserializeHeader", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(string), typeof(MyRoomCustom.CreativeRoomManager.CreativeRoomHeader).MakeByRefType() }, null);

                    if (deserializeHeader != null)
                    {
                        object result = deserializeHeader.Invoke(null, parameters);
                        MyRoomCustom.CreativeRoomManager.CreativeRoomHeader header = (MyRoomCustom.CreativeRoomManager.CreativeRoomHeader)parameters[1];
                        if (header.guid != null)
                        {
                            __instance.ChangeBgMyRoom(header.guid);
                            YotogiAnywhere.roomBGName = new FileInfo(files[0]).Directory.Parent.Name;//header.comment;
                        }
                        else
                        {
                            UnityEngine.Debug.Log("YotogiAnywhere: .Room GUID could not be found.");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.Log("YotogiAnywhere: DeserializeHeader could not be found.");
                    }
                }
            }
        }

        //[HarmonyPatch(typeof(WorldTransformAxis), "OnMouseDragEvent")]
        //[HarmonyPostfix]
        //static void WorldTransformAxis_OnMouseDragEvent(WorldTransformAxis __instance)
        //{
        //    if (YotogiAnywhere.maidMover != null)
        //    {
        //        Transform transform = getTarget(__instance).transform;

        //        //Move "Active" to this position
        //        GameMain.Instance.CharacterMgr.SetCharaAllPos(transform.position);

        //        //Move the original target back to (0,0,0)
        //        transform.position = Vector3.zero;
        //    }
        //}
        //private static GameObject getTarget(WorldTransformAxis __instance)
        //{
        //    WorldTransformAxis parent_obj_ = (WorldTransformAxis)typeof(WorldTransformAxis).GetField("parent_obj_", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(__instance);

        //    if (parent_obj_ != null)
        //    {
        //        return getTarget(parent_obj_);
        //    }

        //    return parent_obj_.TargetObject;
        //}
    }

    public class YotogiStage_Data_Json
    {
        //BG Music
        public string bgm { get; set; }

        //Camera
        public float camera_pos_x { get; set; }
        public float camera_pos_y { get; set; }
        public float camera_pos_z { get; set; }
        public float camera_radius { get; set; }
        public float camera_rot_x { get; set; }
        public float camera_rot_y { get; set; }

        //Guest
        public float kasuko_x { get; set; }
        public float kasuko_y { get; set; }
        public float kasuko_z { get; set; }
        public float kasuko_rot_x { get; set; }
        public float kasuko_rot_y { get; set; }
        public float kasuko_rot_z { get; set; }

        //Skill
        public float skill_x { get; set; }
        public float skill_y { get; set; }

        //Light
        public float light_x { get; set; }
        public float light_y { get; set; }
        public float light_z { get; set; }
        public float light_intensity { get; set; }

        public YotogiStage_Data_Json()
        {
            //Default values taken from existing stage, not even sure how useful they are
            bgm = "BGM022.ogg";

            camera_pos_x = 0.5672674f;
            camera_pos_y = 0.9795773f;
            camera_pos_z = 2.073625f;
            camera_radius = 5.999998f;
            camera_rot_x = -1012.936f;
            camera_rot_y = 2.857115f;

            kasuko_x = -2.9f;
            kasuko_y = 0;
            kasuko_z = 0.55f;
            kasuko_rot_x = 0;
            kasuko_rot_y = 0;
            kasuko_rot_z = 0;

            skill_x = -540.7793f;
            skill_y = -3.353939f;

            light_x = 27.24442f;
            light_y = 205.1153f;
            light_z = 124.5199f;
            light_intensity = 0.95f;
        }
    }
}
