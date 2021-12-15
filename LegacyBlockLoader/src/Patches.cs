using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using CustomModules;
using UnityEngine;
using LegacyBlockLoader.Datastructures;
using CustomModules.LegacyModule;


namespace LegacyBlockLoader
{
    [HarmonyPatch(typeof(TankPreset.BlockSpec), "GetBlockType")]
    internal class PatchSnapshotCompatibility
    {
        private static readonly int[] AcebaIDs = new int[] {
                9000, 9001, 9002, 9003, 9005, 9210, 9211, 10000, 10001,
                98341, 98342, 98343, 98344, 98345,
                98350, 98351, 98352, 98353, 98354, 98355, 98356,
                99990, 99991, 99992, 99993, 99994,
                129380, 129381, 534968, 910601, 934342, 944352,
                1293700, 1293701, 1293702, 1293703,
                1293830, 1293831, 1293832, 1293833, 1293834, 1293835, 1293836, 1293837, 1293838, 1293839,
                1393800, 1393801, 1393802, 1393803, 1393804,
                1393835, 1393836, 1393837, 1393838,
                1980325, 6194710, 6194711, 9827344, 9827345,
                20378946, 29571436, 52363926, 52363927, 92837501,
                3000904
            };
        private static readonly SortedList<int, int> Claims = new SortedList<int, int> {
                { 6000, 1000 },
                { 7000, 1000 },
                { 8000, 1000 },
                { 10000, 1000 },
                { 11000, 500 },
                { 14000, 1000 },
                { 17000, 1000 },
                { 19000, 1000 },
                { 20000, 1000 },
                { 30000, 1000 },
                { 36700, 100 },
                { 40000, 1000 },
                { 51000, 1000 },
                { 52000, 1000 },
                { 69000, 1000 },
                { 75000, 4000 },
                { 80000, 2000 },
                { 100000, 1000 },
                { 101000, 1000 },
                { 117000, 1000 },
                { 300000, 4000 },
                { 404000, 500 },
                { 404500, 200 },
                { 419000, 1000 },
                { 500000, 1000 },
                { 546300, 100 },
                { 584000, 1000 },
                { 620000, 1000 },
                { 800000, 1000 },
                { 910000, 2000 },
                { 962000, 1000 },
                { 980000, 1000 },
                { 1500000, 1000 },
                { 1700000, 2000 },
                { 2000000, 5000 },
                { 2010000, 5000 },
                { 2015000, 2000 },
                { 2499000, 2000 },
                { 2900200, 100 },
                { 3000000, 1000000 },
                { 4300000, 100000 },
                { 5000000, 1000 },
                { 5349000, 1000 },
                { 6666660, 140 },
                { 11235000, 1000 },
                { 12000000, 1000 },
                { 20000000, 10000 },
                { 60000000, 1000 },
                { 69420000, 1000 },
                { 91100000, 200000 },
                { 93000000, 1000 },
            };
        private static readonly Dictionary<int, string> ModCreators = new Dictionary<int, string> {
                {6000, "QUACKDUCK" },
                { 7000, "EXUND" },
                { 8000, "XEVIANLIGHT" },
                { 10000, "FLSOZ" },
                { 11000, "BLUESQUARE" },
                { 14000, "CRIMSON CRIPS" },
                { 17000, "SKIHILEY" },
                { 19000, "SAIKO (Youkai?)" },
                { 20000, "ASTRATHEDRAGON" },
                { 30000, "LEGOMONSTER" },
                { 36700, "DAVID07" },
                { 40000, "BINARY" },
                { 51000, "WHITEDWARF" },
                { 52000, "MARS3885" },
                { 69000, "YES" },
                { 75000, "EDDIE" },
                { 80000, "JUJUTEUX" },
                { 100000, "ISQLTYCHIPS (HS?)" },
                { 101000, "VIOLET" },
                { 117000, "SAELEM (Black Labs?)" },
                { 300000, "PACHU (Old Star?)" },
                { 404000, "ICKYTECH" },
                { 404500, "OVERCHARGEDBATTERY" },
                { 419000, "POTATO (LK?)" },
                { 500000, "SHIDO" },
                { 546300, "RABIS" },
                { 584000, "LEGIONITE (TAC?)" },
                { 620000, "COMMANDERBUBBLES" },
                { 800000, "GALREX" },
                { 910000, "RAFS (GT?)" },
                { 962000, "SACHIHO" },
                { 980000, "21TURTLES" },
                { 1500000, "GWLEGION" },
                { 1700000, "LICFLAGG (FSI?)" },
                { 2000000, "SETH_SETH (Darklight?)" },
                { 2010000, "ENDERJED" },
                { 2015000, "ZUGBUG33" },
                { 2499000, "CHYZMAN" },
                { 2900200, "POKEFAN177" },
                { 3000000, "MINDL3SS" },
                { 4300000, "INDOMINUSBAZ" },
                { 5000000, "GARR8903" },
                { 5349000, "HEX" },
                { 6666660, "JANESPLAYZ" },
                { 11235000, "KYOKO" },
                { 12000000, "FIRERED" },
                { 20000000, "LUKASXPL" },
                { 60000000, "NEH" },
                { 69420000, "TIDGEM" },
                { 91100000, "NAAB007" },
                { 93000000, "XAM5021" },
            };

