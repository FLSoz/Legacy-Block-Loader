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
        private static Dictionary<string, DateTime> FileChanged = new Dictionary<string, DateTime>();
        private const long WatchDogTimeBreaker = 3000;
        internal static readonly string TTSteamDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name == "Assembly-CSharp").First().Location
            .Replace("Assembly-CSharp.dll", ""), @"../../"
        ));
        private static DirectoryInfo m_CBDirectory;
        internal static DirectoryInfo CBDirectory
        {
            get
            {
                if (m_CBDirectory == null)
                {
                    string BlockPath = Path.Combine(TTSteamDir, "Custom Blocks");
                    try
                    {
                        if (!Directory.Exists(BlockPath))
                        {
                            Directory.CreateDirectory(BlockPath);
                            // Add Block Example.json here?
                        }
                    }
                    catch (Exception E)
                    {
                        BlockLoaderMod.logger.Error(E, "Could not access \"" + BlockPath + "\"!");
                        throw E;
                    }
                    m_CBDirectory = new DirectoryInfo(BlockPath);
                }
                return m_CBDirectory;
            }
        }

        private static Dictionary<string, List<string>> AssetPaths = new Dictionary<string, List<string>>();
        private static Dictionary<string, Texture2D> IconStore = new Dictionary<string, Texture2D>();
        private static Dictionary<string, Mesh> MeshStore = new Dictionary<string, Mesh>();

        private static Dictionary<string, HashSet<string>> UsedPathNames = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, string> FileNameReplacements = new Dictionary<string, string>();

        private static Dictionary<int, UnofficialBlock> LegacyBlocks = new Dictionary<int, UnofficialBlock>();
        private static List<UnityEngine.Object> Assets = new List<UnityEngine.Object>();

        private static bool AssetsLoaded = false;

        private static string GetRelAssetPath(string path)
        {
            string assetPath = Path.GetFullPath(path);
            string commonPath = Path.GetFullPath(CBDirectory.FullName);
            return assetPath.Replace(commonPath, "");
        }

        private static IEnumerator<float> RegisterLowLevelAssets<T>(string extension, Func<string, T> LoadFromFile, Dictionary<string, T> assetDict)
        {
            FileInfo[] assets = CBDirectory.GetFiles(extension, SearchOption.AllDirectories);
            int processed = 0;
            int total = assets.Length;
            foreach (FileInfo assetFile in assets)
            {
                string assetPath = Path.GetFullPath(assetFile.FullName);
                T asset = LoadFromFile(assetPath);

                string assetName = Path.GetFileName(assetFile.FullName);
                string relPath = GetRelAssetPath(assetPath);

                assetDict.Add(relPath, asset);
                if (!AssetPaths.ContainsKey(assetName))
                {
                    AssetPaths.Add(assetName, new List<string> { relPath });
                }
                else
                {
                    AssetPaths[assetName].Add(relPath);
                }
                processed++;
                yield return ((float)processed) / total;
            }
            yield break;
        }

        public static IEnumerator<float> LoadAssets()
        {
            if (!AssetsLoaded)
            {
                IEnumerator<float> textureIterator = RegisterLowLevelAssets<Texture2D>("*.png", TextureFromFile, IconStore);
                while (textureIterator.MoveNext())
                {
                    yield return textureIterator.Current;
                }
                IEnumerator<float> meshIterator = RegisterLowLevelAssets<Mesh>("*.obj", MeshFromFile, MeshStore);
                while (meshIterator.MoveNext())
                {
                    yield return meshIterator.Current;
                }

                // Load blocks
                FileInfo[] blocks = CBDirectory.GetFiles("*.json", SearchOption.AllDirectories);
                int processed = 0;
                int total = blocks.Length;
                foreach (FileInfo block in blocks)
                {
                    try
                    {
                        RegisterBlock(block);
                    }
                    catch (Exception e)
                    {
                        BlockLoaderMod.logger.Error("Failed to register block at path " + block.FullName + "\n" + e);
                    }
                    processed++;
                    yield return ((float)processed) / total;
                }

                // Resolve Assets
                IEnumerator<float> assetsIterator = ResolveAssets();
                while (assetsIterator.MoveNext())
                {
                    yield return assetsIterator.Current;
                }
                AssetsLoaded = true;
            }
            yield break;
        }

        private static readonly Regex FilesRegex = new Regex(
            @":\s*" + Regex.Escape("\"") + @"[^" + Regex.Escape("\"") + @"]+\.[a-zA-Z]+" + Regex.Escape("\""),
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        internal static string ResolveFiles(string text, string path)
        {
            string relativePath = GetRelAssetPath(path);
            MatchCollection referencedFiles = FilesRegex.Matches(text);
            Dictionary<string, string> closestFiles = new Dictionary<string, string>();

            // Take note of what files this references.
            foreach (Match file in referencedFiles)
            {
                string fileName = file.Value.Substring(1).Trim().Replace("\"", "");
                BlockLoaderMod.logger.Debug("Found file reference: " + fileName);
                string actualFileName = Path.GetFileName(fileName);
                if (!closestFiles.ContainsKey(actualFileName))
                {
                    if (AssetPaths.TryGetValue(actualFileName, out List<string> paths))
                    {
                        // If there actually is a file by the correct name present, check which instance is closest to the block.json
                        string closest = GetClosestPath(relativePath, paths);
                        closestFiles.Add(actualFileName, closest);
                        string[] fileNameTokens = actualFileName.Split('.');
                        BlockLoaderMod.logger.Debug("Resolved to closest path: " + closest);

                        // Update FileNameReplacements so we know which alias to refer to the filenames by
                        if (UsedPathNames.TryGetValue(actualFileName, out HashSet<string> usedNames))
                        {
                            if (!usedNames.Contains(closest))
                            {
                                FileNameReplacements.Add(closest, fileNameTokens[0] + $"_N{usedNames.Count}." + fileNameTokens[1]);
                                usedNames.Add(closest);
                            }
                        }
                        else
                        {
                            UsedPathNames.Add(actualFileName, new HashSet<string> { closest });
                            FileNameReplacements.Add(closest, fileNameTokens[0] + $"_N0." + fileNameTokens[1]);
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

        public static Mesh MeshFromFile(string path)
        {
            Mesh modelToEdit = new Mesh();
            return FastObjImporter.Instance.ImportFileFromPath(path, modelToEdit);
        }

        public static Texture2D TextureFromFile(string path)
        {
            byte[] data;
            data = File.ReadAllBytes(path);
            Texture2D texture;
            texture = new Texture2D(2, 2);
            texture.LoadImage(data);
            return texture;
        }

        public static void RegisterBlock(FileInfo blockJSON)
        {
            UnofficialBlock block = new UnofficialBlock(blockJSON);
            if (block != null)
            {
                LegacyBlocks[block.ID] = block;
            }
        }
        
        public static IEnumerator<float> ResolveAssets()
        {
            ModContainer container = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");
            BlockLoaderMod.logger.Info("Loaded Mod Container");

            // Foreach asset, we get the actual value, and then rename it accordingly
            foreach (KeyValuePair<string, string> pair in FileNameReplacements)
            {
                string pathName = pair.Key;
                string assetName = pair.Value;

                if (IconStore.TryGetValue(pathName, out Texture2D icon))
                {
                    icon.name = assetName;
                    Assets.Add(icon);
                }
                else if (MeshStore.TryGetValue(pathName, out Mesh mesh))
                {
                    mesh.name = assetName;
                    Assets.Add(mesh);
                }
            }
            BlockLoaderMod.logger.Info("Processed Assets");

            IEnumerator<float> assetsIterator = RegisterAssets(container);
            while (assetsIterator.MoveNext())
            {
                yield return assetsIterator.Current;
            }

            // Clear the stuff we're not using anymore
            AssetPaths.Clear();
            IconStore.Clear();
            MeshStore.Clear();
            UsedPathNames.Clear();
            FileNameReplacements.Clear();

            // Add all assets into the ModContainer
            container.Contents.m_AdditionalAssets.AddRange(Assets);
            yield break;
        }

        // Register assets so we can look them up later
        // Still need to hook into injection since we're not modifying m_Blocks in ModContents
        public static IEnumerator<float> RegisterAssets(ModContainer container)
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
                BlockLoaderMod.logger.Info($"Reassigned block {pair.Value.blockDefinition.m_BlockDisplayName} ({pair.Key}) to unique ID {blockID}");
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

        internal static readonly MethodInfo AutoAssignIDs = typeof(ManMods)
                .GetMethod(
                    "AutoAssignIDs",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(Dictionary<int, string>), typeof(List<string>), typeof(int), typeof(int) },
                    null
                );

        internal static IEnumerator<float> LegacyBlockIterator(
            ModSessionInfo newSessionInfo,
            Dictionary<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> gradeBlockPerCorp,
            Dictionary<int, Sprite> blockSpriteDict
        )
        {
            BlockLoaderMod.logger.Info("INJECTING LEGACY BLOCKS");
            List<string> blocksToAssign = new List<string>();
            Dictionary<string, UnofficialBlock> definitionMap = new Dictionary<string, UnofficialBlock>();
            List<int> portedIds = new List<int>();
            foreach (KeyValuePair<int, UnofficialBlock> pair in LegacyBlocks)
            {
                if (NuterraMod.TryGetSessionID(pair.Key, out int newId))
                {
                    BlockLoaderMod.logger.Warn($"{pair.Value.blockDefinition.m_BlockDisplayName} ({pair.Key}) has been ported to official as {newId}. Using official version.");
                    portedIds.Add(pair.Key);
                }
                else
                {
                    string blockID = ModUtils.CreateCompoundId("LegacyBlockLoader", pair.Value.blockDefinition.name);
                    definitionMap.Add(blockID, pair.Value);
                    blocksToAssign.Add(blockID);
                    BlockLoaderMod.logger.Debug($"Marking Block {pair.Value.blockDefinition.m_BlockDisplayName} [{blockID}] ({pair.Key}) for injection");
                }
            }
            /* 
            foreach (int portedId in portedIds)
            {
                LegacyBlocks.Remove(portedId);
            }
            */

            // inject into IDs
            AutoAssignIDs.Invoke(Singleton.Manager<ManMods>.inst,
                new object[] { newSessionInfo.BlockIDs, blocksToAssign, ManMods.k_FIRST_MODDED_BLOCK_ID, int.MaxValue });

            ModContainer LegacyBlockLoaderContainer = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");

            /* ^ Maps Corp index to block table
            {
                corp_index: {
                    corp_grade: {
                        block_id (int): ModdedBlockDefinition
                    }
                }
            }
            */

            // Inject each block iterator style
            int processed = 0;
            int numBlocks = blocksToAssign.Count;
            foreach (string assignedBlock in blocksToAssign)
            {
                float progress = (float)processed / (float)numBlocks;
                int blockID = newSessionInfo.BlockIDs.FirstOrDefault(x => x.Value == assignedBlock).Key;
                IEnumerator iterator = InjectLegacyBlock(
                    newSessionInfo, blockID, definitionMap[assignedBlock].ID, definitionMap[assignedBlock].blockDefinition,
                    gradeBlockPerCorp, blockSpriteDict
                );
                while (iterator.MoveNext())
                {
                    yield return progress;
                }
                processed++;
                yield return progress;
            }

            // UpdateBlockUnlockTable(dictionary);
            foreach (KeyValuePair<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> keyValuePair2 in gradeBlockPerCorp)
            {
                foreach (KeyValuePair<int, Dictionary<BlockTypes, ModdedBlockDefinition>> keyValuePair3 in keyValuePair2.Value)
                {
                    int grade = keyValuePair3.Key;
                    if (grade < 0)
                    {
                        Dictionary<BlockTypes, ModdedBlockDefinition> invalidBlocks = keyValuePair3.Value;
                        foreach (KeyValuePair<BlockTypes, ModdedBlockDefinition> invalidPair in invalidBlocks)
                        {
                            BlockLoaderMod.logger.Error($"INVALID bock detected: {invalidPair.Value.m_BlockDisplayName} [{invalidPair.Value.name}] ({invalidPair.Key}), Grade {grade}, Corp {invalidPair.Value.m_Corporation}");
                        }
                    }
                }
            }
            yield break;
        }

        internal static void InjectLegacyBlocks(
            ModSessionInfo newSessionInfo,
            Dictionary<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> gradeBlockPerCorp,
            Dictionary<int, Sprite> blockSpriteDict
        )
        {
            IEnumerator iterator = LegacyBlockIterator(newSessionInfo, gradeBlockPerCorp, blockSpriteDict);
            while (iterator.MoveNext())
            {
                // do nothing
            }
        }

        // this should get hooked to run right after ManMods.InjectModdedBlocks
        // We need to update the Auto-Assigned IDs
        internal static FieldInfo sLoaders = AccessTools.Field(typeof(JSONBlockLoader), "sLoaders");
        internal static IEnumerator LoadBlockJSON(ModContainer mod, int blockID, ModdedBlockDefinition def, TankBlock block)
        {
            if (def != null)
            {
                JObject jobject = null;
                try
                {
                    if (Singleton.Manager<ManMods>.inst.ShouldReadFromRawJSON)
                    {
                        string text = mod.AssetBundlePath.Substring(0, mod.AssetBundlePath.LastIndexOf('/')) + "/BlockJSON/" + def.name + ".json";
                        if (File.Exists(text))
                        {
                            jobject = JObject.Parse(File.ReadAllText(text));
                            BlockLoaderMod.logger.Info("[Mods] Read JSON from " + text + " as an override");
                        }
                        else
                        {
                            BlockLoaderMod.logger.Info("[Mods] Block " + def.name + " could not find a JSON override at " + text);
                        }
                    }
                    if (jobject == null)
                    {
                        jobject = JObject.Parse(def.m_Json.text);
                        BlockLoaderMod.logger.Info("[Mods] Read JSON from asset bundle for " + def.name);
                    }
                }
                catch (Exception e)
                {
                    BlockLoaderMod.logger.Error("FAILED to read BlockJSON");
                    BlockLoaderMod.logger.Error(e);
                    yield break;
                }
                if (jobject != null)
                {
                    Dictionary<string, JSONModuleLoader> loaders = (Dictionary<string, JSONModuleLoader>) sLoaders.GetValue(null);
                    foreach (KeyValuePair<string, JToken> keyValuePair in jobject)
                    {
                        JSONModuleLoader jsonmoduleLoader;
                        if (loaders.TryGetValue(keyValuePair.Key, out jsonmoduleLoader))
                        {
                            try
                            {
                                if (!jsonmoduleLoader.CreateModuleForBlock(blockID, def, block, keyValuePair.Value))
                                {
                                    BlockLoaderMod.logger.Error(string.Format("Failed to parse module {0} in JSON for {1}", keyValuePair.Key, def));
                                }
                            }
                            catch (Exception e)
                            {
                                BlockLoaderMod.logger.Error($"FAILED to process block module {keyValuePair.Key}");
                                BlockLoaderMod.logger.Error(e);
                            }
                        }
                        else
                        {
                            BlockLoaderMod.logger.Error(string.Format("Could not parse module {0} in JSON for {1}", keyValuePair.Key, def));
                        }
                        yield return null;
                    }
                }
            }
            yield break;
        }

        #region Injection helpers
        private static readonly FieldInfo m_BlockNames = typeof(ManMods).GetField("m_BlockNames", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_BlockDescriptions = typeof(ManMods).GetField("m_BlockDescriptions", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo m_BlockIDReverseLookup = typeof(ManMods).GetField("m_BlockIDReverseLookup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static IEnumerator InjectLegacyBlock(
            ModSessionInfo sessionInfo, int blockID, int legacyID, ModdedBlockDefinition moddedBlockDefinition,
            Dictionary<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> dictionary,
            Dictionary<int, Sprite> spriteDict
        ) {
            BlockLoaderMod.CurrentOperationSpecifics.SetValue(null, moddedBlockDefinition.m_BlockDisplayName);

            ModContainer mod = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");
            ManMods manMods = Singleton.Manager<ManMods>.inst;
            int hashCode = ItemTypeInfo.GetHashCode(ObjectTypes.Block, blockID);
            FactionSubTypes corpIndex = manMods.GetCorpIndex(moddedBlockDefinition.m_Corporation, sessionInfo);
            TankBlockTemplate physicalPrefab = moddedBlockDefinition.m_PhysicalPrefab;
            Visible visible = physicalPrefab.GetComponent<Visible>();
            if (visible == null)
            {
                BlockLoaderMod.logger.Debug("Injected LEGACY block " + moddedBlockDefinition.name + " and performed first time setup.");
                if (visible == null)
                {
                    visible = physicalPrefab.gameObject.AddComponent<Visible>();
                }
                UnityEngine.Object component = physicalPrefab.gameObject.GetComponent<Damageable>();
                ModuleDamage moduleDamage = physicalPrefab.gameObject.GetComponent<ModuleDamage>();
                if (component == null)
                {
                    physicalPrefab.gameObject.AddComponent<Damageable>();
                }
                if (moduleDamage == null)
                {
                    moduleDamage = physicalPrefab.gameObject.AddComponent<ModuleDamage>();
                }
                TankBlock tankBlock = physicalPrefab.gameObject.GetComponent<TankBlock>();
                tankBlock.m_BlockCategory = moddedBlockDefinition.m_Category;
                tankBlock.m_BlockRarity = moddedBlockDefinition.m_Rarity;
                tankBlock.m_DefaultMass = Mathf.Clamp(moddedBlockDefinition.m_Mass, 0.0001f, float.MaxValue);
                tankBlock.filledCells = physicalPrefab.filledCells.ToArray();
                tankBlock.attachPoints = physicalPrefab.attachPoints.ToArray();
                visible.m_ItemType = new ItemTypeInfo(ObjectTypes.Block, blockID);

                BlockLoaderMod.logger.Trace("Preparing to read BlockJSON");
                IEnumerator iterator = LoadBlockJSON(mod, blockID, moddedBlockDefinition, tankBlock);
                while (iterator.MoveNext())
                {
                    yield return null;
                }

                physicalPrefab = moddedBlockDefinition.m_PhysicalPrefab;
                physicalPrefab.gameObject.SetActive(false);
                Damageable component3 = physicalPrefab.GetComponent<Damageable>();
                moduleDamage = physicalPrefab.GetComponent<ModuleDamage>();
                tankBlock = physicalPrefab.GetComponent<TankBlock>();
                visible = physicalPrefab.GetComponent<Visible>();
                visible.m_ItemType = new ItemTypeInfo(ObjectTypes.Block, blockID);
                component3.m_DamageableType = moddedBlockDefinition.m_DamageableType;
                moduleDamage.maxHealth = moddedBlockDefinition.m_MaxHealth;
                if (moduleDamage.deathExplosion == null)
                {
                    BlockLoaderMod.logger.Trace($"DEATH EXPLOSION OVERRIDEN FOR {moduleDamage.name} ({blockID})");
                    moduleDamage.deathExplosion = manMods.m_DefaultBlockExplosion;
                }
                foreach (MeshRenderer meshRenderer in physicalPrefab.GetComponentsInChildren<MeshRenderer>())
                {
                    MeshRendererTemplate component4 = meshRenderer.GetComponent<MeshRendererTemplate>();
                    if (component4 != null)
                    {
                        meshRenderer.sharedMaterial = manMods.GetMaterial((int)corpIndex, component4.slot);
                        d.Assert(meshRenderer.sharedMaterial != null, "[LegacyBlockLoader] Custom block " + moddedBlockDefinition.m_BlockDisplayName + " could not load texture. Corp was " + moddedBlockDefinition.m_Corporation);
                    }
                }
                physicalPrefab.gameObject.name = moddedBlockDefinition.name;
                physicalPrefab.gameObject.tag = "Untagged";
                physicalPrefab.gameObject.layer = LayerMask.NameToLayer("Tank");
                MeshCollider[] componentsInChildren2 = tankBlock.GetComponentsInChildren<MeshCollider>();
                for (int i = 0; i < componentsInChildren2.Length; i++)
                {
                    componentsInChildren2[i].convex = true;
                }

                BlockLoaderMod.logger.Debug($"Pooling block {moddedBlockDefinition.name}");
                tankBlock.transform.CreatePool(8);
            }
            else
            {
                BlockLoaderMod.logger.Debug("LEGACY block " + moddedBlockDefinition.name + " has visible present - assuming already parsed, and component pool extant");
                physicalPrefab.gameObject.GetComponent<Visible>().m_ItemType = new ItemTypeInfo(ObjectTypes.Block, blockID);
                physicalPrefab.transform.CreatePool(8);
            }

            BlockLoaderMod.logger.Trace("Setting up final injection");
            BlockLoaderMod.CurrentOperationSpecifics.SetValue(null, moddedBlockDefinition.m_BlockDisplayName);

            Dictionary<int, string> names = (Dictionary<int, string>) m_BlockNames.GetValue(manMods);
            names.Add(blockID, moddedBlockDefinition.m_BlockDisplayName);

            Dictionary<int, string> descriptions = (Dictionary<int, string>) m_BlockDescriptions.GetValue(manMods);
            descriptions.Add(blockID, moddedBlockDefinition.m_BlockDescription);

            Dictionary<string, int> blockIDReverseLookup = (Dictionary<string, int>) m_BlockIDReverseLookup.GetValue(manMods);
            blockIDReverseLookup.Add(moddedBlockDefinition.name, blockID);

            Singleton.Manager<ManSpawn>.inst.AddBlockToDictionary(physicalPrefab.gameObject, blockID);
            Singleton.Manager<ManSpawn>.inst.VisibleTypeInfo.SetDescriptor<FactionSubTypes>(hashCode, corpIndex);
            Singleton.Manager<ManSpawn>.inst.VisibleTypeInfo.SetDescriptor<BlockCategories>(hashCode, moddedBlockDefinition.m_Category);
            Singleton.Manager<ManSpawn>.inst.VisibleTypeInfo.SetDescriptor<BlockRarity>(hashCode, moddedBlockDefinition.m_Rarity);
            Singleton.Manager<RecipeManager>.inst.RegisterCustomBlockRecipe(blockID, moddedBlockDefinition.m_Price);
            if (moddedBlockDefinition.m_Icon != null)
            {
                spriteDict[blockID] = Sprite.Create(moddedBlockDefinition.m_Icon, new Rect(0f, 0f, (float)moddedBlockDefinition.m_Icon.width, (float)moddedBlockDefinition.m_Icon.height), Vector2.zero);
            }
            else
            {
                BlockLoaderMod.logger.Error($"Block {moddedBlockDefinition.name} with ID {blockID} failed to inject because icon was not set");
            }
            if (!dictionary.ContainsKey((int)corpIndex))
            {
                dictionary[(int)corpIndex] = new Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>();
            }
            Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>> dictionary3 = dictionary[(int)corpIndex];
            if (!dictionary3.ContainsKey(moddedBlockDefinition.m_Grade - 1))
            {
                dictionary3[moddedBlockDefinition.m_Grade - 1] = new Dictionary<BlockTypes, ModdedBlockDefinition>();
            }
            dictionary3[moddedBlockDefinition.m_Grade - 1].Add((BlockTypes)blockID, moddedBlockDefinition);
            JSONBlockLoader.Inject(blockID, moddedBlockDefinition);
            BlockLoaderMod.logger.Info($"Injected legacy block {moddedBlockDefinition.name} at ID {blockID}");
            yield break;
        }

        private static readonly FieldInfo m_CurrentSession = typeof(ManMods).GetField("m_CurrentSession", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        internal static void UpdateBlockUnlockTable(Dictionary<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> dictionary)
        {
            BlockUnlockTable blockUnlockTable = Singleton.Manager<ManLicenses>.inst.GetBlockUnlockTable();
            ManMods manMods = Singleton.Manager<ManMods>.inst;

            foreach (KeyValuePair<int, Dictionary<int, Dictionary<BlockTypes, ModdedBlockDefinition>>> keyValuePair2 in dictionary)
            {
                foreach (KeyValuePair<int, Dictionary<BlockTypes, ModdedBlockDefinition>> keyValuePair3 in keyValuePair2.Value)
                {
                    BlockLoaderMod.logger.Info($"Adding extra modded blocks for Corp Index {keyValuePair2.Key}, Grade Index {keyValuePair3.Key}");
                    blockUnlockTable.AddModdedBlocks(keyValuePair2.Key, keyValuePair3.Key, keyValuePair3.Value);
                    if (manMods.IsModdedCorp((FactionSubTypes)keyValuePair2.Key))
                    {
                        ModdedCorpDefinition corpDefinition = manMods.GetCorpDefinition((FactionSubTypes)keyValuePair2.Key, (ModSessionInfo) m_CurrentSession.GetValue(manMods));
                        if (corpDefinition.m_RewardCorp != null)
                        {
                            Singleton.Manager<ManLicenses>.inst.GetRewardPoolTable().AddModdedBlockRewards(keyValuePair3.Value, keyValuePair3.Key, manMods.GetCorpIndex(corpDefinition.m_RewardCorp, null));
                        }
                    }
                    else
                    {
                        Singleton.Manager<ManLicenses>.inst.GetRewardPoolTable().AddModdedBlockRewards(keyValuePair3.Value, keyValuePair3.Key, (FactionSubTypes)keyValuePair2.Key);
                    }
                }
            }
        }
        #endregion
    }
}
