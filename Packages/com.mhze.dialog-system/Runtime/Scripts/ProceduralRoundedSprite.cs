// Generates 9-sliced rounded-rectangle sprites in code so UI can have rounded corners without any art asset.

using System.Collections.Generic;
using UnityEngine;

namespace MHZE.DialogSystem
{
public static class ProceduralRoundedSprite
{
    private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

    /// <summary>
    /// Returns a cached 9-sliced rounded-rectangle sprite with the given corner radius (UI pixels).
    /// The straight edges are one pixel thick, so the sprite stretches to any size via Image.Type.Sliced.
    /// </summary>
    public static Sprite Get(int radius)
    {
        radius = Mathf.Max(1, radius);

        if (Cache.TryGetValue(radius, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite sprite = Build(radius);
        Cache[radius] = sprite;
        return sprite;
    }

    /// <summary>
    /// Destroys every cached sprite. Only call when no Image still uses one, since any sprite that is
    /// currently assigned to an Image would become a missing reference.
    /// </summary>
    public static void ClearCache()
    {
        foreach (Sprite sprite in Cache.Values)
        {
            if (sprite == null)
            {
                continue;
            }

            Texture2D texture = sprite.texture;
            DestroyObject(sprite);
            DestroyObject(texture);
        }

        Cache.Clear();
    }

    private static Sprite Build(int radius)
    {
        int size = radius * 2 + 2;
        float half = size * 0.5f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ProceduralRoundedRect_" + radius,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;

                float qx = Mathf.Abs(px) - (half - radius);
                float qy = Mathf.Abs(py) - (half - radius);
                float outsideX = Mathf.Max(qx, 0f);
                float outsideY = Mathf.Max(qy, 0f);
                float distance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                    + Mathf.Min(Mathf.Max(qx, qy), 0f)
                    - radius;

                float alpha = Mathf.Clamp01(0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Vector4 border = new Vector4(radius, radius, radius, radius);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);
        sprite.name = "ProceduralRoundedRect_" + radius;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static void DestroyObject(Object value)
    {
        if (value == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(value);
        }
        else
        {
            Object.DestroyImmediate(value);
        }
    }
}
}
