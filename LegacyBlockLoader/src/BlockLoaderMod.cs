using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomModules;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace LegacyBlockLoader
{
    public class BlockLoaderMod : ModBase
    {
        public static int LoadOrder = 3;

        public static Type[] LoadAfter()
        {
            IEnumerable<Assembly> communityPatchSearch = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.ToString().StartsWith("CommunityPatch,"));
            if (communityPatchSearch.Count() > 0)
            {
                Assembly communityPatch = communityPatchSearch.FirstOrDefault();
                Type communityPatchMod = communityPatch.GetType("CommunityPatch.CommunityPatchMod", true);
                return new Type[] { typeof(NuterraMod), communityPatchMod };
            }
            else
            {
                Console.WriteLine("[LegacyBlockLoader] Community Patch absent! F7 reloading will be broken because it's missing");
            }
            return new Type[] { typeof(NuterraMod) };
        }

        internal static Logger logger;
        public static void ConfigureLogger()
        {
            Logger.TargetConfig config = new Logger.TargetConfig()
            {
                layout = "${longdate} | ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${message}  ${exception}",
                keepOldFiles = false
            };
            logger = new Logger("BlockLoaderMod", config, 4);
        }

        internal const string HarmonyID = "com.flsoz.ttmodding.legacyblockloader";
        private Harmony harmony = new Harmony(HarmonyID);
        private bool Inited = false;

        internal static readonly string TTSteamDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "Assembly-CSharp").First().Location
            .Replace("Assembly-CSharp.dll", ""), @"../../"
        ));
        private static DirectoryInfo m_CBDirectory;
        internal static DirectoryInfo CBDirectory
        {
            get
            {
                if (m_CBDirectory == null)
                {
                    string BlockPath = Path.Combine(TTSteamDir, "Custom Blocks");
                    try
                    {
                        if (!Directory.Exists(BlockPath))
                        {
                            Directory.CreateDirectory(BlockPath);
                            // Add Block Example.json here?
                        }
                    }
                    catch (Exception E)
                    {
                        BlockLoaderMod.logger.Error(E, "Could not access \"" + BlockPath + "\"!");
                        throw E;
                    }
                    m_CBDirectory = new DirectoryInfo(BlockPath);
                }
                return m_CBDirectory;
            }
        }

        internal static ModContainer ThisContainer;

        // This satisfies the 0ModManager interface for loading across multiple frames
        public IEnumerator<float> EarlyInitIterator()
        {
            if (!Inited)
            {
                Inited = true;
                ConfigureLogger();
                ThisContainer = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");
                BlockLoaderMod.logger.Info("📦 Loaded Mod Container");
                PropertyInfo Local = typeof(ModContainer).GetProperty("Local", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Local.SetValue(ThisContainer, true);

                var holder = new GameObject();
                holder.AddComponent<AssetLoaderCoroutine>();
                GameObject.DontDestroyOnLoad(holder);

                BlockLoaderMod.logger.Info("📑 Loading Assets");
                IEnumerator<float> assetSetupIterator = DirectoryAssetLoader.LoadAssets();
                while (assetSetupIterator.MoveNext())
                {
                    yield return assetSetupIterator.Current;
                }
                BlockLoaderMod.logger.Info("🏁 Loaded Assets");

                // Load blocks
                BlockLoaderMod.logger.Info("📑 Reading blocks");
                FileInfo[] blocks = BlockLoaderMod.CBDirectory.GetFiles("*.json", SearchOption.AllDirectories);
                int processed = 0;
                int total = blocks.Length;
                foreach (FileInfo block in blocks)
                {
                    try
                    {
                        DirectoryBlockLoader.RegisterBlock(block);
                        DirectoryAssetLoader.FileChanged[block.FullName] = block.LastWriteTime;
                    }
                    catch (Exception e)
                    {
                        BlockLoaderMod.logger.Error("❌ Failed to register block at path " + block.FullName + "\n" + e);
                    }
                    processed++;
                    yield return ((float)processed) / total;
                }
                BlockLoaderMod.logger.Info("🏁 Read blocks");

                BlockLoaderMod.logger.Info("🏷️ Registering Blocks");
                IEnumerator<float> blockSetupIterator = DirectoryBlockLoader.RegisterBlockDefs(ThisContainer);
                while (blockSetupIterator.MoveNext())
                {
                    yield return blockSetupIterator.Current;
                }
                BlockLoaderMod.logger.Info("🏁 BlockIDs generated");

                // Resolve Assets
                BlockLoaderMod.logger.Info("🏷 Renaming assets");
                IEnumerator<float> assetsIterator = DirectoryAssetLoader.ResolveAssets();
                while (assetsIterator.MoveNext())
                {
                    yield return assetsIterator.Current;
                }
                BlockLoaderMod.logger.Info("🏁 Renamed assets");

                // Add all assets into the ModContainer
                ThisContainer.Contents.m_AdditionalAssets.AddRange(DirectoryAssetLoader.Assets);
            }
            BlockLoaderMod.logger.Trace("🏁 EarlyInit complete");
            yield break;
        }

        public override void EarlyInit()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName).Where(name => name.Contains("ModManager")).Count() > 0)
            {
                logger.Warn($"🚨 EARLY INIT was CALLED for {this.GetType().Name}, but 0ModManager is present!");
                IEnumerator<float> earlyInit = EarlyInitIterator();
                while (earlyInit.MoveNext()) { }
            }
            else
            {
                logger.Warn($"🚨 EARLY INIT was CALLED for {this.GetType().Name}, but 0ModManager is MISSING!");
                IEnumerator<float> earlyInit = EarlyInitIterator();
                while (earlyInit.MoveNext()) { }
            }
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void DeInit()
        {
            harmony.UnpatchAll(HarmonyID);
        }


        internal static readonly MethodInfo AutoAssignIDs = typeof(ManMods)
                .GetMethod(
                    "AutoAssignIDs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(Dictionary<int, string>), typeof(List<string>), typeof(int), typeof(int) },
                    null
                );
        internal static readonly FieldInfo m_CurrentSession = AccessTools.Field(typeof(ManMods), "m_CurrentSession");

        public override void Init()
        {
            harmony.PatchAll();


            ManMods manMods = Singleton.Manager<ManMods>.inst;
            ModSessionInfo currentSession = (ModSessionInfo)m_CurrentSession.GetValue(manMods);

            // populate loaded block dictionary
            logger.Info("📑 Indexing legacy block IDs");
            Dictionary<int, int> sessionIDs = new Dictionary<int, int>();
            foreach (KeyValuePair<int, string> keyValuePair in currentSession.BlockIDs)
            {
                int blockSessionID = keyValuePair.Key;
                string blockID = keyValuePair.Value;
                try
                {
                    logger.Debug($" 🔍 Checking if block is legacy BlockID: {blockID}, Session ID: {blockSessionID}");
                    ModUtils.SplitCompoundId(blockID, out string modId, out string assetId);
                    ModContainer modContainer = manMods.FindMod(modId);
                    if (modContainer != null && modContainer != ThisContainer)
                    {
                        ModdedBlockDefinition moddedBlockDefinition = modContainer.FindAsset<ModdedBlockDefinition>(assetId, true);
                        if (moddedBlockDefinition != null)
                        {
                            logger.Trace($"  🔎 Preparing to parse JSON");
                            int legacyID = 0;

                            // Inject the hook & fetch legacy details
                            JObject blockJSON = JObject.Parse(moddedBlockDefinition.m_Json.text);
                            JProperty NuterraBlock = blockJSON.Property("NuterraBlock");
                            if (NuterraBlock != null)
                            {
                                logger.Debug($"  🔎 NuterraBlock Present");
                                legacyID = LenientTryParseInt(NuterraBlock.Value as JObject, "ID", 0);
                            }

                            if (legacyID != 0)
                            {
                                sessionIDs[legacyID] = blockSessionID;
                                logger.Debug($"  ✔️ Found official legacy block: BlockID: {blockID}, Session ID: {blockSessionID}, LegacyID: {legacyID}");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Error($"❌ FAILED to read details for {blockID} ({blockSessionID})");
                    logger.Error(e);
                }
            }

            // setup new ids
            logger.Info("📝 Assigning unhandled legacy blocks");
            List<string> blocksToAssign = new List<string>();
            Dictionary<string, UnofficialBlock> definitionMap = new Dictionary<string, UnofficialBlock>();
            List<int> portedIds = new List<int>();
            foreach (KeyValuePair<int, UnofficialBlock> pair in DirectoryBlockLoader.LegacyBlocks)
            {
                if (sessionIDs.TryGetValue(pair.Key, out int newId))
                {
                    logger.Info($" ➡ {pair.Value.blockDefinition.m_BlockDisplayName} ({pair.Key}) has been ported to official as {newId}. Using official version.");
                    portedIds.Add(pair.Key);
                }
                else
                {
                    string blockID = ModUtils.CreateCompoundId("LegacyBlockLoader", pair.Value.blockDefinition.name);
                    definitionMap.Add(blockID, pair.Value);
                    blocksToAssign.Add(blockID);
                    logger.Debug($" 💉 Marking Block {pair.Value.blockDefinition.m_BlockDisplayName} [{blockID}] ({pair.Key}) for injection");
                }
            }

            // inject into IDs
            AutoAssignIDs.Invoke(manMods,
                new object[] { currentSession.BlockIDs, blocksToAssign, ManMods.k_FIRST_MODDED_BLOCK_ID, int.MaxValue });
        }


        private static int LenientTryParseInt(JObject obj, string key, int defaultValue)
        {
            JToken jtoken;
            if (obj.TryGetValue(key, out jtoken))
            {
                if (jtoken.Type == JTokenType.Float)
                {
                    return Mathf.FloorToInt(jtoken.ToObject<float>());
                }
                else if (jtoken.Type == JTokenType.Integer)
                {
                    return jtoken.ToObject<int>();
                }
                else if (jtoken.Type == JTokenType.String)
                {
                    if (int.TryParse(jtoken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int parsed))
                    {
                        return parsed;
                    }
                    else if (float.TryParse(jtoken.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedFloat))
                    {
                        return Mathf.FloorToInt(parsedFloat);
                    }
                }
                else if (jtoken.Type == JTokenType.Boolean)
                {
                    return jtoken.ToObject<bool>() ? 1 : 0;
                }
            }
            return defaultValue;
        }
    }
}
