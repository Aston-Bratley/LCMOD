using HarmonyLib;
using LethalLevelLoader;
using LethalLib;
using LethalLib.Modules;
using MrovLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace CameraLibrary.AI_Levels
{
    [HarmonyPatch]
    public class AILevel
    {
        private static bool hasModifiedLevels = false;

        // Configuration - MOVE THIS TO A CONFIG FILE OR ENVIRONMENT VARIABLE
        private const string GEMINI_API_KEY = "AIzaSyAuVb7ke0JE7skw3W1KVIHSeQALx-ImJaY"; // Replace with your actual API key
        private const string GEMINI_MODEL = "gemini-2.5-flash";
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/";

        public static async void ModifyExtendedLevels(StartOfRound startOfRound)
        {
            if (hasModifiedLevels) return;
            hasModifiedLevels = true;

            SelectableLevel[] allSelectableLevels = startOfRound.levels;
            CameraLibrary.Logger.LogError($"[CameraLibrary] Found {allSelectableLevels.Length} SelectableLevel objects");

            try
            {
                if (string.IsNullOrEmpty(GEMINI_API_KEY) || GEMINI_API_KEY == "YOUR_API_KEY_HERE")
                {
                    CameraLibrary.Logger.LogError("[CameraLibrary] No valid API key configured. Applying default modifications.");
                    ApplyDefaultModifications(allSelectableLevels);
                    return;
                }

                var moonDataList = GatherMoonData(allSelectableLevels);
                CameraLibrary.Logger.LogError($"[CameraLibrary] Gathered data for {moonDataList.Count} moons");

                var modifiedMoonData = await ProcessMoonDataWithAI(moonDataList);

                if (modifiedMoonData != null && modifiedMoonData.Count > 0)
                {
                    ApplyMoonModifications(allSelectableLevels, modifiedMoonData);
                    CameraLibrary.Logger.LogError("[CameraLibrary] Successfully applied AI-generated moon modifications");
                }
                else
                {
                    CameraLibrary.Logger.LogError("[CameraLibrary] No valid modifications received from AI. Applying default modifications.");
                    ApplyDefaultModifications(allSelectableLevels);
                }
            }
            catch (Exception ex)
            {
                CameraLibrary.Logger.LogError($"[CameraLibrary] Error during AI moon modification: {ex.Message}");
                CameraLibrary.Logger.LogError($"[CameraLibrary] Stack trace: {ex.StackTrace}");
                ApplyDefaultModifications(allSelectableLevels);
            }
        }

        private static List<MoonData> GatherMoonData(SelectableLevel[] levels)
        {
            var moonDataList = new List<MoonData>();

            foreach (SelectableLevel level in levels)
            {
                try
                {
                    var extendedLevel = LevelManager.GetExtendedLevel(level);
                    var moonData = new MoonData
                    {
                        MoonName = level.PlanetName,
                        CurrentPrice = extendedLevel?.RoutePrice ?? 0,
                        CurrentDifficulty = level.riskLevel.ToString(),
                        CurrentMaxScrap = level.maxScrap,
                        CurrentMinScrap = level.minScrap,
                        CurrentMinScrapValue = level.minTotalScrapValue,
                        CurrentMaxScrapValue = level.maxTotalScrapValue,
                        CurrentFacilitySizeMultiplier = level.factorySizeMultiplier,
                        WeatherConditions = level.currentWeather.ToString(),
                        HasFactory = level.spawnEnemiesAndScrap
                    };

                    moonDataList.Add(moonData);
                    CameraLibrary.Logger.LogError($"[CameraLibrary] Gathered data for moon: {moonData.MoonName}");
                }
                catch (Exception ex)
                {
                    CameraLibrary.Logger.LogError($"[CameraLibrary] Error gathering data for level {level.PlanetName}: {ex.Message}");
                }
            }

            return moonDataList;
        }

        private static async Task<List<ModifiedMoonData>> ProcessMoonDataWithAI(List<MoonData> moonDataList)
        {
            CameraLibrary.Logger.LogError("[CameraLibrary] Starting AI processing...");

            using (var geminiClient = new GeminiAIClient(GEMINI_API_KEY, GEMINI_MODEL))
            {
                string prompt = CreateAIPrompt(moonDataList);
                var schema = CreateMoonModificationSchema();

                CameraLibrary.Logger.LogError($"[CameraLibrary] Sending prompt to AI (length: {prompt.Length} characters)");

                var aiResponse = await geminiClient.GenerateJsonAsync(prompt, schema);

                CameraLibrary.Logger.LogError($"[CameraLibrary] AI Response received. Null: {aiResponse == null}");

                if (aiResponse != null)
                {
                    CameraLibrary.Logger.LogError($"[CameraLibrary] AI Response keys: {string.Join(", ", aiResponse.Keys)}");

                    if (aiResponse.ContainsKey("modified_moons"))
                    {
                        var moonList = JsonConvert.DeserializeObject<List<ModifiedMoonData>>(
                            JsonConvert.SerializeObject(aiResponse["modified_moons"]));

                        CameraLibrary.Logger.LogError($"[CameraLibrary] Successfully parsed {moonList?.Count ?? 0} modified moons");
                        return moonList ?? new List<ModifiedMoonData>();
                    }
                    else
                    {
                        CameraLibrary.Logger.LogError("[CameraLibrary] AI response missing 'modified_moons' key");
                    }
                }

                var lastError = geminiClient.GetLastError();
                if (!string.IsNullOrEmpty(lastError))
                {
                    CameraLibrary.Logger.LogError($"[CameraLibrary] Gemini client error: {lastError}");
                }

                var lastResponse = geminiClient.GetLastResponse();
                if (lastResponse != null)
                {
                    try
                    {
                        var responseJson = JsonConvert.SerializeObject(lastResponse, Formatting.Indented);
                        CameraLibrary.Logger.LogError($"[CameraLibrary] Raw AI response: {responseJson}");
                    }
                    catch (Exception ex)
                    {
                        CameraLibrary.Logger.LogError($"[CameraLibrary] Could not serialize response: {ex.Message}");
                    }
                }

                throw new Exception($"AI response was invalid or empty. Last error: {lastError}");
            }
        }


private static string CreateAIPrompt(List<MoonData> moonDataList)
        {
            var moonNames = moonDataList.Select(m => m.MoonName).ToList();
            var moonNamesJson = JsonConvert.SerializeObject(moonNames, Formatting.Indented);
            return $@"You are a game designer tasked with creating a fresh, balanced moon progression system for Lethal Company that offers meaningful strategic choices.
                        Moon names to configure: {moonNamesJson}

                        DESIGN PHILOSOPHY: Create a well-balanced risk/reward system that feels completely fresh while maintaining engaging gameplay progression. Each moon should offer distinct strategic value and create interesting decision points for players.

                        CORE DESIGN PRINCIPLES:
                        1. **Meaningful Progression**: Early moons teach mechanics, later moons test mastery
                        2. **Strategic Diversity**: Each difficulty tier should offer multiple viable strategies
                        3. **Risk/Reward Clarity**: Players should understand what they're getting into
                        4. **Interesting Choices**: No single 'best' moon at each tier - multiple valid options

                        MOON VARIETY ARCHETYPES (ensure each tier has different types):
                        - **Consistent Earners**: Reliable scrap ranges, moderate risk
                        - **High Stakes**: High variance (big potential, big risk)  
                        - **Efficiency Specialists**: Great scrap-to-time ratio for skilled teams
                        - **Learning Labs**: Forgiving but lower rewards, good for practice
                        - **Endgame Challenges**: Maximum difficulty with premium rewards

                        DIFFICULTY DISTRIBUTION:
                        - **Beginner Tier (D-, D, D+)**: 4-5 moons - Free access, forgiving, teach core mechanics
                        - **Intermediate Tier (C-, C, C+)**: 2-3 moons - 100-400 credits, introduce complexity  
                        - **Advanced Tier (B-, B, B+)**: 3-4 moons - 500-900 credits, require team coordination
                        - **Expert Tier (A-, A, A+)**: 2-3 moons - 1000-1500 credits, high-skill gameplay
                        - **Master Tier (S-, S, S+)**: 1-2 moons - 1600-2200 credits, ultimate challenges

                        BALANCED VALUE RANGES:
                        - facility_size_multiplier: 0.4-2.5 (meaningful variety without extremes)
                        - Scrap ranges should create 20-40% variance from min to max within each moon
                        - Price should reflect both difficulty and reward potential
                        - Each tier should have overlapping reward ranges to create choice

                        STRATEGIC CONSIDERATIONS:
                        - Include 'safe investment' options at each tier for risk-averse players
                        - Include 'high-risk, high-reward' options for aggressive players  
                        - Ensure facility size creates meaningful tactical decisions
                        - Balance scrap density vs facility size to create different optimal strategies

                        DESIGN GOALS:
                        - No moon should be strictly superior to another in its tier
                        - Players should debate which moon to choose based on their team's strengths
                        - Create natural learning curve from beginner to master tiers
                        - Reward different playstyles (cautious vs aggressive, fast vs thorough)

                        OUTPUT REQUIREMENTS:
                        Each moon configuration should feel intentionally designed with a clear strategic purpose. Avoid random assignments - every value should serve the moon's intended role in the progression system.

                        IMPORTANT: You must use the json_output function to provide your response in the exact JSON format specified in the schema.";
        }

        private static Dictionary<string, object> CreateMoonModificationSchema()
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["required"] = new[] { "modified_moons" },
                ["properties"] = new Dictionary<string, object>
                {
                    ["modified_moons"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["required"] = new[] {
                                "moon_name", "description", "price", "difficulty", "max_scrap", "min_scrap",
                                "min_scrap_total_value", "max_scrap_total_value", "facility_size_multiplier"
                            },
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["moon_name"] = new Dictionary<string, object> { ["type"] = "string" },
                                ["description"] = new Dictionary<string, object> { ["type"] = "string" },
                                ["price"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                                ["difficulty"] = new Dictionary<string, object>
                                {
                                    ["type"] = "string",
                                    ["enum"] = new[] { "D-", "D", "D+", "C-", "C", "C+", "B-", "B", "B+", "A-", "A", "A+", "S-", "S", "S+" }
                                },
                                ["max_scrap"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 1 },
                                ["min_scrap"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 1 },
                                ["min_scrap_total_value"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                                ["max_scrap_total_value"] = new Dictionary<string, object> { ["type"] = "integer", ["minimum"] = 0 },
                                ["facility_size_multiplier"] = new Dictionary<string, object> { ["type"] = "number", ["minimum"] = 0.1, ["maximum"] = 5.0 }
                            }
                        }
                    }
                }
            };
        }

        private static void ApplyMoonModifications(SelectableLevel[] levels, List<ModifiedMoonData> modifications)
        {
            foreach (var modification in modifications)
            {
                var level = levels.FirstOrDefault(l => l.PlanetName.Equals(modification.MoonName, StringComparison.OrdinalIgnoreCase));
                if (level == null)
                {
                    CameraLibrary.Logger.LogWarning($"Could not find level for moon: {modification.MoonName}");
                    continue;
                }

                try
                {
                    var extendedLevel = LevelManager.GetExtendedLevel(level);

                    // Apply modifications (enemy power stuff removed)
                    if (extendedLevel != null)
                        extendedLevel.RoutePrice = modification.Price;

                    level.maxScrap = modification.MaxScrap;
                    level.minScrap = modification.MinScrap;
                    level.minTotalScrapValue = modification.MinScrapTotalValue;
                    level.maxTotalScrapValue = modification.MaxScrapTotalValue;
                    level.factorySizeMultiplier = modification.FacilitySizeMultiplier;

                    CameraLibrary.Logger.LogError($"Modified {modification.MoonName}: {modification.Difficulty} difficulty, ${modification.Price} cost - {modification.Description}");
                }
                catch (Exception ex)
                {
                    CameraLibrary.Logger.LogError($"Error applying modifications to {modification.MoonName}: {ex.Message}");
                }
            }
        }

        private static void ApplyDefaultModifications(SelectableLevel[] levels)
        {
            CameraLibrary.Logger.LogWarning("Applying default modifications as fallback");

            foreach (SelectableLevel level in levels)
            {
                try
                {
                    var extendedLevel = LevelManager.GetExtendedLevel(level);
                    if (extendedLevel != null)
                        extendedLevel.RoutePrice = 500;
                }
                catch (Exception ex)
                {
                    CameraLibrary.Logger.LogError($"[CameraLibrary] Error applying default modification to {level.PlanetName}: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPostfix]
        private static void OnStartOfRoundAwake(StartOfRound __instance)
        {
            CameraLibrary.Logger.LogError("[CameraLibrary] StartOfRound.Awake called, modifying levels...");
            ModifyExtendedLevels(__instance);
        }
    }

    // Updated data classes with enemy power stuff removed
    public class MoonData
    {
        public string MoonName { get; set; }
        public int CurrentPrice { get; set; }
        public string CurrentDifficulty { get; set; }
        public int CurrentMaxScrap { get; set; }
        public int CurrentMinScrap { get; set; }
        public int CurrentMinScrapValue { get; set; }
        public int CurrentMaxScrapValue { get; set; }
        public float CurrentFacilitySizeMultiplier { get; set; }
        public string WeatherConditions { get; set; }
        public bool HasFactory { get; set; }
    }

    public class ModifiedMoonData
    {
        [JsonProperty("moon_name")]
        public string MoonName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("price")]
        public int Price { get; set; }

        [JsonProperty("difficulty")]
        public string Difficulty { get; set; }

        [JsonProperty("max_scrap")]
        public int MaxScrap { get; set; }

        [JsonProperty("min_scrap")]
        public int MinScrap { get; set; }

        [JsonProperty("min_scrap_total_value")]
        public int MinScrapTotalValue { get; set; }

        [JsonProperty("max_scrap_total_value")]
        public int MaxScrapTotalValue { get; set; }

        [JsonProperty("facility_size_multiplier")]
        public float FacilitySizeMultiplier { get; set; }
    }

    // Gemini AI Client (unchanged from previous version)
    public class GeminiAIClient : IDisposable
    {
        private readonly HttpClient client;
        private readonly string apiKey;
        private readonly string model;
        private readonly string apiUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
        private string lastError;
        private object lastResponse;
        private bool disposed = false;

        public GeminiAIClient(string apiKey, string model = "gemini-2.5-flash")
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception("API key is required.");

            this.client = new HttpClient();
            this.client.Timeout = TimeSpan.FromSeconds(60);
            this.apiKey = apiKey;
            this.model = model;
        }

        public async Task<Dictionary<string, object>> GenerateJsonAsync(string prompt, Dictionary<string, object> schema)
        {
            var tools = new[]
            {
        new
        {
            function_declarations = new[]
            {
                new
                {
                    name = "json_output",
                    description = "Formats the output as a JSON object that strictly adheres to the provided schema.",
                    parameters = schema
                }
            }
        }
    };

            var requestData = new
            {
                contents = new[]
                {
            new
            {
                role = "user",
                parts = new[] { new { text = prompt } }
            }
        },
                tool_config = new
                {
                    function_calling_config = new
                    {
                        mode = "ANY",
                        allowed_function_names = new[] { "json_output" }
                    }
                },
                tools = tools,
                system_instruction = new
                {
                    parts = new[]
                    {
                new
                {
                    text = "You are an expert game designer specializing in difficulty balancing and progression systems. Provide thoughtful, balanced modifications that enhance gameplay experience. You MUST use the json_output function to return your response."
                }
            }
                }
            };

            try
            {
                var response = await MakeRequestAsync(requestData, "generateContent");

                if (response == null)
                {
                    lastError = "No response received from API";
                    return null;
                }

                // More robust parsing using JsonConvert
                try
                {
                    var responseJson = JsonConvert.SerializeObject(response);
                    var responseObj = JsonConvert.DeserializeObject<GeminiResponse>(responseJson);

                    if (responseObj?.Candidates != null && responseObj.Candidates.Count > 0)
                    {
                        var candidate = responseObj.Candidates[0];
                        if (candidate?.Content?.Parts != null && candidate.Content.Parts.Count > 0)
                        {
                            var part = candidate.Content.Parts[0];
                            if (part?.FunctionCall?.Args != null)
                            {
                                // Convert the args to Dictionary<string, object>
                                var argsJson = JsonConvert.SerializeObject(part.FunctionCall.Args);
                                var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(argsJson);

                                CameraLibrary.Logger.LogError($"[CameraLibrary] Successfully parsed function call args: {string.Join(", ", args.Keys)}");
                                return args;
                            }
                        }
                    }
                }
                catch (Exception parseEx)
                {
                    CameraLibrary.Logger.LogError($"[CameraLibrary] JSON parsing error: {parseEx.Message}");
                }

                // Fallback: try the old method
                if (response.ContainsKey("candidates") && response["candidates"] is Newtonsoft.Json.Linq.JArray candidatesArray)
                {
                    var firstCandidate = candidatesArray[0] as Newtonsoft.Json.Linq.JObject;
                    var content = firstCandidate?["content"] as Newtonsoft.Json.Linq.JObject;
                    var parts = content?["parts"] as Newtonsoft.Json.Linq.JArray;
                    var part = parts?[0] as Newtonsoft.Json.Linq.JObject;
                    var functionCall = part?["functionCall"] as Newtonsoft.Json.Linq.JObject;
                    var args = functionCall?["args"] as Newtonsoft.Json.Linq.JObject;

                    if (args != null)
                    {
                        var argsDict = args.ToObject<Dictionary<string, object>>();
                        CameraLibrary.Logger.LogError($"[CameraLibrary] Fallback parsing successful: {string.Join(", ", argsDict.Keys)}");
                        return argsDict;
                    }
                }

                lastError = "API did not return a valid function call with JSON arguments.";
                return null;
            }
            catch (Exception ex)
            {
                lastError = $"Request failed: {ex.Message}";
                CameraLibrary.Logger.LogError($"[CameraLibrary] Exception in GenerateJsonAsync: {ex}");
                return null;
            }
        }

        private async Task<Dictionary<string, object>> MakeRequestAsync(object data, string endpoint)
        {
            lastError = null;
            lastResponse = null;

            string url = $"{apiUrl}{model}:{endpoint}?key={apiKey}";

            try
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"API Error (HTTP {response.StatusCode}): {responseContent}";
                    return null;
                }

                var responseObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseContent);
                lastResponse = responseObject;

                return responseObject;
            }
            catch (Exception ex)
            {
                lastError = $"Request error: {ex.Message}";
                return null;
            }
        }

        public string GetLastError() => lastError;
        public object GetLastResponse() => lastResponse;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    client?.Dispose();
                }
                disposed = true;
            }
        }

        public class GeminiResponse
        {
            [JsonProperty("candidates")]
            public List<GeminiCandidate> Candidates { get; set; }
        }

        public class GeminiCandidate
        {
            [JsonProperty("content")]
            public GeminiContent Content { get; set; }
        }

        public class GeminiContent
        {
            [JsonProperty("parts")]
            public List<GeminiPart> Parts { get; set; }
        }

        public class GeminiPart
        {
            [JsonProperty("functionCall")]
            public GeminiFunctionCall FunctionCall { get; set; }
        }

        public class GeminiFunctionCall
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("args")]
            public object Args { get; set; }
        }
    }
}
