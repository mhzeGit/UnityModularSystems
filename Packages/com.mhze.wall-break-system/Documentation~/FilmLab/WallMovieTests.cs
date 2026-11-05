using System.Collections;
using System.Collections.Generic;
using System.IO;
using MHZE.WallBreak;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Lab only: builds a wall, a floor and two cameras, strikes the wall six times and films every frame to JPEGs.</summary>
public class WallMovieTests
{
    static Texture2D BrickTexture()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        var px = new Color32[n * n];
        var rng = new System.Random(5);
        int rows = 16, brickW = 64, brickH = n / rows;
        var rowTint = new float[rows * 10];
        for (int i = 0; i < rowTint.Length; i++) rowTint[i] = (float)rng.NextDouble();
        for (int y = 0; y < n; y++)
        {
            int row = y / brickH;
            int offset = (row % 2) * (brickW / 2);
            for (int x = 0; x < n; x++)
            {
                int bx = (x + offset) % n / brickW;
                int inX = (x + offset) % brickW, inY = y % brickH;
                bool mortar = inX < 3 || inY < 3;
                float t = rowTint[(row * 8 + bx) % rowTint.Length];
                float grain = (float)rng.NextDouble() * 0.12f;
                Color c = mortar ? new Color(0.62f, 0.6f, 0.55f) : Color.Lerp(new Color(0.55f, 0.2f, 0.14f), new Color(0.72f, 0.33f, 0.2f), t) * (0.9f + grain);
                px[y * n + x] = c;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Material Lit(Color color, Texture2D tex = null)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", 0.1f);
        return m;
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator FilmTheBreak()
    {
        string dir = System.Environment.GetEnvironmentVariable("WALL_FRAMES");
        if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "../Frames");
        Directory.CreateDirectory(dir);
        foreach (string old in Directory.GetFiles(dir, "*.jpg")) File.Delete(old);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.32f, 0.38f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.15f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor"; floor.transform.position = new Vector3(0, -1.7f, 0); floor.transform.localScale = new Vector3(30f, 0.4f, 30f);
        floor.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.26f, 0.26f, 0.28f));

        var wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallGo.name = "Wall"; wallGo.transform.position = Vector3.zero; wallGo.transform.localScale = new Vector3(4f, 3f, 0.3f);
        wallGo.GetComponent<Renderer>().sharedMaterial = Lit(Color.white, BrickTexture());
        var wall = wallGo.AddComponent<BreakableWall>();
        yield return null;

        // A person for scale.
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "ScaleFigure"; body.transform.position = new Vector3(-2.3f, -0.6f, -1.6f); body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
        body.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.2f, 0.35f, 0.6f));
        Object.Destroy(body.GetComponent<Collider>());

        Camera Cam(string name, Vector3 pos, Vector3 look, out RenderTexture rt)
        {
            var go = new GameObject(name);
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos; cam.transform.LookAt(look);
            cam.fieldOfView = 52f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f);
            rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            return cam;
        }
        Camera camA = Cam("CamFront", new Vector3(2.0f, -0.1f, -3.7f), new Vector3(0.0f, -0.6f, 0f), out RenderTexture rtA);
        Camera camB = Cam("CamBehind", new Vector3(-2.6f, -0.1f, 3.9f), new Vector3(0f, -0.9f, 0.5f), out RenderTexture rtB);
        var readTex = new Texture2D(960, 540, TextureFormat.RGB24, false);

        Time.captureFramerate = 30;
        float[] strikeAt = { 0.7f, 1.7f, 2.6f, 3.4f, 4.1f, 4.8f };
        Vector3[] spots = {
            new Vector3(0.05f, -0.1f, -0.15f), new Vector3(-0.25f, 0.2f, -0.15f), new Vector3(0.3f, -0.4f, -0.15f),
            new Vector3(-0.15f, -0.6f, -0.15f), new Vector3(0.2f, 0.4f, -0.15f), new Vector3(0f, -0.2f, -0.15f) };
        int next = 0, frame = 0, total = 30 * 12;
        int lastPieces = 0;
        while (frame < total)
        {
            float t = frame / 30f;
            if (next < strikeAt.Length && t >= strikeAt[next])
            {
                wall.Strike(spots[next], Vector3.back);
                next++;
            }
            yield return null;
            foreach (var (cam, rt, tag) in new[] { (camA, rtA, "A"), (camB, rtB, "B") })
            {
                cam.Render();
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                readTex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, tag + "_" + frame.ToString("D4") + ".jpg"), readTex.EncodeToJPG(90));
            }
            if (frame % 30 == 0)
            {
                int pieces = 0, resting = 0;
                foreach (var d in Object.FindObjectsByType<WallDebris>(FindObjectsSortMode.None)) { pieces++; var rb = d.GetComponent<Rigidbody>(); if (rb != null && (rb.IsSleeping() || rb.isKinematic)) resting++; }
                Debug.Log("[WALLMOVIE] t=" + t.ToString("F1") + " hits=" + wall.HitsTaken + " broken=" + wall.IsBroken + " pieces=" + pieces + " resting=" + resting);
                lastPieces = pieces;
            }
            frame++;
        }
        Time.captureFramerate = 0;

        Assert.IsTrue(wall.IsBroken, "wall broke");
        Assert.Greater(lastPieces, 10, "pieces fell");
        var lowest = 1000f; var highest = -1000f;
        foreach (var d in Object.FindObjectsByType<WallDebris>(FindObjectsSortMode.None)) { lowest = Mathf.Min(lowest, d.transform.position.y); highest = Mathf.Max(highest, d.transform.position.y); }
        Debug.Log("[WALLMOVIE] rubble y range " + lowest + " .. " + highest);
        Assert.Greater(lowest, -1.7f, "no piece fell through the floor");
    }
}
