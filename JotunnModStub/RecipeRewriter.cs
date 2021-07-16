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

namespace RecipeRewriter
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    internal class RecipeRewriter : BaseUnityPlugin
    {
        public const string PluginGUID = "firoso.reciperewriter";
        public const string PluginName = "RecipeRewriter";
        public const string PluginVersion = "0.0.2";

        private void Awake()
        {
            ItemManager.OnItemsRegistered += InitializeItems;
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
                    Logger.LogError(overallException);
                }
            }
        }

        private void ProcessRecipeRewriteFile(string jsonDocumentFilePath)
        {
            Logger.LogMessage($"Processing candidate RecipeRewriter file at {jsonDocumentFilePath}");

            using (StreamReader reader = File.OpenText(jsonDocumentFilePath))
            {
                JObject o = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                JArray recipes = o.Properties().SingleOrDefault(p => p.Name.Equals("recipes", StringComparison.InvariantCultureIgnoreCase)).Value as JArray;

                foreach (JObject recipe in recipes.Cast<JObject>())
                {
                    try
                    {
                        // match a recipe
                        var matchProperty = recipe.Property("match", StringComparison.InvariantCultureIgnoreCase).Value.Value<JObject>();

                        var nameProperty = matchProperty.Property("name", StringComparison.InvariantCultureIgnoreCase);
                        if (nameProperty != null)
                        {
                            Logger.LogMessage($"Found Recipe Rewrite for '{nameProperty.Value}'");
                        }

                        int amountRequirement = 0;
                        var amountProperty = matchProperty.Property("amount", StringComparison.InvariantCultureIgnoreCase);
                        if (amountProperty != null)
                        {
                            Logger.LogMessage($"Found Requirement 'amount' of '{amountProperty.Value.Value<int>()}'");
                            amountRequirement = amountProperty.Value.Value<int>();
                        }

                        string recipeItemDropName = nameProperty.Value.Value<string>();
                        
                        Logger.LogMessage($"Looking for a recipe to match '{recipeItemDropName}'");

                        Recipe recipeToModify = ObjectDB.instance.m_recipes.SingleOrDefault(r => (r.m_item?.name?.Equals(recipeItemDropName) ?? false) && (amountProperty==null?true:r.m_amount==amountRequirement));
                        Logger.LogMessage($"Found the recipe named '{recipeToModify.name}'");
                        foreach (JProperty property in recipe.Properties().Where(p => !p.Name.Equals("name", StringComparison.InvariantCultureIgnoreCase)))
                        {
                            Logger.LogMessage($"Processing '{property.Name}'");

                            switch (property.Name)
                            {
                                case "amount":
                                    var baseAmount = property.Value.Value<int>();
                                    recipeToModify.m_amount = baseAmount;
                                    Logger.LogMessage($"Setting amount to '{baseAmount}'");
                                    break;
                                case "enabled":
                                    var enabled = property.Value.Value<bool>();
                                    recipeToModify.m_enabled = enabled;
                                    Logger.LogMessage($"Setting enabled to '{enabled}'");
                                    break;
                                case "minStationLevel":
                                    var minStationLevel = property.Value.Value<int>();
                                    recipeToModify.m_minStationLevel = minStationLevel;
                                    Logger.LogMessage($"Setting minStationLevel to '{minStationLevel}'");
                                    break;
                                case "craftingStation":
                                    string craftingStationName = property.Value.Value<string>();
                                    Logger.LogMessage($"Looking up craftingStation {craftingStationName}");

                                    CraftingStation craftingStation = GetCraftingStationByName(craftingStationName);

                                    if (craftingStation != null)
                                    {
                                        recipeToModify.m_craftingStation = craftingStation;
                                        Logger.LogMessage($"Setting craftingStation to {craftingStation.name}");
                                    }
                                    else
                                    {
                                        Logger.LogError($"Unable to resolve craftingStation to {craftingStationName}");
                                    }

                                    break;
                                case "repairStation":
                                    var repairStationName = property.Value<string>();
                                    Logger.LogMessage($"Looking up repairStation {repairStationName}");

                                    CraftingStation repairStation = GetCraftingStationByName(repairStationName);

                                    if (repairStation != null)
                                    {
                                        Logger.LogMessage($"Setting repairStation to {repairStation.name}");
                                        recipeToModify.m_repairStation = repairStation;
                                    }
                                    else
                                    {
                                        Logger.LogError($"Unable to resolve repairStation to {repairStationName}");
                                    }

                                    break;
                                case "resources":
                                    JArray resources = (JArray)property.Value;

                                    var newResouces = resources.Cast<JObject>().Select(resource =>
                                    {
                                        string name = resource.Property("name")?.Value.Value<string>();
                                        int amount = resource.Property("amount")?.Value.Value<int>() ?? 0;
                                        int amountPerLevel = resource.Property("amountPerLevel")?.Value.Value<int>() ?? 0;
                                        bool recover = resource.Property("recover")?.Value.Value<bool>() ?? false;

                                        Logger.LogMessage($"Resource | name: {name} | amount: {amount} | amountPerLevel: {amountPerLevel} | recover: {recover}");
                                        return new Piece.Requirement { m_amount = amount, m_amountPerLevel = amountPerLevel, m_recover = recover, m_resItem = GetItemDropByName(name) };
                                    }).ToArray();

                                    recipeToModify.m_resources = newResouces;

                                    break;
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex);
                    }
                }
            }
        }

        private ItemDrop GetItemDropByName(string name)
        {
            var itemPrefab = PrefabManager.Instance.GetPrefab(name);
            if (itemPrefab == null)
            {
                throw new InvalidOperationException($"Unable to resolve item drop by from name '{name}'");
            }
            var itemDrop = itemPrefab.GetComponent<ItemDrop>();
            if (itemDrop == null)
            {
                throw new InvalidOperationException($"Unable to resolve ItemDrop component on item named '{name}'");
            }

            return itemDrop;
        }

        private CraftingStation GetCraftingStationByName(string craftingStationName)
        {
            Logger.LogMessage($"Looking Up Crafting Station {craftingStationName}!");
            var craftingStationPrefab = PrefabManager.Instance.GetPrefab(craftingStationName);
            Logger.LogMessage($"Got prefab {craftingStationPrefab?.name}!");
            Logger.LogMessage($"Getting CraftingStation component.");
            var craftingStation = craftingStationPrefab?.GetComponent<CraftingStation>();
            Logger.LogMessage($"CraftingStation is {craftingStation?.name ?? "Null"}");
            return craftingStation;
        }
    }
}