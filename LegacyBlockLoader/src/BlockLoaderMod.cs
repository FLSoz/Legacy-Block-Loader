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

namespace LegacyBlockLoader
{
    public class BlockLoaderMod : ModBase
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
                defaultMinLevel = LogLevel.Trace
            };
            Manager.RegisterLogger(logger, config);
        }

        internal const string HarmonyID = "com.flsoz.ttmodding.legacyblockloader";
        private Harmony harmony = new Harmony(HarmonyID);
        private bool Inited = false;

        public void ManagedEarlyInit()
        {
            if (!Inited)
            {
                ConfigureLogger();
                ModContainer container = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");
                PropertyInfo Local = typeof(ModContainer).GetProperty("Local", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Local.SetValue(container, true);
                DirectoryBlockLoader.LoadAssets();
                Inited = true;
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
            harmony.UnpatchAll(HarmonyID);
        }

        public override void Init()
        {
            harmony.PatchAll();
        }
    }
}
