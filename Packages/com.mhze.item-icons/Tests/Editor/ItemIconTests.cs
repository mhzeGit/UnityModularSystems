using System.Collections.Generic;
using System.IO;
using MHZE.PickupSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MHZE.ItemIcons.Tests
{
    /// <summary>Draws real icons, so these need a graphics device: do not run them with -nographics.</summary>
    public sealed class ItemIconTests
    {
        const string Root = "Assets/__ItemIconTests";

        ItemIconSettings settings;
        readonly List<Object> made = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Item icons are drawn on the graphics card: run the tests without -nographics.");

            AssetDatabase.DeleteAsset(Root);
            AssetDatabase.CreateFolder("Assets", "__ItemIconTests");
            OpenSavedScene("Base");
            settings = ScriptableObject.CreateInstance<ItemIconSettings>();
            settings.outputFolder = Root + "/Out";
            settings.searchFolders = new[] { Root };
            settings.size = 128;
            settings.supersample = 2;
            settings.contourPixels = 2;
            settings.hideFlags = HideFlags.HideAndDontSave; // replacing the open scene unloads what nothing refers to
            made.Add(settings);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made)
                if (o != null) Object.DestroyImmediate(o);
            made.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Root);
        }

        // Replaces the open scenes with an empty scene that is saved, as a project scene would be.
        static Scene OpenSavedScene(string name)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Root + "/" + name + ".unity");
            return scene;
        }

        // ------------------------------------------------------------------------------------------ fixtures

        static Shader Lit()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            return shader != null ? shader : Shader.Find("Standard");
        }

        static Shader Unlit() => Shader.Find("Unlit/Color");

        Material NewMaterial(string name, Shader shader, Color color)
        {
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Root + "/" + name + ".mat");
            return material;
        }

        PickableItemData NewData(string name)
        {
            var data = ScriptableObject.CreateInstance<PickableItemData>();
            data.name = name;
            data.ItemName = name;
            AssetDatabase.CreateAsset(data, Root + "/" + name + ".asset");
            return data;
        }

        GameObject NewPrefab(string name, PickableItemData data, PrimitiveType shape, Vector3 scale, Color color, bool lit = false)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = NewMaterial(name + "_Mat", lit ? Lit() : Unlit(), color);
            if (data != null) go.AddComponent<PickableItemBase>().pickableItemData = data;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        Texture2D Load(PickableItemData data)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(settings.IconPathFor(data)));
            made.Add(texture);
            return texture;
        }

        static void AlphaBox(Texture2D texture, out int x0, out int y0, out int x1, out int y1)
        {
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            x0 = width; y0 = texture.height; x1 = -1; y1 = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < 128) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            }
        }

        static float Aspect(Texture2D texture)
        {
            AlphaBox(texture, out int x0, out int y0, out int x1, out int y1);
            float width = x1 - x0 + 1;
            float height = y1 - y0 + 1;
            return Mathf.Min(width, height) / Mathf.Max(width, height);
        }

        // ------------------------------------------------------------------------------------------ tests

        [Test]
        public void AnItemIsDrawnOnATransparentBackground()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Sphere, Vector3.one, Color.red);

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Generated), report.ToString());
            Texture2D icon = Load(data);
            Assert.That(icon.width, Is.EqualTo(128));
            Assert.That(icon.GetPixel(0, 0).a, Is.EqualTo(0f).Within(0.01f), "the corner is transparent");
            Color centre = icon.GetPixel(64, 64);
            Assert.That(centre.a, Is.EqualTo(1f).Within(0.01f), "the middle is solid");
            Assert.That(centre.r, Is.GreaterThan(centre.g * 2f + 0.2f), "it is red");
        }

        [Test]
        public void TheItemIsCentredAndFillsTheFrame()
        {
            PickableItemData data = NewData("Ball");
            NewPrefab("Ball", data, PrimitiveType.Sphere, Vector3.one * 0.3f, Color.cyan);

            ItemIconGenerator.Generate(new[] { data }, settings);
            Texture2D icon = Load(data);

            AlphaBox(icon, out int x0, out int y0, out int x1, out int y1);
            float span = Mathf.Max(x1 - x0 + 1, y1 - y0 + 1) / (float)icon.width;
            Assert.That(span, Is.InRange(0.72f, 0.95f), "fills the frame, with some space round it");
            Assert.That((x0 + x1 + 1) * 0.5f / icon.width, Is.EqualTo(0.5f).Within(0.03f), "centred sideways");
            Assert.That((y0 + y1 + 1) * 0.5f / icon.height, Is.EqualTo(0.5f).Within(0.03f), "centred up and down");
        }

        [Test]
        public void ALongItemIsLaidOnTheDiagonal()
        {
            PickableItemData data = NewData("Bar");
            NewPrefab("Bar", data, PrimitiveType.Cube, new Vector3(1.6f, 0.12f, 0.12f), Color.yellow);

            ItemIconGenerator.Generate(new[] { data }, settings);
            float tilted = Aspect(Load(data));
            Assert.That(tilted, Is.GreaterThan(0.6f), "laid on the diagonal it fills a square");

            settings.overrides.Add(new ItemIconOverride { data = data, tilt = ItemIconTilt.Off });
            ItemIconGenerator.Generate(new[] { data }, settings);
            float flat = Aspect(Load(data));
            Assert.That(flat, Is.LessThan(0.5f), "left alone it is a thin strip");
        }

        [Test]
        public void ACompactItemIsNotTilted()
        {
            PickableItemData data = NewData("Ball");
            NewPrefab("Ball", data, PrimitiveType.Sphere, Vector3.one, Color.cyan);

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).message, Does.Contain("roll 0"), report.ToString());
        }

        [Test]
        public void TheKeyLightComesFromTheUpperLeft()
        {
            PickableItemData data = NewData("Ball");
            NewPrefab("Ball", data, PrimitiveType.Sphere, Vector3.one, Color.white, true);

            ItemIconGenerator.Generate(new[] { data }, settings);
            Texture2D icon = Load(data);

            AlphaBox(icon, out int x0, out int y0, out int x1, out int y1);
            float centreX = (x0 + x1) * 0.5f, centreY = (y0 + y1) * 0.5f, reach = (x1 - x0) * 0.3f;
            Color32[] pixels = icon.GetPixels32();
            float upperLeft = 0f, lowerRight = 0f;
            int upperLeftCount = 0, lowerRightCount = 0;
            for (int y = 0; y < icon.height; y++)
            {
                for (int x = 0; x < icon.width; x++)
                {
                    float dx = x - centreX, dy = y - centreY;
                    if (dx * dx + dy * dy > reach * reach * 4f) continue;
                    Color32 p = pixels[y * icon.width + x];
                    float luminance = (p.r + p.g + p.b) / (3f * 255f);
                    if (dx < -reach * 0.3f && dy > reach * 0.3f) { upperLeft += luminance; upperLeftCount++; }
                    if (dx > reach * 0.3f && dy < -reach * 0.3f) { lowerRight += luminance; lowerRightCount++; }
                }
            }

            Assert.That(upperLeftCount, Is.GreaterThan(20));
            Assert.That(lowerRightCount, Is.GreaterThan(20));
            Assert.That(upperLeft / upperLeftCount, Is.GreaterThan(lowerRight / lowerRightCount + 0.05f), "the upper left is lit, the lower right is not");
        }

        [Test]
        public void RunningTwiceChangesNothing()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Capsule, Vector3.one, Color.red);

            ItemIconGenerator.Generate(new[] { data }, settings);
            byte[] first = File.ReadAllBytes(settings.IconPathFor(data));
            Sprite icon = data.ItemIcon;

            ItemIconReport again = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(again.Find(data).status, Is.EqualTo(ItemIconStatus.Unchanged), again.ToString());
            Assert.That(File.ReadAllBytes(settings.IconPathFor(data)), Is.EqualTo(first));
            Assert.That(data.ItemIcon, Is.EqualTo(icon));
        }

        [Test]
        public void TheIconIsASpriteOnTheItemData()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Cube, Vector3.one, Color.red);

            ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(data.ItemIcon, Is.Not.Null);
            string path = settings.IconPathFor(data);
            Assert.That(AssetDatabase.GetAssetPath(data.ItemIcon), Is.EqualTo(path));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.mipmapEnabled, Is.EqualTo(settings.mipMaps));
        }

        [Test]
        public void AnItemWithoutAModelIsReportedAndNothingIsWritten()
        {
            PickableItemData data = NewData("Nowhere");

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.NoSource));
            Assert.That(report.HasProblems, Is.False);
            Assert.That(File.Exists(settings.IconPathFor(data)), Is.False);
        }

        [Test]
        public void SkippedAndFinishedItemsAreLeftAlone()
        {
            PickableItemData skipped = NewData("Skipped");
            NewPrefab("Skipped", skipped, PrimitiveType.Cube, Vector3.one, Color.red);
            settings.overrides.Add(new ItemIconOverride { data = skipped, skip = true });

            PickableItemData done = NewData("Done");
            NewPrefab("Done", done, PrimitiveType.Cube, Vector3.one, Color.red);
            ItemIconGenerator.Generate(new[] { done }, settings);
            Assert.That(done.ItemIcon, Is.Not.Null);

            ItemIconReport report = ItemIconGenerator.Generate(new[] { skipped, done }, settings, true);

            Assert.That(report.Find(skipped).status, Is.EqualTo(ItemIconStatus.Skipped));
            Assert.That(report.Find(done).status, Is.EqualTo(ItemIconStatus.Skipped), "it has an icon, so a run for the missing ones leaves it");
            Assert.That(File.Exists(settings.IconPathFor(skipped)), Is.False);
        }

        [Test]
        public void APlanWritesNothing()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Cube, Vector3.one, Color.red);

            ItemIconReport report = ItemIconGenerator.Plan(settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Planned));
            Assert.That(report.Find(data).source, Does.Contain("Red.prefab"));
            Assert.That(File.Exists(settings.IconPathFor(data)), Is.False);
            Assert.That(data.ItemIcon, Is.Null);
        }

        [Test]
        public void AnItemPlacedInASceneIsDrawnFromTheScene()
        {
            PickableItemData data = NewData("Placed");
            string scenePath = Root + "/Placed.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Placed Capsule";
            go.GetComponent<Renderer>().sharedMaterial = NewMaterial("Placed_Mat", Unlit(), Color.green);
            go.AddComponent<PickableItemBase>().pickableItemData = data;
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            OpenSavedScene("Elsewhere");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            data = AssetDatabase.LoadAssetAtPath<PickableItemData>(Root + "/Placed.asset"); // replacing scenes unloads unused assets

            int scenesBefore = SceneManager.sceneCount;
            Scene activeBefore = SceneManager.GetActiveScene();
            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Generated), report.ToString());
            Assert.That(report.Find(data).source, Does.Contain("scene"));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenesBefore), "every scene the tool opened is closed again");
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeBefore));
            Color centre = Load(data).GetPixel(64, 64);
            Assert.That(centre.g, Is.GreaterThan(centre.r + 0.3f), "it is green");
        }

        [Test]
        public void AnItemInsideAPrefabIsFound()
        {
            PickableItemData data = NewData("Inner");
            var holder = new GameObject("Holder");
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.name = "Thing";
            child.transform.SetParent(holder.transform, false);
            child.GetComponent<Renderer>().sharedMaterial = NewMaterial("Inner_Mat", Unlit(), Color.magenta);
            child.AddComponent<PickableItemBase>().pickableItemData = data;
            PrefabUtility.SaveAsPrefabAsset(holder, Root + "/Holder.prefab");
            Object.DestroyImmediate(holder);

            ItemIconSource source = new ItemIconCatalog(settings).FindSource(data);
            Assert.That(source.kind, Is.EqualTo(ItemIconSourceKind.Prefab));
            Assert.That(source.objectPath, Is.EqualTo("Holder/Thing"));

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);
            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Generated), report.ToString());
            Assert.That(Load(data).GetPixel(64, 64).a, Is.GreaterThan(0.95f));
        }

        [Test]
        public void TheStudioLeavesNothingBehind()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Cube, Vector3.one, Color.red);
            int scenes = SceneManager.sceneCount;
            Scene active = SceneManager.GetActiveScene();
            AmbientMode ambient = RenderSettings.ambientMode;
            int rootObjects = active.rootCount;

            ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes));
            Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(ambient));
            Assert.That(active.rootCount, Is.EqualTo(rootObjects));
        }

        [Test]
        public void WorksWithAnUntitledSceneOpen()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Cube, Vector3.one, Color.red);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            data = AssetDatabase.LoadAssetAtPath<PickableItemData>(Root + "/Red.asset");

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Generated), report.ToString());
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().path, Is.Empty, "an untitled scene is open again afterwards");
        }

        [Test]
        public void AnItemPlacedInASceneIsDrawnWithAnUntitledSceneOpen()
        {
            PickableItemData data = NewData("Placed");
            string scenePath = Root + "/Placed.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.GetComponent<Renderer>().sharedMaterial = NewMaterial("Placed_Mat", Unlit(), Color.green);
            go.AddComponent<PickableItemBase>().pickableItemData = data;
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            data = AssetDatabase.LoadAssetAtPath<PickableItemData>(Root + "/Placed.asset");

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Generated), report.ToString());
            Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
        }

        [Test]
        public void AnUntitledSceneWithChangesIsNotReplaced()
        {
            PickableItemData data = NewData("Red");
            NewPrefab("Red", data, PrimitiveType.Cube, Vector3.one, Color.red);
            Scene untitled = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            data = AssetDatabase.LoadAssetAtPath<PickableItemData>(Root + "/Red.asset");
            var mine = new GameObject("My unsaved work");
            EditorSceneManager.MarkSceneDirty(untitled);

            Assert.Throws<System.InvalidOperationException>(() => ItemIconGenerator.Generate(new[] { data }, settings));

            Assert.That(mine != null, Is.True, "the unsaved work is still there");
            Assert.That(SceneManager.GetActiveScene().rootCount, Is.EqualTo(1));
        }

        [Test]
        public void AnOverrideCanChooseAnotherModel()
        {
            PickableItemData data = NewData("Named");
            GameObject model = NewPrefab("Model", null, PrimitiveType.Cube, Vector3.one, Color.blue);
            settings.overrides.Add(new ItemIconOverride { data = data, source = model });

            ItemIconReport report = ItemIconGenerator.Generate(new[] { data }, settings);

            Assert.That(report.Find(data).status, Is.EqualTo(ItemIconStatus.Generated), report.ToString());
            Assert.That(report.Find(data).source, Does.Contain("settings override"));
            Color centre = Load(data).GetPixel(64, 64);
            Assert.That(centre.b, Is.GreaterThan(centre.r + 0.3f), "it is blue");
        }

        [Test]
        public void AnItemWithNothingToSeeFailsAloneAndTheRestStillWork()
        {
            PickableItemData empty = NewData("Empty");
            var nothing = new GameObject("Nothing");
            nothing.AddComponent<PickableItemBase>().pickableItemData = empty;
            PrefabUtility.SaveAsPrefabAsset(nothing, Root + "/Nothing.prefab");
            Object.DestroyImmediate(nothing);

            PickableItemData fine = NewData("Fine");
            NewPrefab("Fine", fine, PrimitiveType.Cube, Vector3.one, Color.red);

            LogAssert.ignoreFailingMessages = true;
            ItemIconReport report = ItemIconGenerator.Generate(new[] { empty, fine }, settings);
            LogAssert.ignoreFailingMessages = false;

            Assert.That(report.Find(empty).status, Is.EqualTo(ItemIconStatus.Failed));
            Assert.That(report.Find(empty).message, Does.Contain("nothing to draw"));
            Assert.That(report.Find(fine).status, Is.EqualTo(ItemIconStatus.Generated));
        }
    }
}
