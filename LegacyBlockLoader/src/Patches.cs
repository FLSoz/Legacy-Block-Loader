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

    [HarmonyPatch(typeof(ManMods), "InjectModdedBlocks")]
    internal class PatchBlockInjection
    {
        private static MethodInfo RegisterUnofficialBlocks = typeof(NuterraMod).GetMethod("RegisterUnofficialBlocks", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
        {
            Label myLabel = ilgen.DefineLabel();
            Label targetInjectLegacyBlocksLabel = ilgen.DefineLabel();

            List<CodeInstruction> originalInstructions = new List<CodeInstruction>(instructions);
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>();

            MethodInfo GetKey = typeof(KeyValuePair<int, string>).GetProperty("Key").GetAccessors()[0];

            bool seenBlockDefCall = false;
            bool setBlockDefInd = false;
            bool seenIdCall = false;
            bool setIdInd = false;
            foreach (CodeInstruction instruction in originalInstructions)
            {
                if (instruction.opcode == OpCodes.Ldsfld && (FieldInfo)instruction.operand == typeof(Singleton.Manager<ManUI>).GetField("inst", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    targetInjectLegacyBlocksLabel = instruction.labels[0];
                }
                if (!seenBlockDefCall && instruction.opcode == OpCodes.Call && ((MethodInfo)instruction.operand).ReturnType == typeof(ModdedBlockDefinition))
                {
                    seenBlockDefCall = true;
                }
                if (seenBlockDefCall && !setBlockDefInd && instruction.opcode == OpCodes.Stloc_S)
                {
                    setBlockDefInd = true;
                }
                if (!seenIdCall && instruction.opcode == OpCodes.Call && (MethodInfo)instruction.operand == GetKey)
                {
                    seenIdCall = true;
                }
                if (seenIdCall && !setIdInd && instruction.opcode == OpCodes.Stloc_S)
                {
                    setIdInd = true;
                }
            }

            foreach (CodeInstruction instruction in originalInstructions)
            {
                if (instruction.opcode == OpCodes.Leave_S && (Label)instruction.operand == targetInjectLegacyBlocksLabel)
                {
                    instruction.operand = myLabel;
                }

                // Inject before we do Singleton.Manager<ManUI>.inst.SetModSprites()
                if (instruction.opcode == OpCodes.Ldsfld && (FieldInfo)instruction.operand == typeof(Singleton.Manager<ManUI>).GetField("inst", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    // load newSessionInfo

                    // inject blocks
                    CodeInstruction start = new CodeInstruction(OpCodes.Ldarg_1);
                    if (start.labels == null)
                    {
                        start.labels = new List<Label> { myLabel };
                    }
                    else
                    {
                        start.labels.Add(myLabel);
                    }

                    // Insert call to InjectLegacyBlocks before block table registration happens
                    patchedInstructions.Add(start);
                    patchedInstructions.Add(new CodeInstruction(OpCodes.Ldloc_0));
                    patchedInstructions.Add(new CodeInstruction(OpCodes.Ldloc_1));
                    patchedInstructions.Add(CodeInstruction.Call(typeof(DirectoryBlockLoader), "InjectLegacyBlocks"));
                    // yield return new CodeInstruction(OpCodes.Call, InjectLegacyBlocks);
                }
                patchedInstructions.Add(instruction);
            }

            foreach (CodeInstruction instruction in patchedInstructions)
            {
                yield return instruction;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(ref ManMods __instance, ref ModSessionInfo newSessionInfo)
        {
            RegisterUnofficialBlocks.Invoke(null, new object[] { newSessionInfo });
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

        [HarmonyPatch(typeof(ManSpawn), "IsBlockAvailableOnPlatform")]
        private static class TableFix
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

        internal static class ModuleItemConsume_UnlockDeliveryBlockerRange
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                var check = codes.FirstOrDefault(ci => ci.opcode == OpCodes.Ldc_R4 && (int)ci.operand == 23);
                if (check != null && check != default(CodeInstruction)) check.operand = 512;
                return codes;
            }
        }

        internal static class ModuleItemConsume_CrashWrapper
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = instructions.ToList();
                //var check = codes.FirstOrDefault(ci => ci.opcode == OpCodes.Ldc_R4 && (int)ci.operand == 23);
                //if (check != null && check != default(CodeInstruction)) check.operand = 512;
                return codes;
            }
        }

        internal static class Projectile_UnlockColliderQuantity
        {
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
                    codes.Insert(3, new CodeInstruction(OpCodes.Call, typeof(Projectile_UnlockColliderQuantity).GetMethod("Projectile_GetCollider", BindingFlags.Public | BindingFlags.Static)));
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

        public static int CheckVersion(string baseVersion, string currentCheck)
        {
            var cArr = currentCheck.Split('.');
            var tArr = baseVersion.Split('.');
            int max = Math.Max(cArr.Length, tArr.Length);
            for (int i = 0; i < max; i++)
            {
                int currVal = 0, baseVal = 0;
                if (cArr.Length > i) currVal = int.Parse(cArr[i]);
                if (tArr.Length > i) baseVal = int.Parse(tArr[i]);

                if (currVal == baseVal) continue;
                if (currVal > baseVal) return 1;
                return -1;
            }
            return 0;
        }
    }
}
