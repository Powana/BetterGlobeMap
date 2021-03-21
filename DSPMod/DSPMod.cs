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

        Dictionary<int, string> resNames = new Dictionary<int, string> {
            {0, "None"},
            {1, "Iron Ore"},
            {2, "Copper Ore"},
            {3, "Silicium Ore"},
            {4, "Titanium Ore"},
            {5, "Stone"},
            {6, "Coal"},
            {7, "Oil"},
            {8, "Fireice"},
            {9, "Diamond"},
            {10, "Fractal"},
            {11, "Crysrub"},
            {12, "Grat"},
            {13, "Bamboo"},
            {14, "Mag"},
            {15, "Max"},

        };

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
            // UIRoot.instance.uiGame.planetDetail.transform.GetChild(8).GetComponent;

            Transform[] resGroup = go.transform.Find("res-group").GetComponentsInChildren<Transform>();
            Sprite tempSprite = resGroup[2].Find("icon").GetComponent<Image>().sprite;
            foreach (Transform child in resGroup)
            {
                // Debug.Log(child.gameObject.ToString() + ", has parent: " + child.parent.ToString());
                if (child.gameObject.name.Contains("res-entry") && !child.Find("net-powana-show-nearest"))
                {
                    GameObject tempButton = GameObject.Instantiate<GameObject>(
                        original: child.Find("icon").gameObject,
                        position: new Vector3(child.position.x+0.05f, child.position.y, child.position.z),
                        rotation: Quaternion.identity,
                        parent:   child);

                    tempButton.GetComponent<Image>().sprite = tempSprite;

                    UIButton uiButton = tempButton.GetComponent<UIButton>();
                    uiButton.tips.tipTitle = "Show nearest vein";
                    uiButton.tips.tipText = "Moves camera to the nearest vein.";

                    int tempRefId = child.GetComponent<UIResAmountEntry>().refId; // ID of resource
                    uiButton.onClick += (id) => { HighlightVeins(tempRefId); };
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

        private static void HighlightVeins(int refId)
        {
            Debug.Log("HeighlightVeins called with refId:" + refId.ToString());
         

            // This is the group of gameobjects that contain the following components:
            // RectTransform, CanvasRenderer, UI.Image, UIVeinDetail
            Transform[] veinMarks = GameObject.Find("UI Root/Overlay Canvas/In Game/Scene UIs/Vein Marks/").GetComponentsInChildren<Transform>();
            Vector2 effectDistance = new Vector2(5, 5);

            // Loop through all nodes on planet (all planets?)
            foreach (Transform veinTip in veinMarks)
            {
                
                UIVeinDetail veinDetail = veinTip.GetComponent<UIVeinDetail>();
                if (veinDetail == null) continue;

                foreach (UIVeinDetailNode UIVeinDetailNode in veinDetail.allTips)
                {
                    // Check if the node is of the type we selected, todo: Check if it is also on the right planet.
                    if ((veinDetail.inspectPlanet != null) && (veinDetail.inspectPlanet.veinGroups[UIVeinDetailNode.veinGroupIndex].type == (EVeinType)refId))
                    {
                        PlanetData.VeinGroup matchingVein = veinDetail.inspectPlanet.veinGroups[UIVeinDetailNode.veinGroupIndex];
                        Debug.Log("Matching veinGroup found: " + matchingVein.type + " " + matchingVein.amount.ToString() + " at " + matchingVein.pos + " on " + veinDetail.inspectPlanet.name + ". Nodes: " + matchingVein.count.ToString());
                        
                        Outline ol = UIVeinDetailNode.gameObject.AddComponent(typeof(Outline)) as Outline;
                        ol.effectColor = Color.red;
                        ol.effectDistance = effectDistance;
                        ol.useGraphicAlpha = true;
                    }
                }
               
            }
        }

        // Debug methods below

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

