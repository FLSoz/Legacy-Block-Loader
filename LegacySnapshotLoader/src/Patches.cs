using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LegacyBlockLoader.Datastructures;
using HarmonyLib;
using CustomModules;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static LocalisationEnums;
using static CompoundExpression;
using static Localisation;
using System.Collections;


namespace LegacySnapshotLoader
{
    [HarmonyPatch(typeof(TankPreset.BlockSpec), "GetBlockType")]
    internal class PatchSnapshotLoadCompatibility
    {
        internal static FieldInfo m_Mods = typeof(ManMods).GetField("m_Mods", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static FieldInfo m_BlockNames = typeof(ManMods).GetField("m_BlockNames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static int VanillaIDs = Enum.GetValues(typeof(BlockTypes)).Length;
        private static readonly int[] AcebaIDs = new int[] {
                998,999,
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
                { 700000, 1000 },
                { 800000, 1000 },
                { 810000, 1000 },
                { 910000, 2000 },
                { 962000, 1000 },
                { 980000, 1000 },
                { 999000, 1000 },
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
                { 10000000, 1000 },
                { 11235000, 1000 },
                { 12000000, 1000 },
                { 20000000, 10000 },
                { 60000000, 1000 },
                { 69420000, 1000 },
                { 91100000, 200000 },
                { 92000000, 200 },
                { 93000000, 1000 },
            };
        private static readonly Dictionary<int, string> ModCreators = new Dictionary<int, string> {
                { 6000, "QUACKDUCK" },
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
                { 700000, "ANCHOBI" },
                { 800000, "GALREX" },
                { 810000, "CUPHOLDER" },
                { 910000, "RAFS (GT?)" },
                { 962000, "SACHIHO" },
                { 980000, "21TURTLES" },
                { 999000, "DARVIN" },
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
                { 10000000, "PYROSALAMANDER" },
                { 11235000, "KYOKO" },
                { 12000000, "FIRERED" },
                { 20000000, "LUKASXPL" },
                { 60000000, "NEH" },
                { 69420000, "TIDGEM" },
                { 91100000, "NAAB007" },
                { 92000000, "AUSEAWESOME" },
                { 93000000, "XAM5021" },
            };

        private static int _invalidID;
        internal static int FirstInvalidID
        {
            get
            {
                // Set invalid ID to 1 above highest
                if (_invalidID == 0)
                {
                    Dictionary<int, string> blockNames = (Dictionary<int, string>)m_BlockNames.GetValue(Singleton.Manager<ManMods>.inst);
                    _invalidID = ManMods.k_FIRST_MODDED_BLOCK_ID + blockNames.Count() + 1;
                }
                return _invalidID;
            }
            set {
                _invalidID = value;
            }
        }
        internal static int CurrInvalidID;

        internal static void PurgeMetadata()
        {
            FirstInvalidID = 0;
            CurrInvalidID = 0;
            // Clear dicts used for name fetching
            LegacyLookupIDCache.Clear();
            SessionIDCache.Clear();
            LegacyIDToInvalidID.Clear();
            InvalidIDToLegacyID.Clear();
            LegacyIDToName.Clear();
            NameToInvalidID.Clear();
            InvalidIDToName.Clear();
        }

        internal static LRUCache<int, bool> IsLegacyCache = new LRUCache<int, bool>(500);
        internal static LRUCache<int, int> LegacyLookupIDCache = new LRUCache<int, int>(1000);

        // cannot cache ID to ID for pure session IDs, because we have no guarantees on what the session ID of a single given block will be in a snapshot
        internal static LRUCache<string, int> SessionIDCache = new LRUCache<string, int>(1000);

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

        private static bool SearchLegacyClaims(int id)
        {
            SnapshotLoaderMod.logger.Debug($"Checking if block {id} is a legacy ID");
            if (SearchSortedArray(AcebaIDs, id))
            {
                SnapshotLoaderMod.logger.Debug($"Is a WP ID");
                return true;
            }
            else if (SearchSortedRangeList(Claims, id, out int rangeStart))
            {
                if (ModCreators.TryGetValue(rangeStart, out string creator))
                {
                    SnapshotLoaderMod.logger.Debug($"Is a {creator} ID");
                }
                else
                {
                    SnapshotLoaderMod.logger.Warn($"Is a modded ID from UNKNOWN author");
                }
                return true;
            }
            // SnapshotLoaderMod.logger.Debug($"NOT a modded ID");
            return false;
        }

        // WE ARE ASSUMING NOBODY WILL HAVE 500,000 MODDED BLOCKS, AND VANILLA CUTOFF STAYS AT 1,000,000
        internal static bool IsLegacy(int blockID)
        {
            bool isLegacy = blockID < ManMods.k_FIRST_MODDED_BLOCK_ID;
            if (!isLegacy && !PatchSnapshotLoadCompatibility.IsLegacyCache.TryGetValue(blockID, out isLegacy))
            {
                isLegacy = SearchLegacyClaims(blockID);
                IsLegacyCache.Put(blockID, isLegacy);
            }
            return isLegacy;
        }

        internal static bool GetSessionIDForLegacyBlock(int blockID, string name, out int sessionID)
        {
            object[] args = new object[] { blockID, null };
            string adjName = name + " ";
            if ((bool)TryGetSessionID.Invoke(null, args))
            {
                SnapshotLoaderMod.logger.Debug($"Found SESSION ID: {args[1]} for Legacy block {adjName}({blockID})");
                LegacyLookupIDCache.Put(blockID, (int)args[1]);
                sessionID = (int)args[1];
                return true;
            }
            else
            {
                // We know this is a legacy block, but no session ID has been found
                SnapshotLoaderMod.logger.Warn($"Legacy block {adjName}({blockID}) NOT in session! Trying official as backup");
                if (SearchSortedRangeList(Claims, blockID, out int rangeStart))
                {
                    if (ModCreators.TryGetValue(rangeStart, out string creator))
                    {
                        SnapshotLoaderMod.logger.Debug($"Is a {creator} ID");
                    }
                    else
                    {
                        SnapshotLoaderMod.logger.Warn($"Is a modded ID from UNKNOWN author");
                    }
                }
            }
            sessionID = AssignNewInvalidID(blockID, name);
            return false;
        }

        // Name handling for legacy blocks
        private static Dictionary<int, int> LegacyIDToInvalidID = new Dictionary<int, int>();
        private static Dictionary<int, int> InvalidIDToLegacyID = new Dictionary<int, int>();
        private static Dictionary<int, string> LegacyIDToName = new Dictionary<int, string>();
        // Name handling for missing official blocks
        private static Dictionary<string, int> NameToInvalidID = new Dictionary<string, int>();
        private static Dictionary<int, string> InvalidIDToName = new Dictionary<int, string>();

        internal static int AssignNewInvalidID(int blockID, string name)
        {
            if (IsLegacy(blockID))
            {
                // for legacy IDs, the legacy ID itself is a core part of the name
                if (LegacyIDToInvalidID.TryGetValue(blockID, out int invalidID)) {
                    string oldName = LegacyIDToName[blockID];
                    if (!oldName.Equals(name))
                    {
                        if (name.Length > oldName.Length)
                        {
                            SnapshotLoaderMod.logger.Warn($"Multiple names found for legacy ID {blockID}. Choosing more descriptive [{name}] over ({oldName})");
                            LegacyIDToName[blockID] = name;
                        }
                        else
                        {
                            SnapshotLoaderMod.logger.Warn($"Multiple names found for legacy ID {blockID}. Choosing more descriptive [{oldName}] over ({name})");
                        }
                    }
                    return invalidID;
                }
                else
                {
                    CurrInvalidID = Math.Max(CurrInvalidID, FirstInvalidID) + 1;
                    LegacyIDToInvalidID.Add(blockID, CurrInvalidID);
                    InvalidIDToLegacyID.Add(CurrInvalidID, blockID);
                    LegacyIDToName.Add(blockID, name);
                    return CurrInvalidID;
                }
            }
            else
            {
                if (NameToInvalidID.TryGetValue(name, out int invalidID))
                {
                    return invalidID;
                }
                else
                {
                    CurrInvalidID = Math.Max(CurrInvalidID, FirstInvalidID) + 1;
                    NameToInvalidID.Add(name, CurrInvalidID);
                    InvalidIDToName.Add(CurrInvalidID, name);
                    return CurrInvalidID;
                }
            }
        }

        internal static string GetNameOfInvalidID(int invalidID)
        {
            if (InvalidIDToName.TryGetValue((int)invalidID, out string name))
            {
                return name;
            }
            else if (InvalidIDToLegacyID.TryGetValue((int)invalidID, out int legacyID))
            {
                if (LegacyIDToName.TryGetValue(legacyID, out name))
                {
                    return $"{name} ({legacyID})";
                }
                else
                {
                    SnapshotLoaderMod.logger.Error($"LEGACY ID TO NAME MAP IS CORRUPTED");
                }
            }
            return null;
        }

        [HarmonyPrefix]
        public static bool Prefix(TankPreset.BlockSpec __instance, ref BlockTypes __result)
        {
            // SnapshotLoaderMod.logger.Debug($"Checking if {__instance.block} ({(int) __instance.m_BlockType}) is a legacy block");
            // TryGetSessionID(int legacyId, out int newId)
            int blockID = (int)__instance.m_BlockType;

            // If less than or equal to # of vanilla block IDs, then we know is vanilla
            if (blockID < VanillaIDs)
            {
                __result = __instance.m_BlockType;
                return false;
            }

            int newID;
            if (LegacyLookupIDCache.TryGetValue(blockID, out newID))
            {
                SnapshotLoaderMod.logger.Trace($"Returning cached session ID of {newID} for LEGACY block with name {__instance.block}, saved block ID {blockID}");
                __result = (BlockTypes)newID;
                return false;
            }
            else if (SessionIDCache.TryGetValue(__instance.block, out newID))
            {
                SnapshotLoaderMod.logger.Trace($"Returning cached session ID of {newID} for OFFICIAL block with name {__instance.block}, saved block ID {blockID}");
                __result = (BlockTypes)newID;
                return false;
            }
            else
            {
                SnapshotLoaderMod.logger.Trace($"Trying to load block with name {__instance.block}");
                bool isLegacy = IsLegacy(blockID);
                if (isLegacy && GetSessionIDForLegacyBlock(blockID, __instance.block, out int sessionID))
                {
                    __result = (BlockTypes)sessionID;
                    return false;
                }

                // Try to find this block via official means
                int testBlockID = Singleton.Manager<ManMods>.inst.GetBlockID(__instance.block);
                // It should exist
                if (testBlockID > 3)
                {
                    SnapshotLoaderMod.logger.Debug($"Found session ID {testBlockID} for block [{__instance.block} ({blockID})]");
                    __result = (BlockTypes)testBlockID;
                    if (isLegacy)
                    {
                        LegacyLookupIDCache.Put(blockID, testBlockID);
                    }
                    else
                    {
                        SessionIDCache.Put(__instance.block, testBlockID);
                    }
                    return false;
                }
                else
                {
                    // Does not exist
                    SnapshotLoaderMod.logger.Warn($"FAILED to find session ID for block [{__instance.block} ({blockID})]");
                    if (isLegacy)
                    {
                        // If BlockInjector is present, assume block may have been loaded through there, don't touch it
                        // Otherwise, there may be the case it's a legitimate official modded block that got removed. Set the ID to something invalid so no mistakes are made
                        if (SnapshotLoaderMod.HasBlockInjector)
                        {
                            newID = blockID;
                        }
                        else
                        {
                            newID = AssignNewInvalidID(blockID, __instance.block);
                        }
                        SnapshotLoaderMod.logger.Debug($"BlockInjector found: {SnapshotLoaderMod.HasBlockInjector}");
                        SnapshotLoaderMod.logger.Debug($"Resolving {blockID} to ID {newID}");
                        LegacyLookupIDCache.Put(blockID, newID);
                    }
                    else
                    {
                        newID = AssignNewInvalidID(blockID, __instance.block);
                        SnapshotLoaderMod.logger.Debug($"Invalid official block found, resolved to invalid ID: {newID}");
                        SessionIDCache.Put(__instance.block, newID);
                    }
                    __result = (BlockTypes)newID;
                    return false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ManMods), "FindBlockName")]
    internal class PatchInvalidBlockName
    {
        internal static readonly FieldInfo m_BlockNames = AccessTools.Field(typeof(StringLookup), "m_BlockNames");
        internal static readonly MethodInfo GetString = AccessTools.Method(typeof(StringLookup), "GetString");

        internal static void Postfix(int blockID, ref string __result)
        {
            if (__result.Equals("Unknown Modded Block"))
            {
                string cachedName = PatchSnapshotLoadCompatibility.GetNameOfInvalidID(blockID);
                if (cachedName == null)
                {
                    SnapshotLoaderMod.logger.Error($"ID {blockID} has no cached name?");
                }
                else
                {
                    SnapshotLoaderMod.logger.Info($"Got name {cachedName} for invalid ID {blockID}");
                    __result = cachedName;
                }
            }
        }
    }

    [HarmonyPatch(typeof(TechData.SerializedSnapshotData), "CreateTechData")]
    internal static class ObserveBlockSpecs
    {
        internal static void Postfix(ref TechData __result)
        {
            if (SnapshotLoaderMod.logger.minLoggingLevel == 0 && __result != null && __result.m_BlockSpecs != null)
            {
                SnapshotLoaderMod.logger.Trace($"Patched loading of techData: {__result.Name}");
                for (int i = 0; i < __result.m_BlockSpecs.Count; i++)
                {
                    TankPreset.BlockSpec blockSpec = __result.m_BlockSpecs[i];
                    SnapshotLoaderMod.logger.Trace($"  Found block with ID: {blockSpec.m_BlockType}");
                    if (blockSpec.m_BlockType != blockSpec.GetBlockType())
                    {
                        SnapshotLoaderMod.logger.Warn($"  Inconsistent IDs! recorded: {blockSpec.m_BlockType}, adjusted: {blockSpec.GetBlockType()}");
                        // blockSpec.m_BlockType = blockSpec.GetBlockType();
                    }
                    if (Singleton.Manager<ManSpawn>.inst.GetBlockPrefab(blockSpec.m_BlockType) == null)
                    {
                        SnapshotLoaderMod.logger.Error($"  FAILED TO FIND PREFAB FOR BLOCK {blockSpec.block} ({blockSpec.GetBlockType()})!");
                    }
                    SnapshotLoaderMod.logger.Trace($"    position: {blockSpec.position}");
                    SnapshotLoaderMod.logger.Trace($"    orthoRotation: {blockSpec.orthoRotation}");
                    SnapshotLoaderMod.logger.Trace($"    block: {blockSpec.block}");
                    SnapshotLoaderMod.logger.Trace($"    m_VisibleID: {blockSpec.m_VisibleID}");
                    SnapshotLoaderMod.logger.Trace($"    m_SkinID: {blockSpec.m_SkinID}");
                    SnapshotLoaderMod.logger.Trace($"    m_textSerialData:");
                    if (blockSpec.textSerialData == null) {
                        foreach (string data in blockSpec.textSerialData)
                        {
                            SnapshotLoaderMod.logger.Trace($"      {data}");
                        }
                    }
                    else
                    {
                        SnapshotLoaderMod.logger.Trace($"      <null>");
                    }
                    // __result.m_BlockSpecs[i] = blockSpec;
                }
            }
        }
    }

    /* Should not be needed, since we're now only using official IDs
    [HarmonyPatch(typeof(ManMods), "IsModdedBlock")]
    internal static class PatchLegacyIDCompatibility
    {
        [HarmonyPostfix]
        public static void Postfix(BlockTypes type, ref bool __result)
        {
            if (!__result)
            {
                int blockID = (int)type;
                // we know is in Vanilla range, continue
                if (blockID < PatchSnapshotLoadCompatibility.VanillaIDs)
                {
                    return;
                }

                bool isLegacy = PatchSnapshotLoadCompatibility.IsLegacy(blockID);
                if (isLegacy)
                {
                    if (PatchSnapshotLoadCompatibility.LegacyLookupIDCache.TryGetValue(blockID, out int sessionID))
                    {
                        __result = sessionID < PatchSnapshotLoadCompatibility.InvalidID;
                        return;
                    }
                    else if (PatchSnapshotLoadCompatibility.GetSessionIDForLegacyBlock(blockID, out sessionID))
                    {
                        __result = true;
                        return;
                    }
                }
            }
        }
    }
    */

    [HarmonyPatch(typeof(TechData.SerializedSnapshotData), MethodType.Constructor, new Type[] { typeof(TechData) })]
    public static class PatchSnapshotSaveCompatibility
    {
        internal static FieldInfo m_BoundsDoubleExtents = typeof(TechData).GetField("m_BoundsDoubleExtents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static FieldInfo m_CurrentSession = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static FieldInfo m_AdditionalJsonData = typeof(TankPreset.BlockSpec).GetField("m_AdditionalJsonData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static TankPreset.BlockSpec CopyBlockSpec(TankPreset.BlockSpec input)
        {
            TankPreset.BlockSpec output = new TankPreset.BlockSpec();
            output.block = input.block;
            output.m_BlockType = input.m_BlockType;
            output.m_SkinID = input.m_SkinID;
            output.m_VisibleID = input.m_VisibleID;
            output.orthoRotation = input.orthoRotation;
            output.position = input.position;
            output.saveState = input.saveState;
            output.textSerialData = input.textSerialData;

            IDictionary<string, JToken> additionalData = (IDictionary<string, JToken>) m_AdditionalJsonData.GetValue(input);
            if (additionalData != null)
            {
                m_AdditionalJsonData.SetValue(output, additionalData);
            }

            return output;
        }


        internal static List<TankPreset.BlockSpec> ConvertBlockSpecs(List<TankPreset.BlockSpec> inputList)
        {
            ModSessionInfo currentSession = (ModSessionInfo) m_CurrentSession.GetValue(Singleton.Manager<ManMods>.inst);
            Dictionary<int, string> blockIDs = currentSession.BlockIDs;
            List<TankPreset.BlockSpec> result = new List<TankPreset.BlockSpec>();
            for (int i = 0; i < inputList.Count; i++)
            {
                TankPreset.BlockSpec currBlock = inputList[i];

                int sessionID = (int)currBlock.GetBlockType();
                if (sessionID >= ManMods.k_FIRST_MODDED_BLOCK_ID && blockIDs.TryGetValue(sessionID, out string blockID))
                {
                    // continue
                    if (ModUtils.SplitCompoundId(blockID, out string modID, out string blockName))
                    {
                        if (modID == "LegacyBlockLoader")
                        {
                            if (NuterraMod.TryGetLegacyID(blockID, out int legacyID))
                            {
                                SnapshotLoaderMod.logger.Trace($"Setting BlockType of injected legacy block {blockID} back to legacy ID {legacyID}");
                                TankPreset.BlockSpec copy = CopyBlockSpec(currBlock);
                                copy.m_BlockType = (BlockTypes) legacyID;
                                result.Add(copy);
                            }
                            else
                            {
                                SnapshotLoaderMod.logger.Warn($"FAILED to find legacy ID of legacy block {blockID} ({sessionID})");
                                result.Add(currBlock);
                            }
                        }
                        else
                        {
                            SnapshotLoaderMod.logger.Trace($"Block {blockID} ({sessionID}) is not in mod LegacyBlockLoader");
                            result.Add(currBlock);
                        }
                    }
                    else
                    {
                        // wtf? Can't find mod name properly ... don't touch blockspec
                        SnapshotLoaderMod.logger.Error($"FAILED to parse block ID {blockID} ({sessionID})");
                        result.Add(currBlock);
                    }
                }
                else
                {
                    // wtf? Block not in session? don't touch blockspec
                    if (sessionID >= ManMods.k_FIRST_MODDED_BLOCK_ID)
                    {
                        SnapshotLoaderMod.logger.Error($"FAILED to find session info of block with block ID {sessionID}");
                    }
                    result.Add(currBlock);
                }
            }

            return result;
        }

        [HarmonyPrefix]
        public static void Prefix(ref TechData techData)
        {
            SnapshotLoaderMod.logger.Trace($"Patching saving of Tech {techData.Name}");
            TechData replacement = new TechData() {
                Name = techData.Name,
                m_CreationData = techData.m_CreationData,
                m_BlockSpecs = ConvertBlockSpecs(techData.m_BlockSpecs),
                m_SkinMapping = techData.m_SkinMapping,
                m_TechSaveState = techData.m_TechSaveState
            };

            IntVector3 boundsDoubleExtents = (IntVector3) m_BoundsDoubleExtents.GetValue(techData);
            m_BoundsDoubleExtents.SetValue(replacement, boundsDoubleExtents);

            // Killswitch states moved to block serial data
            // bool[] killSwitchStates = techData.GetKillswitchStates();
            // replacement.SetKillswitchStates(killSwitchStates);

            List<ControlScheme> controlSchemes = techData.GetControlSchemes();
            replacement.SetControlSchemesFromSnapshot(controlSchemes);

            techData = replacement;
        }
    }
}
