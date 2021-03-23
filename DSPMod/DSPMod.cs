using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib; 
using UnityEngine;
using UnityEngine.UI;
using System.IO;


namespace DSPMod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class DSPMod : BaseUnityPlugin
    {
        public const string pluginGuid = "net.powana.plugins.DSPMod";
        public const string pluginName = "Test plugin for DSP";
        public const string pluginVersion = "0.1.0.1";

        static Dictionary<int, bool> highlightEnabled;
        static Sprite spriteEnabled = null; // todo better names
        static Sprite spriteDisabled = null;

        Harmony harmony;

        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            harmony = new Harmony(pluginGuid);
            harmony.PatchAll(typeof(DSPMod));
            Debug.Log("Better globe map started!");
            
        }
        [HarmonyPrefix, HarmonyPatch(typeof(GameMain), "Begin")]
        public static void GameMain_Begin_Prefix()
        {
            Debug.Log("Game main begin.");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIGlobemap), "_OnOpen")]
        public static void UIGlobeMap_OnOpen(UIGlobemap __instance)
        {
            Debug.Log("Globe map opened.");
        }

        // This is the detail view to the right in globe view
        [HarmonyPostfix, HarmonyPatch(typeof(UIPlanetDetail), "_OnOpen")]
        public static void UIPlanetDetail_Postfix(UIPlanetDetail __instance)
        {

            GameObject go = __instance.gameObject;

            highlightEnabled = new Dictionary<int, bool> {
                {0, false},
                {1, false},
                {2, false},
                {3, false},
                {4, false},
                {5, false},
                {6, false},
                {7, false},
                {8, false},
                {9, false},
                {10, false},
                {11, false},
                {12, false},
                {13, false},
                {14, false},
                {15, false},
            };
            Debug.Log("UIPlanetDetail opened.");

            /**
             * Structure of gameobjects is (from trial and error and a lot of Debug.Log):
             * planet-detail-ui:
             *  line
             *  res-entry [Contains the UIResAmountEntry component]:
             *   icon [Contains UIButton component, UI.Image]
             *   value-text
             *  res-entry(clone):
             *   icon
             *   value-text
             * 
             */

            Transform resGroup = go.transform.Find("res-group"); //.GetComponentsInChildren<Transform>;
            if (spriteEnabled == null || spriteDisabled == null)
            {
                spriteEnabled = resGroup.GetChild(2).Find("icon").GetComponent<Image>().sprite;  // todo fix actual sprites
                spriteDisabled = resGroup.GetChild(3).Find("icon").GetComponent<Image>().sprite;
            }
            foreach (Transform child in resGroup)
            {

                // Create buttons for resources that exist and are selectable, that don't already have buttons. todo: fix bugged refids on solar and ocean
                if (child.gameObject.name.Contains("res-entry") && child.GetComponent<UIResAmountEntry>().refId != 0 && child.GetComponent<UIResAmountEntry>().valueString.Trim() != "0" && !child.Find("net-powana-show-nearest"))
                {
                    Debug.Log("Created buttons for " + (EVeinType)child.GetComponent<UIResAmountEntry>().refId + " Amount: " + child.GetComponent<UIResAmountEntry>().valueString);

                    int tempRefId = child.GetComponent<UIResAmountEntry>().refId; // ID of resource
                    Transform iconTransform = child.Find("icon"); 

                    GameObject toggleHighlightButton = GameObject.Instantiate<GameObject>(
                        original: iconTransform.gameObject,
                        position: new Vector3(iconTransform.position.x-0.2f, child.position.y, child.position.z),
                        rotation: Quaternion.identity,
                        parent:   child);

                    GameObject showNearestVeinButton = GameObject.Instantiate<GameObject>(
                        original: iconTransform.gameObject,
                        position: new Vector3(iconTransform.position.x-0.4f, child.position.y, child.position.z),
                        rotation: Quaternion.identity,
                        parent:   child);

                    toggleHighlightButton.GetComponent<Image>().sprite = spriteDisabled;
                    showNearestVeinButton.GetComponent<Image>().sprite = spriteDisabled;

                    UIButton uiButton1 = toggleHighlightButton.GetComponent<UIButton>();
                    uiButton1.tips.tipTitle = "Highlight";
                    uiButton1.tips.tipText = "Highlight veins of the type: " + (EVeinType) tempRefId + ".";

                    UIButton uiButton2 = showNearestVeinButton.GetComponent<UIButton>();
                    uiButton2.tips.tipTitle = "Show nearest vein";
                    uiButton2.tips.tipText = "Move camera to the " + (EVeinType) tempRefId + " nearest the player";

                    uiButton1.onClick += (id) => { ToggleVeinHeighlight(tempRefId, ref toggleHighlightButton); };
                    uiButton1.onRightClick += (id) => { DebugStuff(id); };
                    toggleHighlightButton.name = "net-powana-toggle-highlight";

                    Quaternion startRot = GameCamera.instance.planetPoser.rotation;  // Save player pos
                    uiButton2.onClick += (id) => { ShowNearestVein(tempRefId, startRot, ref showNearestVeinButton); };
                    showNearestVeinButton.name = "net-powana-show-nearest";
                    // todo, turn off highlights when exiting globemap?
                }

            }
           
            foreach (UIResAmountEntry res in __instance.entries)
            {
                /**
                 * res.refId contains the id of the resource counted. According to the enum EVeinType:
                 * 0 = None, used for non-mineral resources ie. ocean type, construction area, wind/solar energy ratio etc.
                 * Iron = 1,
                 * Copper = 2,
                 * Silicium = 3,
                 * Titanium = 4,
                 * Stone = 5,
                 * Coal = 6,
                 * Oil = 7,
                 * Fireice = 8,
                 * Diamond = 9,
                 * Fractal = 10,
                 * Crysrub = 11,
                 * Grat = 12,
                 * Bamboo = 13,
                 * Mag = 14,
                 * Max = 15
                 * 
                 * Non-mineral resources (area, wind energy ratio, solar energy ratio etc.) do not have a stringbuilder attached.
                 */
                // Debug.Log((EVeinType) res.refId + ": " + res.valueString);
                // if (res.sb != null) Debug.Log(res.sb.ToString());
            }
        }

        private static void ShowNearestVein(int refId, Quaternion startRot2, ref GameObject button)
        {
            
            Debug.Log("Show nearest called with refId: " + refId.ToString() + " startRot: " + startRot2.ToString());
            
            GameCamera gameCamera = GameCamera.instance;
            PlanetPoser planetPoser = gameCamera.planetPoser;
            Debug.Log("1");

            // (This ended up not working out) Use reflection to get the (private) start rotation of globemap, easier than trying to look for a player position
            UIGlobemap globemap = UIRoot.instance.uiGame.globemap;
            FieldInfo startRotField = typeof(UIGlobemap).GetField("start_rotation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | BindingFlags.PutRefDispProperty);
            Quaternion startRot = (Quaternion) startRotField.GetValue(globemap);
            

            // crash here
            // Get the veins that have a matching refId, using the planetdata of the planet linked to in the planetdetail pane
            PlanetData.VeinGroup[] veins = UIRoot.instance.uiGame.planetDetail.planet.veinGroups;
            //List<PlanetData.VeinGroup> veins = UIRoot.instance.uiGame.planetDetail.planet.veinGroups.Where(x => x.type == (EVeinType) refId).ToList();
            
            // toRotate can be used with planetPoser to move to vein.pos, default to first vein
            Quaternion toRotate = Quaternion.FromToRotation(Vector3.up, veins[0].pos);
            float closestAngle = Quaternion.Angle(startRot, toRotate);

            float angleDist;
            Quaternion tempRotate;

            foreach (PlanetData.VeinGroup vein in veins)
            {
                if (vein.type == (EVeinType) refId) { 
                    // works but spins the camera, todo: make it not do that, todo: selects the wrong vein.
                    tempRotate = Quaternion.FromToRotation(Vector3.up, vein.pos);
                    angleDist = Quaternion.Angle(startRot, Quaternion.FromToRotation(Vector3.up, vein.pos));
                    if (angleDist < closestAngle)
                    {
                        closestAngle = angleDist;
                        toRotate = tempRotate;
                    }
                }
            }
            planetPoser.rotationWanted = toRotate;
            planetPoser.distWanted = planetPoser.distMax;
            
        }

        private static void ToggleVeinHeighlight(int refId, ref GameObject button)
        {

            Debug.Log("HeighlightVeins called with refId:" + refId.ToString());
            if (refId < 0 || refId > 15)
            {
                return;
            }

            
            // This is the group of gameobjects that contain the following components:
            // RectTransform, CanvasRenderer, UI.Image, UIVeinDetail
            Transform[] veinMarks = GameObject.Find("UI Root/Overlay Canvas/In Game/Scene UIs/Vein Marks/").GetComponentsInChildren<Transform>();
            Vector2 effectDistance = new Vector2(5, 5);
            Color highlightColor = new Color(1F, 0.4F, 0.23F, 0.88f);  // todo make configurable

            highlightEnabled[refId] = !highlightEnabled[refId];
            button.GetComponent<Image>().sprite = highlightEnabled[refId] ? spriteEnabled : spriteDisabled;

            // Loop through all nodes on planet (all planets?)
            foreach (Transform veinTip in veinMarks)
            {
                
                UIVeinDetail uiVeinDetail = veinTip.GetComponent<UIVeinDetail>();
                if (uiVeinDetail == null) continue;

                foreach (UIVeinDetailNode uiVeinDetailNode in uiVeinDetail.allTips)
                {
                    // Check if the node is of the type we selected, todo: Check if it is also on the right planet.
                    if ((uiVeinDetail.inspectPlanet != null) && (uiVeinDetail.inspectPlanet.veinGroups[uiVeinDetailNode.veinGroupIndex].type == (EVeinType)refId))
                    {
                        PlanetData.VeinGroup matchingVein = uiVeinDetail.inspectPlanet.veinGroups[uiVeinDetailNode.veinGroupIndex];
                        Debug.Log("Matching veinGroup found: " + matchingVein.type + " " + matchingVein.amount.ToString() + " at " + matchingVein.pos + " on " + uiVeinDetail.inspectPlanet.name + ". Nodes: " + matchingVein.count.ToString());

                        Outline ol = uiVeinDetailNode.GetComponent<Outline>();
                        if (ol == null)  // Todo make outline pretty
                        {
                            ol = uiVeinDetailNode.gameObject.AddComponent(typeof(Outline)) as Outline;
                            ol.effectColor = highlightColor;
                            ol.effectDistance = effectDistance;
                            ol.useGraphicAlpha = true;
                        }
                        ol.enabled = highlightEnabled[refId];



                    }
                }
                
               
            }
        }



        // Debug methods below
        private static void DebugStuff(int action)
        {
            Debug.Log("Debugstuff called: " + action.ToString());
            /*
            UIGlobemap globemap = UIRoot.instance.uiGame.globemap;
            FieldInfo privFade = typeof(UIGlobemap).GetField("fade", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            FieldInfo privFadeTarget = typeof(UIGlobemap).GetField("fadeTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            privFade.SetValue(globemap, 0f);
            var fadeTarget = privFadeTarget.GetValue(globemap);
            Debug.Log("fadeTarget value:" + fadeTarget);
            privFadeTarget.SetValue(globemap, 1f);
            
            GameCamera gameCamera = GameCamera.instance;          
            gameCamera.rtsPoser.yawWanted = 0f;                   
            gameCamera.rtsPoser.pitchCoefWanted = 0f;
            gameCamera.rtsPoser.distCoefWanted = 0.7f;
            gameCamera.rtsPoser.ToWanted();
            gameCamera.buildPoser.yawWanted = 0f;
            gameCamera.buildPoser.pitchCoefWanted = 0f;
            gameCamera.buildPoser.distCoefWanted = 0.7f;
            gameCamera.buildPoser.ToWanted();
            // UIRoot.instance.uiGame.globemap.FadeOut();
            */

            GameCamera gameCamera = GameCamera.instance;
            PlanetPoser planetPoser = gameCamera.planetPoser;
            planetPoser.distWanted = planetPoser.distMax;
            planetPoser.rotationWanted = Quaternion.identity;

            // planetPoser.ToWanted(); // INSTANTLY SET TO WANTED, NOT CALLING THIS ALLOWS SMOOTH TRANSITION

        }


        public static string GetComponentsStr(GameObject go)
        {
            string s = "Components:";
            foreach (var component in go.GetComponents(typeof(Component))) {
                s += "\n" + component.ToString();
            }
            return s;
        }

        public static string GetLayout(Transform current, int level)
        {
            string s = current.name;
            foreach (Transform child in current)
            {
                s += "\n" + new string(' ', level) + " / " + GetLayout(child, level+1);
            }
            return s;
        }

        public static string GetPath(Transform current)
        {
            if (current.parent == null)
                return current.name;
            return GetPath(current.parent) + "/" + current.name;
        }
    }
}

