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

namespace RecipeRewriter
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class RecipeRewriter : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.jotunnmodstub";
        public const string PluginName = "JotunnModStub";
        public const string PluginVersion = "0.0.1";

        private void Awake()
        {
            ItemManager.OnItemsRegistered += InitializeItems;
        }

        private void InitializeItems()
        {
            using (StreamReader reader = File.OpenText($"{BepInEx.Paths.PluginPath}/RecipeRewriter/test.json"))
            {
                JObject o = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                JArray recipes = o.Properties().SingleOrDefault(p => p.Name.Equals("recipes", StringComparison.InvariantCultureIgnoreCase)).Value as JArray;

                foreach (JObject recipe in recipes.Cast<JObject>())
                {
                    var nameProperty = recipe.Property("name", StringComparison.InvariantCultureIgnoreCase);
                    if (nameProperty != null)
                    {
                        Logger.LogMessage("Name Property exists!");
                        Logger.LogMessage($"Name Value is {nameProperty.Value}!");
                    }

                    string recipeItemDropName = nameProperty.Value.Value<string>();
                    Logger.LogMessage($"Looking for a recipe to match {recipeItemDropName}");

                    Recipe recipeToModify = ObjectDB.instance.m_recipes.SingleOrDefault(r => r.m_item?.name?.Equals(recipeItemDropName) ?? false);
                    Logger.LogMessage($"Found {recipeToModify.name}");
                    foreach (JProperty property in recipe.Properties().Where(p => !p.Name.Equals("name", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        Logger.LogMessage($"Processing {property.Name}");

                        switch (property.Name)
                        {
                            case "amount":
                                recipeToModify.m_amount = property.Value.Value<int>();
                                break;
                            case "enabled":
                                recipeToModify.m_enabled = property.Value.Value<bool>();
                                break;
                            case "minStationLevel":
                                recipeToModify.m_minStationLevel = property.Value.Value<int>();
                                break;
                            case "craftingStation":
                                string craftingStationName = property.Value.Value<string>();
                                Logger.LogMessage($"Getting craftingStation {craftingStationName}");
                                
                                CraftingStation craftingStation = GetCraftingStationByName(craftingStationName);
                                    
                                if (craftingStation != null)
                                {
                                    recipeToModify.m_craftingStation = craftingStation;
                                    Logger.LogMessage($"Setting craftingStation to {craftingStation.name}");
                                }
                                
                                break;
                            case "repairStation":
                                var repairStationName = property.Value<string>();

                                CraftingStation repairStation = GetCraftingStationByName(repairStationName);

                                if (repairStation != null)
                                {
                                    Logger.LogMessage($"Setting repairStation to {repairStation.name}");
                                    recipeToModify.m_repairStation = repairStation;
                                }

                                break;
                            case "resources":
                                var resources = property.Value;

                                var newResouces = resources.Cast<JObject>().Select(resource =>
                                    {
                                        string name = resource.Property("name")?.Value.Value<string>();
                                        int amount = resource.Property("amount")?.Value.Value<int>() ?? 0;
                                        int amountPerLevel = resource.Property("amountPerLevel")?.Value.Value<int>() ?? 0;
                                        bool recover = resource.Property("recover")?.Value.Value<bool>() ?? false;
                                        Logger.LogMessage($"Resource | {name} | {amount} | {amountPerLevel} | {recover}");
                                        return new Piece.Requirement { m_amount = amount, m_amountPerLevel = amountPerLevel, m_recover = recover, m_resItem = GetItemDropByName(name) };
                                    }).ToArray();

                                recipeToModify.m_resources = newResouces;
                                
                                break;
                        }

                    }
                }
            }
        }

        private ItemDrop GetItemDropByName(string name)
        {
            var itemPrefab = PrefabManager.Instance.GetPrefab(name);
            var itemDrop = itemPrefab.GetComponent<ItemDrop>();

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

#if DEBUG
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
            { // Set a breakpoint here to break on F6 key press
            }
        }
#endif
    }
}