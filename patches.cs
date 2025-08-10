using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using TMPro;

namespace BIG_DADDY_MOD.Patches
{
    [HarmonyPatch]
    public class ProgressiveDeteriorationSystem
    {
        public static int currentDeathCount = 0;
        private static int previousDeathCount = 0;
        public static DeteriorationEffects? localPlayerDebuffs;
        private static readonly System.Random rnd = new();

        public static int roundsSurvivedSinceDeathReduction = 0;

        public class DeteriorationEffects
        {
            // --- Configuration Constants ---
            private const int BASE_DAMAGE_PER_TICK = 2;
            private const int BASE_TICK_CHANCE = 9000;
            private const int COUGH_CHANCE = 7500;
            private const int HALLUCINATION_CHANCE_BASE = 10000;
            private const int HEARTBEAT_HALLUCINATION_CHANCE = 15000;
            private const float HALLUCINATION_COOLDOWN = 20f;
            private const float SPRINT_EXHAUSTION_THRESHOLD_BASE = 8f;
            private const float WINDED_COOLDOWN = 5f;

            // --- Player and Component References ---
            private readonly PlayerControllerB player;
            private readonly float baseMovementSpeed;
            private readonly float baseStamina;
            private readonly Vector3 originalItemHolderPosition;
            private int maxHealth;

            // Post-processing components - properly referenced
            private Volume? postProcessVolume;
            private ChromaticAberration? chromaticAberration;
            private FilmGrain? filmGrain;
            private ColorAdjustments? colorAdjustments;
            private Vignette? vignette;

            // Audio components
            private AudioSource? tinnitusSource;
            private AudioSource? heartbeatSource;
            private Coroutine? activeFlickerCoroutine;
            private Coroutine? activeHeartbeatCoroutine;

            // --- Milestone States ---
            private bool milestone1 = false, milestone2 = false, milestone3 = false, milestone4 = false, milestone5 = false;

            // --- Dynamic State Variables ---
            private float sprintDuration = 0f;
            private bool isWinded = false;
            private float windedTimer = 0f;
            private float hallucinationCooldown = 0f;

            // Debuff intensity trackers
            private float permanentDrunkness = 0f;
            private float handTremorIntensity = 0f;
            private float visualDistortionMultiplier = 1.0f;
            private int currentDamagePerTick = BASE_DAMAGE_PER_TICK;
            private int currentTickChance = BASE_TICK_CHANCE;

            // Text corruption tracking
            private readonly Dictionary<ScanNodeProperties, string> originalScanTexts = new();
            private readonly Dictionary<TextMeshProUGUI, string> originalAdvertTexts = new();
            private readonly Dictionary<Item, string> originalItemNames = new();
            private readonly Dictionary<Item, string[]> originalItemTooltips = new();
            private float textCorruptionTimer = 0f;
            private float tooltipCorruptionTimer = 0f;
            private int timeOffsetMinutes = 0;
            private bool tooltipsShuffled = false;
            private bool vehicleControlsCorrupted = false;

            public DeteriorationEffects(PlayerControllerB controller)
            {
                player = controller;
                baseMovementSpeed = player.movementSpeed;
                baseStamina = player.sprintTime;
                maxHealth = 100;
                originalItemHolderPosition = player.localItemHolder.localPosition;

                InitializePostProcessingEffects();
                InitializeAudioEffects();
            }

