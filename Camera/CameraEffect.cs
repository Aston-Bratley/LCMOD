using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BIG_DADDY_MOD.Patches
{
    [HarmonyPatch]
    public class CameraEffectDebugSystem
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

            // Animation state variables
            private float animationTime = 0f;
            private Vector3 originalCameraPosition;
            private Vector3 originalCameraRotation;
            private float originalFOV;
            private bool isAnimatedEffect = false;

            private readonly List<Action> availableEffects;
            private int currentEffectIndex = -1;
            private readonly PlayerControllerB player;

            public CameraEffectController(PlayerControllerB controller)
            {
                player = controller;
                
                // Store original camera values
                if (player.gameplayCamera != null)
                {
                    originalCameraPosition = player.gameplayCamera.transform.localPosition;
                    originalCameraRotation = player.gameplayCamera.transform.localEulerAngles;
                    originalFOV = player.gameplayCamera.fieldOfView;
                }
                
                InitializePostProcessingEffects();
                
                availableEffects = new List<Action>
                {
                    // Static effects
                    ApplyChromaticAberrationEffect,
                    ApplyFilmGrainEffect,
                    ApplyDesaturatedEffect,
                    ApplyVignetteEffect,
                    ApplyLensDistortionEffect,
                    ApplyBloomEffect,
                    ApplyDepthOfFieldEffect,
                    ApplyMotionBlurEffect,
                    ApplyWarmEffect,
                    ApplyColdEffect,
                    ApplyHighContrastEffect,
                    ApplyVintageEffect,
                    ApplyNightVisionEffect,
                    ApplySepiaToneEffect,
                    ApplyPsychedelicEffect,
                    ApplyHorrorEffect,
                    ApplyCinematicEffect,
                    ApplyDreamEffect,
                    ApplyGlitchEffect,
                    ApplyThermalEffect,
                    
                    // New animated effects
                    ApplyWavyCameraEffect,
                    ApplyBobbingCameraEffect,
                    ApplyShakyCameraEffect,
                    ApplyRotatingCameraEffect,
                    ApplyPulsingFOVEffect,
                    ApplySwayingCameraEffect,
                    ApplyFishEyeAnimatedEffect,
                    ApplyDrunkCameraEffect,
                    ApplyEarthquakeEffect,
                    ApplyFloatingCameraEffect,
                    ApplySpinningEffect,
                    ApplyBreathingEffect,
                    ApplyGlitchyMovementEffect,
                    ApplyWarpedRealityEffect,
                    ApplyTremorEffect
                };

                Debug.Log($"Camera Effect Debug System initialized with {availableEffects.Count} effects");
            }

            private void InitializePostProcessingEffects()
            {
                try
                {
                    // Use the exact same initialization pattern as original patches.cs
                    postProcessVolume = null;

                    // Method 1: Try to get from HUDManager
                    if (HUDManager.Instance?.playerGraphicsVolume != null)
                    {
                        postProcessVolume = HUDManager.Instance.playerGraphicsVolume;
                    }

                    // Method 2: Try to find in the scene
                    if (postProcessVolume == null)
                    {
                        postProcessVolume = GameObject.FindObjectOfType<Volume>();
                    }

                    // Method 3: Try to find by camera
                    if (postProcessVolume == null && player.gameplayCamera != null)
                    {
                        var volumes = player.gameplayCamera.GetComponentsInChildren<Volume>();
                        if (volumes.Length > 0)
                        {
                            postProcessVolume = volumes[0];
                        }
                    }

                    if (postProcessVolume?.profile != null)
                    {
                        // Get existing effects or add them - same pattern as original
                        if (!postProcessVolume.profile.TryGet(out chromaticAberration))
                        {
                            chromaticAberration = postProcessVolume.profile.Add<ChromaticAberration>();
                        }
                        chromaticAberration.active = true;

                        if (!postProcessVolume.profile.TryGet(out filmGrain))
                        {
                            filmGrain = postProcessVolume.profile.Add<FilmGrain>();
                        }
                        filmGrain.active = true;

                        if (!postProcessVolume.profile.TryGet(out colorAdjustments))
                        {
                            colorAdjustments = postProcessVolume.profile.Add<ColorAdjustments>();
                        }
                        colorAdjustments.active = true;

                        if (!postProcessVolume.profile.TryGet(out vignette))
                        {
                            vignette = postProcessVolume.profile.Add<Vignette>();
                        }
                        vignette.active = true;

                        // Add additional effects not in original
                        if (!postProcessVolume.profile.TryGet(out lensDistortion))
                        {
                            lensDistortion = postProcessVolume.profile.Add<LensDistortion>();
                        }
                        lensDistortion.active = true;

                        if (!postProcessVolume.profile.TryGet(out bloom))
                        {
                            bloom = postProcessVolume.profile.Add<Bloom>();
                        }
                        bloom.active = true;

                        if (!postProcessVolume.profile.TryGet(out depthOfField))
                        {
                            depthOfField = postProcessVolume.profile.Add<DepthOfField>();
                        }
                        depthOfField.active = true;

                        if (!postProcessVolume.profile.TryGet(out motionBlur))
                        {
                            motionBlur = postProcessVolume.profile.Add<MotionBlur>();
                        }
                        motionBlur.active = true;

                        if (!postProcessVolume.profile.TryGet(out whiteBalance))
                        {
                            whiteBalance = postProcessVolume.profile.Add<WhiteBalance>();
                        }
                        whiteBalance.active = true;

                        if (!postProcessVolume.profile.TryGet(out channelMixer))
                        {
                            channelMixer = postProcessVolume.profile.Add<ChannelMixer>();
                        }
                        channelMixer.active = true;

                        if (!postProcessVolume.profile.TryGet(out splitToning))
                        {
                            splitToning = postProcessVolume.profile.Add<SplitToning>();
                        }
                        splitToning.active = true;

                        Debug.Log("Post-processing effects initialized successfully");
                    }
                    else
                    {
                        Debug.LogWarning("Could not find post-processing volume or profile");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize post-processing effects: {ex.Message}");
                }
            }

            // This method is called continuously from Update, just like the original deterioration effects
            public void Update()
            {
                try
                {
                    animationTime += Time.deltaTime;

                    // Apply random effect if not yet applied (similar to how deterioration effects work)
                    if (!effectApplied && postProcessVolume?.profile != null)
                    {
                        ApplyRandomEffect();
                        effectApplied = true;
                    }

                    // Keep the effects active (similar to UpdateVisualsAndEffects in original)
                    MaintainCurrentEffect();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in camera effect update: {ex.Message}");
                }
            }

            private void MaintainCurrentEffect()
            {
                // This prevents the effects from being reset by the game
                // Similar to how the original patches continuously apply effects in UpdateVisualsAndEffects
                if (currentEffectIndex >= 0 && currentEffectIndex < availableEffects.Count)
                {
                    try
                    {
                        // Re-apply current effect to maintain it
                        availableEffects[currentEffectIndex].Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error maintaining camera effect: {ex.Message}");
                    }
                }
            }

            public void ApplyRandomEffect()
            {
                if (availableEffects.Count == 0) return;

                ResetAllEffects();

                int newIndex = rnd.Next(availableEffects.Count);
                currentEffectIndex = newIndex;

                // Check if this is an animated effect
                isAnimatedEffect = newIndex >= 20; // First 20 are static effects

                try
                {
                    availableEffects[currentEffectIndex].Invoke();
                    Debug.Log($"Applied camera effect #{currentEffectIndex + 1}: {GetEffectName(currentEffectIndex)}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to apply camera effect #{currentEffectIndex + 1}: {ex.Message}");
                }
            }

            private string GetEffectName(int index)
            {
                string[] effectNames = {
                    "Chromatic Aberration", "Film Grain", "Desaturated", "Vignette", "Lens Distortion",
                    "Bloom", "Depth of Field", "Motion Blur", "Warm Tone", "Cold Tone",
                    "High Contrast", "Vintage", "Night Vision", "Sepia Tone", "Psychedelic",
                    "Horror", "Cinematic", "Dream", "Glitch", "Thermal",
                    "Wavy Camera", "Bobbing Camera", "Shaky Camera", "Rotating Camera", "Pulsing FOV",
                    "Swaying Camera", "Animated Fish Eye", "Drunk Camera", "Earthquake", "Floating Camera",
                    "Spinning Effect", "Breathing Effect", "Glitchy Movement", "Warped Reality", "Tremor Effect"
                };
                return index < effectNames.Length ? effectNames[index] : "Unknown";
            }

            private void ResetAllEffects()
            {
                try
                {
                    // Reset post-processing effects
                    if (chromaticAberration != null)
                    {
                        chromaticAberration.intensity.Override(0f);
                        chromaticAberration.active = false;
                    }

                    if (filmGrain != null)
                    {
                        filmGrain.intensity.Override(0f);
                        filmGrain.active = false;
                    }

                    if (colorAdjustments != null)
                    {
                        colorAdjustments.saturation.Override(0f);
                        colorAdjustments.contrast.Override(0f);
                        colorAdjustments.postExposure.Override(0f);
                        colorAdjustments.active = false;
                    }

                    if (vignette != null)
                    {
                        vignette.intensity.Override(0f);
                        vignette.active = false;
                    }

                    if (lensDistortion != null)
                    {
                        lensDistortion.intensity.Override(0f);
                        lensDistortion.active = false;
                    }

                    if (bloom != null)
                    {
                        bloom.intensity.Override(0f);
                        bloom.threshold.Override(1f);
                        bloom.active = false;
                    }

                    if (depthOfField != null)
                    {
                        depthOfField.focusDistance.Override(10f);
                        depthOfField.active = false;
                    }

                    if (motionBlur != null)
                    {
                        motionBlur.intensity.Override(0f);
                        motionBlur.active = false;
                    }

                    if (whiteBalance != null)
                    {
                        whiteBalance.temperature.Override(0f);
                        whiteBalance.tint.Override(0f);
                        whiteBalance.active = false;
                    }

                    if (channelMixer != null)
                    {
                        channelMixer.redOutRedIn.Override(100f);
                        channelMixer.redOutGreenIn.Override(0f);
                        channelMixer.redOutBlueIn.Override(0f);
                        channelMixer.greenOutRedIn.Override(0f);
                        channelMixer.greenOutGreenIn.Override(100f);
                        channelMixer.greenOutBlueIn.Override(0f);
                        channelMixer.blueOutRedIn.Override(0f);
                        channelMixer.blueOutGreenIn.Override(0f);
                        channelMixer.blueOutBlueIn.Override(100f);
                        channelMixer.active = false;
                    }

                    if (splitToning != null)
                    {
                        splitToning.highlights.Override(Color.white);
                        splitToning.shadows.Override(Color.white);
                        splitToning.balance.Override(0f);
                        splitToning.active = false;
                    }

                    // Reset camera position and rotation to original
                    if (player.gameplayCamera != null)
                    {
                        player.gameplayCamera.transform.localPosition = originalCameraPosition;
                        player.gameplayCamera.transform.localEulerAngles = originalCameraRotation;
                        player.gameplayCamera.fieldOfView = originalFOV;
                    }

                    isAnimatedEffect = false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to reset effects: {ex.Message}");
                }
            }

            #region Static Effects (Original)
            
            private void ApplyChromaticAberrationEffect()
            {
                if (chromaticAberration != null)
                {
                    chromaticAberration.intensity.Override(0.8f);
                    chromaticAberration.active = true;
                }
            }

            private void ApplyFilmGrainEffect()
            {
                if (filmGrain != null)
                {
                    filmGrain.intensity.Override(0.6f);
                    filmGrain.response.Override(0.8f);
                    filmGrain.active = true;
                }
            }

            private void ApplyDesaturatedEffect()
            {
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(-60f);
                    colorAdjustments.active = true;
                }
            }

            private void ApplyVignetteEffect()
            {
                if (vignette != null)
                {
                    vignette.intensity.Override(0.5f);
                    vignette.smoothness.Override(0.4f);
                    vignette.color.Override(Color.black);
                    vignette.active = true;
                }
            }

            private void ApplyLensDistortionEffect()
            {
                if (lensDistortion != null)
                {
                    lensDistortion.intensity.Override(0.3f);
                    lensDistortion.active = true;
                }
            }

            private void ApplyBloomEffect()
            {
                if (bloom != null)
                {
                    bloom.intensity.Override(2f);
                    bloom.threshold.Override(0.8f);
                    bloom.active = true;
                }
            }

            private void ApplyDepthOfFieldEffect()
            {
                if (depthOfField != null)
                {
                    depthOfField.focusDistance.Override(3f);
                    depthOfField.active = true;
                }
            }

            private void ApplyMotionBlurEffect()
            {
                if (motionBlur != null)
                {
                    motionBlur.intensity.Override(0.5f);
                    motionBlur.active = true;
                }
            }

            private void ApplyWarmEffect()
            {
                if (whiteBalance != null)
                {
                    whiteBalance.temperature.Override(20f);
                    whiteBalance.tint.Override(5f);
                    whiteBalance.active = true;
                }
            }

            private void ApplyColdEffect()
            {
                if (whiteBalance != null)
                {
                    whiteBalance.temperature.Override(-20f);
                    whiteBalance.tint.Override(-5f);
                    whiteBalance.active = true;
                }
            }

            private void ApplyHighContrastEffect()
            {
                if (colorAdjustments != null)
                {
                    colorAdjustments.contrast.Override(30f);
                    colorAdjustments.postExposure.Override(0.2f);
                    colorAdjustments.active = true;
                }
            }

            private void ApplyVintageEffect()
            {
                if (filmGrain != null)
                {
                    filmGrain.intensity.Override(0.4f);
                    filmGrain.active = true;
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(-20f);
                    colorAdjustments.contrast.Override(15f);
                    colorAdjustments.active = true;
                }
                if (vignette != null)
                {
                    vignette.intensity.Override(0.3f);
                    vignette.color.Override(new Color(0.8f, 0.7f, 0.5f));
                    vignette.active = true;
                }
            }

            private void ApplyNightVisionEffect()
            {
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(-100f);
                    colorAdjustments.postExposure.Override(0.5f);
                    colorAdjustments.active = true;
                }
                if (channelMixer != null)
                {
                    channelMixer.redOutRedIn.Override(0f);
                    channelMixer.redOutGreenIn.Override(100f);
                    channelMixer.redOutBlueIn.Override(0f);
                    channelMixer.greenOutRedIn.Override(0f);
                    channelMixer.greenOutGreenIn.Override(100f);
                    channelMixer.greenOutBlueIn.Override(0f);
                    channelMixer.blueOutRedIn.Override(0f);
                    channelMixer.blueOutGreenIn.Override(100f);
                    channelMixer.blueOutBlueIn.Override(0f);
                    channelMixer.active = true;
                }
            }

            private void ApplySepiaToneEffect()
            {
                if (splitToning != null)
                {
                    splitToning.highlights.Override(new Color(1f, 0.9f, 0.7f));
                    splitToning.shadows.Override(new Color(0.8f, 0.6f, 0.4f));
                    splitToning.balance.Override(0f);
                    splitToning.active = true;
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(-30f);
                    colorAdjustments.active = true;
                }
            }

            private void ApplyPsychedelicEffect()
            {
                if (chromaticAberration != null)
                {
                    chromaticAberration.intensity.Override(1f);
                    chromaticAberration.active = true;
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(50f);
                    colorAdjustments.contrast.Override(20f);
                    colorAdjustments.active = true;
                }
                if (channelMixer != null)
                {
                    channelMixer.redOutRedIn.Override(50f);
                    channelMixer.redOutGreenIn.Override(25f);
                    channelMixer.redOutBlueIn.Override(25f);
                    channelMixer.greenOutRedIn.Override(25f);
                    channelMixer.greenOutGreenIn.Override(50f);
                    channelMixer.greenOutBlueIn.Override(25f);
                    channelMixer.blueOutRedIn.Override(25f);
                    channelMixer.blueOutGreenIn.Override(25f);
                    channelMixer.blueOutBlueIn.Override(50f);
                    channelMixer.active = true;
                }
            }

            private void ApplyHorrorEffect()
            {
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(-40f);
                    colorAdjustments.contrast.Override(25f);
                    colorAdjustments.postExposure.Override(-0.3f);
                    colorAdjustments.active = true;
                }
                if (vignette != null)
                {
                    vignette.intensity.Override(0.6f);
                    vignette.color.Override(new Color(0.2f, 0.1f, 0.1f));
                    vignette.active = true;
                }
                if (filmGrain != null)
                {
                    filmGrain.intensity.Override(0.5f);
                    filmGrain.active = true;
                }
            }

            private void ApplyCinematicEffect()
            {
                if (vignette != null)
                {
                    vignette.intensity.Override(0.4f);
                    vignette.color.Override(Color.black);
                    vignette.active = true;
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.contrast.Override(15f);
                    colorAdjustments.saturation.Override(-10f);
                    colorAdjustments.active = true;
                }
                if (depthOfField != null)
                {
                    depthOfField.focusDistance.Override(5f);
                    depthOfField.active = true;
                }
            }

            private void ApplyDreamEffect()
            {
                if (bloom != null)
                {
                    bloom.intensity.Override(1.5f);
                    bloom.threshold.Override(0.6f);
                    bloom.active = true;
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.postExposure.Override(0.3f);
                    colorAdjustments.saturation.Override(20f);
                    colorAdjustments.active = true;
                }
                if (whiteBalance != null)
                {
                    whiteBalance.temperature.Override(10f);
                    whiteBalance.active = true;
                }
            }

            private void ApplyGlitchEffect()
            {
                if (chromaticAberration != null)
                {
                    chromaticAberration.intensity.Override(1.2f);
                    chromaticAberration.active = true;
                }
                if (lensDistortion != null)
                {
                    lensDistortion.intensity.Override(0.5f);
                    lensDistortion.active = true;
                }
                if (filmGrain != null)
                {
                    filmGrain.intensity.Override(0.8f);
                    filmGrain.active = true;
                }
                if (colorAdjustments != null)
                {
                    colorAdjustments.contrast.Override(40f);
                    colorAdjustments.active = true;
                }
            }

            private void ApplyThermalEffect()
            {
                if (colorAdjustments != null)
                {
                    colorAdjustments.saturation.Override(-100f);
                    colorAdjustments.contrast.Override(30f);
                    colorAdjustments.postExposure.Override(0.2f);
                    colorAdjustments.active = true;
                }
                if (splitToning != null)
                {
                    splitToning.highlights.Override(new Color(1f, 0.3f, 0f));
                    splitToning.shadows.Override(new Color(0f, 0f, 1f));
                    splitToning.balance.Override(0.3f);
                    splitToning.active = true;
                }
            }

            #endregion

            #region Animated Effects (New)

            private void ApplyWavyCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Smooth wavy motion in X and Y
                    float waveX = Mathf.Sin(animationTime * 2f) * 0.02f;
                    float waveY = Mathf.Cos(animationTime * 1.5f) * 0.015f;
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + new Vector3(waveX, waveY, 0f);
                }
            }

            private void ApplyBobbingCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Bobbing motion like walking but more pronounced
                    float bob = Mathf.Sin(animationTime * 3f) * 0.03f;
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + new Vector3(0f, bob, 0f);
                }
            }

            private void ApplyShakyCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Random shake
                    float shakeIntensity = 0.01f;
                    Vector3 shake = new Vector3(
                        (UnityEngine.Random.value - 0.5f) * shakeIntensity,
                        (UnityEngine.Random.value - 0.5f) * shakeIntensity,
                        0f
                    );
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + shake;
                }
            }

            private void ApplyRotatingCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Slow rotation around Z axis
                    float rotation = Mathf.Sin(animationTime * 0.5f) * 5f; // ±5 degrees
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation + new Vector3(0f, 0f, rotation);
                }
            }

            private void ApplyPulsingFOVEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Pulsing field of view
                    float fovOffset = Mathf.Sin(animationTime * 2f) * 10f; // ±10 degrees
                    player.gameplayCamera.fieldOfView = originalFOV + fovOffset;
                }
            }

            private void ApplySwayingCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Gentle swaying motion
                    float swayX = Mathf.Sin(animationTime * 1f) * 2f;
                    float swayY = Mathf.Cos(animationTime * 0.7f) * 1.5f;
                    
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation + new Vector3(swayY, swayX, 0f);
                }
            }

            private void ApplyFishEyeAnimatedEffect()
            {
                if (lensDistortion != null)
                {
                    // Animated lens distortion
                    float distortion = Mathf.Sin(animationTime * 1.5f) * 0.3f;
                    lensDistortion.intensity.Override(distortion);
                    lensDistortion.active = true;
                }
            }

            private void ApplyDrunkCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Combine multiple drunk-like movements
                    float drunkX = Mathf.Sin(animationTime * 1.2f) * 0.025f;
                    float drunkY = Mathf.Cos(animationTime * 0.8f) * 0.02f;
                    float drunkRot = Mathf.Sin(animationTime * 0.6f) * 3f;
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + new Vector3(drunkX, drunkY, 0f);
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation + new Vector3(0f, 0f, drunkRot);
                }
            }

            private void ApplyEarthquakeEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Intense random shaking
                    float intensity = 0.03f;
                    Vector3 earthquake = new Vector3(
                        (UnityEngine.Random.value - 0.5f) * intensity,
                        (UnityEngine.Random.value - 0.5f) * intensity,
                        (UnityEngine.Random.value - 0.5f) * intensity * 0.5f
                    );
                    
                    // Also add rotational shake
                    Vector3 rotShake = new Vector3(
                        (UnityEngine.Random.value - 0.5f) * 2f,
                        (UnityEngine.Random.value - 0.5f) * 2f,
                        (UnityEngine.Random.value - 0.5f) * 3f
                    );
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + earthquake;
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation + rotShake;
                }
            }

            private void ApplyFloatingCameraEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Gentle floating motion like underwater
                    float floatY = Mathf.Sin(animationTime * 0.8f) * 0.04f;
                    float floatX = Mathf.Cos(animationTime * 0.6f) * 0.02f;
                    float floatZ = Mathf.Sin(animationTime * 0.4f) * 0.01f;
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + new Vector3(floatX, floatY, floatZ);
                }
            }

            private void ApplySpinningEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Continuous slow spinning
                    float spin = animationTime * 10f; // 10 degrees per second
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation + new Vector3(0f, 0f, spin);
                }
            }

            private void ApplyBreathingEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Simulate breathing motion
                    float breath = Mathf.Sin(animationTime * 4f) * 0.008f; // Faster breathing
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + new Vector3(0f, breath, breath * 0.5f);
                    
                    // Also affect FOV slightly
                    float fovBreath = Mathf.Sin(animationTime * 4f) * 1f;
                    player.gameplayCamera.fieldOfView = originalFOV + fovBreath;
                }
            }

            private void ApplyGlitchyMovementEffect()
            {
                if (player.gameplayCamera != null && UnityEngine.Random.value < 0.1f) // 10% chance per frame
                {
                    // Random glitchy jumps
                    float glitchIntensity = 0.05f;
                    Vector3 glitch = new Vector3(
                        (UnityEngine.Random.value - 0.5f) * glitchIntensity,
                        (UnityEngine.Random.value - 0.5f) * glitchIntensity,
                        0f
                    );
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + glitch;
                    
                    // Add chromatic aberration for glitch effect
                    if (chromaticAberration != null)
                    {
                        chromaticAberration.intensity.Override(UnityEngine.Random.Range(0f, 1.5f));
                        chromaticAberration.active = true;
                    }
                }
                else if (player.gameplayCamera != null)
                {
                    // Return to normal when not glitching
                    player.gameplayCamera.transform.localPosition = originalCameraPosition;
                }
            }

            private void ApplyWarpedRealityEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // Combine multiple effects for a surreal experience
                    float warpTime = animationTime * 0.7f;
                    
                    // Position warping
                    float warpX = Mathf.Sin(warpTime * 2.3f) * 0.03f;
                    float warpY = Mathf.Cos(warpTime * 1.7f) * 0.025f;
                    
                    // Rotation warping
                    float warpRotX = Mathf.Sin(warpTime * 1.1f) * 4f;
                    float warpRotY = Mathf.Cos(warpTime * 0.9f) * 3f;
                    float warpRotZ = Mathf.Sin(warpTime * 1.3f) * 6f;
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + new Vector3(warpX, warpY, 0f);
                    player.gameplayCamera.transform.localEulerAngles = originalCameraRotation + new Vector3(warpRotX, warpRotY, warpRotZ);
                    
                    // FOV warping
                    float warpFOV = Mathf.Sin(warpTime * 1.5f) * 15f;
                    player.gameplayCamera.fieldOfView = originalFOV + warpFOV;
                }
                
                // Add visual distortion
                if (lensDistortion != null)
                {
                    float distortion = Mathf.Sin(animationTime * 2f) * 0.4f;
                    lensDistortion.intensity.Override(distortion);
                    lensDistortion.active = true;
                }
            }

            private void ApplyTremorEffect()
            {
                if (player.gameplayCamera != null)
                {
                    // High frequency, low amplitude tremor
                    float tremorFreq = 15f;
                    float tremorAmp = 0.005f;
                    
                    Vector3 tremor = new Vector3(
                        Mathf.Sin(animationTime * tremorFreq) * tremorAmp,
                        Mathf.Cos(animationTime * tremorFreq * 1.1f) * tremorAmp,
                        0f
                    );
                    
                    player.gameplayCamera.transform.localPosition = originalCameraPosition + tremor;
                }
            }

            #endregion

            public void Reset()
            {
                ResetAllEffects();
                effectApplied = false;
                currentEffectIndex = -1;
                animationTime = 0f;
                Debug.Log("All camera effects reset");
            }
        }

        // Main update patch - exactly like the original deterioration system
        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        private static void LateUpdatePatch(PlayerControllerB __instance)
        {
            if (__instance != GameNetworkManager.Instance.localPlayerController ||
                !__instance.isPlayerControlled ||
                __instance.isPlayerDead) return;

            // Initialize camera effect system if needed - same pattern as deterioration system
            localPlayerEffects ??= new CameraEffectController(__instance);

            // Update camera effects continuously - same as deterioration system
            localPlayerEffects.Update();
        }

        // Reset when returning to lobby - trigger new effect
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPostfix]
        private static void OnReturnToLobby()
        {
            try
            {
                if (localPlayerEffects != null)
                {
                    localPlayerEffects.Reset();
                    Debug.Log("Camera effects reset when returning to lobby");
                }
                effectApplied = false; // Allow new effect to be applied
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to reset camera effects on lobby return: {ex.Message}");
            }
        }

        // Reset and cleanup on game end
        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void Cleanup()
        {
            if (localPlayerEffects != null)
            {
                localPlayerEffects.Reset();
                localPlayerEffects = null;
                Debug.Log("Camera effect debug system cleaned up");
            }
            effectApplied = false;
        }

        // Trigger new effect when changing levels
        [HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
        [HarmonyPostfix]
        private static void OnLevelChange()
        {
            try
            {
                if (localPlayerEffects != null)
                {
                    localPlayerEffects.ApplyRandomEffect();
                    Debug.Log("New random camera effect applied on level change");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to apply camera effect on level change: {ex.Message}");
            }
        }
    }
}
