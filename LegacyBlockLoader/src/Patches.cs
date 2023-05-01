using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using CustomModules;
using UnityEngine;
using CustomModules.LegacyModule;


namespace LegacyBlockLoader
{
    // patch recycle to always succeed
    [HarmonyPatch(typeof(ComponentPool), "ReturnItemToPool")]
    internal class PatchRecycle
    {
        internal static Type ItemToReturnToPool;
        internal static FieldInfo m_Poolable;

        [HarmonyFinalizer]
        internal static Exception Finalizer(Exception __exception, System.Object itemToReturn)
        {
            if (ItemToReturnToPool == null)
            {
                ItemToReturnToPool = itemToReturn.GetType();
                m_Poolable = AccessTools.Field(ItemToReturnToPool, "m_Poolable");
            }
            ComponentPool.Pool.Poolable poolable = (ComponentPool.Pool.Poolable) m_Poolable.GetValue(itemToReturn);
            if (poolable.component is Transform transform)
            {
                TankBlock block = transform.GetComponent<TankBlock>();
                if (block != null)
                {
                    return null;
                }
            }
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ManMods), "DoReparseAll")]
    class ReparseJSONPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ref ManMods __instance)
        {
            BlockLoaderMod.logger.Info("Patching NuterraMod metadata due to reparse");
            NuterraMod.ClearMetadata();
            NuterraMod.SetupMetadata();
        }
    }

    internal static class LegacyPatches
    {
        internal static FieldInfo m_Mods = typeof(ManMods).GetField("m_Mods", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static FieldInfo m_BlockIDReverseLookup = typeof(ManMods).GetField("m_BlockIDReverseLookup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static ManMods instance;
        internal static Dictionary<string, ModContainer> modsDict;
        internal static Dictionary<string, int> reverseBlockIDLookup;

        [HarmonyPatch(typeof(ManMods), "Start")]
        private static class PatchSingletonFetch
        {
            private static void Postfix(ref ManMods __instance)
            {
                instance = __instance;
                modsDict = (Dictionary<string, ModContainer>) m_Mods.GetValue(__instance);
                reverseBlockIDLookup = (Dictionary<string, int>) m_BlockIDReverseLookup.GetValue(__instance);
            }
        }

        [HarmonyPatch(typeof(RecipeTable.Recipe.ItemSpec), "GetHashCode")]
        private static class CraftingPatch_FixHashOptimization
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                FixHashOptimization(ref codes);
                BlockLoaderMod.logger.Info("Injected RecipeTable.Recipe.ItemSpec.GetHashCode()");
                return codes;
            }

            private static void FixHashOptimization(ref List<CodeInstruction> codes)
            {
                for (int i = 0; i < codes.Count; i++)
                    if (codes[i].opcode == OpCodes.Callvirt)
                    {
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, 65535));
                        codes.Insert(i + 2, new CodeInstruction(OpCodes.And));
                        return;
                    }
            }
        }

        /* Remove these global BlockInjector patches, as we're now using only Official IDs
        [HarmonyPatch(typeof(ManSpawn), "IsBlockAllowedInCurrentGameMode")]
        private static class TableFix1
        {
            private static void Postfix(ref bool __result, BlockTypes blockType)
            {
                if (!__result && !Enum.IsDefined(typeof(BlockTypes), blockType)) __result = true;
            }
        }

        [HarmonyPatch(typeof(ManSpawn), "IsBlockAllowedInLaunchedConfig")]
        private static class TableFix2
        {
            private static void Postfix(ref bool __result, BlockTypes blockType)
            {
                if (!__result && !Enum.IsDefined(typeof(BlockTypes), blockType)) __result = true;
            }
        }

        [HarmonyPatch(typeof(BlockFilterTable), "CheckBlockAllowed")]
        private static class TableFixCoOp
        {
            private static void Postfix(ref bool __result, BlockTypes blockType)
            {
                if (!__result && !Enum.IsDefined(typeof(BlockTypes), blockType)) __result = true;
            }
        }
        */

        [HarmonyPatch(typeof(StringLookup), "GetString")]
        private static class OnStringLookup
        {
            private static void Postfix(ref string __result, int itemType, LocalisationEnums.StringBanks stringBank, string defaultString)
            {
                if (
                    (stringBank == LocalisationEnums.StringBanks.BlockNames || stringBank == LocalisationEnums.StringBanks.BlockDescription) &&
                    itemType >= ManMods.k_FIRST_MODDED_BLOCK_ID
                ) { return; }

                if (__result == defaultString)
                {
                    __result = $"MissingNo.{itemType} <{stringBank}>";
                }
            }
        }

        // Patch ModuleCustomBlock Emission
        [HarmonyPatch(typeof(ModuleLight), "EnableLights")]
        private static class OverrideEmission
        {
            private static void Postfix(ref ModuleLight __instance)
            {
                ModuleCustomBlock cb = __instance.GetComponent<ModuleCustomBlock>();
                if (cb != null && cb.BlockEmissionMode != ModuleCustomBlock.EmissionMode.None)
                {
                    cb.Invoke("UpdateEmission", 1.5f);
                }
            }
        }

        [HarmonyPatch(typeof(Projectile), "PrePool")]
        internal static class Projectile_UnlockColliderQuantity
        {
            [HarmonyTranspiler]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                //for (int i = 0; i < codes.Count; i++)
                //    BlockLoaderMod.logger.Trace($">{i} {codes[i]}");
                int stfld = codes.FindIndex(ci => ci.opcode == OpCodes.Stfld);
                if (stfld != -1)
                {
                    // load arg 0
                    // load arg 0
                    // call get_gameObject
                    codes.RemoveRange(3, stfld - 3); // Keeps the first 3 ILcodes
                    codes.Insert(3, new CodeInstruction(OpCodes.Call, typeof(Projectile_UnlockColliderQuantity).GetMethod(nameof(Projectile_GetCollider), BindingFlags.Public | BindingFlags.Static)));
                }
                BlockLoaderMod.logger.Debug($"Projectile_UnlockColliderQuantity: Transpiling removed {stfld - 3} IL lines, added 1");
                //for (int i = 0; i < codes.Count; i++)
                //    BlockLoaderMod.logger.Trace($">{i} {codes[i]}");
                return codes;
            }

            public static Collider Projectile_GetCollider(GameObject go)
            {
                foreach (var c in go.EnumerateHierarchy())
                {
                    var col = c.GetComponent<Collider>();
                    if (col != null && !col.isTrigger) return col;
                }
                return null;
            }
        }
    }
}
