using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MHZE.ItemIcons
{
    /// <summary>The picture of one item and what was decided about how to show it.</summary>
    public sealed class ItemIconImage
    {
        public int size;
        public Color32[] pixels;

        /// <summary>Degrees the item was turned about the vertical to bring the long side of its footprint across the picture.</summary>
        public float alignYaw;

        /// <summary>Degrees the camera was rolled to lay the item on the diagonal.</summary>
        public float roll;

        /// <summary>Long side divided by short side of the item's silhouette.</summary>
        public float elongation;
    }

    /// <summary>
    /// Draws items for icons. It builds a scene of its own, far from anything else, with a camera and two lights fixed to
    /// the camera, so every icon is lit from the same side. Each item is drawn through the project's own render
    /// pipeline, twice (on black and on white) to get a clean transparent edge whatever the post effects do, then
    /// framed, turned and given a contour. Use it inside a <c>using</c>: the scene is closed when it is disposed.
    /// </summary>
    public sealed class ItemIconRenderer : IDisposable
    {
        const int ProbeSize = 128;

        readonly ItemIconSettings settings;
        bool replacedUntitled;
        Scene studio;
        Scene previousActive;
        GameObject rig;
        Camera camera;
        RenderTexture probeTarget;
        RenderTexture finalTarget;
        int qualityBefore = -1;

        // Where the camera is.
        Vector3 target;
        Quaternion rotation = Quaternion.identity;
        float distance = 1f;
        float orthoSize = 0.5f;
        float radius = 0.5f;
        float zoom = 1f;

        public ItemIconRenderer(ItemIconSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Item icons need a graphics device: start Unity without -nographics.");

            previousActive = SceneManager.GetActiveScene();
            studio = OpenStudio();
            SceneManager.SetActiveScene(studio);

            if (!string.IsNullOrEmpty(settings.qualityLevel))
            {
                string[] names = QualitySettings.names;
                int index = Array.IndexOf(names, settings.qualityLevel);
                if (index < 0) throw new InvalidOperationException("No quality level called '" + settings.qualityLevel + "'.");
                qualityBefore = QualitySettings.GetQualityLevel();
                QualitySettings.SetQualityLevel(index, false);
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = settings.ambientColor;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            rig = new GameObject("Item Icon Studio");
            rig.transform.position = settings.studioPosition;

            var cameraObject = new GameObject("Item Icon Camera");
            cameraObject.transform.SetParent(rig.transform, false);
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = ~0;
            camera.allowHDR = false;
            camera.allowMSAA = false; // the supersampling smooths the edges; some post effects (an outline with a layer mask) cannot take a multisampled target
            camera.useOcclusionCulling = false;
            camera.aspect = 1f;

            // The lights are children of the camera, so they stay on the same side of every picture whatever the roll.
            AddLight("Key Light", settings.keyColor, settings.keyIntensity, settings.keyAngles);
            AddLight("Fill Light", settings.fillColor, settings.fillIntensity, settings.fillAngles);
        }

        // The scene the icons are drawn in: a new empty scene, added beside the open ones. Unity cannot add a scene beside one
        // that was never saved, so then the open scene is replaced for the length of the run, but only if it is the only one
        // and has no changes (the command line entry point opens a saved scene first, so it never gets here).
        Scene OpenStudio()
        {
            bool untitled = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path)) untitled = true;

            if (!untitled) return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            if (SceneManager.sceneCount == 1 && !SceneManager.GetSceneAt(0).isDirty)
            {
                replacedUntitled = true;
                return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            throw new InvalidOperationException("The open scene has never been saved and has changes. Save it, or open a saved scene, before making item icons.");
        }

        void AddLight(string name, Color color, float intensity, Vector2 angles)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(camera.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(angles.x, angles.y, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        public void Dispose()
        {
            if (probeTarget != null) { probeTarget.Release(); UnityEngine.Object.DestroyImmediate(probeTarget); probeTarget = null; }
            if (finalTarget != null) { finalTarget.Release(); UnityEngine.Object.DestroyImmediate(finalTarget); finalTarget = null; }
            if (rig != null) UnityEngine.Object.DestroyImmediate(rig);

            if (replacedUntitled)
            {
                // What Unity starts with: an untitled scene with a camera and a light.
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }
            else
            {
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                if (studio.IsValid()) EditorSceneManager.CloseScene(studio, true);
            }

            if (qualityBefore >= 0) QualitySettings.SetQualityLevel(qualityBefore, false);
        }

        // ------------------------------------------------------------------------------------------ one item

        /// <summary>Draws one item. <paramref name="source"/> is a prefab asset or an object in an open scene; it is not changed.</summary>
        public ItemIconImage Render(GameObject source, ItemIconOverride entry)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureProbeTarget();

            GameObject subject = BuildSubject(source, entry);
            try
            {
                var image = new ItemIconImage { size = settings.size };
                if (!SubjectBounds(subject, out Bounds bounds))
                    throw new InvalidOperationException("the object has nothing to draw: it has no enabled renderer");

                radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                target = bounds.center;
                zoom = entry != null ? Mathf.Max(0.1f, entry.zoom) : 1f;
                bool ownView = entry != null && entry.overrideView;
                float yaw = ownView ? entry.yaw : settings.yaw;
                float pitch = ownView ? entry.pitch : settings.pitch;

                // 1. Look straight down and turn the item so that the long side of its footprint runs across the picture.
                if (settings.alignLongestSide && !ownView)
                {
                    rotation = Quaternion.Euler(90f, 0f, 0f);
                    ResetDistance();
                    Place();
                    if (Refine(3))
                    {
                        LongSide(CaptureMatte(probeTarget, 1), ProbeSize, out float line, out float elongation, out _);
                        if (elongation >= settings.alignFrom && Mathf.Abs(line) > 0.5f)
                        {
                            subject.transform.RotateAround(target, Vector3.up, line);
                            image.alignYaw = line;
                            if (SubjectBounds(subject, out bounds))
                            {
                                radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                                target = bounds.center;
                            }
                        }
                    }
                }

                // 2. The view of the icon.
                Quaternion viewRotation = Quaternion.Euler(pitch, 180f + yaw, 0f);
                rotation = viewRotation;
                ResetDistance();
                Place();
                if (!Refine(3)) throw new InvalidOperationException("nothing of the item is visible to the camera (check its materials and its layers)");

                // 3. Long items are laid on the diagonal.
                ItemIconTilt mode = entry != null ? entry.tilt : (settings.tilt ? ItemIconTilt.Auto : ItemIconTilt.Off);
                LongSide(CaptureMatte(probeTarget, 1), ProbeSize, out _, out float longness, out Vector2 axis);
                image.elongation = longness;
                if (mode == ItemIconTilt.Fixed || (mode == ItemIconTilt.Auto && longness >= settings.tiltFrom))
                {
                    float wanted = entry != null && mode == ItemIconTilt.Fixed ? entry.tiltAngle : settings.tiltAngle;
                    image.roll = Tilt(viewRotation, axis, wanted);
                }

                // 4. The picture.
                int supersample = Mathf.Clamp(settings.supersample, 1, 4);
                EnsureFinalTarget(settings.size * supersample);
                Color32[] pixels = CaptureMatte(finalTarget, supersample);
                ApplyContour(pixels, settings.size);
                image.pixels = pixels;
                return image;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(subject);
            }
        }

        // A copy of the item in the studio, standing the way it stands in the world but facing no particular way, and
        // dead: no scripts, no lights, no labels.
        GameObject BuildSubject(GameObject source, ItemIconOverride entry)
        {
            Quaternion worldRotation = source.transform.rotation;
            Vector3 scale = source.transform.lossyScale;

            GameObject clone = UnityEngine.Object.Instantiate(source);
            clone.name = source.name;
            clone.transform.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(clone, studio);
            clone.SetActive(true);

            foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) behaviour.enabled = false;
            foreach (Light light in clone.GetComponentsInChildren<Light>(true))
                if (light != null) UnityEngine.Object.DestroyImmediate(light);
            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && IsLabel(renderer)) renderer.gameObject.SetActive(false);
            foreach (LODGroup group in clone.GetComponentsInChildren<LODGroup>(true))
                if (group != null) group.ForceLOD(0);

            Vector3 euler = worldRotation.eulerAngles;
            Quaternion rotationToUse = Quaternion.Euler(euler.x, 0f, euler.z);
            if (entry != null) rotationToUse = Quaternion.Euler(entry.extraRotation) * rotationToUse;
            clone.transform.SetPositionAndRotation(settings.studioPosition, rotationToUse);
            clone.transform.localScale = scale;

            if (settings.particleSeconds > 0f)
                foreach (ParticleSystem particles in clone.GetComponentsInChildren<ParticleSystem>(true))
                    if (particles != null && particles.gameObject.activeInHierarchy)
                    {
                        particles.useAutoRandomSeed = false; // the same flames every run, so a second run changes nothing
                        particles.randomSeed = 1234u;
                        particles.Simulate(settings.particleSeconds, true, true);
                    }

            return clone;
        }

        static bool IsLabel(Renderer renderer)
        {
            foreach (Component component in renderer.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component is TextMesh || component.GetType().Name == "TextMeshPro") return true;
            }

            return false;
        }

        static bool SubjectBounds(GameObject subject, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (Renderer renderer in subject.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            return any;
        }

        // ------------------------------------------------------------------------------------------ the camera

        void ResetDistance()
        {
            bool ortho = settings.fieldOfView < 0.5f;
            distance = ortho ? radius * 3f : radius / Mathf.Sin(settings.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
            orthoSize = radius * 1.05f;
        }

        void Place()
        {
            Transform t = camera.transform;
            t.rotation = rotation;
            t.position = target - t.forward * distance;
            bool ortho = settings.fieldOfView < 0.5f;
            camera.orthographic = ortho;
            if (ortho) camera.orthographicSize = orthoSize;
            else camera.fieldOfView = settings.fieldOfView;
            camera.nearClipPlane = Mathf.Max(0.005f, distance - radius * 1.8f);
            camera.farClipPlane = distance + radius * 1.8f + 1f;
        }

        // Looks at the item through the probe and moves and zooms the camera until the item is centred and fills the
        // picture as the padding says. Measured on pixels, so it does not depend on the shape of the mesh.
        bool Refine(int rounds)
        {
            float wanted = (1f - 2f * settings.padding - 2f * settings.contourPixels / Mathf.Max(32f, settings.size)) * zoom;
            for (int round = 0; round < rounds; round++)
            {
                Color32[] matte = CaptureMatte(probeTarget, 1);
                if (!AlphaBounds(matte, ProbeSize, 77, out int x0, out int y0, out int x1, out int y1)) return false;

                float width = (x1 - x0 + 1) / (float)ProbeSize;
                float height = (y1 - y0 + 1) / (float)ProbeSize;
                float centreX = (x0 + x1 + 1) * 0.5f / ProbeSize * 2f - 1f;
                float centreY = (y0 + y1 + 1) * 0.5f / ProbeSize * 2f - 1f;

                Transform t = camera.transform;
                float half = camera.orthographic ? orthoSize : distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                target += t.right * (centreX * half) + t.up * (centreY * half);

                float factor = Mathf.Max(width, height) / Mathf.Max(0.05f, wanted);
                factor = Mathf.Clamp(factor, 0.4f, 2.5f);
                if (camera.orthographic) orthoSize *= factor;
                else distance *= factor;
                Place();
            }

            return true;
        }

        // Rolls the camera so that the line from the top of the item to its bottom points at `wanted` degrees (counter-clockwise
        // from the right). It tries the roll and its opposite and keeps the one that lands nearest, so it does not rely on the
        // direction a positive roll turns the picture.
        float Tilt(Quaternion viewRotation, Vector2 axisUp, float wanted)
        {
            float topToBottom = Mathf.Atan2(-axisUp.y, -axisUp.x) * Mathf.Rad2Deg;
            float turn = Mathf.DeltaAngle(topToBottom, wanted);
            float best = 0f;
            float bestError = float.MaxValue;

            foreach (float roll in new[] { -turn, turn })
            {
                rotation = viewRotation * Quaternion.Euler(0f, 0f, roll);
                Place();
                if (!Refine(2)) continue;
                LongSide(CaptureMatte(probeTarget, 1), ProbeSize, out _, out _, out Vector2 axisNow);
                float now = Mathf.Atan2(-axisNow.y, -axisNow.x) * Mathf.Rad2Deg;
                float error = Mathf.Abs(Mathf.DeltaAngle(now, wanted));
                if (error < bestError) { bestError = error; best = roll; }
                if (error < 8f) break;
            }

            rotation = viewRotation * Quaternion.Euler(0f, 0f, best);
            Place();
            Refine(3);
            return best;
        }

        // ------------------------------------------------------------------------------------------ pixels

        void EnsureProbeTarget()
        {
            if (probeTarget == null) probeTarget = NewTarget(ProbeSize, 1);
        }

        void EnsureFinalTarget(int pixels)
        {
            if (finalTarget != null && finalTarget.width == pixels) return;
            if (finalTarget != null) { finalTarget.Release(); UnityEngine.Object.DestroyImmediate(finalTarget); }
            finalTarget = NewTarget(pixels, 1);
        }

        static RenderTexture NewTarget(int pixels, int samples)
        {
            var target = new RenderTexture(pixels, pixels, 24, RenderTextureFormat.ARGB32) { name = "Item Icon", antiAliasing = samples, useMipMap = false };
            target.Create();
            return target;
        }

        Color32[] Capture(RenderTexture texture, Color background)
        {
            camera.targetTexture = texture;
            camera.backgroundColor = background;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
            Color32[] pixels = readable.GetPixels32();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(readable);
            return pixels;
        }

        // The item on a transparent background. It is drawn on black and on white: a pixel the item covers by a fraction
        // `a` is `a * colour` on black and `a * colour + (1 - a)` on white, so `1 - (white - black)` is the coverage and
        // `black / a` the colour, whatever the render pipeline's post effects do to the picture.
        Color32[] CaptureMatte(RenderTexture texture, int downsample)
        {
            Color32[] black = Capture(texture, Color.black);
            Color32[] white = Capture(texture, Color.white);
            if (downsample > 1)
            {
                black = Downsample(black, texture.width, downsample);
                white = Downsample(white, texture.width, downsample);
            }

            var matte = new Color32[black.Length];
            for (int i = 0; i < matte.Length; i++)
            {
                Color32 onBlack = black[i];
                Color32 onWhite = white[i];
                float difference = ((onWhite.r - onBlack.r) + (onWhite.g - onBlack.g) + (onWhite.b - onBlack.b)) / (3f * 255f);
                float alpha = Mathf.Clamp01(1f - difference);
                if (alpha < 0.01f) { matte[i] = default; continue; }
                float inverse = 1f / alpha;
                matte[i] = new Color32(ToByte(onBlack.r * inverse), ToByte(onBlack.g * inverse), ToByte(onBlack.b * inverse), ToByte(alpha * 255f));
            }

            return matte;
        }

        static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }

        static Color32[] Downsample(Color32[] source, int sourceSize, int factor)
        {
            int size = sourceSize / factor;
            var result = new Color32[size * size];
            int count = factor * factor;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0;
                    for (int dy = 0; dy < factor; dy++)
                    {
                        int row = (y * factor + dy) * sourceSize + x * factor;
                        for (int dx = 0; dx < factor; dx++)
                        {
                            Color32 c = source[row + dx];
                            r += c.r; g += c.g; b += c.b;
                        }
                    }

                    result[y * size + x] = new Color32((byte)(r / count), (byte)(g / count), (byte)(b / count), 255);
                }
            }

            return result;
        }

        // A line of the contour colour round the item, one pixel soft at its edge. Transparent pixels also take the nearest
        // colour, so smoothing the icon later never pulls dark or white fringes in.
        void ApplyContour(Color32[] pixels, int size)
        {
            int radiusPixels = settings.contourPixels;
            Color32[] source = (Color32[])pixels.Clone();
            Color contour = settings.contourColor;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    float alpha = source[index].a / 255f;
                    float outline = radiusPixels > 0 ? alpha : 0f;

                    if (radiusPixels > 0)
                    {
                        for (int dy = -radiusPixels; dy <= radiusPixels; dy++)
                        {
                            int yy = y + dy;
                            if (yy < 0 || yy >= size) continue;
                            for (int dx = -radiusPixels; dx <= radiusPixels; dx++)
                            {
                                int xx = x + dx;
                                if (xx < 0 || xx >= size) continue;
                                float reach = radiusPixels + 0.5f - Mathf.Sqrt(dx * dx + dy * dy);
                                if (reach <= 0f) continue;
                                float value = source[yy * size + xx].a / 255f * Mathf.Min(1f, reach);
                                if (value > outline) outline = value;
                            }
                        }
                    }

                    float total = alpha + outline * (1f - alpha);
                    if (total < 0.004f)
                    {
                        pixels[index] = new Color32(ToByte(contour.r * 255f), ToByte(contour.g * 255f), ToByte(contour.b * 255f), 0);
                        continue;
                    }

                    float itemWeight = alpha / total;
                    float r = source[index].r / 255f * itemWeight + contour.r * (1f - itemWeight);
                    float g = source[index].g / 255f * itemWeight + contour.g * (1f - itemWeight);
                    float b = source[index].b / 255f * itemWeight + contour.b * (1f - itemWeight);
                    pixels[index] = new Color32(ToByte(r * 255f), ToByte(g * 255f), ToByte(b * 255f), ToByte(total * 255f));
                }
            }
        }

        // ------------------------------------------------------------------------------------------ silhouettes

        static bool AlphaBounds(Color32[] matte, int size, byte threshold, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = size; minY = size; maxX = -1; maxY = -1;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (matte[y * size + x].a < threshold) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            return maxX >= 0;
        }

        // The long side of the smallest rectangle round the silhouette. `lineAngle` is its angle from the right, in degrees
        // within (-90, 90]; `axis` points along it, upward in the picture. `elongation` is long side over short side.
        static void LongSide(Color32[] matte, int size, out float lineAngle, out float elongation, out Vector2 axis)
        {
            var points = new List<Vector2>();
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (matte[y * size + x].a >= 96) points.Add(new Vector2(x + 0.5f, y + 0.5f));

            lineAngle = 0f;
            elongation = 1f;
            axis = Vector2.right;
            if (points.Count < 3) return;

            List<Vector2> hull = ConvexHull(points);
            float bestArea = float.MaxValue;
            Vector2 bestAxis = Vector2.right;
            float bestLong = 1f, bestShort = 1f;

            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 edge = hull[(i + 1) % hull.Count] - hull[i];
                if (edge.sqrMagnitude < 1e-6f) continue;
                Vector2 along = edge.normalized;
                Vector2 across = new Vector2(-along.y, along.x);
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (Vector2 p in hull)
                {
                    float a = Vector2.Dot(p, along);
                    float b = Vector2.Dot(p, across);
                    if (a < minA) minA = a;
                    if (a > maxA) maxA = a;
                    if (b < minB) minB = b;
                    if (b > maxB) maxB = b;
                }

                float lengthA = maxA - minA + 1f;
                float lengthB = maxB - minB + 1f;
                float area = lengthA * lengthB;
                if (area >= bestArea) continue;
                bestArea = area;
                if (lengthA >= lengthB) { bestAxis = along; bestLong = lengthA; bestShort = lengthB; }
                else { bestAxis = across; bestLong = lengthB; bestShort = lengthA; }
            }

            if (bestAxis.y < -1e-4f || (Mathf.Abs(bestAxis.y) <= 1e-4f && bestAxis.x < 0f)) bestAxis = -bestAxis;
            axis = bestAxis;
            elongation = bestLong / Mathf.Max(1f, bestShort);
            float angle = Mathf.Atan2(bestAxis.y, bestAxis.x) * Mathf.Rad2Deg;
            if (angle > 90f) angle -= 180f;
            if (angle <= -90f) angle += 180f;
            lineAngle = angle;
        }

        static List<Vector2> ConvexHull(List<Vector2> points)
        {
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var hull = new List<Vector2>();
            for (int i = 0; i < points.Count; i++)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }

            int lower = hull.Count + 1;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(points[i]);
            }

            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        static float Cross(Vector2 origin, Vector2 a, Vector2 b)
        {
            return (a.x - origin.x) * (b.y - origin.y) - (a.y - origin.y) * (b.x - origin.x);
        }
    }
}
