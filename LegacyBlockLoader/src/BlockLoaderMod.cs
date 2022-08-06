using System;
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
        internal static readonly Type ModdedContentLoaderType = ModManager.GetType("ModManager.ModdedContentLoader", true);
        internal static readonly FieldInfo CurrentOperationSpecifics = AccessTools.Field(ModManagerType, "CurrentOperationSpecifics");
        internal static MethodInfo InjectLegacyBlocks = AccessTools.Method(ModdedContentLoaderType, "InjectLegacyBlocks");

        // This satisfies the 0ModManager interface for loading across multiple frames
        public IEnumerator<float> EarlyInitIterator()
        {
            if (!Inited)
            {
                ConfigureLogger();
                ModContainer container = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");
                PropertyInfo Local = typeof(ModContainer).GetProperty("Local", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Local.SetValue(container, true);

                IEnumerator<float> setupIterator = DirectoryBlockLoader.LoadAssets();
                while (setupIterator.MoveNext())
                {
                    yield return setupIterator.Current;
                }
                Inited = true;
            }
        }

        public override void EarlyInit()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName).Where(name => name.Contains("ModManager")).Count() > 0)
            {
                logger.Warn($"EARLY INIT was CALLED for {this.GetType().Name}, but 0ModManager is present!");
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
            harmony.Patch(InjectLegacyBlocks, prefix: new HarmonyMethod(AccessTools.Method(typeof(BlockLoaderMod), nameof(BlockLoaderMod.InjectLegacyBlocksPrefix))));
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
