using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomModules;
using HarmonyLib;

namespace LegacySnapshotLoader
{
    public class SnapshotLoaderMod : ModBase
    {
        public static int LoadOrder = 3;
        public static Type[] LoadAfter()
        {
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
            logger = new Logger("SnapshotLoaderMod", config, 4);
        }

        internal static bool HasBlockInjector = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName).Where(name => name.Contains("ModManager")).Count() > 0;
        internal static bool inited = false;
        internal const string HarmonyID = "com.flsoz.ttmodding.legacysnapshotloader";
        private Harmony harmony = new Harmony(HarmonyID);

        public void ManagedEarlyInit()
        {
            if (!inited)
            {
                Console.WriteLine("[SnapshotLoaderMod] Configuring logger");
                ConfigureLogger();
                inited = true;
            }
        }

        public override void EarlyInit()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName).Where(name => name.Contains("ModManager")).Count() > 0)
            {
                logger.Warn($"EARLY INIT was CALLED for \"{this.GetType().Name}\", but 0ModManager is present!");
            }
            else
            {
                logger.Warn($"EARLY INIT was CALLED for \"{this.GetType().Name}\", but 0ModManager is MISSING!");
                this.ManagedEarlyInit();
            }
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void DeInit()
        {
            PatchSnapshotLoadCompatibility.LegacyLookupIDCache.Clear();
            PatchSnapshotLoadCompatibility.SessionIDCache.Clear();
            PatchSnapshotLoadCompatibility.InvalidID = 0;
            harmony.UnpatchAll(HarmonyID);
        }

        public override void Init()
        {
            harmony.PatchAll();
        }
    }
}
