using BepInEx;
using BepInEx.Configuration;
using GorillaNetworking;
using Gorillapad.Animators;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Utilla;
using Utilla.Attributes;
using Gorillapad.Animators;
using Gorillapad.Menus;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad
{
    [ModdedGamemode]
    [BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static bool IsSteam { get; protected set; }
        private static bool _enabled = true;
        public AnimatorBase 
            walkAnimator,
            flyAnimator, 
            emoteAnimator, 
            handAnimator,
            grabAnimator,
            swimAnimator,
            ziplineAnimator,
            ropeAnimator,
            gliderAnimator,
            broomAnimator,
            hoverboardAnimator;
        public static ConfigEntry<bool> ShowControls;

        public bool IsWalking => Rig.Instance && Rig.Instance.Animator && Rig.Instance.Animator == walkAnimator;
        public ComputerGUI computerGUI;
        public ControlsOverlay controlsOverlay;
        public AssetBundle bundle;
        public RadialMenu radialMenu;

        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            protected set
            {
                _enabled = value;
                if (value)
                {
                    this.gameObject.GetOrAddComponent<InputHandler>();
                    this.gameObject.GetOrAddComponent<Rig>();
                    walkAnimator = this.gameObject.GetOrAddComponent<WalkAnimator>();
                    flyAnimator = this.gameObject.GetOrAddComponent<FlyAnimator>();
                    emoteAnimator = this.gameObject.GetOrAddComponent<EmoteAnimator>();
                    handAnimator = this.gameObject.GetOrAddComponent<PoseAnimator>();
                    grabAnimator = this.gameObject.GetOrAddComponent<InteractAnimator>();
                    swimAnimator = this.gameObject.GetOrAddComponent<SwimAnimator>();
                    ziplineAnimator = this.gameObject.GetOrAddComponent<ZiplineAnimator>();
                    ropeAnimator = this.gameObject.GetOrAddComponent<RopeAnimator>();
                    gliderAnimator = this.gameObject.GetOrAddComponent<GliderAnimator>();
                    broomAnimator = this.gameObject.GetOrAddComponent<BroomAnimator>();
                    hoverboardAnimator = this.gameObject.GetOrAddComponent<HoverboardAnimator>();
                    if (!radialMenu)
                    {
                        radialMenu = Instantiate(bundle.LoadAsset<GameObject>("Radial Menu")).AddComponent<RadialMenu>();
                    }
                    computerGUI = this.gameObject.GetOrAddComponent<ComputerGUI>();
                    controlsOverlay = this.gameObject.GetOrAddComponent<ControlsOverlay>();
                    // This runs again on every modded room join/leave, so leave the animator
                    // that's in use running (its Update handles things like jumping)
                    foreach (var animator in new[] { walkAnimator, flyAnimator, emoteAnimator, handAnimator, grabAnimator, swimAnimator, ziplineAnimator, ropeAnimator, gliderAnimator, broomAnimator, hoverboardAnimator })
                        animator.enabled = animator == Rig.Instance.Animator;
                    StartCoroutine(SelectDefaultAnimator());
                }
                else
                {
                    InputHandler.Instance?.Obliterate();
                    Rig.Instance?.Obliterate();
                    walkAnimator?.Obliterate();
                    flyAnimator?.Obliterate();
                    emoteAnimator?.Obliterate();
                    handAnimator?.Obliterate();
                    grabAnimator?.Obliterate();
                    swimAnimator?.Obliterate();
                    ziplineAnimator?.Obliterate();
                    ropeAnimator?.Obliterate();
                    gliderAnimator?.Obliterate();
                    broomAnimator?.Obliterate();
                    hoverboardAnimator?.Obliterate();
                    computerGUI?.Obliterate();
                    controlsOverlay?.Obliterate();
                }
            }
        }

        // Without a headset, start out walking so you can look and move right away. With a headset,
        // start in VR mode; V (or the Tab menu) switches to desktop controls.
        // Wait a couple frames so the radial menu has been hidden (hiding it applies its own selection).
        IEnumerator SelectDefaultAnimator()
        {
            yield return null;
            yield return null;
            if (!Enabled || !Rig.Instance || Rig.Instance.Animator) yield break;
            if (VRHeadsetActive)
                Logging.Info("VR headset active: starting in VR mode (press V or Tab for desktop controls)");
            else
                Rig.Instance.Animator = walkAnimator;
        }

        public static bool VRHeadsetActive
        {
            get
            {
                try
                {
                    return UnityEngine.XR.XRSettings.isDeviceActive
                        || SteamVRCompat.Running;
                }
                catch { return false; }
            }
        }

        // V: switch between VR (the mod stays out of the way) and desktop controls
        public void ToggleVRMode()
        {
            if (!Rig.Instance) return;
            bool toVR = Rig.Instance.Animator;
            Rig.Instance.Animator = toVR ? null : walkAnimator;
            Logging.Info(toVR ? "Switched to VR mode" : "Switched to desktop controls");
        }

        void Awake()
        {
            Instance = this;
            Logging.Init();
            ShowControls = Config.Bind("Interface", "Show Controls", true,
                "Show the controls for the current mode in the corner of the screen. Toggle in game with H.");

            // open the file at BepInEx/config/BepInEx.cfg and replace the value of the setting
            // HideManagerGameObject with "true" using regex. 
            try
            {
                string configPath = Paths.ConfigPath + "/BepInEx.cfg";
                string configText = System.IO.File.ReadAllText(configPath);
                configText = System.Text.RegularExpressions.Regex.Replace(configText, @"HideManagerGameObject = .+", "HideManagerGameObject = true");
                System.IO.File.WriteAllText(configPath, configText);
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }

            bundle = Tools.AssetUtils.LoadAssetBundle("Gorillapad/assetbundle");            
        }

        void Start()
        {
            Utilla.Events.GameInitialized += OnGameInitialized;
            Utilla.Events.RoomJoined += OnRoomJoined;
            Utilla.Events.RoomLeft += OnRoomLeft;
        }

        [ModdedGamemodeLeave]
        void ModdedRoomLeft(string _) => NetworkSystem.Instance.ReturnToSinglePlayer();

        private void OnRoomLeft(object sender, Events.RoomJoinedArgs e)
        {
            Enabled = true;
        }

        void OnRoomJoined(object sender, Events.RoomJoinedArgs args)
        {
            string gamemode = args.Gamemode;
            Enabled = gamemode.ToUpper().Contains("MODDED");
        }

        void OnEnable()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        void OnDisable()
        {
            HarmonyPatches.RemoveHarmonyPatches();
        }

        void OnGameInitialized(object sender, EventArgs e)
        {
            string platform = PlayFabAuthenticator.instance.platform?.ToString() ?? "";
            IsSteam = platform.ToLower().Contains("steam");
            Logging.Info("Game initialized; platform:", platform, "SteamVR state:", SteamVRCompat.State);
            Enabled = true;
        }

        bool setGamemode = false;
        float nextGamemodeAttempt;
        Type selectorType;
        MethodInfo nextPage;
        void FixedUpdate()
        {
            TryInitializeGamemode();
        }

        public void TryInitializeGamemode()
        {
            if (setGamemode || Time.time < nextGamemodeAttempt) return;
            nextGamemodeAttempt = Time.time + 1f;
            try
            {
                // Utilla's selector is internal, so go through reflection (looked up once; it's slow)
                if (selectorType == null)
                {
                    selectorType = AccessTools.TypeByName("Utilla.Behaviours.UtillaGamemodeSelector");
                    if (selectorType == null)
                    {
                        Logging.Warning("Utilla gamemode selector not found; not auto-selecting modded casual");
                        setGamemode = true;
                        return;
                    }
                    nextPage = AccessTools.Method(selectorType, "NextPage");
                }
                var selector = FindAnyObjectByType(selectorType);
                if (!selector) return;
                nextPage.Invoke(selector, null);
                foreach (var btn in FindObjectsByType<ModeSelectButton>(FindObjectsSortMode.None))
                {
                    if (btn.gameMode.ToUpper().Contains("MODDED_CASUAL"))
                    {
                        btn.ButtonActivationWithHand(false);
                    }
                }
                setGamemode = true;
            }
            catch { }
        }

        public Dictionary<string, float> sliders = new Dictionary<string, float>();
        public Dictionary<string, string> labels = new Dictionary<string, string>();
        // fucked this up so it won't run
        void OnGUI()
        {
            float width = 400;
            float height = 40;
            float x = Screen.width - width - 10;
            float y = 10;
            
            GUI.skin.label.fontSize = 20;
            foreach (string key in new List<string>(labels.Keys))
            {
                y += height;
                GUI.Label(new Rect(x, y, width, height), key + ": " + labels[key]);
            }
            foreach (string key in new List<string>(sliders.Keys))
            {
                y += height;
                sliders[key] = GUI.HorizontalSlider(new Rect(x, y, width, height), sliders[key], 0.0f, 10.0f);
                GUI.Label(new Rect(x - width, y, width, height), key + ": " + sliders[key]);
            }
        }
    }
}
