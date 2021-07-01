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


namespace BetterGlobeMap
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class BetterGlobeMap : BaseUnityPlugin
    {
        public const string pluginGuid = "net.powana.plugins.DSP.BGM";
        public const string pluginName = "Better Globe Map";
        public const string pluginVersion = "1.0.2.0";

        static bool[] highlightEnabled = new bool[15];
        static bool[] buttonCreated = new bool[15]; //  Needed because the refIds of resources bug out, this ensures no duplicate buttons are created

        private static List<GameObject> createdObjects = new List<GameObject>();

        static AssetBundle bundle;
        static Sprite spriteShowNearest = null;
        static Sprite spriteHighlight = null;

        private static Color enabledButtonColor = new Color(83f, 202f, 252f, 0.5f); // Colour that is set on activated button
        private static Color defaultHighlightButtonColor;                           // Colour of highlight button
        private static Color highlightColor = new Color(1F, 0.22F, 0.11F, 0.95f);      // Colour of vein highlights todo make configurable
        private static Vector2 effectDistance = new Vector2(5, 5);  // Size of highlight outline
        
        private static RaycastHit hitInfo = new RaycastHit();
        private static bool hit = false;

        private static Vector3 dragBeginMousePosition;

        Harmony harmony;

        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            harmony = new Harmony(pluginGuid);
            // string pluginfolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // bundle = AssetBundle.LoadFromFile($"{pluginfolder}/net-powana-bgm-bundle");  // todo change back to: $"{pluginfolder}/net-powana-bgm-bundle", or a better string, check where the mod installer places the bundle file
            bundle = AssetBundle.LoadFromFile("G:/Games/Steam Games/steamapps/common/Dyson Sphere Program/BepInEx/scripts/net-powana-bgm-bundle");


            spriteShowNearest = bundle.LoadAsset<Sprite>("assets/ui/iconNearest.png");
            spriteHighlight = bundle.LoadAsset<Sprite>("assets/ui/iconHighlight.png");

            harmony.PatchAll(typeof(BetterGlobeMap));
            Debug.Log("Better Globe Map started!");
            
        }

        // This is the detail view to the right in globe view
        [HarmonyPostfix, HarmonyPatch(typeof(UIPlanetDetail), "_OnOpen")]
        public static void UIPlanetDetail_Postfix(UIPlanetDetail __instance)
        {

            // open starpmap on v check


            GameObject go = __instance.gameObject;

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

            Transform resGroup = go.transform.Find("res-group");
            foreach (Transform child in resGroup)
            {

                // Create buttons for resources that exist and are selectable, that don't already have buttons.
                if (child.gameObject.name.Contains("res-entry") && child.GetComponent<UIResAmountEntry>().refId != 0 && !buttonCreated[child.GetComponent<UIResAmountEntry>().refId] && child.GetComponent<UIResAmountEntry>().valueString.Trim() != "0")
                {

                    int tempRefId = (int) child.GetComponent<UIResAmountEntry>().refId; // ID of resource
                    Transform iconTransform = child.Find("icon"); 

                    GameObject toggleHighlightButton = GameObject.Instantiate<GameObject>(
                        original: iconTransform.gameObject,
                        position: new Vector3(child.position.x+1.25f, child.position.y-0.02f, child.position.z),
                        rotation: Quaternion.identity,
                        parent:   child.parent); // The child object (res-entry) actually moves each time the map is opened for some reason

                    GameObject showNearestVeinButton = GameObject.Instantiate<GameObject>(
                        original: iconTransform.gameObject,
                        position: new Vector3(child.position.x+1f, child.position.y-0.02f, child.position.z),
                        rotation: Quaternion.identity,
                        parent:   child.parent);

                    Image highlightImage = toggleHighlightButton.GetComponent<Image>();
                    highlightImage.sprite = spriteHighlight;
                    highlightImage.preserveAspect = true;
                    highlightImage.rectTransform.sizeDelta = new Vector2(28, 14);
                    defaultHighlightButtonColor = highlightImage.color; // Store the default color so we can switch back later

                    Image showNearestImage = showNearestVeinButton.GetComponent<Image>();
                    showNearestImage.sprite = spriteShowNearest;
                    showNearestImage.preserveAspect = true;
                    showNearestImage.rectTransform.sizeDelta = new Vector2(28, 14);

                    UIButton uiButton1 = toggleHighlightButton.GetComponent<UIButton>();

                    uiButton1.tips.tipTitle = "Highlight";
                    uiButton1.tips.tipText = "Highlight veins of the type: " + (EVeinType) tempRefId + ".";

                    UIButton uiButton2 = showNearestVeinButton.GetComponent<UIButton>();
                    uiButton2.tips.tipTitle = "Show nearest vein";
                    uiButton2.tips.tipText = "Move camera to the " + (EVeinType) tempRefId + " nearest the player";

                    uiButton1.onClick += (id) => { ToggleVeinHeighlight(tempRefId, ref toggleHighlightButton); };
                    // uiButton1.onRightClick += (id) => { DebugStuff(tempRefId); };
                    toggleHighlightButton.name = "net-powana-toggle-highlight";

                    uiButton2.onClick += (id) => { ShowNearestVein(tempRefId, ref showNearestVeinButton); };
                    showNearestVeinButton.name = "net-powana-show-nearest";

                    // todo, turn off highlights when exiting globemap?
                    buttonCreated[tempRefId] = true;

                    createdObjects.Add(toggleHighlightButton);
                    createdObjects.Add(showNearestVeinButton);
                    // Debug.Log("Created buttons for " + (EVeinType)child.GetComponent<UIResAmountEntry>().refId + " Amount: " + child.GetComponent<UIResAmountEntry>().valueString);
                }

            }

        }


        [HarmonyPrefix, HarmonyPatch(typeof(UIGlobemap), "FadeOut")]
        public static void UIPGlobemap_FadeOut_Prefix(UIGlobemap __instance)
        {
            Debug.Log("FADEOUT: " + createdObjects.ToString());
            foreach (GameObject go in createdObjects)
            {
                go.SetActive(false);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIGlobemap), "FadeIn")]
        public static void UIPGlobemap_FadeIn_Postfix(UIGlobemap __instance)
        {
            Debug.Log("FADEIN: " + createdObjects.ToString());

            foreach (GameObject go in createdObjects)
            {
                go.SetActive(true);
            }
        }


        private static void ShowNearestVein(int refId, ref GameObject button)
        {
            
            Debug.Log("\nShow nearest called with refId: " + refId.ToString());
            
            GameCamera gameCamera = GameCamera.instance;
            PlanetPoser planetPoser = gameCamera.planetPoser;
            Vector3 playerPos = GameMain.mainPlayer.position;

            // Get the veins that have a matching refId, using the planetdata of the planet linked to in the planetdetail pane
            PlanetData.VeinGroup[] veins = UIRoot.instance.uiGame.planetDetail.planet.veinGroups;
            
            // toRotate can be used with planetPoser to move to vein.pos, default to first vein
            Quaternion toRotate = Quaternion.FromToRotation(Vector3.up, veins[0].pos);

            // default to first
            float closestPosMag = (playerPos.normalized - veins[0].pos).magnitude;

            Quaternion tempRotate;

            foreach (PlanetData.VeinGroup vein in veins)
            {
                if (vein.type == (EVeinType) refId) {
                    // Quat to rotate the planet towards
                    tempRotate = Quaternion.FromToRotation(Vector3.up, vein.pos);

                    if ((playerPos.normalized - vein.pos).magnitude < closestPosMag )
                    {
                        closestPosMag = (playerPos.normalized - vein.pos).magnitude;
                        toRotate = tempRotate;
                    }
                }
            }

            // wip, todo make it not rotate only north, keep current rotation
            Vector3 vector3 = toRotate * Vector3.up;
            Vector3 up = Vector3.up;
            Vector3 normalized = Vector3.Cross(vector3, up).normalized;
            toRotate = Quaternion.LookRotation(Vector3.Cross(normalized, vector3), vector3);

            planetPoser.rotationWanted = toRotate;
            planetPoser.distWanted = Mathf.Max(planetPoser.dist, planetPoser.distMax*0.2f); // Maybe this should just be planetPoser.dist

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
            

            highlightEnabled[refId] = !highlightEnabled[refId];
            
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

                        Outline ol = uiVeinDetailNode.GetComponent<Outline>();
                        if (ol == null)  // Todo make outline prettier
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

            Image img = button.GetComponent<Image>();
            img.color = highlightEnabled[refId] ? enabledButtonColor : defaultHighlightButtonColor;
        }

        // Every GameTick, check if player is using the move button, todo: It would be simpled to find whatever disables movement and alter that instead of altering GameTick
        [HarmonyPostfix, HarmonyPatch(typeof(PlayerController), "GameTick")]
        private static void PlayerControllerGameTick_Postfix(PlayerController __instance, long time)
        {
            if (!UIRoot.instance.uiGame.globemap.active) return;  // Only modify behaviour when the globe map is open
            bool moveCameraConflict = VFInput.rtsMoveCameraConflict;
            bool mineCameraConflict = VFInput.rtsMineCameraConflict;

            if (VFInput._rtsMove.onDown)
            {
                dragBeginMousePosition = Input.mousePosition;
                Debug.Log(GameMain.gameTick.ToString());
                hit = Physics.Raycast(Camera.main.ScreenPointToRay(dragBeginMousePosition), out hitInfo, 800f, 8720, QueryTriggerInteraction.Collide);
            }

            
            // Check if the player moved the mouse a significant distance indicating they want to drag the camera, not move the character.
            else if (VFInput._rtsMove.onUp && ((double)(dragBeginMousePosition - Input.mousePosition).sqrMagnitude < 800.0))
            {
                if (hit)
                {
                    GameMain.data.mainPlayer.Order(OrderNode.MoveTo(hitInfo.point), (bool)VFInput._multiOrdering);
                    RTSTargetGizmo.Create(hitInfo.point);
                    hit = false;
                }
                
            }
        }


        void OnDestroy()
        {
            AssetBundle.UnloadAllAssetBundles(true);
            Debug.Log("Destroying self ;)");
            harmony.UnpatchSelf();
        }


        // Debug methods below
        private static void DebugStuff(int action)
        {
            Debug.Log("Debugstuff called: " + action.ToString());
        }

        private static void AddRedOutline(ref GameObject gameObject)
        {
            Outline ol = gameObject.AddComponent(typeof(Outline)) as Outline;
            ol.effectColor = Color.red;
            ol.effectDistance = new Vector2(3, 3);
            ol.useGraphicAlpha = true;
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

