using System;
using System.Collections.Generic;
using System.Text;
using MHZE.PickupSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MHZE.ItemIcons
{
    public enum ItemIconSourceKind
    {
        None,

        /// <summary>The settings name a prefab for this item.</summary>
        Override,

        /// <summary>The item data's own Item Prefab.</summary>
        ItemPrefab,

        /// <summary>A prefab asset with a component that points at the item data.</summary>
        Prefab,

        /// <summary>An object placed in a scene, found when no prefab uses the item data.</summary>
        Scene,
    }

    /// <summary>Where the model of one item comes from.</summary>
    public sealed class ItemIconSource
    {
        public ItemIconSourceKind kind;

        /// <summary>The prefab asset, or the scene asset.</summary>
        public string path = string.Empty;

        /// <summary>The object to draw, for everything but a scene (a scene's object is found when the scene is open).</summary>
        public GameObject prefab;

        /// <summary>Where the item sits inside its prefab, for the report.</summary>
        public string objectPath = string.Empty;

        /// <summary>Other prefabs or scenes that also use the item data; only the first is drawn.</summary>
        public readonly List<string> others = new List<string>();

        public string Describe()
        {
            switch (kind)
            {
                case ItemIconSourceKind.Override: return "settings override " + path;
                case ItemIconSourceKind.ItemPrefab: return "item prefab " + path;
                case ItemIconSourceKind.Prefab: return "prefab " + path + (string.IsNullOrEmpty(objectPath) ? string.Empty : " (" + objectPath + ")");
                case ItemIconSourceKind.Scene: return "scene " + path;
                default: return "none";
            }
        }
    }

    /// <summary>Finds the item data assets of a project and the model each one is drawn from.</summary>
    public sealed class ItemIconCatalog
    {
        readonly ItemIconSettings settings;
        readonly Dictionary<string, HashSet<string>> dependencies = new Dictionary<string, HashSet<string>>();
        string[] prefabPaths;
        string[] scenePaths;

        public ItemIconCatalog(ItemIconSettings settings)
        {
            this.settings = settings;
        }

        public static string[] Folders(ItemIconSettings settings)
        {
            var folders = new List<string>();
            if (settings.searchFolders != null)
                foreach (string folder in settings.searchFolders)
                    if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)) folders.Add(folder);
            if (folders.Count == 0) folders.Add("Assets");
            return folders.ToArray();
        }

        /// <summary>Every item data asset in the search folders, in path order.</summary>
        public static List<PickableItemData> AllItemData(ItemIconSettings settings)
        {
            var list = new List<PickableItemData>();
            foreach (string guid in AssetDatabase.FindAssets("t:PickableItemData", Folders(settings)))
            {
                var data = AssetDatabase.LoadAssetAtPath<PickableItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data != null) list.Add(data);
            }

            list.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            return list;
        }

        /// <summary>The model to draw for an item: a settings override, the item data's prefab, a prefab that uses it, or a scene that does.</summary>
        public ItemIconSource FindSource(PickableItemData data)
        {
            var result = new ItemIconSource();

            ItemIconOverride entry = settings.FindOverride(data);
            if (entry != null && entry.source != null)
            {
                result.kind = ItemIconSourceKind.Override;
                result.prefab = entry.source;
                result.path = AssetDatabase.GetAssetPath(entry.source);
                return result;
            }

            if (data.ItemPrefab != null)
            {
                result.kind = ItemIconSourceKind.ItemPrefab;
                result.prefab = data.ItemPrefab;
                result.path = AssetDatabase.GetAssetPath(data.ItemPrefab);
                return result;
            }

            string dataPath = AssetDatabase.GetAssetPath(data);
            foreach (string prefabPath in PrefabPaths())
            {
                if (!DependsOn(prefabPath, dataPath)) continue;
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (root == null) continue;
                GameObject item = FindItemObject(root, data);
                if (item == null) continue;

                if (result.kind == ItemIconSourceKind.None)
                {
                    result.kind = ItemIconSourceKind.Prefab;
                    result.path = prefabPath;
                    result.prefab = item;
                    result.objectPath = HierarchyPath(item.transform);
                }
                else
                {
                    result.others.Add(prefabPath);
                }
            }

            if (result.kind != ItemIconSourceKind.None || !settings.searchScenes) return result;

            foreach (string scenePath in ScenePaths())
            {
                if (!DependsOn(scenePath, dataPath)) continue;
                if (result.kind == ItemIconSourceKind.None)
                {
                    result.kind = ItemIconSourceKind.Scene;
                    result.path = scenePath;
                }
                else
                {
                    result.others.Add(scenePath);
                }
            }

            return result;
        }

        /// <summary>The object in a prefab, or scene root, that carries the item data (the item itself).</summary>
        public static GameObject FindItemObject(GameObject root, PickableItemData data)
        {
            foreach (PickableItemBase item in root.GetComponentsInChildren<PickableItemBase>(true))
                if (item != null && item.pickableItemData == data) return item.gameObject;

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && !(behaviour is PickableItemBase) && Refers(behaviour, data)) return behaviour.gameObject;

            return null;
        }

        /// <summary>The item in an open scene: the first match that is switched on and has something to draw, else the first match.</summary>
        public static GameObject FindItemInScene(Scene scene, PickableItemData data)
        {
            GameObject first = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PickableItemBase item in root.GetComponentsInChildren<PickableItemBase>(true))
                {
                    if (item == null || item.pickableItemData != data) continue;
                    if (first == null) first = item.gameObject;
                    if (item.gameObject.activeInHierarchy && item.GetComponentInChildren<Renderer>(false) != null) return item.gameObject;
                }
            }

            if (first != null) return first;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                GameObject other = FindItemObject(root, data);
                if (other != null) return other;
            }

            return null;
        }

        static bool Refers(MonoBehaviour behaviour, PickableItemData data)
        {
            var serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            for (bool enter = true; property.NextVisible(enter); enter = false)
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == data) return true;
            return false;
        }

        bool DependsOn(string assetPath, string dataPath)
        {
            if (!dependencies.TryGetValue(assetPath, out HashSet<string> set))
            {
                set = new HashSet<string>(AssetDatabase.GetDependencies(assetPath, false), StringComparer.Ordinal);
                dependencies[assetPath] = set;
            }

            return set.Contains(dataPath);
        }

        string[] PrefabPaths()
        {
            if (prefabPaths != null) return prefabPaths;
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Folders(settings))) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            prefabPaths = paths.ToArray();
            return prefabPaths;
        }

        // Scenes of the build first (in build order), then the rest by path.
        string[] ScenePaths()
        {
            if (scenePaths != null) return scenePaths;
            var all = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", Folders(settings))) all.Add(AssetDatabase.GUIDToAssetPath(guid));
            all.Sort(StringComparer.Ordinal);

            var ordered = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled && all.Remove(scene.path)) ordered.Add(scene.path);
            ordered.AddRange(all);
            scenePaths = ordered.ToArray();
            return scenePaths;
        }

        public static string HierarchyPath(Transform transform)
        {
            var builder = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent) builder.Insert(0, parent.name + "/");
            return builder.ToString();
        }
    }
}
