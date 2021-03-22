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
using System.Threading.Tasks;


namespace DSPMod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class DSPMod : BaseUnityPlugin
    {
        public const string pluginGuid = "net.powana.plugins.DSPMod";
        public const string pluginName = "Test plugin for DSP";
        public const string pluginVersion = "0.0.0.1";

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

                // Create buttons for resources that exist and are selectable, that don't already have buttons.
                if (child.gameObject.name.Contains("res-entry") && child.GetComponent<UIResAmountEntry>().refId != 0 && child.GetComponent<UIResAmountEntry>().valueString.Trim() != "0" && !child.Find("net-powana-show-nearest"))
                {
                    Debug.Log("Created button for " + (EVeinType)child.GetComponent<UIResAmountEntry>().refId + " Amount: " + child.GetComponent<UIResAmountEntry>().valueString);

                    GameObject tempButton = GameObject.Instantiate<GameObject>(
                        original: child.Find("icon").gameObject,
                        position: new Vector3(child.Find("icon").position.x-0.2f, child.position.y, child.position.z),
                        rotation: Quaternion.identity,
                        parent:   child);

                    tempButton.GetComponent<Image>().sprite = spriteDisabled;

                    UIButton uiButton = tempButton.GetComponent<UIButton>();
                    uiButton.tips.tipTitle = "Show nearest vein";
                    uiButton.tips.tipText = "Moves camera to the nearest vein.";

                    int tempRefId = child.GetComponent<UIResAmountEntry>().refId; // ID of resource
                    uiButton.onClick += (id) => { ToggleVeinHeighlight(tempRefId, ref tempButton); };
                    uiButton.onRightClick += (id) => { debugStuff(id); };
                    tempButton.name = "net-powana-show-nearest";

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
        private static void debugStuff(int action)
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

