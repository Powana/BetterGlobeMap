using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace DSPMod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class DSPMod : BaseUnityPlugin
    {
        public const string pluginGuid = "com.benwooldridge.plugins.DSPMod";
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
            resNames[1] = "";
            Debug.Log("Awake now!");
            
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
            // .entries contains the values for the resources listed. todo: update comments
            Debug.Log("UIPlanetDetail opened.");
            /**
             * Structure of gameobjects is (from trial and error and a lot of Debug.Log):
             * planet-detail-ui:
             *  line
             *  res-entry [Contains the UIResAmountEntry component]:
             *   icon
             *   value-text
             *  res-entry(clone):
             *   icon
             *   value-text
             * 
             */
            Transform[] resGroup = go.transform.Find("res-group").GetComponentsInChildren<Transform>();
            foreach (Transform child in resGroup)
            {
                Debug.Log(child.gameObject.ToString() + ", has parent: " + child.parent.ToString());
                if (child.gameObject.name.Contains("res-entry"))
                {
                    foreach (var component in child.gameObject.GetComponents(typeof(Component))) {
                        // Debug.Log(component.name + " | " + component.ToString());
                    }
                    Debug.Log("");
                }
                
            }
                // VeinData[] planetVeins = __instance.planet.data.veinPool;
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
                // Debug.Log(planetVeins[res.refId])
                Debug.Log((EVeinType) res.refId + ": " + res.valueString);
                // if (res.sb != null) Debug.Log(res.sb.ToString());
            }
        }
    }
}

