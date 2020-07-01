using BepInEx;
using GearMenu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Yotogis;

namespace COM3D2.YotogiAnywhere.Plugin.Core
{
    [BepInPlugin("org.guest4168.plugins.yotogianywhereplugin", "Yotogi Location Caching Plug-In", "1.0.0.0")]
    public class YotogiAnywhere : BaseUnityPlugin
    {
        private UnityEngine.GameObject managerObject;
        private UnityEngine.GameObject menuButton;
        public void Awake()
        {
            //Copied from examples
            UnityEngine.Debug.Log("YotogiAnywhere: Awake");
            UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)this);
            this.managerObject = new UnityEngine.GameObject("yotogiAnywhereManager");
            UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)this.managerObject);
            this.managerObject.AddComponent<YotogiAnywhereManager>().Initialize();
        }



        //Data
        public static Dictionary<string, List<int>> defaultAvailableSkills;
        public static List<int> newStageIds;
        public static bool enableAllSkillsAllLocations = false;
        private Dictionary<String, List<KeyValuePair<string, PhotoBGObjectData>>> bgObjectDict;

        //Yotogi
        #region
        public static bool nextSkillWasCalled = false;
        public static string roomBGName = null;
        private static bool inYotogi = false;
        private static bool inStudio = false;

        //Maid
        public static PhotoTransTargetObject maidMover;

        //BGObjects
        private static Dictionary<String, YTABgObject> activeBGObjects;
        #endregion

        //UI
        private static bool displayUI = false;
        private static bool savePressed = false;
        private static bool overrideDefault = false;
        private static bool overrideSkill = false;
        private static bool importPressed = false;
        private static bool overrideImport = false;
        private static bool loadPressed = false;

        private static bool showMaidPositionGizmoPressed = false;
        private static bool showMaidRotationGizmoPressed = false;
        private static int cameraPositionPressed = -1;
        private static bool showBGObjPositionGizmoPressed = false;
        private static bool showBGObjRotationGizmoPressed = false;


        public void OnLevelWasLoaded(int level)
        {
            inYotogi = false;
            inStudio = false;
            maidMover = null;

            if (level == 14 || level == 26)
            {
                //Add the button
                if (GameMain.Instance != null && GameMain.Instance.SysShortcut != null && !Buttons.Contains("YotogiAnywhere"))
                {
                    menuButton = GearMenu.Buttons.Add("YotogiAnywhere", "YotogiAnywhere Yotogi Level Only", GearIcon, OnMenuButtonClickCallback);
                }

                if (level == 14)
                {
                    inYotogi = true;
                }

                if (level == 26)
                {
                    inStudio = true;
                }
            }

            //Hide the UI when switching levels
            displayUI = false;
        }

        private void OnMenuButtonClickCallback(GameObject gearMenuButton)
        {
            //Open/Close the UI
            displayUI = !displayUI;

            //Initialize the BG Object Data
            PhotoBGObjectData.Create();

            if (bgObjectDict == null)
            {
                bgObjectDict = new Dictionary<string, List<KeyValuePair<string, PhotoBGObjectData>>>();
                foreach (KeyValuePair<string, List<PhotoBGObjectData>> category in PhotoBGObjectData.category_list)
                {
                    if (!bgObjectDict.ContainsKey(category.Key))
                    {
                        bgObjectDict.Add(category.Key, new List<KeyValuePair<string, PhotoBGObjectData>>());
                        if (getHardcodedTranslation(category.Key).Equals(category.Key))
                        {
                            UnityEngine.Debug.Log("YotogiAnywhere: Category: " + category.Key);
                        }
                    }
                    for (int index = 0; index < category.Value.Count; ++index)
                    {
                        bgObjectDict[category.Key].Add(new KeyValuePair<string, PhotoBGObjectData>(category.Value[index].name, category.Value[index]));

                        if (getHardcodedTranslation(category.Value[index].name).Equals(category.Value[index].name))
                        {
                            UnityEngine.Debug.Log("YotogiAnywhere: Object: " + category.Value[index].name);
                        }
                    }
                }
            }
        }

        public static byte[] GearIcon
        {
            get
            {
                if (png == null)
                {
                    png = Convert.FromBase64String(gearIconBase64);
                }
                return png;
            }
        }

        static byte[] png = null;

        private static string gearIconBase64 = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAR3SURBVFhHtZb/T1tVGMb5f9wXlW8DNhBmGQVEMsIsggmYGMBICCSLJjDNiCMiKogMib9AfyC6xKRbAousEOhGtxaYJeBYoS2DDGiBMnC0YIHyeN5D2/XenrVI8SSfUO4993mec857zr0xXq8XXu8+g/4GE3otmibSOzg4AAuwj729vbCcRCNDuS5d4zMgvxHMSTa5dsQAJ93kXmEDBDfPxgusG0bg6O/F2sgwdhx2351Xzbuzgw3TGFbu3cXqkBZbc1bfnVeN1jzY47UB/G3LZsHk1c8wlHwKw0lvSHj8yYd4MW6EZ90Jc+OXuP/O2/y6LqiP4Uo27HfvcGNqrwkgLUK6SM1xrw+61Ldwn5lPZcRiXpmE5dzzsDMWlMl4cjEOD1JOYSQzCSZmPnfpHJZzUvj959nJmFEkwHDhDA/yV10NvB4P15V7hcwA/b9u0PNRGy6c5WIkKmKJGT7LShLe80PhKcT09S+OuAQ7bjx6/yIenj/DDUSi/5VJX4jVB0MSL2GA5V4N72xjUyoSOw60dPqU0/jz01KJlzDAVF0tH71IKBqoZoZZiH9evpQHkBbh+Mcf8KISiUTDXNY5PrN/W2bkAaQzMMYC0JqJRKJhge2iIwWYYlvm/5oB2lmCJZAGWLzzO6uB00KRaKAaeFxeIvESBvC4XHiYlw5rZqJQ6DjwXcAG5Rjsl3j5AoS+jtf0OujZIbR4QucAHUaTn1eF+AhnwM/Cr2oYU89GfRhNvxvPdpYKnq2tEI+wAQjbLz9iNIoQZD760WVssxeWSD9iAML2c8uxQjwl8+J8bDvXhLrEkQIQlvbveQj/2y4SZG4szsP22qpQz48vQORvQsL607cYS30zYggzmaveg3t1RagTzJFmwOl0YmJiAgMDWsz8cCNsCLOCzHMwbTRgdHQUDodDqOlHGMBqtUKt7kZNTQ2ysrKgUChQUlKC6upqaLVamL/72hdCbp4A45UcOJ9ZUVdXh9LSUiiVSmRkZKCyshKdnZ18IMFegQAe9rXS29sLlUqF3Nxc3LzZDiMbxebmpuQBzu4uzE3XMZ7GQvjM6evHWKiEa3kxpL+LHWxk3NXVhaKiIq6vVqvhdrsOA1CHiooKxMbGora2Fjvs41IuImefYf7mKx6CRm4ouAT38pKwr5zW1lbuVVBQALvdjpienh5+gWhsbBQ+JIKHuHENjy4r4Fp6Luwj4tat3wJ+DQ0NiNHpdIELRH19PSwWi/BhObts6dwr4QvND42WRp+QkBDw6u7uPqwBjUaDtLQ0SZDCwkI0Nzejr68PZvNTvpYiYRG0jDabDYODA2hra+MFGR8fH9BOTExER0cH7xvYBRsbG7w4iouLJUGCSU9P52tHglQ3VVVVHKrysrIyHjozMxNxcXHC5/Pz89He3o6lpcNiDewCf3I/K2xa+/v/QEtLCzfIy8vjqUWiImi02dnZKC8vR1NTE27f1mB+fj7Ehwegb3X6EQl6gA4lOidoWxmNRuj1eg5tWZPJhNnZWRZ+hW9rkUYoXvwLtDiytyYR7yAAAAAASUVORK5CYII=";

        private void Update()
        {
            //If the button was pressed outside of Yotogi, disable UI
            if (!(inYotogi || inStudio))
            {
                //Hide the UI
                displayUI = false;

                //Maid Gizmo Cleanup
                RemoveMaidGizmos();

                //BG Object Cleanup
                RemoveAllBGObjectsAndGizmos();

                //Reset the Room name
                roomBGName = null;
                uiBgName = null;
            }
            else if (inYotogi)
            {
                //Initialize Variables
                PhotoBGObjectData.Create();
                if (bgObjectDict == null)
                {
                    bgObjectDict = new Dictionary<string, List<KeyValuePair<string, PhotoBGObjectData>>>();
                    foreach (KeyValuePair<string, List<PhotoBGObjectData>> category in PhotoBGObjectData.category_list)
                    {
                        if (!bgObjectDict.ContainsKey(category.Key))
                        {
                            bgObjectDict.Add(category.Key, new List<KeyValuePair<string, PhotoBGObjectData>>());
                            if (getHardcodedTranslation(category.Key).Equals(category.Key))
                            {
                                UnityEngine.Debug.Log("YotogiAnywhere: Category: " + category.Key);
                            }
                        }
                        for (int index = 0; index < category.Value.Count; ++index)
                        {
                            bgObjectDict[category.Key].Add(new KeyValuePair<string, PhotoBGObjectData>(category.Value[index].name, category.Value[index]));

                            if (getHardcodedTranslation(category.Value[index].name).Equals(category.Value[index].name))
                            {
                                UnityEngine.Debug.Log("YotogiAnywhere: Object: " + category.Value[index].name);
                            }
                        }
                    }
                }

                //Initialize the active bg object
                if (activeBGObjects == null)
                {
                    activeBGObjects = new Dictionary<string, YTABgObject>();
                }

                setUIBGName();

                //Camera
                {
                    //Key pressed to move camera
                    if (cameraPositionPressed != -1)
                    {
                        UltimateOrbitCamera uoc = GameMain.Instance.MainCamera.GetComponent<UltimateOrbitCamera>();
                        if (uoc != null)
                        {
                            UnityEngine.Camera component = uoc.GetComponent<UnityEngine.Camera>();
                            Vector3 position = uoc.target.position;
                            switch (cameraPositionPressed)
                            {
                                case 1:
                                    position += component.transform.TransformDirection(Vector3.up) * (cameraMoveSpeed / 100);
                                    uoc.SetTargetPos(position);
                                    break;
                                case 2:
                                    position += component.transform.TransformDirection(Vector3.down) * (cameraMoveSpeed / 100);
                                    uoc.SetTargetPos(position);
                                    break;
                                case 3:
                                    position += component.transform.TransformDirection(Vector3.left) * (cameraMoveSpeed / 100);
                                    uoc.SetTargetPos(position);
                                    break;
                                case 4:
                                    position += component.transform.TransformDirection(Vector3.right) * (cameraMoveSpeed / 100);
                                    uoc.SetTargetPos(position);
                                    break;
                            }
                        }

                        //Reset Button Press
                        cameraPositionPressed = -1;
                    }
                }

                //Maids/Men
                {
                    //Key pressed to create/show Position Gizmo - only works in yotogi, after we've tried loading the skill
                    if (showMaidPositionGizmoPressed)//UnityEngine.Input.GetKeyDown(KeyCode.B) && inYotogi)
                    {
                        //Reset Button press
                        showMaidPositionGizmoPressed = false;

                        //Gizmo
                        if (maidMover == null)
                        {
                            Update_createMaidMoverGizmo();
                        }
                        if (maidMover != null)
                        {
                            maidMover.rotate_obj.Visible = false;
                            maidMover.axis_obj.Visible = !maidMover.axis_obj.Visible;
                        }
                    }

                    //Key pressed to create/show Rotation Gizmo - only works in yotogi, after we've tried loading the skill
                    if (showMaidRotationGizmoPressed)//UnityEngine.Input.GetKeyDown(KeyCode.G) && inYotogi)
                    {
                        //Reset Button press
                        showMaidRotationGizmoPressed = false;

                        //Gizmo
                        if (maidMover == null)
                        {
                            Update_createMaidMoverGizmo();
                        }
                        if (maidMover != null)
                        {
                            maidMover.axis_obj.Visible = false;
                            maidMover.rotate_obj.Visible = !maidMover.rotate_obj.Visible;
                        }
                    }
                }

                //BG Objects
                {
                    //Key pressed to create/show Position Gizmo - only works in yotogi, after we've tried loading the skill
                    if (showBGObjPositionGizmoPressed)
                    {
                        //Reset the Button press
                        showBGObjPositionGizmoPressed = false;

                        //Hide all other gizmos
                        foreach (KeyValuePair<string, YTABgObject> bgObj in activeBGObjects)
                        {
                            if (bgObj.Value != null && bgObj.Value.gizmo != null && !bgObj.Key.Equals(currentBGObjectId))
                            {
                                bgObj.Value.gizmo.axis_obj.Visible = false;
                                bgObj.Value.gizmo.rotate_obj.Visible = false;
                            }
                        }
                        //Gizmo
                        if (currentBGObjectId != null)
                        {
                            if (activeBGObjects[currentBGObjectId].gizmo == null)
                            {
                                Update_createBGObjMoverGizmo();
                            }
                            if (activeBGObjects[currentBGObjectId].gizmo != null)
                            {
                                activeBGObjects[currentBGObjectId].gizmo.rotate_obj.Visible = false;
                                activeBGObjects[currentBGObjectId].gizmo.axis_obj.Visible = !activeBGObjects[currentBGObjectId].gizmo.axis_obj.Visible;
                            }
                        }
                    }

                    //Key pressed to create/show Rotation Gizmo - only works in yotogi, after we've tried loading the skill
                    if (showBGObjRotationGizmoPressed)
                    {
                        //Reset the Button press
                        showBGObjRotationGizmoPressed = false;

                        //Hide all other gizmos
                        foreach (KeyValuePair<string, YTABgObject> bgObj in activeBGObjects)
                        {
                            if (bgObj.Value != null && bgObj.Value.gizmo != null && !bgObj.Key.Equals(currentBGObjectId))
                            {
                                bgObj.Value.gizmo.axis_obj.Visible = false;
                                bgObj.Value.gizmo.rotate_obj.Visible = false;
                            }
                        }

                        //Gizmo
                        if (currentBGObjectId != null)
                        {
                            if (activeBGObjects[currentBGObjectId].gizmo == null)
                            {
                                Update_createBGObjMoverGizmo();
                            }
                            if (activeBGObjects[currentBGObjectId].gizmo != null)
                            {
                                activeBGObjects[currentBGObjectId].gizmo.axis_obj.Visible = false;
                                activeBGObjects[currentBGObjectId].gizmo.rotate_obj.Visible = !activeBGObjects[currentBGObjectId].gizmo.rotate_obj.Visible;
                            }
                        }
                    }

                    //Scale the BG Object
                    if (currentBGObjectId != null && inYotogi)
                    {
                        activeBGObjects[currentBGObjectId].game_object.transform.localScale = new Vector3(currentBGObjectScale, currentBGObjectScale, currentBGObjectScale);
                    }
                }

                //Automatic Loading
                {
                    //On Next Skill was called
                    if (nextSkillWasCalled)
                    {
                        //Don't run this again until the next skill
                        nextSkillWasCalled = false;

                        //Load in the data
                        Update_loadPositionRotations();
                    }
                }

                //Cleanup at ResultPanel
                GameObject resultPanel = GameObject.Find("ResultPanel");
                if (resultPanel != null)
                {
                    if (resultPanel.activeSelf)
                    {
                        //Maid Gizmo Cleanup
                        RemoveMaidGizmos();

                        //BG Object Cleanup
                        RemoveAllBGObjectsAndGizmos();

                        //Reset the Room name
                        roomBGName = null;
                    }
                }

                //UI Display issue
                GameObject go = UnityEngine.GameObject.Find("YotogiPlayManager");
                if (go != null)
                {
                    YotogiPlayManager manager = go.GetComponent<YotogiPlayManager>();
                    if (!(manager != null && manager.playingSkill != null && manager.playingSkill.skill_pair != null && manager.playingSkill.skill_pair.base_data != null && manager.playingSkill.skill_pair.base_data.name != null))
                    {
                        displayUI = false;
                    }
                }
                else
                {
                    displayUI = false;
                }
            }
            else if (inStudio)
            {
                setUIBGName();
            }

            //SAVE
            {
                //Key pressed to record
                if (savePressed)//UnityEngine.Input.GetKeyDown(KeyCode.C))
                {
                    //Reset button press
                    savePressed = false;

                    //Save the data
                    Update_savePositionRotations();

                    overrideDefault = false;
                    overrideSkill = false;
                }
            }

            //LOAD
            {
                //Key pressed to load
                if (loadPressed)//UnityEngine.Input.GetKeyDown(KeyCode.V))
                {
                    //Reset button pressed
                    loadPressed = false;

                    //Load in the data
                    Update_loadPositionRotations();
                }
            }
        }

        private void setUIBGName()
        {
            Transform bgTransform = GameMain.Instance.BgMgr.Parent.transform;
            if (bgTransform != null)
            {
                List<Transform> bgContainers = bgTransform.transform.GetComponentsInChildren<Transform>().Where(trans => trans.parent.Equals(bgTransform)).ToList();
                for (int i = 0; i < bgContainers.Count; i++)
                {
                    Transform bgContainerTransform = bgContainers[i];

                    //Determine folder from BG or Room data
                    if (roomBGName == null)
                    {
                        //Asset BG
                        if (bgContainerTransform.name.Contains("(Clone)"))
                        {
                            GameObject bgContainer = bgContainerTransform.gameObject;
                            uiBgName = bgContainer.name.Replace("(Clone)", "");
                            break;
                        }
                    }
                    else
                    {
                        //Room
                        uiBgName = roomBGName;
                        break;
                    }
                }
            }
        }
        private void Update_createMaidMoverGizmo()
        {
            if (maidMover == null)
            {
                Maid maid = GameMain.Instance.CharacterMgr.GetMaid(0);

                if (maid != null)
                {
                    maidMover = new PhotoTransTargetObject(maid.gameObject.transform.parent.parent.gameObject, string.Empty, string.Empty, PhotoTransTargetObject.Type.Maid, new Vector2(0.1f, 5.0f));
                    maidMover.axis_obj.Visible = false;
                    maidMover.rotate_obj.Visible = false;
                }
            }
        }
        private void Update_createBGObjMoverGizmo()
        {
            if (activeBGObjects[currentBGObjectId].gizmo == null)
            {
                GameObject obj = activeBGObjects[currentBGObjectId].game_object;
                if (obj != null)
                {
                    activeBGObjects[currentBGObjectId].gizmo = new PhotoTransTargetObject(obj.gameObject, string.Empty, string.Empty, PhotoTransTargetObject.Type.Prefab, new Vector2(0.1f, 5.0f));
                    activeBGObjects[currentBGObjectId].gizmo.axis_obj.Visible = false;
                    activeBGObjects[currentBGObjectId].gizmo.rotate_obj.Visible = false;
                }
            }
        }


        private void Update_savePositionRotations()
        {
            //Create Path
            string mainPath = UTY.gameProjectPath + "\\Mod\\[YotogiAnywhere]\\[BGSkillData]";

            //Get the BG to determine path to save file
            Transform bgTransform = GameMain.Instance.BgMgr.Parent.transform;
            if (bgTransform != null)
            {
                List<Transform> bgContainers = bgTransform.transform.GetComponentsInChildren<Transform>().Where(trans => trans.parent.Equals(bgTransform)).ToList();
                for (int i = 0; i < bgContainers.Count; i++)
                {
                    Transform bgContainerTransform = bgContainers[i];

                    //Determine folder from BG or Room data
                    string bgFolderName = null;
                    if (roomBGName == null)
                    {
                        //Asset BG
                        if (bgContainerTransform.name.Contains("(Clone)"))
                        {
                            GameObject bgContainer = bgContainerTransform.gameObject;
                            bgFolderName = bgContainer.name.Replace("(Clone)", "");
                        }
                    }
                    else
                    {
                        //Room
                        if (bgContainerTransform.name.Equals("部屋でーた"))
                        {
                            bgFolderName = roomBGName;
                        }
                    }

                    //We have a folder to save to
                    if (bgFolderName != null)
                    {
                        Maid maid = GameMain.Instance.CharacterMgr.GetMaid(0);

                        if (maid != null)
                        {
                            //Data
                            YTAData data = new YTAData();

                            //Camera
                            data.cam_target_position = GameMain.Instance.MainCamera.GetTargetPos().ToString("G9");
                            data.cam_distance = GameMain.Instance.MainCamera.GetDistance().ToString("G9");
                            data.cam_rotation = GameMain.Instance.MainCamera.GetAroundAngle().ToString("G9");

                            if (!inYotogi)
                            {
                                //In Studio Mode we can use the maid's actual position
                                data.all_pos_x = maid.gameObject.transform.position.x;
                                data.all_pos_y = maid.gameObject.transform.position.y;
                                data.all_pos_z = maid.gameObject.transform.position.z;

                                data.all_rot_x = maid.gameObject.transform.localRotation.eulerAngles.x;
                                data.all_rot_y = maid.gameObject.transform.localRotation.eulerAngles.y;
                                data.all_rot_z = maid.gameObject.transform.localRotation.eulerAngles.z;
                            }
                            else
                            {
                                //Maids/Men Position/Rotation aka "Actice" object's
                                data.all_pos_x = maid.gameObject.transform.parent.parent.transform.position.x;
                                data.all_pos_y = maid.gameObject.transform.parent.parent.transform.position.y;
                                data.all_pos_z = maid.gameObject.transform.parent.parent.transform.position.z;

                                data.all_rot_x = maid.gameObject.transform.parent.parent.transform.localRotation.eulerAngles.x;
                                data.all_rot_y = maid.gameObject.transform.parent.parent.transform.localRotation.eulerAngles.y;
                                data.all_rot_z = maid.gameObject.transform.parent.parent.transform.localRotation.eulerAngles.z;
                            }

                            //Create the folder
                            if (!System.IO.Directory.Exists(mainPath + "\\" + bgFolderName))
                            {
                                System.IO.Directory.CreateDirectory(mainPath + "\\" + bgFolderName);
                            }

                            //If the default file doesn't exist, create one with current location
                            if (!System.IO.File.Exists(mainPath + "\\" + bgFolderName + "/" + "__default.json") || overrideDefault)
                            {
                                System.IO.File.WriteAllText(mainPath + "\\" + bgFolderName + "/" + "__default.json", Newtonsoft.Json.JsonConvert.SerializeObject(data));
                                UnityEngine.Debug.Log("YotogiAnywhere: Default Saved Successfully");
                                lastMessage = "Default Saved Successfully";
                            }
                            else if (!overrideDefault && !inYotogi)
                            {
                                //Save was pressed in Studio but not override default
                                UnityEngine.Debug.Log("YotogiAnywhere: Default already exists, toggle override to Save");
                                lastMessage = "Default already exists, toggle override to Save";
                            }

                            //If we are in Yotogi
                            if (inYotogi && UnityEngine.GameObject.Find("YotogiPlayPanel") != null)
                            {
                                //BG Objects should be saved too
                                Update_saveBGObjects(data);

                                YotogiPlayManager manager = UnityEngine.GameObject.Find("YotogiPlayManager").GetComponent<YotogiPlayManager>();

                                string ytFileName = manager.playingSkill.skill_pair.base_data.name;

                                //Create skill specific file- meh always override
                                if (!System.IO.File.Exists(mainPath + "\\" + bgFolderName + "/" + ytFileName + ".json") || overrideSkill)
                                {
                                    System.IO.File.WriteAllText(mainPath + "\\" + bgFolderName + "/" + ytFileName + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(data));
                                    UnityEngine.Debug.Log("YotogiAnywhere: Skill " + ytFileName + " Saved Successfully for " + bgFolderName);
                                    lastMessage = "Skill " + ytFileName + " Saved Successfully for " + bgFolderName;
                                }
                                else if (!overrideSkill)
                                {
                                    UnityEngine.Debug.Log("YotogiAnywhere: Skill " + ytFileName + " alread exists, toggle override to Save");
                                    lastMessage = "Skill " + ytFileName + " alread exists, toggle override to Save";
                                }

                                //Load what was just saved
                                //Update_loadPositionRotations();
                            }
                        }
                        else
                        {
                            UnityEngine.Debug.Log("YotogiAnywhere: Maid missing for position capture");
                            lastMessage = "Maid missing for position capture";
                        }
                    }
                }
            }
        }

        private void Update_saveBGObjects(YTAData data)
        {
            //Clear json data list
            data.bgObjects = new List<YTABGObjectData>();

            //Loop active objects and create entry
            foreach (KeyValuePair<String, YTABgObject> kvp in activeBGObjects)
            {
                YTABGObjectData entry = new YTABGObjectData();

                //Model Data
                entry.name = kvp.Value.create_time;
                entry.id = kvp.Value.data.id.ToString();

                //Position Data
                entry.pos_x = kvp.Value.game_object.transform.position.x;
                entry.pos_y = kvp.Value.game_object.transform.position.y;
                entry.pos_z = kvp.Value.game_object.transform.position.z;
                entry.rot_x = kvp.Value.game_object.transform.rotation.x;
                entry.rot_y = kvp.Value.game_object.transform.rotation.y;
                entry.rot_z = kvp.Value.game_object.transform.rotation.z;
                entry.rot_w = kvp.Value.game_object.transform.rotation.w;
                entry.scale = kvp.Value.game_object.transform.localScale.x;

                data.bgObjects.Add(entry);
            }
        }

        private void Update_loadPositionRotations()
        {
            //Check if the skill is in our collection
            string mainPath = UTY.gameProjectPath + "\\Mod\\[YotogiAnywhere]\\[BGSkillData]";

            //Get the BG to determine path to load file
            Transform bgTransform = GameMain.Instance.BgMgr.Parent.transform;
            if (bgTransform != null)
            {
                List<Transform> bgContainers = bgTransform.transform.GetComponentsInChildren<Transform>().Where(trans => trans.parent.Equals(bgTransform)).ToList();
                for (int i = 0; i < bgContainers.Count; i++)
                {
                    Transform bgContainerTransform = bgContainers[i];

                    //Destroy the Maids/Mens Gizmo
                    RemoveMaidGizmos();

                    //Destroy any old BG Objects from previous skill
                    RemoveAllBGObjectsAndGizmos();

                    //Determine folder from BG or Room data
                    string bgFolderName = null;
                    if (roomBGName == null)
                    {
                        //Asset BG
                        if (bgContainerTransform.name.Contains("(Clone)"))
                        {
                            GameObject bgContainer = bgContainerTransform.gameObject;
                            bgFolderName = bgContainer.name.Replace("(Clone)", "");
                        }
                    }
                    else
                    {
                        //Room
                        if (bgContainerTransform.name.Equals("部屋でーた"))
                        {
                            bgFolderName = roomBGName;
                        }
                    }

                    //We have a folder to load from
                    if (bgFolderName != null)
                    {
                        //If a __default or skill specific for the BG was setup
                        if (System.IO.Directory.Exists(mainPath + "\\" + bgFolderName))
                        {
                            mainPath = mainPath + "\\" + bgFolderName;

                            //Get the current Yotogi Skill Name if we can
                            string currentYotogiName = "";
                            GameObject go = UnityEngine.GameObject.Find("YotogiPlayManager");
                            int skillId = -1;
                            if (go != null)
                            {
                                YotogiPlayManager manager = go.GetComponent<YotogiPlayManager>();
                                currentYotogiName = manager.playingSkill.skill_pair.base_data.name;
                                skillId = manager.playingSkill.skill_pair.base_data.id;

                                UnityEngine.Debug.Log("YotogiAnywhere: BG: " + bgFolderName + " SKILL: " + skillId);
                            }

                            //If yotogi specific file exist, if not use the default for the BG
                            if (System.IO.File.Exists(mainPath + "\\" + currentYotogiName + ".json"))
                            {
                                mainPath = mainPath + "\\" + currentYotogiName;
                            }
                            else
                            {
                                mainPath = mainPath + "\\" + "__default";
                            }

                            //The default file may not exist for some reason
                            if (System.IO.File.Exists(mainPath + ".json"))
                            {
                                //If its not the __default or its not a skill that KISS made available to the room
                                if (!mainPath.EndsWith("default") || (defaultAvailableSkills.ContainsKey(bgFolderName.Trim().ToLower()) && !defaultAvailableSkills[bgFolderName.Trim().ToLower()].Contains(skillId)))
                                {
                                    YTAData data = Newtonsoft.Json.JsonConvert.DeserializeObject<YTAData>(System.IO.File.ReadAllText(mainPath + ".json"));

                                    //Move the maid/man/objects - there may be multiple commands for this in a ks file, especially when objects like table, but this only covers the generic "allPos" for now
                                    if (true || data.all_pos_x != 0f || data.all_pos_y != 0f || data.all_pos_z != 0f)
                                    {
                                        Vector3 zero1 = Vector3.zero;
                                        zero1.x = data.all_pos_x;
                                        zero1.y = data.all_pos_y;
                                        zero1.z = data.all_pos_z;

                                        //Fix issue caused by position Gizmo
                                        //for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetMaidCount(); ++nMaidNo)
                                        //{
                                        //    Maid maid = GameMain.Instance.CharacterMgr.GetMaid(nMaidNo);
                                        //    if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                        //    {
                                        //        maid.gameObject.transform.localPosition = new Vector3(0f, 0f, 0f);
                                        //    }
                                        //}

                                        //Set "Active"'s position
                                        GameMain.Instance.CharacterMgr.SetCharaAllPos(zero1);

                                        ////Get every Object in Active and set that objects transform position (in this case just get maids and men)
                                        //for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetMaidCount(); ++nMaidNo)
                                        //{
                                        //    Maid maid = GameMain.Instance.CharacterMgr.GetMaid(nMaidNo);
                                        //    if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                        //    {
                                        //        maid.gameObject.transform.localPosition = new Vector3(zero1.x, zero1.y, zero1.z);
                                        //    }
                                        //}
                                        //for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetManCount(); ++nMaidNo)
                                        //{
                                        //    Maid maid = GameMain.Instance.CharacterMgr.GetMan(nMaidNo);
                                        //    if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                        //    {
                                        //        maid.gameObject.transform.localPosition = new Vector3(zero1.x, zero1.y, zero1.z);
                                        //    }
                                        //}


                                        //Adjust the physics hit using Maid's Y - this is also a tag setting, but seems redundant and non-controllable without just knowning
                                        if (!GameMain.Instance.ScriptMgr.compatibilityMode)
                                        {
                                            for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetMaidCount(); ++nMaidNo)
                                            {
                                                Maid maid = GameMain.Instance.CharacterMgr.GetMaid(nMaidNo);
                                                if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                                {
                                                    maid.body0.SetBoneHitHeightY(zero1.y);
                                                }
                                            }
                                        }
                                    }
                                    if (true || data.all_rot_x != 0f || data.all_rot_y != 0f || data.all_rot_z != 0f)
                                    {
                                        Vector3 zero2 = Vector3.zero;
                                        zero2.x = data.all_rot_x;
                                        zero2.y = data.all_rot_y;
                                        zero2.z = data.all_rot_z;

                                        //Fix issue cause by rotation gizmo
                                        for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetMaidCount(); ++nMaidNo)
                                        {
                                            Maid maid = GameMain.Instance.CharacterMgr.GetMaid(nMaidNo);
                                            if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                            {
                                                maid.gameObject.transform.localRotation = Quaternion.identity;
                                            }
                                        }

                                        //Set "Active"'s rotation
                                        GameMain.Instance.CharacterMgr.SetCharaAllRot(zero2);

                                        ////Get every Object in Active and set that objects transform rotation(in this case just get maids and men)
                                        //for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetMaidCount(); ++nMaidNo)
                                        //{
                                        //    Maid maid = GameMain.Instance.CharacterMgr.GetMaid(nMaidNo);
                                        //    if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                        //    {
                                        //        maid.gameObject.transform.localRotation = Quaternion.Euler(new Vector3(zero2.x, zero2.y, zero2.z));
                                        //    }
                                        //}
                                        //for (int nMaidNo = 0; nMaidNo < GameMain.Instance.CharacterMgr.GetManCount(); ++nMaidNo)
                                        //{
                                        //    Maid maid = GameMain.Instance.CharacterMgr.GetMan(nMaidNo);
                                        //    if ((UnityEngine.Object)maid != (UnityEngine.Object)null && (UnityEngine.Object)maid.body0 != (UnityEngine.Object)null)
                                        //    {
                                        //        maid.gameObject.transform.localRotation = Quaternion.Euler(new Vector3(zero2.x, zero2.y, zero2.z));
                                        //    }
                                        //}
                                    }

                                    //Adjust the Camera
                                    GameMain.Instance.MainCamera.SetTargetPos(wf.Parse.Vector3(data.cam_target_position), true);
                                    GameMain.Instance.MainCamera.SetDistance(float.Parse(data.cam_distance), true);
                                    GameMain.Instance.MainCamera.SetAroundAngle(wf.Parse.Vector2(data.cam_rotation), true);

                                    //Adjust Light - lol no no way of determining that

                                    //BG Objects
                                    Update_loadBGObjects(data.bgObjects);
                                }
                            }
                            else
                            {
                                UnityEngine.Debug.Log("YotogiAnywhere: Could not find data file: " + mainPath);
                                lastMessage = "Could not find data file: " + mainPath;
                            }
                        }

                        break;
                    }
                }
            }
        }

        private void Update_loadBGObjects(List<YTABGObjectData> data)
        {
            //Ensure theres a BG???
            if ((UnityEngine.Object)GameMain.Instance == (UnityEngine.Object)null || (UnityEngine.Object)GameMain.Instance.BgMgr == (UnityEngine.Object)null || (UnityEngine.Object)GameMain.Instance.BgMgr.bg_parent_object == (UnityEngine.Object)null)
            {
                return;
            }

            //Reset as precaution
            activeBGObjects = new Dictionary<string, YTABgObject>();
            addCategory = false;
            currentCategory = null;
            currentBGObjectId = null;
            currentBGObjectScale = 1f;

            //Load in current skill's BG Objects
            for (int i = 0; i < data.Count; i++)
            {
                YTABGObjectData jsonBGObj = data[i];

                YTABgObject bgObject = this.AddObject(PhotoBGObjectData.Get(long.Parse(jsonBGObj.id)), jsonBGObj.name);
                if (bgObject != null && (UnityEngine.Object)bgObject.game_object != (UnityEngine.Object)null)
                {
                    Transform transform = bgObject.game_object.transform;
                    transform.position = new Vector3(jsonBGObj.pos_x, jsonBGObj.pos_y, jsonBGObj.pos_z);
                    transform.rotation = new Quaternion(jsonBGObj.rot_x, jsonBGObj.rot_y, jsonBGObj.rot_z, jsonBGObj.rot_w);
                    transform.localScale = new Vector3(jsonBGObj.scale, jsonBGObj.scale, jsonBGObj.scale);
                    bgObject.game_object.SetActive(true);
                }
            }
        }

        private YTABgObject AddObject(PhotoBGObjectData add_bg_data, string create_time = "")
        {
            //If the model object doesnt exist
            if (add_bg_data == null)
                return null;

            //Create new Object and set data
            YTABgObject bgObject = new YTABgObject(add_bg_data, create_time);

            //If the object already exists don't recreate it
            if (activeBGObjects.ContainsKey(bgObject.create_time))
            {
                return null;
            }

            //Create the actual model object
            bgObject.game_object = bgObject.data.Instantiate(bgObject.create_time);
            if (bgObject.game_object == null)
            {
                return null;
            }

            //Add to dictionary
            activeBGObjects.Add(bgObject.create_time, bgObject);

            return bgObject;
        }

        public static void RemoveMaidGizmos()
        {
            //Fix the Maids/Mens Gizmos
            if (maidMover != null)
            {
                //Hide Gizmo
                maidMover.axis_obj.Visible = false;
                maidMover.rotate_obj.Visible = false;

                //Remove Gizmo
                maidMover.Delete();

                //Reset variables
                maidMover = null;
            }
        }

        public static void RemoveAllBGObjectsAndGizmos()
        {
            //Ensure theres a BG???
            if ((UnityEngine.Object)GameMain.Instance == (UnityEngine.Object)null || (UnityEngine.Object)GameMain.Instance.BgMgr == (UnityEngine.Object)null || (UnityEngine.Object)GameMain.Instance.BgMgr.bg_parent_object == (UnityEngine.Object)null)
            {
                return;
            }

            //Destroy any old BG Objects and gizmos
            if (activeBGObjects != null)
            {
                List<String> keys = activeBGObjects.Keys.ToList();
                foreach (String key in keys)
                {
                    //Remove the Gizmo
                    if (activeBGObjects[key].gizmo != null)
                    {
                        activeBGObjects[key].gizmo.Delete();
                    }

                    //Remove from Game
                    UnityEngine.Object.DestroyImmediate(activeBGObjects[key].game_object);

                    //Remove from collection
                    activeBGObjects.Remove(key);
                }
            }

            //Reset Variables
            activeBGObjects = new Dictionary<string, YTABgObject>();

            //Reset UI variables
            addCategory = false;
            currentCategory = null;
            currentBGObjectId = null;
            currentBGObjectScale = 1f;
        }

        #region UI
        Rect uiWindow = new Rect(Screen.width / 2 + 20, 20, 120, 50);
        private static string lastMessage = "";
        private static string uiBgName = null;
        private static float cameraMoveSpeed = 1f;
        private static bool addCategory = false;
        private static string currentCategory = null;
        private static string currentBGObjectId = null;
        private static float currentBGObjectScale = 1f;
        Vector2 bgObjectCategoryScrollPosition;
        Vector2 bgObjectAvailableScrollPosition;
        Vector2 bgObjectActiveScrollPosition;
        Vector2 importScrollPosition;

        private void OnGUI()
        {
            if (displayUI)
            {
                uiWindow = GUILayout.Window(41680, uiWindow, DisplayUIWindow, "Yotogi Anywhere", GUILayout.Height(Screen.height * 2 / 3 - 40));
            }
        }

        private void DisplayUIWindow(int windowId)
        {
            GUILayout.BeginVertical("box");

            //Basic Functions
            GUILayout.Label("Skill/Stage Data");

            //Info
            string mainPath = UTY.gameProjectPath + "\\Mod\\[YotogiAnywhere]\\[BGSkillData]";

            GUILayout.Label(lastMessage);
            GUILayout.Label("Stage: " + uiBgName);
            GUI.enabled = false;
            GUILayout.Toggle(File.Exists(mainPath + "\\" + uiBgName + "\\__default.json"), "Default Exists");
            if (inYotogi)
            {
                string currentYotogiName = "NULL";
                GameObject go = UnityEngine.GameObject.Find("YotogiPlayManager");
                if (go != null)
                {
                    YotogiPlayManager manager = go.GetComponent<YotogiPlayManager>();
                    if (manager != null && manager.playingSkill != null && manager.playingSkill.skill_pair != null && manager.playingSkill.skill_pair.base_data != null && manager.playingSkill.skill_pair.base_data.name != null)
                    {
                        currentYotogiName = manager.playingSkill.skill_pair.base_data.name;
                    }
                }
                GUILayout.Toggle(File.Exists(mainPath + "\\" + uiBgName + "\\" + currentYotogiName + ".json"), "Skill Exists");
            }
            GUI.enabled = true;

            //Save/Load
            GUILayout.Label("Save/Load");

            GUILayout.BeginHorizontal();
            overrideDefault = GUILayout.Toggle(overrideDefault, "Overwrite Default");
            if (inYotogi)
            {
                overrideSkill = GUILayout.Toggle(overrideSkill, "Overwrite Skill");
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Save"))
            {
                savePressed = true;
            }
            if (inYotogi)
            {
                if (!importPressed)
                {
                    if (GUILayout.Button("Import"))
                    {
                        importPressed = true;
                    }
                }
                else
                {
                    Stages_ImportSelect();
                }
            }
            if (GUILayout.Button("Force Load"))
            {
                loadPressed = true;
            }

            //Yotogi Related Sections
            if (inYotogi)
            {
                //Camera
                GUILayout.Label("Camera");

                GUILayout.Label("Speed");
                if (GUILayout.Button("1"))
                {
                    cameraMoveSpeed = 1f;
                }
                cameraMoveSpeed = GUILayout.HorizontalSlider(cameraMoveSpeed, .1f, 5f);
                GUILayout.BeginHorizontal();
                if (GUILayout.RepeatButton("↑"))
                {
                    cameraPositionPressed = 1;
                }
                if (GUILayout.RepeatButton("↓"))
                {
                    cameraPositionPressed = 2;
                }
                if (GUILayout.RepeatButton("←"))
                {
                    cameraPositionPressed = 3;
                }
                if (GUILayout.RepeatButton("→"))
                {
                    cameraPositionPressed = 4;
                }
                GUILayout.EndHorizontal();

                //Maids/Men Position/Rotation
                GUILayout.Label("Maids/Men");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Position"))
                {
                    UnityEngine.Debug.Log("YotogiAnywhere: Maid Position Pressed");
                    showMaidPositionGizmoPressed = true;
                }
                if (GUILayout.Button("Rotate"))
                {
                    UnityEngine.Debug.Log("YotogiAnywhere: Maid Position Pressed");
                    showMaidRotationGizmoPressed = true;
                }
                GUILayout.EndHorizontal();

                //BG Objects
                GUILayout.Label("Manage BG Objects");

                if (!addCategory)
                {
                    if (GUILayout.Button("Add"))
                    {
                        addCategory = true;
                        UnityEngine.Debug.Log("YotogiAnywhere: Add BG Pressed");
                    }
                }
                else
                {
                    BGObjects_DrawCategorySelect();
                }
                if (currentCategory != null && bgObjectDict.ContainsKey(currentCategory))
                {
                    BGObjects_DrawObjectSelect();
                }

                //Create the Position/Rotation Buttons
                if (currentBGObjectId == null || activeBGObjects == null)
                {
                    GUI.enabled = false;
                    GUILayout.Label("Current Object: null");
                }
                else
                {
                    GUI.enabled = true;
                    GUILayout.Label("Current Object: " + getHardcodedTranslation(activeBGObjects[currentBGObjectId].data.name) + "-" + currentBGObjectId);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Position"))
                {
                    showBGObjPositionGizmoPressed = true;
                }
                if (GUILayout.Button("Rotate"))
                {
                    showBGObjRotationGizmoPressed = true;
                }
                if (GUILayout.Button("Remove"))
                {
                    //Remove the Gizmo
                    activeBGObjects[currentBGObjectId].gizmo.Delete();

                    //Remove
                    UnityEngine.Object.DestroyImmediate(activeBGObjects[currentBGObjectId].game_object);

                    //Remove from collection
                    activeBGObjects.Remove(currentBGObjectId);

                    //Set back to null to disable buttons
                    currentBGObjectId = null;
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Scale");
                if (GUILayout.Button("1"))
                {
                    currentBGObjectScale = 1f;
                }
                currentBGObjectScale = GUILayout.HorizontalSlider(currentBGObjectScale, .1f, 5f);
                GUI.enabled = true;

                GUILayout.Label("Active Objects");

                bgObjectActiveScrollPosition = GUILayout.BeginScrollView(bgObjectActiveScrollPosition, GUILayout.Width(Screen.width / 8 - 40), GUILayout.Height(Screen.height / 4 - 40));
                if (activeBGObjects != null)
                {
                    foreach (KeyValuePair<String, YTABgObject> kvp in activeBGObjects)
                    {
                        if (GUILayout.Button(getHardcodedTranslation(kvp.Value.data.name) + "-" + kvp.Key))
                        {
                            currentBGObjectId = kvp.Key;

                            //Display this gizmo
                            showBGObjPositionGizmoPressed = true;

                            //Fix scale
                            currentBGObjectScale = kvp.Value.game_object.transform.localScale.x;
                        }
                    }
                }
                GUILayout.EndScrollView();
            }

            GUILayout.EndVertical();

            //Make it draggable this must be last always
            GUI.DragWindow();
        }

        private void BGObjects_DrawCategorySelect()
        {
            //Create a Back Button
            if (GUILayout.Button("Back"))
            {
                //Reset Category
                addCategory = false;
                currentCategory = null;
            }

            //Create a Button for each Category
            GUILayout.Label("Category");
            bgObjectCategoryScrollPosition = GUILayout.BeginScrollView(bgObjectCategoryScrollPosition, GUILayout.Width(Screen.width / 8 - 40), GUILayout.Height(Screen.height / 4 - 40));
            if (bgObjectDict != null)
            {
                foreach (KeyValuePair<string, List<KeyValuePair<string, PhotoBGObjectData>>> kvp in bgObjectDict)
                {
                    if (GUILayout.Button(getHardcodedTranslation(kvp.Key)))
                    {
                        //Hide Category
                        addCategory = false;

                        //Show Objects for Category
                        currentCategory = kvp.Key;
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private void BGObjects_DrawObjectSelect()
        {
            //Create a Back Button
            if (GUILayout.Button("Back"))
            {
                //Reset Category
                addCategory = true;
                currentCategory = null;
            }

            //Create a Button for each BG Object in Category
            GUILayout.Label("BG Objects");
            bgObjectAvailableScrollPosition = GUILayout.BeginScrollView(bgObjectAvailableScrollPosition, GUILayout.Width(Screen.width / 8 - 40), GUILayout.Height(Screen.height / 4 - 40));
            if (bgObjectDict != null && currentCategory != null && bgObjectDict.ContainsKey(currentCategory))
            {
                List<KeyValuePair<string, PhotoBGObjectData>> bgObjects = bgObjectDict[currentCategory];
                if (bgObjects != null)
                {
                    foreach (KeyValuePair<string, PhotoBGObjectData> kvp in bgObjects)
                    {
                        if (GUILayout.Button(getHardcodedTranslation(kvp.Value.name)))
                        {
                            //Create the object
                            YTABgObject obj = AddObject(kvp.Value, "");
                            currentBGObjectId = obj.create_time;

                            //Display this gizmo
                            showBGObjPositionGizmoPressed = true;

                            //Return to main view
                            addCategory = false;
                            currentCategory = null;

                            //Reset Scale
                            currentBGObjectScale = 1f;
                        }
                    }
                }
            }
            GUILayout.EndScrollView();

        }

        private void Stages_ImportSelect()
        {
            //Create a Back Button
            if (GUILayout.Button("Back"))
            {
                //Reset Category
                importPressed = false;
            }

            //Overide
            overrideImport = GUILayout.Toggle(overrideImport, "Override");

            //List Stages
            string mainPath = UTY.gameProjectPath + "\\Mod\\[YotogiAnywhere]\\[BGSkillData]";
            importScrollPosition = GUILayout.BeginScrollView(importScrollPosition, GUILayout.Width(Screen.width / 8 - 40), GUILayout.Height(Screen.height / 4 - 40));
            if (Directory.Exists(mainPath))
            {
                string[] folderNames = Directory.GetDirectories(mainPath, "*", SearchOption.TopDirectoryOnly);
                foreach (string folderName in folderNames)
                {
                    string dir = folderName.Substring(folderName.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                    if (!dir.Equals(uiBgName))
                    {
                        if (GUILayout.Button(dir))
                        {
                            string[] copyFiles = Directory.GetFiles(folderName);
                            foreach (string copyFile in copyFiles)
                            {
                                string fileName = Path.GetFileName(copyFile);
                                string newFileName = Path.Combine(mainPath + "\\" + uiBgName, fileName);
                                if (!File.Exists(newFileName) || overrideImport)
                                {
                                    try
                                    {
                                        File.Copy(copyFile, newFileName, true);
                                    }
                                    catch (Exception ex)
                                    {
                                        UnityEngine.Debug.Log("YotogiAnywhere: Import Error: " + fileName + "\n" + ex.ToString());
                                    }
                                }
                                importPressed = false;
                                overrideImport = false;
                                lastMessage = "Import Finished Check Log for possible Errors";
                            }
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
        }

        private string getHardcodedTranslation(string jp)
        {
            Dictionary<String, String> categories = new Dictionary<string, string>()
            {
                { "家具", "Furniture" },
                { "道具", "Tools"},
                { "文房具", "Stationary" },
                { "グルメ", "Food" },
                { "ドリンク", "Drinks" },
                { "その他", "Other"},
                { "カジノアイテム", "Casino" },
                { "プレイアイテム", "Yotogi" },
                { "パーティクル", "Particle Effects" },
                { "マイオブジェクト", "Custom Studio PNG"},
                { "mirrors", "Mirrors" }
            };

            Dictionary<String, String> objects = new Dictionary<string, string>()
            {
                {"イスA", "Chair A"},
                {"イスB", "Chair B"},
                {"イスC", "Chair C"},
                {"イスD", "Chair D"},
                {"ふかふかチェア", "Soft Chair"},
                {"マリーゴールド", "Marigolds"},
                {"シンプルテーブル", "Simple Table"},
                {"サロンのソファ", "Salon Sofa"},
                {"サロンのソファ2", "Salon Sofa 2"},
                {"サロンのソファ3", "Salon Sofa 3"},
                {"サロンのソファ4", "Salon Sofa 4"},

                {"ワイングラス", "Wine Glass"},
                {"カレー入り鍋", "Curry Pan"},
                {"じょうろ", "Watering Can"},
                {"華道道具", "Flower Tool"},
                {"華道イス", "Flower Chair"},
                {"まな板", "Chopping Board"},
                {"スタンドマイク", "Microphone Stand"},
                {"スタンドマイクベース", "Microphone Stand Base"},
                {"コアラマイク", "Koala Mike"},
                {"断界斧ゴルガンド・黒", "Black Ax"},
                {"断界斧ゴルガンド・金", "Gold Ax"},
                {"断界斧ゴルガンド・銀", "Silver Ax"},
                {"断界斧ゴルガンド・白", "White Ax"},
                {"創界杖サナラーク・黒", "Black Wand"},
                {"創界杖サナラーク・金", "Gold Wand"},
                {"創界杖サナラーク・銀", "Silver Wand"},
                {"創界杖サナラーク・白", "White Wand"},
                {"錬界書アルマリリス・黒", "Black Spell Book"},
                {"錬界書アルマリリス・茶", "Gold Spell Book"},
                {"錬界書アルマリリス・紫", "Silver Spell Book"},
                {"錬界書アルマリリス・白", "White Spell Book"},
                {"ARウィンドウ", "AR Window"},
                {"水風船_黄", "Yellow Water Balloon"},
                {"水風船_青", "Blue Water Balloon"},
                {"水風船_橙", "Orange Water Balloon"},
                {"水風船_桃", "Pink Water Balloon"},
                {"ばち", "Bachi Stick (Drum)"},
                {"祭うちわ_赤", "Red Festival Fan"},
                {"祭うちわ_青", "Blue Festival Fan"},
                {"クレープ屋台", "Crepe Stall"},
                {"クレープ屋台_夜", "Crepe Stall (Night)"},
                {"ゴザ", "Goza Mat"},
                {"ゴザ_夜", "Goza Mat (Night)"},
                {"くじ引き屋台", "Lottery Stall"},
                {"くじ引き屋台_夜", "Lottery Stall (Night)"},
                {"ポテトフライ屋台", "Fried Potato Stall"},
                {"ポテトフライ屋台_夜", "Fried Potato Stall (Night)"},
                {"りんごあめ屋台", "Candy Apple Stall"},
                {"りんごあめ屋台_夜", "Candy Apple Stall (Night)"},
                {"射的屋台", "Shooting Gallery Stall"},
                {"射的屋台_夜", "Shooting Gallery Stall (Night)"},
                {"たこ焼き屋台", "Takoyaki Stall"},
                {"たこ焼き屋台_夜", "Takoyaki Stall (Night)"},
                {"焼きそば屋台", "Yakisoba Stall"},
                {"焼きそば屋台_夜", "Yakisoba Stall (Night)"},
                {"ビニールプール", "Inflatable Pool"},
                {"ビニールプール・水入り", "Inflatable Pool (Filled)"},
                {"七輪", "Grill"},
                {"ろうそく", "Candle"},
                {"4枚のカード", "4 Cards"},
                {"1枚のカード", "1 Card"},
                {"ハタキ", "Duster"},
                {"シルクハット", "Top Hat"},
                {"鳩入りシルクハット", "Top Hat (Dove)"},
                {"VRコントローラー", "VR Controller"},
                {"雑巾A", "Cleaning Rag A"},
                {"雑巾B", "Cleaning Rag B"},
                {"紙袋", "Paper Bag"},
                {"ダンボール", "Cardboard Box"},
                {"水族館の椅子(大)", "Aquarium Bench (Large)"},
                {"水族館の椅子(小)", "Aquarium Bench (Small)"},
                {"ハメ撮りカメラ", "Camcorder"},

                {"ノート", "Notepad"},
                {"参考書（開）", "Book (Opened)"},
                {"参考書A（閉）", "Book A (Closed)"},
                {"参考書B（閉）", "Book B (Closed)"},
                {"参考書C（閉）", "Book C (Closed)"},
                {"参考書D（閉）", "Book D (Closed)"},
                {"参考書E（閉）", "Book E (Closed)"},
                {"鉛筆（緑）", "Pencil (Green)"},
                {"鉛筆（黒）", "Pencil (Black)"},
                {"鉛筆（赤）", "Pencil (Red)"},
                {"消しゴム（青）", "Eraser (Blue)"},
                {"消しゴム（紫）", "Eraser (Purple)"},
                {"消しゴム（黄）", "Eraser (Yellow)"},
                {"ペン（桃）", "Pen (Pink)"},
                {"ペン（黒）", "Pen (Black)"},
                {"ペン（茶）", "Pen (Brown)"},
                {"ペン（緑）", "Pen (Green)"},
                {"スティック糊", "Glue Stick"},

                {"アクアパッザ", "Acqua Pazza"},
                {"バースデーケーキ", "Birthday Cake"},
                {"スープ", "Soup"},
                {"ショートケーキ", "Shortcake"},
                {"サンドイッチ", "Sandwich"},
                {"モンブラン", "Mont Blanc"},
                {"ケーキ", "Cake"},
                {"フードプレート", "Plate"},
                {"焼きそば", "Fried noodles"},
                {"コーンアイス・チョコミント", "Mint Ice Cream"},
                {"コーンアイス・ストロベリー", "Strawberry Ice Cream"},
                {"コーンアイス・バニラ", "Vanilla Ice Cream"},
                {"にくまん", "Steamed Buns"},
                {"蒸籠にくまん", "Steamed Buns Basket"},
                {"月見団子", "Dumplings"},
                {"マンガ肉", "Manga Meat"},
                {"樽エール", "Barrel Ale"},
                {"チョコバナナ", "Chocolate Banana"},
                {"イカ焼き", "Grilled Squid"},
                {"リンゴ飴", "Apple Candy"},
                {"焼いたサンマ", "Baked Saury (Fish)"},
                {"焼いたトウモロコシ", "Baked Corn"},
                {"カットスイカ", "Sliced Watermelon"},
                {"ラムネ", "Ramune"},
                {"弁当", "Bento"},
                {"シャンパン", "Champagne"},
                {"中華料理", "Chinese Food"},
                {"もやし炒め", "Stir-fried Sprouts"},
                {"もやし炒め(空)", "Stir-fried Sprouts (Empty)"},
                {"アクアパッツァ(銀トレー)", "Acqua Pazza (Silver Tray)"},
                {"オムライス(銀トレー)", "Omurice (Silver Tray)"},
                {"ステーキ", "Steak"},
                {"SUSHI", "Sushi"},
                {"クレープ", "Crêpe"},
                {"ハンバーガー盛り合わせ", "Hamburger"},
                {"焼鳥盛り合わせ", "Yakitori"},
                {"バレンタインチョコレート", "Valentine Chocolate"},

                {"ワインボトル開封済み", "Wine Bottle (Opened)"},
                {"ワインボトル未開封", "Wine Bottle (Closed)"},
                {"グリーンスムージー", "Green Smoothie"},
                {"レッドスムージー", "Red Smoothie"},
                {"カクテル・ブルー", "Blue Cocktail"},
                {"カクテル・レッド", "Red Cocktail"},
                {"カクテル・イエロー", "Yellow Cocktail"},
                {"コーヒー", "Coffee"},
                {"缶ビール", "Beer (Can)"},
                {"ジョッキビール", "Beer (Mug)"},
                {"トロピカルアイスティー", "Iced Tea"},

                {"メロン", "Melon"},
                {"シャチの乗り物", "Inflatable Orca"},
                {"ねい人形", "Puppet"},
                {"ロボねい人形", "Robot Girl"},
                {"メイドねい人形", "Maid Doll"},
                {"ランス10・ハニー・赤", "Weird Ghost (Red)"},
                {"ランス10・ハニー・青", "Weird Ghost (Blue)"},
                {"ランス10・ハニー・緑", "Weird Ghost (Green)"},
                {"ランス10・ハニー・茶", "Weird Ghost (Brown)"},
                {"ビーチボール・ブルー", "Beachball (Blue)"},
                {"ビーチボール・グリーン", "Beachball (Green)"},
                {"ビーチボール・レッド", "Beachball (Red)"},
                {"ビーチボール・イエロー", "Beachball (Yellow)"},
                {"お祓い棒", "Ogibo (Miko Staff)"},
                {"ススキ", "Susuki (White Weed"},
                {"藁人形", "Straw Doll"},
                {"藁人形（釘有り）", "Straw Doll (With Nails)"},
                {"お守り", "Amulet"},
                {"竹帚", "Broom"},
                {"スノーボード・青", "Snowboard (Blue)"},
                {"スノーボード・緑", "Snowboard (Green)"},
                {"スノーボード・赤", "Snowboard (Red)"},
                {"スノーボード・黄", "Snowboard (Yellow)"},
                {"雪だるま・青", "Snowman (Blue)"},
                {"雪だるま・緑", "Snowman (Green)"},
                {"雪だるま・赤", "Snowman (Red)"},
                {"雪だるま・黄", "Snowman (Yellow)"},
                {"雪玉", "Snowball"},
                {"雪玉の山", "Snowball Pile"},
                {"雪クマ親分", "SnowBear"},
                {"キュロちゃん", "Kuro-chan"},
                {"冥王弾・青", "Fireworks (Blue)"},
                {"冥王弾・緑", "Fireworks (Green)"},
                {"冥王弾・紫", "Fireworks (Purple)"},
                {"冥王弾・赤", "Fireworks (Red)"},

                {"カードデッキ", "Deck of Cards"},
                {"カードシューター", "Card Shooter"},
                {"カード・クローバー・A", "Clubs A"},
                {"カード・クローバー・2", "Clubs 2"},
                {"カード・クローバー・3", "Clubs 3"},
                {"カード・クローバー・4", "Clubs 4"},
                {"カード・クローバー・5", "Clubs 5"},
                {"カード・クローバー・6", "Clubs 6"},
                {"カード・クローバー・7", "Clubs 7"},
                {"カード・クローバー・8", "Clubs 8"},
                {"カード・クローバー・9", "Clubs 9"},
                {"カード・クローバー・10", "Clubs 10"},
                {"カード・クローバー・J", "Clubs J"},
                {"カード・クローバー・Q", "Clubs Q"},
                {"カード・クローバー・K", "Clubs K"},
                {"カード・ダイヤ・A", "Diamonds A"},
                {"カード・ダイヤ・2", "Diamonds 2"},
                {"カード・ダイヤ・3", "Diamonds 3"},
                {"カード・ダイヤ・4", "Diamonds 4"},
                {"カード・ダイヤ・5", "Diamonds 5"},
                {"カード・ダイヤ・6", "Diamonds 6"},
                {"カード・ダイヤ・7", "Diamonds 7"},
                {"カード・ダイヤ・8", "Diamonds 8"},
                {"カード・ダイヤ・9", "Diamonds 9"},
                {"カード・ダイヤ・10", "Diamonds 10"},
                {"カード・ダイヤ・J", "Diamonds J"},
                {"カード・ダイヤ・Q", "Diamonds Q"},
                {"カード・ダイヤ・K", "Diamonds K"},
                {"カード・ハート・A", "Hearts A"},
                {"カード・ハート・2", "Hearts 2"},
                {"カード・ハート・3", "Hearts 3"},
                {"カード・ハート・4", "Hearts 4"},
                {"カード・ハート・5", "Hearts 5"},
                {"カード・ハート・6", "Hearts 6"},
                {"カード・ハート・7", "Hearts 7"},
                {"カード・ハート・8", "Hearts 8"},
                {"カード・ハート・9", "Hearts 9"},
                {"カード・ハート・10", "Hearts 10"},
                {"カード・ハート・J", "Hearts J"},
                {"カード・ハート・Q", "Hearts Q"},
                {"カード・ハート・K", "Hearts K"},
                {"カード・スペード・A", "Spades A"},
                {"カード・スペード・2", "Spades 2"},
                {"カード・スペード・3", "Spades 3"},
                {"カード・スペード・4", "Spades 4"},
                {"カード・スペード・5", "Spades 5"},
                {"カード・スペード・6", "Spades 6"},
                {"カード・スペード・7", "Spades 7"},
                {"カード・スペード・8", "Spades 8"},
                {"カード・スペード・9", "Spades 9"},
                {"カード・スペード・10", "Spades 10"},
                {"カード・スペード・J", "Spades J"},
                {"カード・スペード・Q", "Spades Q"},
                {"カード・スペード・K", "Spades K"},
                {"カード・JOKER", "Joker"},
                {"カジノチップ10", "Casino Chips (10)"},
                {"カジノチップ100", "Casino Chips (100)"},
                {"カジノチップ1000", "Casino Chips (1000)"},

                {"コンドーム開き", "Opened Condom"},
                {"コンドーム閉じ", "Sealed Condom"},
                {"コンドーム袋", "Condom"},
                {"ディルドボックス", "Riding Dildo"},
                {"ギロチン", "Guillotine"},
                {"ギロチン台", "Guillotine Stand"},
                {"ロングマット", "Long Mat"},
                {"ラブソファー", "Love Sofa"},
                {"拘束台", "Restraint"},
                {"拘束台H型", "Restraint H Table"},
                {"拘束台騎乗位用", "Restraint Cowgirl Chair"},
                {"スケベイス", "Bath Stool"},
                {"ソープマット", "Soapland Mat"},
                {"三角木馬", "Wooden Horse"},
                {"パンダさんラブバイブ", "Panda Vibrator"},
                {"ゾウさんラブバイブ", "Elephant Vibrator"},
                {"リスさんラブバイブ", "Squirell Vibrator"},

                {"紙吹雪", "Confetti"},
                {"キッチン用-水", "Kitchen Water"},
                {"キッチン用-泡", "Kitchen Bubbles"},
                {"キッチン用手元の泡", "Kitchen Scrubbing Bubbles"},
                {"キッチン用-湯気", "Kitchen Steam"},
                {"キッチン用-湯気(黒)", "Kitchen Black Smoke"},
                {"お風呂用ー湯気", "Bath Steam"},
                {"空間-星", "Stars"},
                {"空間-泡", "Large Bubbles"},
                {"空間-粉雪1", "Snow 1"},
                {"空間-粉雪2", "Snow 2"},
                {"空間-湯気", "Large Steam"},
                {"空間-吹き出す煙", "Large White Smoke"},

                {"handmirror", "Hand Mirror"},
                {"maidroom_mirror_l", "Maid Room Mirror (L)"},
                {"maidroom_mirror_s", "Maid Room Mirror (S)"},
                {"rect_mirror", "Rectangular Mirror"},
                {"round_mirror", "Round Mirror"},
                {"square_mirror", "Square Mirror"}
            };

            if (categories.ContainsKey(jp))
            {
                return categories[jp];
            }
            if (objects.ContainsKey(jp))
            {
                return objects[jp];
            }

            return jp;
        }
        #endregion
    }

    public class YTAData
    {
        public float all_pos_x { get; set; }
        public float all_pos_y { get; set; }
        public float all_pos_z { get; set; }

        public float all_rot_x { get; set; }
        public float all_rot_y { get; set; }
        public float all_rot_z { get; set; }

        public string cam_target_position { get; set; }
        public string cam_distance { get; set; }
        public string cam_rotation { get; set; }

        public List<YTABGObjectData> bgObjects { get; set; }

        public YTAData()
        {
            cam_target_position = "";
            cam_distance = "";
            cam_rotation = "";

            bgObjects = new List<YTABGObjectData>();
        }
    }

    public class YTABGObjectData
    {
        public string name { get; set; }
        public string id { get; set; }

        public float pos_x { get; set; }
        public float pos_y { get; set; }
        public float pos_z { get; set; }

        public float rot_x { get; set; }
        public float rot_y { get; set; }
        public float rot_z { get; set; }
        public float rot_w { get; set; }

        public float scale { get; set; }

        public YTABGObjectData()
        {
            name = null;
            id = null;
        }
    }

    public class YTABgObject : IComparable<YTABgObject>
    {
        public string create_time;
        public GameObject game_object;
        public PhotoBGObjectData data;
        public PhotoTransTargetObject gizmo;

        public YTABgObject()
        {
            this.create_time = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }

        public YTABgObject(PhotoBGObjectData _data, string _create_time)
        {
            data = _data;
            create_time = _create_time;

            if (String.IsNullOrEmpty(_create_time))
            {
                this.create_time = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            }
        }

        public int CompareTo(YTABgObject obj)
        {
            return obj == null ? 1 : long.Parse(this.create_time).CompareTo(long.Parse(obj.create_time));
        }
    }
}
