using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;


namespace LegacyBlockLoader
{
    public class UnofficialBlock
    {
        public readonly ModdedBlockDefinition blockDefinition;
        public readonly JObject jObject;
        public readonly int ID;

        private interface EnumParser {
            object ParseEnum(int val, object defaultValue);
        }
        private class EnumParser<T> : EnumParser
        {
            Dictionary<int, T> mappingDict;
            public object ParseEnum(int val, object defaultValue)
            {
                if (mappingDict is null)
                {
                    Array values = Enum.GetValues(typeof(T));
                    mappingDict = new Dictionary<int, T>();
                    foreach (T value in values)
                    {
                        mappingDict[Convert.ToInt32(value)] = value;
                    }
                }
                if (mappingDict.TryGetValue(val, out T result))
                {
                    return result;
                }
                return defaultValue;
            }
        }
        private static Dictionary<Type, EnumParser> EnumDict = new Dictionary<Type, UnofficialBlock.EnumParser>();

        internal static string Format(string input)
        {
            // JavaScriptSerializer doesn't accept commented-out JSON,
            // so we'll strip them out ourselves;
            input = Regex.Replace(input, @"^\s*//.*$", "", RegexOptions.Multiline);  // removes line comments like this
            input = Regex.Replace(input, @"/\*(\s|\S)*?\*/", "", RegexOptions.Multiline); /* comments like this */
            input = Regex.Replace(input, @"([,\[\{\]\}\." + Regex.Escape("\"") + @"0-9]|null)\s*//[^\n]*\n", "$1\n", RegexOptions.Multiline);    // Removes mixed JSON comments
            input = Regex.Replace(input, @",\s*([\}\]])", "\n$1", RegexOptions.Multiline);  // remove trailing ,
            return input.Replace("JSONBLOCK", "Deserializer");
        }

        private static T TryParseEnum<T>(int val, T defaultValue) where T : Enum
        {
            if (EnumDict.TryGetValue(typeof(T), out EnumParser parser))
            {
                return (T) parser.ParseEnum(val, defaultValue);
            }
            else
            {
                parser = new EnumParser<T>();
                EnumDict.Add(typeof(T), parser);
                return (T)parser.ParseEnum(val, defaultValue);
            }
        }

        public UnofficialBlock(string path)
        {
            string fileParsed;
            try
            {
                string raw = File.ReadAllText(path);
                JObject json = JObject.Parse(raw);
                fileParsed = DirectoryBlockLoader.ResolveFiles(json.ToString(), path).Trim();
                // BlockLoaderMod.logger.Trace(fileParsed);
            }
            catch (Exception e)
            {
                BlockLoaderMod.logger.Error(e, "FAILED to parse file " + path);
                throw e;
            }

            try
            {
                BlockLoaderMod.logger.Trace($"Preparing to parse file:\n{fileParsed}");
                this.jObject = JObject.Parse(fileParsed);
                UnofficialBlockDefinition unofficialDef = this.jObject.ToObject<UnofficialBlockDefinition>(new JsonSerializer() { MissingMemberHandling = MissingMemberHandling.Ignore });
                FactionSubTypes corpType = TryParseEnum<FactionSubTypes>(unofficialDef.Faction, FactionSubTypes.GSO);
                if (corpType == FactionSubTypes.NULL)
                {
                    corpType = FactionSubTypes.GSO;
                }
                BlockLoaderMod.logger.Info($"Read mod as {unofficialDef.ID}, {unofficialDef.Name}, {unofficialDef.Description} for corp {corpType}");

                this.ID = unofficialDef.ID;
                if (unofficialDef.Name is null || unofficialDef.Name.Length == 0)
                {
                    unofficialDef.Name = ID.ToString();
                }
                unofficialDef.Grade++;  // Add 1 to Grade, b/c Legacy grade is 0-indexed, official blocks are 1-indexed

                this.jObject = JObject.Parse(fileParsed);
                JProperty Grade = jObject.Property("Grade");
                if (Grade != null)
                {
                    Grade.Value = unofficialDef.Grade;
                }
                else
                {
                    jObject.Add("Grade", unofficialDef.Grade);
                }

                JProperty Category = jObject.Property("Category");
                BlockCategories blockCategory = TryParseEnum<BlockCategories>(unofficialDef.Category, BlockCategories.Standard);
                if (blockCategory == BlockCategories.Null)
                {
                    blockCategory = BlockCategories.Standard;
                }
                if (Category != null)
                {
                    Category.Value = blockCategory.ToString();
                }

                JProperty Rarity = jObject.Property("Rarity");
                BlockRarity blockRarity = TryParseEnum<BlockRarity>(unofficialDef.Rarity, BlockRarity.Common);
                if (Rarity != null)
                {
                    Rarity.Value = blockRarity.ToString();
                }

                this.blockDefinition = ScriptableObject.CreateInstance<ModdedBlockDefinition>();
                this.blockDefinition.m_BlockIdentifier = this.ID.ToString();
                this.blockDefinition.m_BlockDisplayName = unofficialDef.Name;
                this.blockDefinition.m_BlockDescription = unofficialDef.Description;
                this.blockDefinition.m_Corporation = corpType.ToString();
                this.blockDefinition.m_Category = blockCategory;
                this.blockDefinition.m_Rarity = blockRarity;
                this.blockDefinition.m_Grade = unofficialDef.Grade;
                this.blockDefinition.m_Price = unofficialDef.Price;
                this.blockDefinition.m_UnlockWithLicense = true;
                this.blockDefinition.m_DamageableType = TryParseEnum<ManDamage.DamageableType>(unofficialDef.DamageableType, ManDamage.DamageableType.Standard);
                this.blockDefinition.m_Mass = unofficialDef.Mass;
                this.blockDefinition.name = unofficialDef.Name;

                BlockLoaderMod.logger.Info($"Injecting into Corp {this.blockDefinition.m_Corporation}, Grade: {this.blockDefinition.m_Grade}");

                GameObject prefab = new GameObject($"{unofficialDef.Name}_Prefab");
                prefab.AddComponent<MeshFilter>();
                prefab.AddComponent<MeshRenderer>();
                prefab.AddComponent<TankBlockTemplate>();
                prefab.AddComponent<BoxCollider>();
                prefab.SetActive(false);
                this.blockDefinition.m_PhysicalPrefab = prefab.GetComponent<TankBlockTemplate>();
                this.WrapJSON();
            }
            catch (Exception e)
            {
                BlockLoaderMod.logger.Error(e, "FAILED to read JSON");
                throw e;
            }
        }
        public UnofficialBlock(FileInfo file) : this(file.FullName) { }

        public void WrapJSON()
        {
            JObject wrappedJSON = new JObject();
            try
            {
                this.jObject.Add("AutoImported", true);
            }
            catch (ArgumentException)
            {
                // this is fine - means AutoImported is already set
            }
            wrappedJSON.Add("NuterraBlock", this.jObject);
            this.blockDefinition.m_Json = new UnityEngine.TextAsset(wrappedJSON.ToString());
        }
    }
}
