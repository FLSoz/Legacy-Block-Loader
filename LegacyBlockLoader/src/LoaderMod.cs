using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomModules;
using NLog;
using HarmonyLib;


namespace LegacyBlockLoader
{
    public class LoaderMod : ModBase
    {
        public static int LoadOrder = 3;
        public static Type[] LoadAfter()
        {
            return new Type[] { typeof(NuterraMod) };
        }
        public void ManagedEarlyInit()
        {
            DirectoryBlockLoader.LoadAssets();
        }

        internal static Logger logger = NLog.LogManager.GetCurrentClassLogger();
        internal const string HarmonyID = "com.flsoz.ttmodding.legacyblockloader";
        private Harmony harmony = new Harmony(HarmonyID);

        public override void EarlyInit()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.FullName).Where(name => name.Contains("0ModManager")).Count() > 0)
            {
                logger.Warn("EARLY INIT was CALLED for {Class}, but 0ModManager is present!", this.GetType().Name);
            }
            else
            {
                logger.Warn("EARLY INIT was CALLED for {Class}, but 0ModManager is MISSING!", this.GetType().Name);
            }
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void DeInit()
        {
            PatchSnapshotCompatibility.IDLookupCache.Clear();
            harmony.UnpatchAll(HarmonyID);
        }

        public override void Init()
        {
            harmony.PatchAll();
        }
    }
}
