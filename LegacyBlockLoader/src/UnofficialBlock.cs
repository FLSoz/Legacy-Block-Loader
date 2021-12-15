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
        public ModdedBlockDefinition blockDefinition;
        public JObject jObject;
        public int ID;

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
            // Console.WriteLine(input);
            input = Regex.Replace(input, @"([,\[\{\]\}\." + Regex.Escape("\"") + @"0-9]|null)\s*//[^\n]*\n", "$1\n", RegexOptions.Multiline);    // Removes mixed JSON comments
            // Console.WriteLine(input);
            input = Regex.Replace(input, @",\s*([\}\]])", "\n$1", RegexOptions.Multiline);  // remove trailing ,
            // Console.WriteLine(input);
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
            string raw = File.ReadAllText(path);


            string fileParsed;
            try
            {
                JObject json = JObject.Parse(raw);
                File.ReadAllText(path);
                fileParsed = DirectoryBlockLoader.ResolveFiles(json.ToString(), path).Trim();
                // Console.WriteLine(fileParsed);
            }
            catch (Exception e)
            {
                Console.WriteLine("FAILED to parse files: \n" + raw);
                throw e;
            }

            try
            {
                this.jObject = JObject.Parse(fileParsed);
                UnofficialBlockDefinition unofficialDef = this.jObject.ToObject<UnofficialBlockDefinition>(new JsonSerializer() { MissingMemberHandling = MissingMemberHandling.Ignore });
                FactionSubTypes corpType = TryParseEnum<FactionSubTypes>(unofficialDef.Faction, FactionSubTypes.GSO);
                if (corpType == FactionSubTypes.NULL)
                {
                    corpType = FactionSubTypes.GSO;
                }
                Console.WriteLine($"[Nuterra] Read mod as {unofficialDef.ID}, {unofficialDef.Name}, {unofficialDef.Description} for corp {corpType}");

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

                Console.WriteLine($"[Nuterra] Injecting into Corp {this.blockDefinition.m_Corporation}, Grade: {this.blockDefinition.m_Grade}");

                GameObject prefab = new GameObject($"{unofficialDef.Name}_Prefab");
                prefab.AddComponent<TankBlockTemplate>();
                prefab.AddComponent<MeshFilter>();
                prefab.AddComponent<MeshRenderer>();
                prefab.AddComponent<BoxCollider>();
                prefab.SetActive(false);
                this.blockDefinition.m_PhysicalPrefab = prefab.GetComponent<TankBlockTemplate>();
                this.WrapJSON();
            }
            catch (Exception e)
            {
                Console.WriteLine("[Nuterra] FAILED to read JSON: \n" + fileParsed);
                throw e;
            }
            // Console.WriteLine(fileParsed);
        }
        public UnofficialBlock(FileInfo file) : this(file.FullName) { }

        public void WrapJSON()
        {
            JObject wrappedJSON = new JObject();
            this.jObject.Add("AutoImported", true);
            wrappedJSON.Add("NuterraBlock", this.jObject);
            this.blockDefinition.m_Json = new UnityEngine.TextAsset(wrappedJSON.ToString());
        }
    }
}
