using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using LegacyBlockLoader.Datastructures;
using HarmonyLib;
using CustomModules;
using UnityEngine;


namespace LegacySnapshotLoader
{
    [HarmonyPatch(typeof(TankPreset.BlockSpec), "GetBlockType")]
    internal class PatchSnapshotCompatibility
    {
        internal static FieldInfo m_Mods = typeof(ManMods).GetField("m_Mods", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static FieldInfo m_BlockNames = typeof(ManMods).GetField("m_BlockNames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static int VanillaIDs = Enum.GetValues(typeof(BlockTypes)).Length;
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

        internal static int InvalidID = 0;

        internal static LRUCache<int, bool> IsLegacyCache = new LRUCache<int, bool>(500);
        internal static LRUCache<int, int> LegacyLookupIDCache = new LRUCache<int, int>(1000);
        internal static LRUCache<int, int> SessionIDCache = new LRUCache<int, int>(1000);

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
            SnapshotLoaderMod.logger.Info($"Checking if block {id} is a legacy ID");
            if (SearchSortedArray(AcebaIDs, id))
            {
                SnapshotLoaderMod.logger.Info($"Is a WP ID");
                return true;
            }
            else if (SearchSortedRangeList(Claims, id, out int rangeStart))
            {
                if (ModCreators.TryGetValue(rangeStart, out string creator))
                {
                    SnapshotLoaderMod.logger.Info($"Is a {creator} ID");
                }
                else
                {
                    SnapshotLoaderMod.logger.Warn($"Is a modded ID from UNKNOWN author");
                }
                return true;
            }
            // SnapshotLoaderMod.logger.Info($"NOT a modded ID");
            return false;
        }

        [HarmonyPrefix]
        public static bool Prefix(TankPreset.BlockSpec __instance, ref BlockTypes __result)
        {
            // SnapshotLoaderMod.logger.Info($"Checking if {__instance.block} ({(int) __instance.m_BlockType}) is a legacy block");
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
                __result = (BlockTypes)newID;
                return false;
            }
            else if (SessionIDCache.TryGetValue(blockID, out newID))
            {
                __result = (BlockTypes)newID;
                return false;
            }
            else
            {
                SnapshotLoaderMod.logger.Trace("Trying to load block with name {}", __instance.block);
                bool isLegacy = blockID < ManMods.k_FIRST_MODDED_BLOCK_ID;
                if (!isLegacy && !IsLegacyCache.TryGetValue(blockID, out isLegacy)) 
                {
                    isLegacy = IsLegacyID(blockID);
                    IsLegacyCache.Put(blockID, isLegacy);
                }
                if (isLegacy)
                {
                    object[] args = new object[] { blockID, null };
                    if ((bool)TryGetSessionID.Invoke(null, args))
                    {
                        SnapshotLoaderMod.logger.Debug($"Found SESSION ID: {args[1]} for Legacy block [{__instance.block} ({blockID})]");
                        LegacyLookupIDCache.Put(blockID, (int)args[1]);
                        __result = (BlockTypes)args[1];
                        return false;
                    }
                    else
                    {
                        // We know this is a legacy block, but no session ID has been found
                        SnapshotLoaderMod.logger.Warn($"Legacy block [{__instance.block} ({blockID})] NOT in session! Trying official as backup");
                    }
                }

                // Try to find this block via official means
                int testBlockID = Singleton.Manager<ManMods>.inst.GetBlockID(__instance.block);
                // It should exist
                if (testBlockID > 3)
                {
                    SnapshotLoaderMod.logger.Debug("Found session ID {} for block [{} ({})]", testBlockID, __instance.block, blockID);
                    __result = (BlockTypes)testBlockID;
                    if (isLegacy)
                    {
                        LegacyLookupIDCache.Put(blockID, testBlockID);
                    }
                    else
                    {
                        SessionIDCache.Put(blockID, testBlockID);
                    }
                    return false;
                }
                else
                {
                    // Does not exist
                    SnapshotLoaderMod.logger.Error("FAILED to find session ID for block [{} ({})]", __instance.block, blockID);
                    if (isLegacy)
                    {
                        if (blockID >= ManMods.k_FIRST_MODDED_BLOCK_ID)
                        {
                            // Set invalid ID to 1 above highest
                            if (InvalidID == 0)
                            {
                                Dictionary<int, string> blockNames = (Dictionary<int, string>)m_BlockNames.GetValue(Singleton.Manager<ManMods>.inst);
                                InvalidID = ManMods.k_FIRST_MODDED_BLOCK_ID + blockNames.Count() + 1;
                            }
                            newID = InvalidID;
                        }
                        else
                        {
                            newID = blockID;
                        }
                        LegacyLookupIDCache.Put(blockID, newID);
                    }
                    else
                    {
                        // Set invalid ID to 1 above highest
                        if (InvalidID == 0)
                        {
                            Dictionary<int, string> blockNames = (Dictionary<int, string>)m_BlockNames.GetValue(Singleton.Manager<ManMods>.inst);
                            InvalidID = ManMods.k_FIRST_MODDED_BLOCK_ID + blockNames.Count() + 1;
                        }

                        newID = InvalidID;
                        SessionIDCache.Put(blockID, newID);
                    }
                    __result = (BlockTypes)newID;
                    return false;
                }
            }
        }
    }
}