        internal static LRUCache<int, int> IDLookupCache = new LRUCache<int, int>(500);
        private static LRUCache<int, bool> IsLegacyIDCache = new LRUCache<int, bool>(1000);

        private static FieldInfo m_CurrentSession = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static MethodInfo TryGetSessionID = typeof(NuterraMod).GetMethod("TryGetSessionID", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static bool SearchSortedArray(int[] arr, int id)
        {
            return Array.BinarySearch(arr, id) >= 0;
        }

        private static bool SearchSortedRangeList(SortedList<int, int> rangeList, int id, out int rangeStart)
        {
            rangeStart = -1;
            int min = 0;
            int max = rangeList.Count - 1;
            while (min <= max)
            {
                int mid = (min + max) / 2;
                if (id < rangeList.Keys[mid])
                {
                    max = mid - 1;
                }
                else if (id == rangeList.Keys[mid] || id < rangeList.Keys[mid] + rangeList.Values[mid])
                {
                    rangeStart = rangeList.Keys[mid];
                    return true;
                }
                else
                {
                    min = mid + 1;
                }
            }
            return false;
        }

        private static bool IsLegacyID(int id)
        {
            ModSessionInfo session = (ModSessionInfo) m_CurrentSession.GetValue(Singleton.Manager<ManMods>.inst);
            if (id > ManMods.k_FIRST_MODDED_BLOCK_ID + (2 * session.BlockIDs.Count))
            {
                return true;
            }

            // Console.WriteLine($"Checking if block {id} is a legacy ID");
            if (SearchSortedArray(AcebaIDs, id))
            {
                // Console.WriteLine($"Is a WP ID");
                return true;
            }
            else if (SearchSortedRangeList(Claims, id, out int rangeStart))
            {
                if (ModCreators.TryGetValue(rangeStart, out string creator))
                {
                    // Console.WriteLine($"Is a {creator} ID");
                }
                else
                {
                    // Console.WriteLine($"Is a modded ID from UNKNOWN author");
                }
                return true;
            }
            // Console.WriteLine($"NOT a modded ID");
            return false;
        }

        [HarmonyPrefix]
        public static bool Prefix(TankPreset.BlockSpec __instance, ref BlockTypes __result)
        {
            // Console.WriteLine($"Checking if {__instance.block} ({(int) __instance.m_BlockType}) is a legacy block");
            // TryGetSessionID(int legacyId, out int newId)
            int blockID = (int)__instance.m_BlockType;

            if (IDLookupCache.TryGetValue(blockID, out int newID))
            {
                __result = (BlockTypes)newID;
                return false;
            }
            else
            {
                object[] args = new object[] { (int)__instance.m_BlockType, null };
                if ((bool)TryGetSessionID.Invoke(null, args))
                {
                    // Console.WriteLine($"Found SESSION ID: {args[1]}");
                    IDLookupCache.Put(blockID, (int)args[1]);
                    __result = (BlockTypes)args[1];
                    return false;
                }
                else
                {
                    IDLookupCache.Put(blockID, blockID);
                    Console.WriteLine($"Block {(int)__instance.m_BlockType} NOT in session!");
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ManMods), "InjectModdedBlocks")]
    internal class PatchBlockInjection
    {
        private static MethodInfo TryRegisterUnofficialBlock = typeof(NuterraMod).GetMethod("TryRegisterUnofficialBlock", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
        {
            Label myLabel = ilgen.DefineLabel();
            Label targetInjectLegacyBlocksLabel = ilgen.DefineLabel();
            Label targetRegisterLegacyIDsLabel = ilgen.DefineLabel();

            List<CodeInstruction> originalInstructions = new List<CodeInstruction>(instructions);
            List<CodeInstruction> patchedInstructions = new List<CodeInstruction>();
            MethodInfo CreatePool = typeof(ComponentPoolExtensions).GetMethod("CreatePool", BindingFlags.Static);

            MethodInfo GetKey = typeof(KeyValuePair<int, string>).GetProperty("Key").GetAccessors()[0];

            LocalBuilder idVar = ilgen.DeclareLocal(typeof(int));
            LocalBuilder blockDefVar = ilgen.DeclareLocal(typeof(ModdedBlockDefinition));

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
                    blockDefVar = (LocalBuilder)instruction.operand;
                }
                if (!seenIdCall && instruction.opcode == OpCodes.Call && (MethodInfo)instruction.operand == GetKey)
                {
                    seenIdCall = true;
                }
                if (seenIdCall && !setIdInd && instruction.opcode == OpCodes.Stloc_S)
                {
                    setIdInd = true;
                    idVar = (LocalBuilder)instruction.operand;
                }
            }

            bool seenFirst = false;
            bool seenSecond = false;
            bool patchedCall = false;


            foreach (CodeInstruction instruction in originalInstructions)
            {
                if (instruction.opcode == OpCodes.Leave_S && (Label)instruction.operand == targetInjectLegacyBlocksLabel)
                {
                    instruction.operand = myLabel;
                }

                // Inject before we do Singleton.Manager<ManUI>.inst.SetModSprites()
                if (instruction.opcode == OpCodes.Ldsfld && (FieldInfo)instruction.operand == typeof(Singleton.Manager<ManUI>).GetField("inst", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    // Console.WriteLine("PATCH TARGET FOUND");
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

                if (instruction.opcode == OpCodes.Ldc_I4_8)
                {
                    if (seenFirst)
                    {
                        seenSecond = true;
                    }
                    else
                    {
                        seenFirst = true;
                    }
                }

                if (seenSecond && instruction.opcode == OpCodes.Call && !patchedCall)
                {
                    // If block is not being reparsed (and thus not being registered through NuterraSteam ModuleLoader),
                    // We need to register it.
                    // Add a call to manage it for workshop blocks, after the component pool is created
                    patchedInstructions.Add(new CodeInstruction(OpCodes.Ldloc_S, idVar));
                    patchedInstructions.Add(new CodeInstruction(OpCodes.Ldloc_S, blockDefVar));
                    patchedInstructions.Add(new CodeInstruction(OpCodes.Call, TryRegisterUnofficialBlock));
                    patchedCall = true;
                }
            }

            foreach (CodeInstruction instruction in patchedInstructions)
            {
                yield return instruction;
            }
        }
    }

    internal class Patches
    {
        [HarmonyPatch(typeof(RecipeTable.Recipe.ItemSpec), "GetHashCode")]
        private static class CraftingPatch_FixHashOptimization
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                FixHashOptimization(ref codes);
                Console.WriteLine("Injected RecipeTable.Recipe.ItemSpec.GetHashCode()");
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
                //    Console.WriteLine($">{i} {codes[i]}");
                int stfld = codes.FindIndex(ci => ci.opcode == OpCodes.Stfld);
                if (stfld != -1)
                {
                    // load arg 0
                    // load arg 0
                    // call get_gameObject
                    codes.RemoveRange(3, stfld - 3); // Keeps the first 3 ILcodes
                    codes.Insert(3, new CodeInstruction(OpCodes.Call, typeof(Projectile_UnlockColliderQuantity).GetMethod("Projectile_GetCollider", BindingFlags.Public | BindingFlags.Static)));
                }
                Console.WriteLine($"Projectile_UnlockColliderQuantity: Transpiling removed {stfld - 3} IL lines, added 1");
                //for (int i = 0; i < codes.Count; i++)
                //    Console.WriteLine($">{i} {codes[i]}");
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
