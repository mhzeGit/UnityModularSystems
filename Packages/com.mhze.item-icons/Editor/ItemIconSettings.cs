using System;
using System.Collections.Generic;
using System.IO;
using MHZE.PickupSystem;
using UnityEditor;
using UnityEngine;

namespace MHZE.ItemIcons
{
    /// <summary>How an item is turned in its icon.</summary>
    public enum ItemIconTilt
    {
        /// <summary>Long items are laid on the diagonal, compact ones stay upright.</summary>
        Auto,

        /// <summary>The item is never tilted.</summary>
        Off,

        /// <summary>The item is always tilted so that its long side lies at the entry's Tilt Angle.</summary>
        Fixed,
    }

    /// <summary>Changes for one item. Everything left alone follows the settings.</summary>
    [Serializable]
    public sealed class ItemIconOverride
    {
        [Tooltip("The item data this entry is for.")]
        public PickableItemData data;

        [Tooltip("No icon is made for this item, and the one it has is kept.")]
        public bool skip;

        [Tooltip("Draw this prefab instead of the object the tool finds for the item.")]
        public GameObject source;

        [Tooltip("Turn the item by these angles (world axes, degrees) before it is shown. The fix for an item that lies the wrong way in its prefab.")]
        public Vector3 extraRotation;

        [Tooltip("Use Yaw and Pitch below instead of the settings' view, and do not turn the item's long side across the picture.")]
        public bool overrideView;

        [Tooltip("Camera angle around the item, in degrees. 0 looks at the item's +Z side, positive moves the camera to the right.")]
        public float yaw = 32f;

        [Tooltip("Camera angle above the item, in degrees.")]
        [Range(-10f, 89f)] public float pitch = 26f;

        public ItemIconTilt tilt = ItemIconTilt.Auto;

        [Tooltip("With Tilt = Fixed: the angle of the line from the top of the item to its bottom, counted counter-clockwise from the right. -45 puts the top at the upper left.")]
        public float tiltAngle = -45f;

        [Tooltip("1 fills the frame as the padding says; more is closer.")]
        [Range(0.5f, 1.5f)] public float zoom = 1f;
    }

    /// <summary>
    /// How the icons look and where they go. Find or make one with Tools > MHZE > Item Icons > Select Settings;
    /// without one the tool uses these defaults and writes to Assets/ItemIcons.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemIconSettings", menuName = "MHZE/Item Icons/Settings")]
    public sealed class ItemIconSettings : ScriptableObject
    {
        [Header("Output")]
        [Tooltip("Where the PNG files go. Made when it is missing.")]
        public string outputFolder = "Assets/ItemIcons";

        [Tooltip("Put in front of the item name in the file name.")]
        public string filePrefix = "Icon_";

        [Tooltip("Cut from the front of the item data's name before the prefix is added (PickableItemData_Carrot -> Icon_Carrot).")]
        public string removeFromName = "PickableItemData_";

        [Range(32, 1024)] public int size = 256;

        [Tooltip("Draw at this many times the size and shrink, for clean edges.")]
        [Range(1, 4)] public int supersample = 3;

        [Tooltip("Mip maps keep icons clean when the interface draws them much smaller than the file.")]
        public bool mipMaps = true;

        [Header("Where to look for the models")]
        [Tooltip("Folders searched for prefabs and scenes that use an item data asset.")]
        public string[] searchFolders = { "Assets" };

        [Tooltip("An item that has no prefab can be drawn from the scene it is placed in. The scene is opened beside the others and closed again; nothing in it changes.")]
        public bool searchScenes = true;

        [Header("View")]
        [Tooltip("Camera angle around the item, in degrees. 0 looks at the item's +Z side, positive moves the camera to the right.")]
        public float yaw = 32f;

        [Tooltip("Camera angle above the item, in degrees.")]
        [Range(-10f, 89f)] public float pitch = 26f;

