using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MHZE.PickupSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace MHZE.ItemIcons
{
    public enum ItemIconStatus
    {
        /// <summary>The icon file or the item data's icon changed.</summary>
        Generated,

        /// <summary>The icon came out the same as the one already there.</summary>
        Unchanged,

        /// <summary>Left alone on purpose: the settings skip it, or it has an icon and only missing ones were asked for.</summary>
        Skipped,

        /// <summary>No model could be found for the item.</summary>
        NoSource,

        /// <summary>Something went wrong; the message says what.</summary>
        Failed,

        /// <summary>A dry run found a model for it; nothing was drawn.</summary>
        Planned,
    }

    public sealed class ItemIconResult
    {
        public PickableItemData data;
        public string dataPath = string.Empty;
        public ItemIconStatus status;
        public string source = string.Empty;
        public string iconPath = string.Empty;
        public string message = string.Empty;
    }

    /// <summary>What a run did, item by item. <see cref="ToString"/> is the text to print or save.</summary>
    public sealed class ItemIconReport
    {
        public readonly List<ItemIconResult> results = new List<ItemIconResult>();
        public string pipeline = string.Empty;
        public double seconds;
        public bool dryRun;

        public int Count(ItemIconStatus status)
        {
            int count = 0;
            foreach (ItemIconResult result in results)
                if (result.status == status) count++;
            return count;
        }

        public bool HasProblems => Count(ItemIconStatus.Failed) > 0;

        public ItemIconResult Find(PickableItemData data)
        {
            foreach (ItemIconResult result in results)
                if (result.data == data) return result;
            return null;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append(dryRun ? "Item icons (dry run, nothing written): " : "Item icons: ")
                .Append(dryRun ? Count(ItemIconStatus.Planned) + " would be drawn, " : string.Empty)
                .Append(Count(ItemIconStatus.Generated)).Append(" generated, ")
                .Append(Count(ItemIconStatus.Unchanged)).Append(" unchanged, ")
                .Append(Count(ItemIconStatus.Skipped)).Append(" skipped, ")
                .Append(Count(ItemIconStatus.NoSource)).Append(" without a model, ")
                .Append(Count(ItemIconStatus.Failed)).Append(" failed")
                .Append(" (").Append(pipeline).Append(", ").Append(seconds.ToString("0.0")).AppendLine(" s)");

            foreach (ItemIconResult result in results)
            {
                text.Append("  ").Append(result.status.ToString().PadRight(9)).Append(' ').Append(result.dataPath);
                if (!string.IsNullOrEmpty(result.source)) text.Append("  <- ").Append(result.source);
                if (!string.IsNullOrEmpty(result.iconPath)) text.Append("  -> ").Append(result.iconPath);
                if (!string.IsNullOrEmpty(result.message)) text.Append("  : ").Append(result.message);
                text.AppendLine();
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Draws an icon for each pickable item from its own model and puts it on the item data (<c>ItemIcon</c>).
    /// Everything is a static call so a person, a menu, a command line or an agent can run it: see the README.
    /// </summary>
    public static class ItemIconGenerator
    {
        sealed class Job
        {
            public PickableItemData data;
            public ItemIconSource source;
            public ItemIconOverride entry;
            public ItemIconResult result;
            public ItemIconImage image;
            public string note = string.Empty;
        }

        /// <summary>Makes an icon for every item data asset in the search folders and assigns it.</summary>
        /// <param name="settings">The settings asset; the project's own (or defaults) when null.</param>
        /// <param name="onlyMissing">Leave items that already have an icon alone.</param>
        public static ItemIconReport GenerateAll(ItemIconSettings settings = null, bool onlyMissing = false)
        {
            return Run(null, settings, onlyMissing, false);
        }

        /// <summary>Makes icons for the given items only.</summary>
        public static ItemIconReport Generate(IEnumerable<PickableItemData> items, ItemIconSettings settings = null, bool onlyMissing = false)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            return Run(items, settings, onlyMissing, false);
        }

        /// <summary>Says what a run would do (which model each item is drawn from, where its icon goes) and writes nothing.</summary>
        public static ItemIconReport Plan(ItemIconSettings settings = null, bool onlyMissing = false)
        {
            return Run(null, settings, onlyMissing, true);
        }

        /// <summary>Draws one item and returns the picture without writing anything. The caller destroys the texture.</summary>
        public static Texture2D Preview(PickableItemData data, ItemIconSettings settings = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            settings = settings != null ? settings : ItemIconSettings.FindOrDefaults();
            ItemIconSource source = new ItemIconCatalog(settings).FindSource(data);
            if (source.kind == ItemIconSourceKind.None) throw new InvalidOperationException("No model found for " + data.name);

            string textureName = data.name + " (icon preview)";
            var pinned = new List<UnityEngine.Object>();
            Pin(data, pinned);
            Pin(settings, pinned);
            Pin(source.prefab, pinned);
            ItemIconImage image = null;
            try
            {
                image = DrawPreview(data, settings, source);
            }
            finally
            {
                Unpin(pinned);
            }

            var texture = new Texture2D(image.size, image.size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, name = textureName };
            texture.SetPixels32(image.pixels);
            texture.Apply(false);
            return texture;
        }

        static ItemIconImage DrawPreview(PickableItemData data, ItemIconSettings settings, ItemIconSource source)
        {
            ItemIconImage image = null;
            using (var renderer = new ItemIconRenderer(settings))
            {
                ItemIconOverride entry = settings.FindOverride(data);
                if (source.kind != ItemIconSourceKind.Scene)
                {
                    image = renderer.Render(source.prefab, entry);
                }
                else
                {
                    bool wasOpen = IsOpen(source.path, out Scene scene);
                    if (!wasOpen) scene = EditorSceneManager.OpenScene(source.path, OpenSceneMode.Additive);
                    try
                    {
                        GameObject item = ItemIconCatalog.FindItemInScene(scene, data);
                        if (item == null) throw new InvalidOperationException("The scene no longer holds the item.");
                        image = renderer.Render(item, entry);
                    }
                    finally
                    {
                        if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            return image;
        }

        static ItemIconReport Run(IEnumerable<PickableItemData> items, ItemIconSettings settings, bool onlyMissing, bool dryRun)
        {
            var watch = Stopwatch.StartNew();
            settings = settings != null ? settings : ItemIconSettings.FindOrDefaults();
            var report = new ItemIconReport { dryRun = dryRun, pipeline = DescribePipeline() };
            var catalog = new ItemIconCatalog(settings);

            // Opening or closing a scene for the studio can unload assets nothing in the scene refers to, which would
            // destroy the item data and prefabs this run is holding. They are marked so that Unity leaves them loaded.
            var pinned = new List<UnityEngine.Object>();
            Pin(settings, pinned);
            try
            {
                return RunPinned(items, settings, onlyMissing, dryRun, watch, report, catalog, pinned);
            }
            finally
            {
                Unpin(pinned);
            }
        }

        static void Unpin(List<UnityEngine.Object> pinned)
        {
            foreach (UnityEngine.Object pinnedObject in pinned)
                if (pinnedObject != null) pinnedObject.hideFlags &= ~HideFlags.DontUnloadUnusedAsset;
            pinned.Clear();
        }

        static void Pin(UnityEngine.Object asset, List<UnityEngine.Object> pinned)
        {
            if (asset == null || (asset.hideFlags & HideFlags.DontUnloadUnusedAsset) != 0) return;
            asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            pinned.Add(asset);
        }

        static ItemIconReport RunPinned(IEnumerable<PickableItemData> items, ItemIconSettings settings, bool onlyMissing, bool dryRun,
            Stopwatch watch, ItemIconReport report, ItemIconCatalog catalog, List<UnityEngine.Object> pinned)
        {

            List<PickableItemData> all = items != null ? new List<PickableItemData>(items) : ItemIconCatalog.AllItemData(settings);
            var jobs = new List<Job>();
            foreach (PickableItemData data in all)
            {
                if (data == null) continue;
                Pin(data, pinned);
                var result = new ItemIconResult { data = data, dataPath = AssetDatabase.GetAssetPath(data), iconPath = settings.IconPathFor(data) };
                report.results.Add(result);

                ItemIconOverride entry = settings.FindOverride(data);
                if (entry != null && entry.skip)
                {
                    result.status = ItemIconStatus.Skipped;
                    result.iconPath = string.Empty;
                    result.message = "skipped by the settings";
                    continue;
                }

                if (onlyMissing && data.ItemIcon != null)
                {
                    result.status = ItemIconStatus.Skipped;
                    result.iconPath = string.Empty;
                    result.message = "already has an icon";
                    continue;
                }

                ItemIconSource source = catalog.FindSource(data);
                if (source.kind == ItemIconSourceKind.None)
                {
                    result.status = ItemIconStatus.NoSource;
                    result.iconPath = string.Empty;
                    result.message = "no prefab or scene uses this item data, so there is no model to draw (name one under Items in the settings)";
                    continue;
                }

                Pin(source.prefab, pinned);
                result.source = source.Describe();
                if (source.others.Count > 0) result.message = "also used by " + string.Join(", ", source.others) + "; only the first is drawn";
                result.status = dryRun ? ItemIconStatus.Planned : ItemIconStatus.Unchanged;
                jobs.Add(new Job { data = data, source = source, entry = entry, result = result });
            }

            if (!dryRun && jobs.Count > 0)
            {
                RenderJobs(jobs, settings);

                // The studio is gone: nothing is opened or closed from here on. The marks come off before anything is saved,
                // or DontUnloadUnusedAsset would be written into the item data (m_ObjectHideFlags).
                Unpin(pinned);
                WriteJobs(jobs, settings);
            }

            watch.Stop();
            report.seconds = watch.Elapsed.TotalSeconds;
            return report;
        }

        // ---------------------------------------------------------------------------------- drawing

        static void RenderJobs(List<Job> jobs, ItemIconSettings settings)
        {
            using (var renderer = new ItemIconRenderer(settings))
            {
                foreach (Job job in jobs)
                    if (job.source.kind != ItemIconSourceKind.Scene) Draw(renderer, job, job.source.prefab);

                var byScene = new Dictionary<string, List<Job>>();
                var order = new List<string>();
                foreach (Job job in jobs)
                {
                    if (job.source.kind != ItemIconSourceKind.Scene) continue;
                    if (!byScene.TryGetValue(job.source.path, out List<Job> list))
                    {
                        list = new List<Job>();
                        byScene[job.source.path] = list;
                        order.Add(job.source.path);
                    }

                    list.Add(job);
                }

                foreach (string path in order)
                {
                    bool wasOpen = IsOpen(path, out Scene scene);
                    try
                    {
                        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        foreach (Job job in byScene[path])
                        {
                            GameObject item = ItemIconCatalog.FindItemInScene(scene, job.data);
                            if (item == null) Fail(job, "the scene no longer holds the item");
                            else Draw(renderer, job, item);
                        }
                    }
                    catch (Exception exception)
                    {
                        foreach (Job job in byScene[path])
                            if (job.image == null && job.result.status != ItemIconStatus.Failed) Fail(job, "could not open " + path + ": " + exception.Message);
                    }
                    finally
                    {
                        if (!wasOpen && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        static void Draw(ItemIconRenderer renderer, Job job, GameObject source)
        {
            try
            {
                job.image = renderer.Render(source, job.entry);
                job.note = "yaw " + job.image.alignYaw.ToString("0") + ", roll " + job.image.roll.ToString("0") + ", " + job.image.elongation.ToString("0.0") + "x long";
            }
            catch (Exception exception)
            {
                Fail(job, exception.Message);
            }
        }

        static void Fail(Job job, string message)
        {
            job.result.status = ItemIconStatus.Failed;
            job.result.message = message;
            Debug.LogWarning("[ItemIcons] " + job.data.name + ": " + message);
        }

        static bool IsOpen(string path, out Scene scene)
        {
            scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        // ---------------------------------------------------------------------------------- files and assets

        static void WriteJobs(List<Job> jobs, ItemIconSettings settings)
        {
            string folder = settings.IconPathFor(jobs[0].data);
            folder = folder.Substring(0, folder.LastIndexOf('/'));
            Directory.CreateDirectory(folder);

            var changed = new HashSet<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Job job in jobs)
                {
                    if (job.image == null) continue;
                    string path = settings.IconPathFor(job.data);
                    byte[] png = Encode(job.image);
                    bool same = File.Exists(path) && BytesEqual(File.ReadAllBytes(path), png);
                    if (!same)
                    {
                        File.WriteAllBytes(path, png);
                        changed.Add(path);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (string path in changed) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            bool reimport = false;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Job job in jobs)
                {
                    if (job.image == null) continue;
                    if (ConfigureImporter(settings.IconPathFor(job.data), settings)) reimport = true;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (reimport) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            bool assigned = false;
            foreach (Job job in jobs)
            {
                if (job.image == null) continue;
                string path = settings.IconPathFor(job.data);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                bool newIcon = sprite != null && job.data.ItemIcon != sprite;
                if (newIcon)
                {
                    job.data.ItemIcon = sprite;
                    EditorUtility.SetDirty(job.data);
                    assigned = true;
                }

                job.result.status = changed.Contains(path) || newIcon ? ItemIconStatus.Generated : ItemIconStatus.Unchanged;
                string message = job.note;
                job.result.message = string.IsNullOrEmpty(job.result.message) ? message : message + "; " + job.result.message;
                if (sprite == null)
                {
                    job.result.status = ItemIconStatus.Failed;
                    job.result.message = "the file was written but Unity did not import it as a sprite";
                }
            }

            if (assigned) AssetDatabase.SaveAssets();
        }

        static byte[] Encode(ItemIconImage image)
        {
            var texture = new Texture2D(image.size, image.size, TextureFormat.RGBA32, false);
            texture.SetPixels32(image.pixels);
            texture.Apply(false);
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            return png;
        }

        static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        // Sprite, clamped, with mip maps so the interface can draw it much smaller than the file. Returns true when it changed.
        static bool ConfigureImporter(string path, ItemIconSettings settings)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return false;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled != settings.mipMaps) { importer.mipmapEnabled = settings.mipMaps; changed = true; }
            if (importer.filterMode != FilterMode.Bilinear) { importer.filterMode = FilterMode.Bilinear; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (importer.isReadable) { importer.isReadable = false; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.CompressedHQ) { importer.textureCompression = TextureImporterCompression.CompressedHQ; changed = true; }
            int maxSize = Mathf.Max(32, Mathf.NextPowerOfTwo(settings.size));
            if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }

            if (changed) importer.SaveAndReimport();
            return changed;
        }

        static string DescribePipeline()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            string name = pipeline != null ? pipeline.name : "built-in pipeline";
            return name + ", quality '" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "'";
        }
    }
}
