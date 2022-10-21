using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace LegacyBlockLoader
{
    internal static class DirectoryAssetLoader
    {
        internal static Dictionary<string, DateTime> FileChanged = new Dictionary<string, DateTime>();
        internal static Dictionary<string, List<string>> AssetPaths = new Dictionary<string, List<string>>();
        internal static Dictionary<string, Texture2D> IconStore = new Dictionary<string, Texture2D>();
        internal static Dictionary<string, Mesh> MeshStore = new Dictionary<string, Mesh>();
        internal static List<UnityEngine.Object> Assets = new List<UnityEngine.Object>();

        internal static string GetRelAssetPath(string path)
        {
            string assetPath = Path.GetFullPath(path);
            string commonPath = Path.GetFullPath(BlockLoaderMod.CBDirectory.FullName);
            return assetPath.Replace(commonPath, "");
        }

        private static IEnumerator<float> RegisterLowLevelAssets<T>(string extension, Func<string, T> LoadFromFile, Dictionary<string, T> assetDict)
        {
            FileInfo[] assets = BlockLoaderMod.CBDirectory.GetFiles(extension, SearchOption.AllDirectories);
            int processed = 0;
            int total = assets.Length;
            foreach (FileInfo assetFile in assets)
            {
                string assetPath = Path.GetFullPath(assetFile.FullName);
                T asset = LoadFromFile(assetPath);

                string assetName = Path.GetFileName(assetFile.FullName);
                string relPath = GetRelAssetPath(assetPath);
                string emojiPrefix = "";
                if (extension == "*.png") { emojiPrefix = "🖼️ "; }
                else if (extension == "*.obj") { emojiPrefix = "📐 "; }
                BlockLoaderMod.logger.Debug($" {emojiPrefix}Located asset {relPath}");

                FileChanged[relPath] = assetFile.LastWriteTime;

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
            yield break;
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

        public static IEnumerator<float> ResolveAssets()
        {
            // Foreach asset, we get the actual value, and then rename it accordingly
            foreach (KeyValuePair<string, string> pair in DirectoryBlockLoader.FileNameReplacements)
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
            yield break;
        }

        // Asset parsing still not fully functional. Meshes *should* work, but textures look like they won't
        internal static void UpdateTexture(string relPath, Texture2D newAsset)
        {
            BlockLoaderMod.logger.Info($"Texture at path {relPath} has been updated");
            Texture2D currentTexture = IconStore[relPath];
            newAsset.name = currentTexture.name;
            int index = BlockLoaderMod.ThisContainer.Contents.m_AdditionalAssets.FindIndex(_ => _ == currentTexture);
            BlockLoaderMod.ThisContainer.Contents.m_AdditionalAssets[index] = newAsset;
        }
        internal static void UpdateMesh(string relPath, Mesh newAsset)
        {
            BlockLoaderMod.logger.Info($"Mesh at path {relPath} has been updated");
            Mesh currentMesh = MeshStore[relPath];
            newAsset.name = currentMesh.name;
            int index = BlockLoaderMod.ThisContainer.Contents.m_AdditionalAssets.FindIndex(_ => _ == currentMesh);
            BlockLoaderMod.ThisContainer.Contents.m_AdditionalAssets[index] = newAsset;
        }

        // Block reparsing won't update any block grade characteristics. Also won't update any recipes
        internal static FieldInfo m_CurrentSession = AccessTools.Field(typeof(ManMods), "m_CurrentSession");
        internal static void ReparseBlock(UnofficialBlock block)
        {
            BlockLoaderMod.logger.Info($"Unofficial block {block.blockDefinition.m_BlockDisplayName} ({block.ID}) must be reparsed");
            block.ParseJSON();
            BlockLoaderMod.logger.Info($"BlockJSON Reparsed");

            JProperty Grade = block.jObject.Property("Grade");
            if (Grade != null)
            {
                Grade.Value = Grade.Value.ToObject<int>() + 1;
            }
            else
            {
                block.jObject.Add("Grade", 1);
            }
            BlockLoaderMod.logger.Info($"Grade set to {Grade.Value}");

            JProperty Category = block.jObject.Property("Category");
            if (Category != null)
            {
                BlockCategories blockCategory = UnofficialBlock.TryParseEnum<BlockCategories>(Category.Value.ToObject<int>(), BlockCategories.Standard);
                BlockLoaderMod.logger.Info($"Category set to {blockCategory}");
            }

            block.WrapJSON();
            BlockLoaderMod.logger.Info($"BlockJSON Wrapped");

            // Fetch needed info
            TankBlockTemplate oldPrefab = block.blockDefinition.m_PhysicalPrefab;
            oldPrefab.transform.DeletePool<Transform>();
            BlockLoaderMod.logger.Debug($"Old pool deleted");

            TankBlock tankBlock = oldPrefab.gameObject.GetComponent<TankBlock>();
            ManMods manMods = Singleton.Manager<ManMods>.inst;
            ModSessionInfo currentSession = (ModSessionInfo)m_CurrentSession.GetValue(manMods);
            FactionSubTypes corpIndex = manMods.GetCorpIndex(block.blockDefinition.m_Corporation, currentSession);
            Visible visible = oldPrefab.GetComponent<Visible>();

            // Run the processor on this again
            int blockSessionID = visible.m_ItemType.ItemType;
            IEnumerator processor = ProcessBlockJSON(blockSessionID, corpIndex, block.blockDefinition, tankBlock);
            while (processor.MoveNext())
            {
            }
            BlockLoaderMod.logger.Info($"BlockJSON Reprocessed {blockSessionID}");
            TankBlockTemplate newPrefab = block.blockDefinition.m_PhysicalPrefab;
            if (newPrefab == null)
            {
                BlockLoaderMod.logger.Error("PREFAB IS NULL");
            }

            if (oldPrefab != newPrefab)
            {
                GameObject.DestroyImmediate(oldPrefab);
                BlockLoaderMod.logger.Debug($"Old prefab destroyed");
            }

            newPrefab.transform.CreatePool<Transform>(8);
            BlockLoaderMod.logger.Debug($"New pool created");

            TankBlock newBlock = newPrefab.GetComponent<TankBlock>();

            Singleton.Manager<ManSpawn>.inst.RemoveBlockFromDictionary(blockSessionID);
            Singleton.Manager<ManSpawn>.inst.AddBlockToDictionary(newPrefab.gameObject, blockSessionID);
            BlockLoaderMod.logger.Debug($"Dictionary handled");

            TankBlock updatedPrefab = Singleton.Manager<ManSpawn>.inst.GetBlockPrefab((BlockTypes) blockSessionID);
            if (updatedPrefab == newBlock && Singleton.Manager<ManSpawn>.inst.IsValidBlockToSpawn((BlockTypes) blockSessionID))
            {
                BlockLoaderMod.logger.Info($"{blockSessionID} Prefabs rewired");
            }
            else
            {
                BlockLoaderMod.logger.Error($"{blockSessionID} FAILED to rewire prefab");
            }
        }

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
                    Dictionary<string, JSONModuleLoader> loaders = (Dictionary<string, JSONModuleLoader>)sLoaders.GetValue(null);
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
                                else
                                {
                                    BlockLoaderMod.logger.Trace($"Parsed module {keyValuePair.Key} successfully");
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

        internal static IEnumerator ProcessBlockJSON(int blockID, FactionSubTypes corpIndex, ModdedBlockDefinition moddedBlockDefinition, TankBlock tankBlock)
        {
            ManMods manMods = Singleton.Manager<ManMods>.inst;
            ModContainer mod = Singleton.Manager<ManMods>.inst.FindMod("LegacyBlockLoader");
            BlockLoaderMod.logger.Trace("Preparing to read BlockJSON");
            IEnumerator iterator = LoadBlockJSON(mod, blockID, moddedBlockDefinition, tankBlock);
            while (iterator.MoveNext())
            {
                yield return null;
            }

            TankBlockTemplate physicalPrefab = moddedBlockDefinition.m_PhysicalPrefab;
            physicalPrefab.gameObject.SetActive(false);
            Damageable component3 = physicalPrefab.GetComponent<Damageable>();
            ModuleDamage moduleDamage = physicalPrefab.GetComponent<ModuleDamage>();
            tankBlock = physicalPrefab.GetComponent<TankBlock>();
            Visible visible = physicalPrefab.GetComponent<Visible>();
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
                    if (meshRenderer.sharedMaterial == null)
                    {
                        BlockLoaderMod.logger.Error("[LegacyBlockLoader] Custom block " + moddedBlockDefinition.m_BlockDisplayName + " could not load texture. Corp was " + moddedBlockDefinition.m_Corporation);
                    }
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
            yield break;
        }

        internal static IEnumerator<object> ReloadAssets()
        {
            FileInfo[] imageAssets = BlockLoaderMod.CBDirectory.GetFiles("*.png", SearchOption.AllDirectories);
            FileInfo[] objAssets = BlockLoaderMod.CBDirectory.GetFiles("*.obj", SearchOption.AllDirectories);
            FileInfo[] blockJSON = BlockLoaderMod.CBDirectory.GetFiles("*.json", SearchOption.AllDirectories);

            HashSet<UnofficialBlock> blocksToReparse = new HashSet<UnofficialBlock>();
            List<string> newBlocks = new List<string>();
            foreach (FileInfo file in blockJSON)
            {
                if (FileChanged.TryGetValue(file.FullName, out DateTime lastEdited))
                {
                    if (file.LastWriteTime > lastEdited)
                    {
                        if (DirectoryBlockLoader.BlockByPath.TryGetValue(file.FullName, out UnofficialBlock block))
                        {
                            blocksToReparse.Add(block);
                        }
                        else
                        {
                            newBlocks.Add(file.FullName);
                        }
                        FileChanged[file.FullName] = file.LastWriteTime;
                    }
                }
                else
                {
                    newBlocks.Add(file.FullName);
                }
                yield return null;
            }

            // Images
            foreach (FileInfo file in imageAssets)
            {
                string assetPath = Path.GetFullPath(file.FullName);
                string relPath = GetRelAssetPath(assetPath);
                if (FileChanged.TryGetValue(relPath, out DateTime lastEdited))
                {
                    if (file.LastWriteTime > lastEdited)
                    {
                        // update .png
                        Texture2D texture = TextureFromFile(assetPath);
                        UpdateTexture(relPath, texture);
                        if (DirectoryBlockLoader.ConsumingBlocksPerAssetPath.TryGetValue(relPath, out List<UnofficialBlock> consumingBlocks))
                        {
                            foreach (UnofficialBlock block in consumingBlocks)
                            {
                                blocksToReparse.Add(block);
                            }
                        }
                        FileChanged[relPath] = file.LastWriteTime;
                    }
                }
                else
                {
                    // new asset
                }
                yield return null;
            }

            // Obj
            foreach (FileInfo file in objAssets)
            {
                string assetPath = Path.GetFullPath(file.FullName);
                string relPath = GetRelAssetPath(assetPath);
                if (FileChanged.TryGetValue(relPath, out DateTime lastEdited))
                {
                    if (file.LastWriteTime > lastEdited)
                    {
                        // update .obj
                        Mesh mesh = MeshFromFile(assetPath);
                        UpdateMesh(relPath, mesh);
                        if (DirectoryBlockLoader.ConsumingBlocksPerAssetPath.TryGetValue(relPath, out List<UnofficialBlock> consumingBlocks))
                        {
                            foreach (UnofficialBlock block in consumingBlocks)
                            {
                                blocksToReparse.Add(block);
                            }
                        }
                        FileChanged[relPath] = file.LastWriteTime;
                    }
                }
                else
                {
                    // new asset
                }
                yield return null;
            }

            // Reparse blocks
            foreach (UnofficialBlock block in blocksToReparse)
            {
                ReparseBlock(block);
                yield return null;
            }

            yield break;
        }
    }
}
