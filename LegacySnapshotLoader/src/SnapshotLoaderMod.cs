using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomModules;
using NLog;
using HarmonyLib;
using LogManager;

namespace LegacySnapshotLoader
{
    public class SnapshotLoaderMod : ModBase
    {
        public static int LoadOrder = 3;
        public static Type[] LoadAfter()
        {
            return new Type[] { typeof(NuterraMod) };
        }

        internal static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void ConfigureLogger()
        {
            Manager.LogConfig config = new Manager.LogConfig
            {
                layout = "${longdate} | ${level:uppercase=true:padding=-5:alignmentOnTruncation=left} | ${logger:shortName=true} | ${message}  ${exception}",
                keepOldFiles = false,
                defaultMinLevel = LogLevel.Info
            };
            Manager.RegisterLogger(logger, config);
        }

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
                logger.Warn("EARLY INIT was CALLED for {Class}, but 0ModManager is present!", this.GetType().Name);
            }
            else
            {
                logger.Warn("EARLY INIT was CALLED for {Class}, but 0ModManager is MISSING!", this.GetType().Name);
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