            private void InitializePostProcessingEffects()
            {
                try
                {
                    // Try multiple methods to get the post-processing volume
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
                        // Get existing effects or add them if they don't exist
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

            private void InitializeAudioEffects()
            {
                try
                {
                    // Tinnitus effect
                    GameObject tinnitusObject = new("DeteriorationTinnitusEffect");
                    tinnitusObject.transform.SetParent(player.gameplayCamera.transform);
                    tinnitusSource = tinnitusObject.AddComponent<AudioSource>();

                    if (HUDManager.Instance?.radiationWarningAudio != null)
                    {
                        tinnitusSource.clip = HUDManager.Instance.radiationWarningAudio;
                    }

                    tinnitusSource.loop = true;
                    tinnitusSource.playOnAwake = false;
                    tinnitusSource.spatialBlend = 0f;
                    tinnitusSource.volume = 0f;
                    tinnitusSource.priority = 128;

                    Debug.Log("Audio effects initialized successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize audio effects: {ex.Message}");
                }
            }

            public void Update(int totalDeaths)
            {
                try
                {
                    // Enforce max health
                    if (player.health > maxHealth)
                    {
                        player.health = maxHealth;
                        HUDManager.Instance?.UpdateHealthUI(player.health, false);
                    }

                    UpdateVisualsAndEffects();
                    UpdateAudioEffects();
                    UpdatePermanentDebuffs();
                    UpdateTextCorruption(totalDeaths);
                    UpdateTooltipCorruption(totalDeaths);
                    UpdateTimeDistortion();

                    if (milestone1) ProcessDeteriorationTick(totalDeaths);
                    if (milestone3) UpdateExhaustionSystem(totalDeaths);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in deterioration update: {ex.Message}");
                }
            }

            public void ApplyDeteriorationLevel(int totalDeaths)
            {
                try
                {
                    // Apply permanent stat reductions with exponential decay
                    float reductionFactor = Mathf.Pow(0.97f, totalDeaths);
                    player.movementSpeed = baseMovementSpeed * reductionFactor;
                    player.sprintTime = baseStamina * reductionFactor;
                    SetMaxHealth(Mathf.Max(10, 100 - (totalDeaths * 5)));

                    // Check and apply milestone effects
                    CheckAndApplyMilestones(totalDeaths);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error applying deterioration level: {ex.Message}");
                }
            }

            private void UpdateTextCorruption(int totalDeaths)
            {
                if (totalDeaths < 3) return; // Start text corruption at 3+ deaths

                textCorruptionTimer += Time.deltaTime;

                // Corruption frequency increases with deaths (every 2-8 seconds based on death count)
                float corruptionInterval = Mathf.Max(2f, 8f - (totalDeaths * 0.5f));

                if (textCorruptionTimer >= corruptionInterval)
                {
                    textCorruptionTimer = 0f;
                    CorruptRandomScanNode(totalDeaths);
                    CorruptRandomAdvertisement(totalDeaths);
                }
            }

            private void UpdateTooltipCorruption(int totalDeaths)
            {
                // At milestone 4+ (8 deaths), start shuffling tooltips instead of corrupting them
                if (totalDeaths >= 8 && !tooltipsShuffled)
                {
                    ShuffleAllTooltips();
                    tooltipsShuffled = true;
                    return;
                }

                // Lower death counts still use the old corruption method
                if (totalDeaths >= 4 && totalDeaths < 8)
                {
                    tooltipCorruptionTimer += Time.deltaTime;

                    // Tooltip corruption happens less frequently than text corruption
                    float tooltipCorruptionInterval = Mathf.Max(5f, 15f - (totalDeaths * 0.8f));

                    if (tooltipCorruptionTimer >= tooltipCorruptionInterval)
                    {
                        tooltipCorruptionTimer = 0f;
                        CorruptRandomTooltip(totalDeaths);
                        CorruptInventoryItemNames(totalDeaths);
                    }
                }
            }

            private void CorruptRandomScanNode(int totalDeaths)
            {
                try
                {
                    var scanNodes = UnityEngine.Object.FindObjectsOfType<ScanNodeProperties>();
                    if (scanNodes.Length == 0) return;

                    var randomNode = scanNodes[rnd.Next(scanNodes.Length)];
                    if (randomNode == null || string.IsNullOrEmpty(randomNode.headerText)) return;

                    // Store original text if not already stored
                    if (!originalScanTexts.ContainsKey(randomNode))
                    {
                        originalScanTexts[randomNode] = randomNode.headerText;
                    }

                    // Corruption intensity based on death count (10% to 60% of characters)
                    float corruptionPercent = Mathf.Clamp(0.1f + (totalDeaths * 0.05f), 0.1f, 0.6f);
                    string corruptedText = CorruptString(originalScanTexts[randomNode], corruptionPercent);
                    randomNode.headerText = corruptedText;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error corrupting scan node: {ex.Message}");
                }
            }

            private void CorruptRandomAdvertisement(int totalDeaths)
            {
                try
                {
                    // Find advertisement text elements (they're usually TextMeshProUGUI components)
                    var textElements = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>();
                    if (textElements.Length == 0) return;

                    // Filter for likely advertisement text (avoid UI elements)
                    var adTexts = textElements.Where(t =>
                        !string.IsNullOrEmpty(t.text) &&
                        t.text.Length > 10 &&
                        (t.name.ToLower().Contains("ad") ||
                         t.name.ToLower().Contains("poster") ||
                         t.name.ToLower().Contains("sign") ||
                         t.text.Contains("Company") ||
                         t.text.Contains("Buy") ||
                         t.text.Contains("Sale"))).ToArray();

                    if (adTexts.Length == 0) return;

                    var randomAd = adTexts[rnd.Next(adTexts.Length)];

                    // Store original text if not already stored
                    if (!originalAdvertTexts.ContainsKey(randomAd))
                    {
                        originalAdvertTexts[randomAd] = randomAd.text;
                    }

                    // Lighter corruption for ads (5% to 30% of characters)
                    float corruptionPercent = Mathf.Clamp(0.05f + (totalDeaths * 0.025f), 0.05f, 0.3f);
                    string corruptedText = CorruptString(originalAdvertTexts[randomAd], corruptionPercent);
                    randomAd.text = corruptedText;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error corrupting advertisement: {ex.Message}");
                }
            }

            private string CorruptString(string original, float corruptionPercent)
            {
                if (string.IsNullOrEmpty(original)) return original;

                char[] chars = original.ToCharArray();
                int corruptCount = Mathf.RoundToInt(chars.Length * corruptionPercent);

                // List of corruption characters that look like glitches/static
                char[] corruptionChars = { '█', '▓', '▒', '░', '?', '#', '@', '&', '%', '!', '※', '∞', '◊', '∴' };

                for (int i = 0; i < corruptCount; i++)
                {
                    int randomIndex = rnd.Next(chars.Length);

                    // Don't corrupt spaces or special formatting
                    if (chars[randomIndex] != ' ' && chars[randomIndex] != '\n' && chars[randomIndex] != '\t')
                    {
                        chars[randomIndex] = corruptionChars[rnd.Next(corruptionChars.Length)];
                    }
                }

                return new string(chars);
            }

            private void CorruptRandomTooltip(int totalDeaths)
            {
                try
                {
                    // Target various UI tooltip elements
                    var allItems = Resources.FindObjectsOfTypeAll<Item>();
                    if (allItems.Length == 0) return;

                    var randomItem = allItems[rnd.Next(allItems.Length)];
                    if (randomItem == null) return;

                    // Store original item name if not already stored
                    if (!originalItemNames.ContainsKey(randomItem) && !string.IsNullOrEmpty(randomItem.itemName))
                    {
                        originalItemNames[randomItem] = randomItem.itemName;
                    }

                    // Store original item tooltips array if not already stored
                    if (!originalItemTooltips.ContainsKey(randomItem) && randomItem.toolTips != null && randomItem.toolTips.Length > 0)
                    {
                        originalItemTooltips[randomItem] = (string[])randomItem.toolTips.Clone();
                    }

                    // Corrupt item names (lighter corruption)
                    if (originalItemNames.ContainsKey(randomItem))
                    {
                        float nameCorruptionPercent = Mathf.Clamp(0.03f + (totalDeaths * 0.02f), 0.03f, 0.25f);
                        randomItem.itemName = CorruptString(originalItemNames[randomItem], nameCorruptionPercent);
                    }

                    // Corrupt tooltips array (heavier corruption)
                    if (originalItemTooltips.ContainsKey(randomItem) && randomItem.toolTips != null)
                    {
                        for (int i = 0; i < randomItem.toolTips.Length; i++)
                        {
                            if (!string.IsNullOrEmpty(randomItem.toolTips[i]))
                            {
                                float tooltipCorruptionPercent = Mathf.Clamp(0.08f + (totalDeaths * 0.04f), 0.08f, 0.5f);
                                randomItem.toolTips[i] = CorruptString(originalItemTooltips[randomItem][i], tooltipCorruptionPercent);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error corrupting tooltip: {ex.Message}");
                }
            }

            private void CorruptInventoryItemNames(int totalDeaths)
            {
                try
                {
                    // Target items currently in player's inventory
                    if (player.ItemSlots == null) return;

                    foreach (var item in player.ItemSlots)
                    {
                        if (item?.itemProperties == null) continue;

                        var itemProps = item.itemProperties;

                        // Store originals if not already stored
                        if (!originalItemNames.ContainsKey(itemProps) && !string.IsNullOrEmpty(itemProps.itemName))
                        {
                            originalItemNames[itemProps] = itemProps.itemName;
                        }

                        if (!originalItemTooltips.ContainsKey(itemProps) && itemProps.toolTips != null && itemProps.toolTips.Length > 0)
                        {
                            originalItemTooltips[itemProps] = (string[])itemProps.toolTips.Clone();
                        }

                        // Higher chance to corrupt items actually being held
                        if (rnd.Next(0, 100) < (totalDeaths * 8)) // 8% chance per death
                        {
                            if (originalItemNames.ContainsKey(itemProps))
                            {
                                float nameCorruptionPercent = Mathf.Clamp(0.05f + (totalDeaths * 0.03f), 0.05f, 0.4f);
                                itemProps.itemName = CorruptString(originalItemNames[itemProps], nameCorruptionPercent);
                            }

                            if (originalItemTooltips.ContainsKey(itemProps) && itemProps.toolTips != null)
                            {
                                for (int i = 0; i < itemProps.toolTips.Length; i++)
                                {
                                    if (!string.IsNullOrEmpty(itemProps.toolTips[i]))
                                    {
                                        float tooltipCorruptionPercent = Mathf.Clamp(0.1f + (totalDeaths * 0.05f), 0.1f, 0.6f);
                                        itemProps.toolTips[i] = CorruptString(originalItemTooltips[itemProps][i], tooltipCorruptionPercent);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error corrupting inventory item names: {ex.Message}");
                }
            }

            private void ShuffleAllTooltips()
            {
                try
                {
                    var allItems = Resources.FindObjectsOfTypeAll<Item>();
                    if (allItems.Length < 2) return;

                    // Store all original tooltips first
                    List<Item> itemsToShuffle = new List<Item>();
                    List<string[]> originalTooltipArrays = new List<string[]>();
                    List<string> originalNames = new List<string>();

                    foreach (var item in allItems)
                    {
                        if (item != null && item.toolTips != null && item.toolTips.Length > 0)
                        {
                            if (!originalItemTooltips.ContainsKey(item))
                            {
                                originalItemTooltips[item] = (string[])item.toolTips.Clone();
                            }
                            if (!originalItemNames.ContainsKey(item) && !string.IsNullOrEmpty(item.itemName))
                            {
                                originalItemNames[item] = item.itemName;
                            }

                            itemsToShuffle.Add(item);
                            originalTooltipArrays.Add((string[])item.toolTips.Clone());
                            originalNames.Add(item.itemName);
                        }
                    }

                    if (itemsToShuffle.Count < 2) return;

                    // Shuffle the tooltips and names
                    for (int i = 0; i < itemsToShuffle.Count; i++)
                    {
                        int randomIndex = rnd.Next(itemsToShuffle.Count);

                        // Swap tooltip arrays - give each item a random other item's tooltips
                        itemsToShuffle[i].toolTips = (string[])originalTooltipArrays[randomIndex].Clone();

                        // 50% chance to also swap the name for extra confusion
                        if (rnd.Next(0, 2) == 0)
                        {
                            itemsToShuffle[i].itemName = originalNames[randomIndex];
                        }
                    }

                    Debug.Log($"Shuffled tooltips for {itemsToShuffle.Count} items due to severe deterioration");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error shuffling tooltips: {ex.Message}");
                }
            }

            private void UpdateTimeDistortion()
            {
                try
                {
                    if (milestone2) // Start time distortion at milestone 2 (5+ deaths)
                    {
                        // Randomly adjust time offset (gets more erratic with more deaths)
                        if (rnd.Next(0, 1000) < (currentDeathCount * 2)) // Chance increases with deaths
                        {
                            int maxOffset = currentDeathCount * 10; // Max minutes offset
                            timeOffsetMinutes = rnd.Next(-maxOffset, maxOffset + 1);
                        }

                        // Apply time distortion to HUD
                        if (HUDManager.Instance?.clockNumber != null)
                        {
                            var clockText = HUDManager.Instance.clockNumber;
                            string originalTime = clockText.text;

                            if (!string.IsNullOrEmpty(originalTime) && originalTime.Contains(":"))
                            {
                                string distortedTime = DistortTime(originalTime, timeOffsetMinutes);
                                clockText.text = distortedTime;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error distorting time: {ex.Message}");
                }
            }

            private string DistortTime(string originalTime, int offsetMinutes)
            {
                try
                {
                    // Parse the original time (format usually "12:34 AM" or "23:45")
                    if (originalTime.Contains(":"))
                    {
                        var parts = originalTime.Split(':');
                        if (parts.Length >= 2)
                        {
                            if (int.TryParse(parts[0], out int hours))
                            {
                                var minutePart = parts[1].Split(' ')[0]; // Remove AM/PM if present
                                if (int.TryParse(minutePart, out int minutes))
                                {
                                    // Add the offset
                                    minutes += offsetMinutes;

                                    // Handle minute overflow/underflow
                                    while (minutes >= 60)
                                    {
                                        minutes -= 60;
                                        hours++;
                                    }
                                    while (minutes < 0)
                                    {
                                        minutes += 60;
                                        hours--;
                                    }

                                    // Handle hour overflow/underflow
                                    while (hours >= 24) hours -= 24;
                                    while (hours < 0) hours += 24;

                                    // Format back to string
                                    string amPm = originalTime.Contains("AM") ? "AM" :
                                                 originalTime.Contains("PM") ? "PM" : "";

                                    if (!string.IsNullOrEmpty(amPm))
                                    {
                                        // Convert to 12-hour format
                                        int displayHours = hours == 0 ? 12 : hours > 12 ? hours - 12 : hours;
                                        amPm = hours < 12 ? "AM" : "PM";
                                        return $"{displayHours}:{minutes:D2} {amPm}";
                                    }
                                    else
                                    {
                                        // 24-hour format
                                        return $"{hours:D2}:{minutes:D2}";
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing time: {ex.Message}");
                }

                return originalTime; // Return original if parsing fails
            }

            private void RestoreOriginalTexts()
            {
                try
                {
                    // Restore scan node texts
                    foreach (var kvp in originalScanTexts)
                    {
                        if (kvp.Key != null)
                        {
                            kvp.Key.headerText = kvp.Value;
                        }
                    }
                    originalScanTexts.Clear();

                    // Restore advertisement texts
                    foreach (var kvp in originalAdvertTexts)
                    {
                        if (kvp.Key != null)
                        {
                            kvp.Key.text = kvp.Value;
                        }
                    }
                    originalAdvertTexts.Clear();

                    // Restore item names and descriptions
                    foreach (var kvp in originalItemNames)
                    {
                        if (kvp.Key != null)
                        {
                            kvp.Key.itemName = kvp.Value;
                        }
                    }
                    originalItemNames.Clear();

                    foreach (var kvp in originalItemTooltips)
                    {
                        if (kvp.Key != null)
                        {
                            kvp.Key.toolTips = (string[])kvp.Value.Clone();
                        }
                    }
                    originalItemTooltips.Clear();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error restoring original texts: {ex.Message}");
                }
            }

            private void ProcessDeteriorationTick(int totalDeaths)
            {
                if (hallucinationCooldown > 0f) hallucinationCooldown -= Time.deltaTime;

                // Random damage tick - chance decreases as deterioration increases
                if (rnd.Next(currentTickChance) == 1)
                {
                    player.DamagePlayer(currentDamagePerTick, causeOfDeath: CauseOfDeath.Unknown);
                }

                // Coughing/visor impact
                if (rnd.Next(COUGH_CHANCE) == 1)
                {
                    player.StartCoroutine(VisorImpactEffect());
                }

                // UI Hallucinations (milestone 3+)
                if (milestone3 && hallucinationCooldown <= 0f && rnd.Next(HALLUCINATION_CHANCE_BASE) == 1)
                {
                    hallucinationCooldown = HALLUCINATION_COOLDOWN;
                    if (activeFlickerCoroutine == null)
                    {
                        activeFlickerCoroutine = player.StartCoroutine(UIFlickerCoroutine());
                    }
                }

                // Severe hallucinations (milestone 5)
                if (milestone5 && hallucinationCooldown <= 0f && rnd.Next(HEARTBEAT_HALLUCINATION_CHANCE) == 1)
                {
                    hallucinationCooldown = HALLUCINATION_COOLDOWN;
                    if (activeHeartbeatCoroutine == null)
                    {
                        activeHeartbeatCoroutine = player.StartCoroutine(HeartbeatHallucinationCoroutine());
                    }
                }
            }

            public void Reset()
            {
                try
                {
                    // Reset player stats
                    player.movementSpeed = baseMovementSpeed;
                    player.sprintTime = baseStamina;
                    player.playerBodyAnimator.SetBool("Limp", false);
                    player.drunkness = 0f;
                    player.insanityLevel = 0f;
                    player.localItemHolder.localPosition = originalItemHolderPosition;

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
                        colorAdjustments.active = false;
                    }
                    if (vignette != null)
                    {
                        vignette.intensity.Override(0f);
                        vignette.active = false;
                    }

                    // Clean up audio sources
                    if (tinnitusSource != null)
                    {
                        UnityEngine.Object.Destroy(tinnitusSource.gameObject);
                    }
                    if (heartbeatSource != null)
                    {
                        UnityEngine.Object.Destroy(heartbeatSource.gameObject);
                    }

                    // Stop coroutines
                    if (player != null)
                    {
                        if (activeFlickerCoroutine != null) player.StopCoroutine(activeFlickerCoroutine);
                        if (activeHeartbeatCoroutine != null) player.StopCoroutine(activeHeartbeatCoroutine);
                    }

                    // Clean up text corruption
                    RestoreOriginalTexts();

                    // Reset state
                    milestone1 = milestone2 = milestone3 = milestone4 = milestone5 = false;
                    activeFlickerCoroutine = null;
                    activeHeartbeatCoroutine = null;
                    permanentDrunkness = 0f;
                    handTremorIntensity = 0f;
                    visualDistortionMultiplier = 1.0f;
                    currentDamagePerTick = BASE_DAMAGE_PER_TICK;
                    currentTickChance = BASE_TICK_CHANCE;
                    timeOffsetMinutes = 0;
                    tooltipCorruptionTimer = 0f;
                    tooltipsShuffled = false;
                    vehicleControlsCorrupted = false;

                    Debug.Log("Deterioration effects reset successfully");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error resetting deterioration effects: {ex.Message}");
                }
            }

            #region Private Methods

            private void SetMaxHealth(int newMaxHealth)
            {
                maxHealth = Math.Max(1, newMaxHealth);
                if (player.health > maxHealth)
                {
                    player.health = maxHealth;
                    HUDManager.Instance?.UpdateHealthUI(player.health, false);
                }
            }

            private void UpdateVisualsAndEffects()
            {
                // Handle winded state
                if (isWinded)
                {
                    player.drunkness = Mathf.Lerp(player.drunkness, 0.9f, Time.deltaTime * 5f);
                    player.increasingDrunknessThisFrame = true;
                    HUDManager.Instance?.ShakeCamera(ScreenShakeType.Big);
                }

                // Hand tremors
                if (handTremorIntensity > 0f)
                {
                    Vector3 tremor = UnityEngine.Random.insideUnitSphere * handTremorIntensity;
                    player.localItemHolder.localPosition = originalItemHolderPosition + tremor;
                }

                // Apply post-processing effects with proper Override calls
                if (postProcessVolume != null && postProcessVolume.profile != null)
                {
                    float chromaticIntensity = Mathf.Clamp01(0.05f * visualDistortionMultiplier);
                    float grainIntensity = Mathf.Clamp01(0.08f * visualDistortionMultiplier);
                    float saturationValue = Mathf.Clamp(-10f * visualDistortionMultiplier, -80f, 0f);
                    float vignetteIntensity = Mathf.Clamp01(0.1f * (visualDistortionMultiplier - 1.0f));

                    if (chromaticAberration != null && chromaticIntensity > 0f)
                    {
                        chromaticAberration.intensity.Override(chromaticIntensity);
                        chromaticAberration.active = true;
                    }

                    if (filmGrain != null && grainIntensity > 0f)
                    {
                        filmGrain.intensity.Override(grainIntensity);
                        filmGrain.active = true;
                    }

                    if (colorAdjustments != null && saturationValue < 0f)
                    {
                        colorAdjustments.saturation.Override(saturationValue);
                        colorAdjustments.active = true;
                    }

                    if (vignette != null && vignetteIntensity > 0f)
                    {
                        vignette.intensity.Override(vignetteIntensity);
                        vignette.active = true;
                    }
                }
            }

            private void UpdateAudioEffects()
            {
                if (milestone4 && tinnitusSource != null)
                {
                    float targetVolume = Mathf.Clamp01(0.1f * (visualDistortionMultiplier - 2.0f)) * 0.5f;
                    tinnitusSource.volume = Mathf.Lerp(tinnitusSource.volume, targetVolume, Time.deltaTime * 2f);

                    if (targetVolume > 0f && !tinnitusSource.isPlaying)
                    {
                        tinnitusSource.Play();
                    }
                }
            }

            private void UpdatePermanentDebuffs()
            {
                player.insanityLevel = Mathf.Max(player.insanityLevel, currentDeathCount * 1.5f);

                if (permanentDrunkness > 0)
                {
                    player.drunkness = Mathf.Max(player.drunkness, permanentDrunkness);
                    player.increasingDrunknessThisFrame = true;
                }
            }

            private void UpdateExhaustionSystem(int totalDeaths)
            {
                if (isWinded)
                {
                    windedTimer -= Time.deltaTime;
                    if (windedTimer <= 0f) isWinded = false;
                    return;
                }

                if (player.isSprinting && player.sprintMeter > 0)
                {
                    sprintDuration += Time.deltaTime;
                }
                else
                {
                    sprintDuration = Mathf.Max(0, sprintDuration - Time.deltaTime * 2);
                }

                float currentExhaustionThreshold = SPRINT_EXHAUSTION_THRESHOLD_BASE - (totalDeaths * 0.25f);
                currentExhaustionThreshold = Mathf.Max(1f, currentExhaustionThreshold);

                if (sprintDuration > currentExhaustionThreshold)
                {
                    isWinded = true;
                    windedTimer = WINDED_COOLDOWN;
                    sprintDuration = 0f;
                }
            }

            private IEnumerator UIFlickerCoroutine()
            {
                var inventoryCG = HUDManager.Instance?.Inventory?.canvasGroup;
                var playerInfoCG = HUDManager.Instance?.PlayerInfo?.canvasGroup;

                if (inventoryCG == null || playerInfoCG == null)
                {
                    activeFlickerCoroutine = null;
                    yield break;
                }

                float duration = milestone5 ? 2.5f : 1.25f;
                float interval = milestone5 ? 0.02f : 0.04f;
                float timer = 0f;

                float originalInventoryAlpha = inventoryCG.alpha;
                float originalPlayerInfoAlpha = playerInfoCG.alpha;

                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    float alpha = rnd.Next(0, 3) > 0 ? 1f : 0f; // More visible time than invisible
                    inventoryCG.alpha = alpha;
                    playerInfoCG.alpha = alpha;
                    yield return new WaitForSeconds(interval);
                }

                inventoryCG.alpha = originalInventoryAlpha;
                playerInfoCG.alpha = originalPlayerInfoAlpha;
                activeFlickerCoroutine = null;
            }

            private IEnumerator HeartbeatHallucinationCoroutine()
            {
                // Severe hallucination with multiple effects
                HUDManager.Instance?.ShakeCamera(ScreenShakeType.Long);

                if (heartbeatSource != null && heartbeatSource.clip != null)
                {
                    heartbeatSource.pitch = 0.7f; // Lower pitch for more ominous effect
                    heartbeatSource.Play();
                }

                // Brief screen distortion
                if (chromaticAberration != null)
                {
                    float originalIntensity = chromaticAberration.intensity.value;
                    chromaticAberration.intensity.Override(1f);
                    yield return new WaitForSeconds(0.5f);
                    chromaticAberration.intensity.Override(originalIntensity);
                }

                yield return new WaitForSeconds(1.5f);
                activeHeartbeatCoroutine = null;
            }

            private void CheckAndApplyMilestones(int totalDeaths)
            {
                // Milestone 1: 2 deaths - Begin deterioration
                if (totalDeaths >= 2 && !milestone1)
                {
                    milestone1 = true;
                    visualDistortionMultiplier = 1.8f;
                    permanentDrunkness = 0.15f;
                    Debug.Log("Milestone 1 reached: Initial deterioration begins");
                }

                // Milestone 2: 5 deaths - Physical impairment
                if (totalDeaths >= 5 && !milestone2)
                {
                    milestone2 = true;
                    player.playerBodyAnimator.SetBool("Limp", true);
                    currentTickChance = BASE_TICK_CHANCE / 2;
                    currentDamagePerTick = BASE_DAMAGE_PER_TICK + 2;
                    visualDistortionMultiplier = 3.2f;
                    permanentDrunkness = 0.35f;
                    Debug.Log("Milestone 2 reached: Physical impairment activated");
                }

                // Milestone 3: 7 deaths - Severe symptoms
                if (totalDeaths >= 7 && !milestone3)
                {
                    milestone3 = true;
                    visualDistortionMultiplier = 4.5f;
                    permanentDrunkness = 0.55f;
                    handTremorIntensity = 0.003f;
                    Debug.Log("Milestone 3 reached: Severe symptoms manifest");
                }

                // Milestone 4: 8 deaths - Critical deterioration
                if (totalDeaths >= 8 && !milestone4)
                {
                    milestone4 = true;
                    visualDistortionMultiplier = 6.0f;
                    currentDamagePerTick = BASE_DAMAGE_PER_TICK + 4;
                    permanentDrunkness = 0.65f;
                    handTremorIntensity = 0.005f;

                    // Enable vehicle control corruption at this milestone
                    vehicleControlsCorrupted = true;
                    Debug.Log("Milestone 4 reached: Critical deterioration - Vehicle controls compromised");
                }

                // Milestone 5: 9+ deaths - Terminal stage
                if (totalDeaths >= 9 && !milestone5)
                {
                    milestone5 = true;
                    visualDistortionMultiplier = 8.0f;
                    currentTickChance = BASE_TICK_CHANCE / 4;
                    currentDamagePerTick = BASE_DAMAGE_PER_TICK + 6;
                    permanentDrunkness = 0.75f;
                    handTremorIntensity = 0.008f;
                    Debug.Log("Milestone 5 reached: Terminal stage");
                }
            }

            #endregion
        }

        #region Harmony Patches and Utilities

        private static IEnumerator VisorImpactEffect()
        {
            if (HUDManager.Instance == null) yield break;

            HUDManager.Instance.flashbangScreenFilter.weight = 0.6f;
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            yield return new WaitForSeconds(0.15f);
            HUDManager.Instance.flashbangScreenFilter.weight = 0f;
        }

        // Vehicle control corruption patches
        [HarmonyPatch(typeof(VehicleController), "Update")]
        [HarmonyPostfix]
        private static void CorruptVehicleControls(VehicleController __instance)
        {
            if (localPlayerDebuffs?.vehicleControlsCorrupted != true) return;
            if (__instance?.currentDriver != GameNetworkManager.Instance.localPlayerController) return;

            try
            {
                // Get the current input
                float originalSteering = __instance.steeringInput;
                bool originalThrottle = __instance.drivePedalPressed;
                bool originalBrake = __instance.brakePedalPressed;

                // Apply control corruption based on deterioration level
                if (currentDeathCount >= 8)
                {
                    // Reverse brake and throttle controls
                    if (originalBrake && !__instance.drivePedalPressed)
                    {
                        // Player trying to brake -> force accelerate instead
                        __instance.drivePedalPressed = true;
                        __instance.brakePedalPressed = false;
                    }
                    else if (originalThrottle && !originalBrake)
                    {
                        // Randomly apply brake while accelerating (25% chance per frame)
                        if (rnd.Next(0, 100) < 25)
                        {
                            __instance.brakePedalPressed = false;
                            __instance.drivePedalPressed = true;
                        }
                    }

                    // Add steering shake/drift
                    if (Mathf.Abs(originalSteering) > 0.1f || __instance.averageVelocity.magnitude > 2f)
                    {
                        float steeringCorruption = UnityEngine.Random.Range(-0.3f, 0.3f);
                        __instance.steeringInput = Mathf.Clamp(originalSteering + steeringCorruption, -1f, 1f);
                    }

                    // Terminal stage (9+ deaths) - even worse corruption
                    if (currentDeathCount >= 9)
                    {
                        // Randomly invert steering (15% chance per frame)
                        if (rnd.Next(0, 100) < 15)
                        {
                            __instance.steeringInput = -originalSteering;
                        }

                        // Random phantom acceleration (10% chance per frame)
                        if (rnd.Next(0, 100) < 10)
                        {
                            __instance.drivePedalPressed = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error corrupting vehicle controls: {ex.Message}");
            }
        }

        // Alternative approach for different vehicle types
        [HarmonyPatch]
        private static class VehicleInputCorruption
        {
            [HarmonyPatch(typeof(PlayerControllerB), "Update")]
            [HarmonyPostfix]
            private static void CorruptVehicleInputs(PlayerControllerB __instance)
            {
                if (__instance != GameNetworkManager.Instance.localPlayerController) return;
                if (localPlayerDebuffs?.vehicleControlsCorrupted != true) return;
                if (!__instance.inVehicleAnimation) return;

                try
                {
                    // Find the vehicle the player is in
                    var vehicles = UnityEngine.Object.FindObjectsOfType<VehicleController>();
                    foreach (var vehicle in vehicles)
                    {
                        if (vehicle.currentDriver == __instance)
                        {
                            // Apply input corruption through reflection if needed
                            CorruptVehicleControlsAdvanced(vehicle);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in vehicle input corruption: {ex.Message}");
                }
            }

            private static void CorruptVehicleControlsAdvanced(VehicleController vehicle)
            {
                if (vehicle == null) return;

                // Additional corruption for milestone 5 (terminal stage)
                if (currentDeathCount >= 9)
                {
                    // Randomly disable brakes entirely (5% chance per frame)
                    if (rnd.Next(0, 100) < 5)
                    {
                        vehicle.brakePedalPressed = false;
                    }

                    // Add random steering jolts
                    if (rnd.Next(0, 100) < 8)
                    {
                        float joltDirection = rnd.Next(0, 2) == 0 ? -1f : 1f;
                        vehicle.steeringInput = joltDirection * UnityEngine.Random.Range(0.5f, 1f);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "OnDestroy")]
        [HarmonyPostfix]
        private static void ResetOnNewGame()
        {
            if (localPlayerDebuffs != null)
            {
                localPlayerDebuffs.Reset();
                localPlayerDebuffs = null;
            }
            currentDeathCount = 0;
            previousDeathCount = 0;
            roundsSurvivedSinceDeathReduction = 0;
            Debug.Log("Deterioration system reset for new game");
        }

        [HarmonyPatch(typeof(StartOfRound), "EndGameClientRpc")]
        [HarmonyPostfix]
        private static void OnRoundEnd(StartOfRound __instance, int playerClientId)
        {
            // Check if the round ended due to an anomaly (like fire exit or other special conditions)
            // We can check various conditions to determine if this was a "successful" round
            bool wasAnomalyExit = false;

            // Check if fire exit was used or other anomaly conditions
            if (__instance != null)
            {
                // If the level is "Liquidation" or similar, it might be an anomaly
                wasAnomalyExit = __instance.currentLevel?.name?.Contains("Liquidation") == true;
            }

            if (wasAnomalyExit)
            {
                roundsSurvivedSinceDeathReduction = 0;
                return;
            }

            roundsSurvivedSinceDeathReduction++;

            // Reduce death count every 3 successful rounds
            if (roundsSurvivedSinceDeathReduction >= 3)
            {
                if (currentDeathCount > 0)
                {
                    currentDeathCount--;
                    Debug.Log($"Death count reduced to {currentDeathCount} after surviving 3 rounds");
                }
                roundsSurvivedSinceDeathReduction = 0;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayer")]
        [HarmonyPostfix]
        private static void DamagePlayerPatch(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            if (__instance != GameNetworkManager.Instance.localPlayerController ||
                causeOfDeath != CauseOfDeath.Unknown) return;

            // Add visual feedback when taking deterioration damage
            if (currentDeathCount >= 3)
            {
                __instance.StartCoroutine(VisorImpactEffect());
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayer")]
        [HarmonyPostfix]
        private static void KillPlayerPatch(PlayerControllerB __instance)
        {
            if (__instance == GameNetworkManager.Instance.localPlayerController)
            {
                currentDeathCount++;
                roundsSurvivedSinceDeathReduction = 0;
                Debug.Log($"Player died. Death count: {currentDeathCount}");
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
        [HarmonyPostfix]
        private static void LateUpdatePatch(PlayerControllerB __instance)
        {
            if (__instance != GameNetworkManager.Instance.localPlayerController ||
                !__instance.isPlayerControlled ||
                __instance.isPlayerDead) return;

            // Initialize deterioration system if needed
            localPlayerDebuffs ??= new DeteriorationEffects(__instance);

            // Apply deterioration level changes
            if (currentDeathCount != previousDeathCount)
            {
                localPlayerDebuffs.ApplyDeteriorationLevel(currentDeathCount);
                previousDeathCount = currentDeathCount;
            }

            // Update deterioration effects
            localPlayerDebuffs.Update(currentDeathCount);
        }

        // Alternative patch for round completion - this might be more reliable
        [HarmonyPatch(typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel")]
        [HarmonyPostfix]
        private static void OnLevelComplete()
        {
            // This gets called when a level is completed successfully
            if (localPlayerDebuffs != null)
            {
                roundsSurvivedSinceDeathReduction++;

                if (roundsSurvivedSinceDeathReduction >= 3)
                {
                    if (currentDeathCount > 0)
                    {
                        currentDeathCount--;
                        Debug.Log($"Death count reduced to {currentDeathCount} after surviving 3 rounds");
                    }
                    roundsSurvivedSinceDeathReduction = 0;
                }
            }
        }

        #endregion
    }
}
