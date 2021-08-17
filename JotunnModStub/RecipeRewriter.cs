// JotunnModStub
// a Valheim mod skeleton using Jötunn
// 
// File:    JotunnModStub.cs
// Project: JotunnModStub

using BepInEx;
using UnityEngine;
using BepInEx.Configuration;
using Jotunn.Utils;
using Jotunn.Managers;
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using static Heightmap;

namespace RecipeRewriter
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class RecipeRewriter : BaseUnityPlugin
    {
        public const string PluginGUID = "firoso.reciperewriter";
        public const string PluginName = "RecipeRewriter";
        public const string PluginVersion = "0.1.0";

        public static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            ItemManager.OnItemsRegistered += InitializeItems;
            Log = this.Logger;
        }

        private void InitializeItems()
        {
            foreach (var jsonDocument in Directory.GetFiles(new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    ProcessRecipeRewriteFile(jsonDocument);
                }
                catch (Exception overallException)
                {
                    Log.LogError(overallException);
                }
            }
        }

        private void ProcessRecipeRewriteFile(string jsonDocumentFilePath)
        {
            Log.LogMessage($"Processing candidate RecipeRewriter file at {jsonDocumentFilePath}");

            using (StreamReader reader = File.OpenText(jsonDocumentFilePath))
            {
                JObject o = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                JArray recipes = o.Properties().SingleOrDefault(p => p.Name.Equals("recipes", StringComparison.InvariantCultureIgnoreCase)).Value as JArray;

                if (recipes != null)
                {
                    ProcessRewrites<Recipe>(recipes, GetMatchingRecipeFromObjectDb, UpdateRecipeFromJsonObject);
                }

                JArray pieces = o.Properties().SingleOrDefault(p => p.Name.Equals("pieces", StringComparison.InvariantCultureIgnoreCase)).Value as JArray;

                if (pieces != null)
                {
                    ProcessRewrites<Piece>(pieces, GetMatchingPieceFromObjectDb, UpdatePieceFromJsonObject);
                }
            }
        }

        private void ProcessRewrites<TRewrittenComponent>(JArray rewrites, Func<Match, TRewrittenComponent> componentLocator, Action<JObject, TRewrittenComponent> componentUpdater)
        {
            foreach (JObject recipeJObject in rewrites.Cast<JObject>())
            {
                try
                {
                    Match match = Match.FromJsonObjectWithMatchProperty(recipeJObject);
                    Log.LogMessage($"Looking for a rewrite to match '{match.Name}' using {componentLocator.Method.Name}");
                    TRewrittenComponent componentToModify = componentLocator(match);
                    Log.LogMessage($"Found a matching component.");
                    componentUpdater(recipeJObject, componentToModify);
                }
                catch (Exception ex)
                {
                    Log.LogError(ex);
                }
            }
        }

        private Recipe GetMatchingRecipeFromObjectDb(Match match)
        {
            if (match.Name == null)
            {
                throw new InvalidOperationException("Recipe lookup requires the 'name' property to be set for the 'match' argument.");
            }

            return ObjectDB.instance.m_recipes
                .SingleOrDefault(r => (r.m_item?.name?.Equals(match.Name) ?? false) && (match.Amount == null ? true : r.m_amount == match.Amount));
        }

        private Piece GetMatchingPieceFromObjectDb(Match match)
        {
            if (match.Name == null)
            {
                throw new InvalidOperationException("Piece lookup requires the 'name' property to be set for the 'match' argument.");
            }

            if (match.BuildTool == null)
            {
                throw new InvalidOperationException("Piece lookup requires the 'buildTool' property to be set for the 'match' argument.");
            }

            var itemDrop = GetComponentFromPrefab<ItemDrop>(match.BuildTool);

            var pieceTable = itemDrop.m_itemData.m_shared.m_buildPieces;

            var piecePrefab = pieceTable.m_pieces.Find(piece => piece.name.Equals(match.Name, StringComparison.InvariantCultureIgnoreCase));

            return GetComponentFromPrefab<Piece>(piecePrefab);
        }


        private void UpdateRecipeFromJsonObject(JObject recipe, Recipe recipeToModify)
        {
            foreach (JProperty property in recipe.Properties().Where(p => !p.Name.Equals("match", StringComparison.InvariantCultureIgnoreCase)))
            {
                Log.LogMessage($"Processing '{property.Name}'");

                switch (property.Name)
                {
                    case "amount":
                        var baseAmount = property.Value.Value<int>();
                        recipeToModify.m_amount = baseAmount;
                        Log.LogMessage($"Setting amount to '{baseAmount}'");
                        break;
                    case "enabled":
                        var enabled = property.Value.Value<bool>();
                        recipeToModify.m_enabled = enabled;
                        Log.LogMessage($"Setting enabled to '{enabled}'");
                        break;
                    case "minStationLevel":
                        var minStationLevel = property.Value.Value<int>();
                        recipeToModify.m_minStationLevel = minStationLevel;
                        Log.LogMessage($"Setting minStationLevel to '{minStationLevel}'");
                        break;
                    case "craftingStation":
                        string craftingStationName = property.Value.Value<string>();
                        Log.LogMessage($"Looking up craftingStation {craftingStationName}");

                        CraftingStation craftingStation = GetComponentFromPrefab<CraftingStation>(craftingStationName);

                        if (craftingStation != null)
                        {
                            recipeToModify.m_craftingStation = craftingStation;
                            Log.LogMessage($"Setting craftingStation to {craftingStation.name}");
                        }
                        else
                        {
                            Log.LogError($"Unable to resolve craftingStation to {craftingStationName}");
                        }

                        break;
                    case "repairStation":
                        var repairStationName = property.Value<string>();
                        Log.LogMessage($"Looking up repairStation {repairStationName}");

                        CraftingStation repairStation = GetComponentFromPrefab<CraftingStation>(repairStationName);

                        if (repairStation != null)
                        {
                            Log.LogMessage($"Setting repairStation to {repairStation.name}");
                            recipeToModify.m_repairStation = repairStation;
                        }
                        else
                        {
                            Log.LogError($"Unable to resolve repairStation to {repairStationName}");
                        }

                        break;
                    case "resources":
                        JArray resources = (JArray)property.Value;
                        Piece.Requirement[] requirements = GetRequirementsFromJsonResources(resources);

                        recipeToModify.m_resources = requirements;

                        break;
                }

            }
        }

        private Piece.Requirement[] GetRequirementsFromJsonResources(JArray resources) => 
            resources
                .Cast<JObject>()
                .Select(resource =>
                {
                    string name = resource.Property("name")?.Value.Value<string>();
                    int amount = resource.Property("amount")?.Value.Value<int>() ?? 0;
                    int amountPerLevel = resource.Property("amountPerLevel")?.Value.Value<int>() ?? 0;
                    bool recover = resource.Property("recover")?.Value.Value<bool>() ?? false;

                    Log.LogMessage($"Resource | name: {name} | amount: {amount} | amountPerLevel: {amountPerLevel} | recover: {recover}");
                    return new Piece.Requirement { m_amount = amount, m_amountPerLevel = amountPerLevel, m_recover = recover, m_resItem = GetComponentFromPrefab<ItemDrop>(name) };
                })
                .ToArray();

        private void UpdatePieceFromJsonObject(JObject pieceJObject, Piece pieceToModify)
        {
            foreach (JProperty property in pieceJObject.Properties().Where(p => !p.Name.Equals("match", StringComparison.InvariantCultureIgnoreCase)))
            {
                Log.LogMessage($"Processing '{property.Name}'");

                switch (property.Name)
                {
                    case "enabled":
                        var enabled = property.Value.Value<bool>();
                        pieceToModify.m_enabled = enabled;
                        Log.LogMessage($"Setting enabled to '{enabled}'");
                        break;
                    case "craftingStation":
                        string craftingStationName = property.Value.Value<string>();
                        Log.LogMessage($"Looking up craftingStation {craftingStationName}");

                        CraftingStation craftingStation = GetComponentFromPrefab<CraftingStation>(craftingStationName);

                        if (craftingStation != null)
                        {
                            pieceToModify.m_craftingStation = craftingStation;
                            Log.LogMessage($"Setting craftingStation to {craftingStation.name}");
                        }
                        else
                        {
                            Log.LogError($"Unable to resolve craftingStation to {craftingStationName}");
                        }

                        break;
                    case "biomes":
                        JArray biomes = (JArray)property.Value;
                        Biome targetBiomes = Biome.None;
                        foreach (var biome in biomes)
                        {
                            Biome biomeValue = (Biome)Enum.Parse(typeof(Biome), biome.Value<string>());
                            targetBiomes = targetBiomes | biomeValue;
                        }
                        pieceToModify.m_onlyInBiome = targetBiomes;
                        break;
                    case "resources":
                        JArray resources = (JArray)property.Value;
                        Piece.Requirement[] requirements = GetRequirementsFromJsonResources(resources);
                        pieceToModify.m_resources = requirements;
                        break;
                }
            }
        }

        private GameObject GetPrefabByName(string name)
        {
            var itemPrefab = PrefabManager.Instance.GetPrefab(name);
            if (itemPrefab == null)
            {
                throw new InvalidOperationException($"Unable to resolve prefab by from name '{name}'");
            }

            return itemPrefab;
        }

        private T GetComponentFromPrefab<T>(GameObject prefab)
        {
            var component = prefab.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"Unable to resolve '{typeof(T)}' component on item named '{prefab.name}'");
            }

            return component;
        }

        private T GetComponentFromPrefab<T>(string prefabName)
        {
            return GetComponentFromPrefab<T>(GetPrefabByName(prefabName));
        }
    }
}