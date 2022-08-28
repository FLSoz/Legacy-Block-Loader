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

        internal static readonly Assembly ModManager = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "TTModManager").First();
        internal static readonly Type ModManagerType = ModManager.GetType("ModManager.ModManager", true);
        internal static readonly Type ModdedContentLoaderType = ModManager.GetType("ModManager.ModdedContentLoader", false);
        internal static readonly FieldInfo CurrentOperationSpecifics = AccessTools.Field(ModManagerType, "CurrentOperationSpecifics");
        internal static MethodInfo InjectLegacyBlocksIterator = ModdedContentLoaderType != null ? AccessTools.Method(ModdedContentLoaderType, "InjectLegacyBlocksIterator") : null;


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
                BlockLoaderMod.logger.Info("Loaded Mod Container");
                PropertyInfo Local = typeof(ModContainer).GetProperty("Local", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Local.SetValue(ThisContainer, true);

                var holder = new GameObject();
                holder.AddComponent<AssetLoaderCoroutine>();
                GameObject.DontDestroyOnLoad(holder);

                IEnumerator<float> assetSetupIterator = DirectoryAssetLoader.LoadAssets();
                while (assetSetupIterator.MoveNext())
                {
                    yield return assetSetupIterator.Current;
                }

                // Load blocks
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
                        BlockLoaderMod.logger.Error("Failed to register block at path " + block.FullName + "\n" + e);
                    }
                    processed++;
                    yield return ((float)processed) / total;
                }

                IEnumerator<float> blockSetupIterator = DirectoryBlockLoader.RegisterBlockDefs(ThisContainer);
                while (blockSetupIterator.MoveNext())
                {
                    yield return blockSetupIterator.Current;
                }

                // Resolve Assets
                IEnumerator<float> assetsIterator = DirectoryAssetLoader.ResolveAssets();
                while (assetsIterator.MoveNext())
                {
                    yield return assetsIterator.Current;
                }

                // Add all assets into the ModContainer
                ThisContainer.Contents.m_AdditionalAssets.AddRange(DirectoryAssetLoader.Assets);
            }
            yield break;
        }

        public override void EarlyInit()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName).Where(name => name.Contains("ModManager")).Count() > 0)
            {
                logger.Warn($"EARLY INIT was CALLED for {this.GetType().Name}, but 0ModManager is present!");
                IEnumerator<float> earlyInit = EarlyInitIterator();
                while (earlyInit.MoveNext()) { }
            }
            else
            {
                logger.Warn($"EARLY INIT was CALLED for {this.GetType().Name}, but 0ModManager is MISSING!");
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

        public override void Init()
        {
            harmony.PatchAll();
            if (InjectLegacyBlocksIterator != null)
            {
                harmony.Patch(InjectLegacyBlocksIterator, prefix: new HarmonyMethod(AccessTools.Method(typeof(BlockLoaderMod), nameof(BlockLoaderMod.InjectLegacyBlocksPrefix))));
            }
        }

        internal static bool InjectLegacyBlocksPrefix(
            ModSessionInfo newSessionInfo,
            Dictionary<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> gradeBlockPerCorp,
            Dictionary<int, Sprite> blockSpriteDict,
            ref IEnumerator<float> __result
        )
        {
            __result = DirectoryBlockLoader.LegacyBlockIterator(newSessionInfo, gradeBlockPerCorp, blockSpriteDict);
            return false;
        }
    }
}
