using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using CustomModules;
using Newtonsoft.Json.Linq;
using HarmonyLib;


namespace LegacyBlockLoader
{
    public static class DirectoryBlockLoader
    {
        private static Dictionary<string, HashSet<string>> UsedPathNames = new Dictionary<string, HashSet<string>>();
        internal static Dictionary<string, string> FileNameReplacements = new Dictionary<string, string>();

        internal static Dictionary<UnofficialBlock, HashSet<string>> UsedAssetPathsPerBlock = new Dictionary<UnofficialBlock, HashSet<string>>();
        internal static Dictionary<string, List<UnofficialBlock>> ConsumingBlocksPerAssetPath = new Dictionary<string, List<UnofficialBlock>>();
        internal static Dictionary<string, UnofficialBlock> BlockByPath = new Dictionary<string, UnofficialBlock>();
        internal static Dictionary<int, UnofficialBlock> LegacyBlocks = new Dictionary<int, UnofficialBlock>();

        private static readonly Regex FilesRegex = new Regex(
            @":\s*" + Regex.Escape("\"") + @"[^" + Regex.Escape("\"") + @"]+\.[a-zA-Z]+" + Regex.Escape("\""),
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        internal static string ResolveUsedAssetFilenames(string text, string path, out HashSet<string> usedFilePaths)
        {
            string relativePath = DirectoryAssetLoader.GetRelAssetPath(path);
            MatchCollection referencedFiles = FilesRegex.Matches(text);
            Dictionary<string, string> closestFiles = new Dictionary<string, string>();
            usedFilePaths = new HashSet<string>();

            // Take note of what files this references.
            foreach (Match file in referencedFiles)
            {
                string fileName = file.Value.Substring(1).Trim().Replace("\"", "");
                BlockLoaderMod.logger.Debug("  🔎 Found file reference: " + fileName);
                string actualFileName = Path.GetFileName(fileName);
                if (!closestFiles.ContainsKey(actualFileName))
                {
                    if (DirectoryAssetLoader.AssetPaths.TryGetValue(actualFileName, out List<string> paths))
                    {
                        // If there actually is a file by the correct name present, check which instance is closest to the block.json
                        string closest = GetClosestPath(relativePath, paths);
                        closestFiles.Add(actualFileName, closest);
                        string[] fileNameTokens = actualFileName.Split('.');
                        BlockLoaderMod.logger.Debug("  ➡ Resolved to closest path: " + closest);

                        // Update FileNameReplacements so we know which alias to refer to the filenames by
                        if (UsedPathNames.TryGetValue(actualFileName, out HashSet<string> usedNames))
                        {
                            if (!usedNames.Contains(closest))
                            {
                                FileNameReplacements.Add(closest, fileNameTokens[0] + $"_N{usedNames.Count}." + fileNameTokens[1]);
                                usedNames.Add(closest);
                                usedFilePaths.Add(closest);
                            }
                        }
                        else
                        {
                            UsedPathNames.Add(actualFileName, new HashSet<string> { closest });
                            FileNameReplacements.Add(closest, fileNameTokens[0] + $"_N0." + fileNameTokens[1]);
                            usedFilePaths.Add(closest);
                        }
                    }
                }
            }

            StringBuilder sb = new StringBuilder(text);
            foreach (KeyValuePair<string, string> pair in closestFiles)
            {
                sb.Replace(pair.Key, FileNameReplacements[pair.Value]);
            }
            return sb.ToString();
        }
        private static int PathMatch(string path1, string path2)
        {
            string[] path1Tokens = path1.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            string[] path2Tokens = path2.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            int minLength = Math.Min(path1Tokens.Length, path2Tokens.Length);
            int score = 0;
            for (int i = 0; i < minLength; i++)
            {
                if (path1Tokens[i] == path2Tokens[i])
                {
                    score++;
                }
                else
                {
                    break;
                }
            }
            return score;
        }
        private static string GetClosestPath(string path, List<string> paths)
        {
            int maxMatch = 0;
            string closestPath = paths.First();
            foreach (string possiblePath in paths)
            {
                int match = PathMatch(path, possiblePath);
                if (match > maxMatch)
                {
                    match = maxMatch;
                    closestPath = possiblePath;
                }
            }
            return closestPath;
        }

        internal static void RegisterAssetPathsUsedByBlock(UnofficialBlock block, HashSet<string> usedAssetPaths)
        {
            BlockByPath[block.path] = block;

            bool needsRepair = false;
            HashSet<string> newAssetPaths = new HashSet<string>();
            HashSet<string> oldAssetPaths = new HashSet<string>();

            // Determine if need to repair hashmap
            if (UsedAssetPathsPerBlock.TryGetValue(block, out HashSet<string> currentPaths))
            {
                foreach (string path in currentPaths)
                {
                    if (!usedAssetPaths.Contains(path))
                    {
                        oldAssetPaths.Add(path);
                    }
                }
                foreach (string path in usedAssetPaths)
                {
                    if (!currentPaths.Contains(path))
                    {
                        newAssetPaths.Add(path);
                    }
                }
                needsRepair = oldAssetPaths.Count > 0;
            }
            else
            {
                newAssetPaths = usedAssetPaths;
            }
            if (needsRepair)
            {
                foreach (string path in oldAssetPaths)
                {
                    if (ConsumingBlocksPerAssetPath.TryGetValue(path, out List<UnofficialBlock> usedBlockList))
                    {
                        usedBlockList.Remove(block);
                    }
                }
            }
            foreach (string path in newAssetPaths)
            {
                if (ConsumingBlocksPerAssetPath.TryGetValue(path, out List<UnofficialBlock> usedBlockList))
                {
                    usedBlockList.Add(block);
                }
                else
                {
                    usedBlockList = new List<UnofficialBlock>() { block };
                    ConsumingBlocksPerAssetPath[path] = usedBlockList;
                }
            }
            HashSet<string> replacement = new HashSet<string>();
            foreach (string path in usedAssetPaths)
            {
                replacement.Add(path);
            }
            if (UsedAssetPathsPerBlock.TryGetValue(block, out HashSet<string> oldSet))
            {
                oldSet.Clear();
            }
            UsedAssetPathsPerBlock[block] = replacement;
        }

        // Register assets so we can look them up later
        // Still need to hook into injection since we're not modifying m_Blocks in ModContents
        public static IEnumerator<float> RegisterBlockDefs(ModContainer container)
        {
            yield return 0.0f;
            // Assert block id uniqueness:
            Dictionary<string, UnofficialBlock> definitionMap = new Dictionary<string, UnofficialBlock>();
            foreach (KeyValuePair<int, UnofficialBlock> pair in LegacyBlocks)
            {
                string blockID = ModUtils.CreateCompoundId("LegacyBlockLoader", pair.Value.blockDefinition.name);
                int version = 0;
                while (definitionMap.ContainsKey(blockID + (version > 0 ? "_" + version.ToString() : "")))
                {
                    version++;
                }
                blockID += (version > 0 ? "_" + version.ToString() : "");
                pair.Value.blockDefinition.name = ModUtils.GetAssetFromCompoundId(blockID);
                BlockLoaderMod.logger.Debug($" ✔️ Reassigned block {pair.Value.blockDefinition.m_BlockDisplayName} ({pair.Key}) to unique ID {blockID}");
                definitionMap.Add(blockID, pair.Value);
            }

            // Add ModDefinitions as Moddedassets
            foreach (UnofficialBlock block in LegacyBlocks.Values)
            {
                container.RegisterAsset(block.blockDefinition);
                // Assets.Add(block.blockDefinition);
            }
            yield return 1.0f;
            yield break;
        }

        public static void RegisterBlock(FileInfo blockJSON)
        {
            UnofficialBlock block = new UnofficialBlock(blockJSON);
            if (block != null)
            {
                DirectoryBlockLoader.RegisterAssetPathsUsedByBlock(block, block.usedAssetNames);
                LegacyBlocks[block.ID] = block;
            }
        }
    }
}
