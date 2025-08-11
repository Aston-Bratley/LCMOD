using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BIG_DADDY_MOD.Patches
{
    [HarmonyPatch]
    public class CameraEffectSystem
    {
        public static CameraEffectController? localPlayerEffects;
        private static bool effectApplied = false;
        
        public class CameraEffectController
        {
            private Volume? postProcessVolume;
            private ChromaticAberration? chromaticAberration;
            private FilmGrain? filmGrain;
            private ColorAdjustments? colorAdjustments;
            private Vignette? vignette;
            private LensDistortion? lensDistortion;
            private Bloom? bloom;
            private DepthOfField? depthOfField;
            private MotionBlur? motionBlur;
            private WhiteBalance? whiteBalance;
            private ChannelMixer? channelMixer;
            private SplitToning? splitToning;
            private float animationTime = 0f;
            private Vector3 originalCameraPosition;
            private Vector3 originalCameraRotation;
            private float originalFOV;
            private readonly Dictionary<string, CameraEffect> registeredEffects;
            private CameraEffect? currentEffect;
            private readonly PlayerControllerB player;

            public CameraEffectController(PlayerControllerB controller)
            {
                player = controller;
                registeredEffects = new Dictionary<string, CameraEffect>();
                if (player.gameplayCamera != null) {
                    originalCameraPosition = player.gameplayCamera.transform.localPosition;
                    originalCameraRotation = player.gameplayCamera.transform.localEulerAngles;
                    originalFOV = player.gameplayCamera.fieldOfView;
                }
                
                InitializePostProcessingEffects();
                Debug.Log("Camera Effect System initialized");
            }

            private void InitializePostProcessingEffects() {
                postProcessVolume = null;
                if (HUDManager.Instance?.playerGraphicsVolume != null) postProcessVolume = HUDManager.Instance.playerGraphicsVolume;
                if (postProcessVolume == null) postProcessVolume = GameObject.FindObjectOfType<Volume>();
                if (postProcessVolume == null && player.gameplayCamera != null) {
                    var volumes = player.gameplayCamera.GetComponentsInChildren<Volume>();
                    if (volumes.Length > 0) postProcessVolume = volumes[0];
                }

                if (postProcessVolume?.profile != null) {
                    // Initialize all post-processing effects
                    chromaticAberration = GetOrAddEffect<ChromaticAberration>();
                    filmGrain = GetOrAddEffect<FilmGrain>();
                    colorAdjustments = GetOrAddEffect<ColorAdjustments>();
                    vignette = GetOrAddEffect<Vignette>();
                    lensDistortion = GetOrAddEffect<LensDistortion>();
                    bloom = GetOrAddEffect<Bloom>();
                    depthOfField = GetOrAddEffect<DepthOfField>();
                    motionBlur = GetOrAddEffect<MotionBlur>();
                    whiteBalance = GetOrAddEffect<WhiteBalance>();
                    channelMixer = GetOrAddEffect<ChannelMixer>();
                    splitToning = GetOrAddEffect<SplitToning>();

                    Debug.Log("Post-processing effects initialized successfully");
                }
            }

            private T GetOrAddEffect<T>() where T : VolumeComponent {
                if (!postProcessVolume.profile.TryGet(out T effect)) effect = postProcessVolume.profile.Add<T>();
                effect.active = true;
                return effect;
            }

            public void RegisterEffect(string name, CameraEffect effect) {
                registeredEffects[name] = effect;
            }

            public void ApplyEffect(string effectName) {
                if (registeredEffects.TryGetValue(effectName, out CameraEffect effect)) {
                    ResetAllEffects();
                    currentEffect = effect;
                    effect.Apply(this);
                    Debug.Log($"Applied camera effect: {effectName}");
                }
            }

            public void Update() {
                animationTime += Time.deltaTime;
                currentEffect?.Update(this, animationTime);
            }

            public void ResetAllEffects() {
                chromaticAberration?.intensity.Override(0f);
                chromaticAberration?.active.Override(false);
                filmGrain?.intensity.Override(0f);
                filmGrain?.active.Override(false);
                colorAdjustments?.saturation.Override(0f);
                colorAdjustments?.contrast.Override(0f);
                colorAdjustments?.postExposure.Override(0f);
                colorAdjustments?.active.Override(false);
                vignette?.intensity.Override(0f);
                vignette?.active.Override(false);
                lensDistortion?.intensity.Override(0f);
                lensDistortion?.active.Override(false);
                bloom?.intensity.Override(0f);
                bloom?.threshold.Override(1f);
                bloom?.active.Override(false);
                depthOfField?.focusDistance.Override(10f);
                depthOfField?.active.Override(false);
                motionBlur?.intensity.Override(0f);
                motionBlur?.active.Override(false);
                whiteBalance?.temperature.Override(0f);
                whiteBalance?.tint.Override(0f);
                whiteBalance?.active.Override(false);
                channelMixer?.redOutRedIn.Override(100f);
                channelMixer?.redOutGreenIn.Override(0f);
                channelMixer?.redOutBlueIn.Override(0f);
                channelMixer?.greenOutRedIn.Override(0f);
                channelMixer?.greenOutGreenIn.Override(100f);
                channelMixer?.greenOutBlueIn.Override(0f);
                channelMixer?.blueOutRedIn.Override(0f);
                channelMixer?.blueOutGreenIn.Override(0f);
                channelMixer?.blueOutBlueIn.Override(100f);
                channelMixer?.active.Override(false);
                splitToning?.highlights.Override(Color.white);
                splitToning?.shadows.Override(Color.white);
                splitToning?.balance.Override(0f);
                splitToning?.active.Override(false);
                if (player.gameplayCamera != null) {
                    player.gameplayCamera.transform.localPosition = originalCameraPosition;
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation;
                    player.gameplayCamera.fieldOfView = originalFOV;
                }

                currentEffect = null;
            }

            public void Reset()
            {
                ResetAllEffects();
                effectApplied = false;
                animationTime = 0f;
                Debug.Log("All camera effects reset");
            }
          
            public ChromaticAberration? ChromaticAberration => chromaticAberration;
            public FilmGrain? FilmGrain => filmGrain;
            public ColorAdjustments? ColorAdjustments => colorAdjustments;
            public Vignette? Vignette => vignette;
            public LensDistortion? LensDistortion => lensDistortion;
            public Bloom? Bloom => bloom;
            public DepthOfField? DepthOfField => depthOfField;
            public MotionBlur? MotionBlur => motionBlur;
            public WhiteBalance? WhiteBalance => whiteBalance;
            public ChannelMixer? ChannelMixer => channelMixer;
            public SplitToning? SplitToning => splitToning;
            public PlayerControllerB Player => player;
            public Vector3 OriginalCameraPosition => originalCameraPosition;
            public Vector3 OriginalCameraRotation => originalCameraRotation;
            public float OriginalFOV => originalFOV;
        }

      
        //Harmony Patches
        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        private static void LateUpdatePatch(PlayerControllerB __instance) {
            if (__instance != GameNetworkManager.Instance.localPlayerController || !__instance.isPlayerControlled || __instance.isPlayerDead) return;
            localPlayerEffects ??= new CameraEffectController(__instance);
            localPlayerEffects.Update();
        }

        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        private static void OnReturnToLobby() {
              if (localPlayerEffects != null) localPlayerEffects.Reset();
              effectApplied = false;
        }

        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void Cleanup() {
            if (localPlayerEffects != null) {
                localPlayerEffects.Reset();
                localPlayerEffects = null;
            }
            effectApplied = false;
        }
    }
}