        [Tooltip("Camera field of view in degrees. 0 is an orthographic camera.")]
        [Range(0f, 60f)] public float fieldOfView = 18f;

        [Tooltip("Free space on each side, as a fraction of the picture.")]
        [Range(0f, 0.35f)] public float padding = 0.06f;

        [Tooltip("Turn the item about the vertical until the long side of its footprint runs across the picture.")]
        public bool alignLongestSide = true;

        [Tooltip("The footprint is only turned when its long side is this many times its short side.")]
        public float alignFrom = 1.25f;

        [Header("Tilt")]
        [Tooltip("Lay long items on the diagonal, the way inventory icons usually show a key or a tool.")]
        public bool tilt = true;

        [Tooltip("An item is tilted when its silhouette is this many times longer than wide.")]
        public float tiltFrom = 1.8f;

        [Tooltip("The angle of the line from the top of the item to its bottom, counted counter-clockwise from the right. -45 puts the top at the upper left.")]
        public float tiltAngle = -45f;

        [Header("Light")]
        public Color keyColor = new Color(1f, 0.96f, 0.88f);
        public float keyIntensity = 1.15f;

        [Tooltip("Direction the key light shines along, in camera space. 35, 35 comes from the upper left.")]
        public Vector2 keyAngles = new Vector2(35f, 35f);

        public Color fillColor = new Color(0.74f, 0.82f, 1f);
        public float fillIntensity = 0.4f;

        [Tooltip("Direction the fill light shines along, in camera space. -20, -50 comes from the lower right.")]
        public Vector2 fillAngles = new Vector2(-20f, -50f);

        public Color ambientColor = new Color(0.42f, 0.4f, 0.38f);

        [Header("Contour")]
        [Tooltip("A near-black line round the item, in pixels of the finished icon. 0 for none.")]
        [Range(0, 8)] public int contourPixels = 3;

        public Color contourColor = new Color(0.07f, 0.055f, 0.045f);

        [Header("Studio")]
        [Tooltip("The item is drawn this far from the world's origin, in a scene of its own, so nothing the open scenes contain can show.")]
        public Vector3 studioPosition = new Vector3(0f, 2000f, 0f);

        [Tooltip("The quality level (and so the render pipeline asset) used for drawing. Empty keeps the current one.")]
        public string qualityLevel = "";

        [Tooltip("Particle systems on the item are run this long before the picture is taken.")]
        public float particleSeconds = 0.8f;

        [Header("Items")]
        public List<ItemIconOverride> overrides = new List<ItemIconOverride>();

        /// <summary>The settings asset of the project (the first one found), or null.</summary>
        public static ItemIconSettings Find()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ItemIconSettings"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ItemIconSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }

            return null;
        }

        /// <summary>The settings asset of the project, or the defaults when there is none.</summary>
        public static ItemIconSettings FindOrDefaults()
        {
            ItemIconSettings found = Find();
            if (found != null) return found;
            var defaults = CreateInstance<ItemIconSettings>();
            defaults.hideFlags = HideFlags.DontSave;
            return defaults;
        }

        public ItemIconOverride FindOverride(PickableItemData data)
        {
            if (data == null || overrides == null) return null;
            foreach (ItemIconOverride entry in overrides)
                if (entry != null && entry.data == data) return entry;
            return null;
        }

        /// <summary>The path the icon of this item is written to.</summary>
        public string IconPathFor(PickableItemData data)
        {
            string name = data.name;
            if (!string.IsNullOrEmpty(removeFromName) && name.StartsWith(removeFromName, StringComparison.Ordinal) && name.Length > removeFromName.Length)
                name = name.Substring(removeFromName.Length);
            foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
            string folder = string.IsNullOrEmpty(outputFolder) ? "Assets" : outputFolder.Replace('\\', '/').TrimEnd('/');
            return folder + "/" + filePrefix + name + ".png";
        }
    }
}
