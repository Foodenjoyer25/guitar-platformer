using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GuitarPlatformer.EditorTools
{
    internal static class BuilderUtil
    {
        public const string Root = "Assets/GuitarPlatformer";
        public const string Generated = Root + "/Generated";
        public const string Materials = Generated + "/Materials";
        public const string Textures = Generated + "/Textures";
        public const string Meshes = Generated + "/Meshes";
        public const string Profiles = Generated + "/Profiles";
        public const string SceneFolder = Root + "/Scenes";
        public const string ScenePath = SceneFolder + "/GuitarFretboard.unity";

        public static void EnsureFolders()
        {
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory(Textures);
            Directory.CreateDirectory(Meshes);
            Directory.CreateDirectory(Profiles);
            Directory.CreateDirectory(SceneFolder);
            AssetDatabase.Refresh();
        }

        public static Material GetMaterial(string name, string shaderName)
        {
            string path = Materials + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material Lit(string name, Color baseColor, float smoothness = 0.5f, float metallic = 0f,
            Color? emission = null)
        {
            Material m = GetMaterial(name, "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Unlit(string name, Color color, bool transparent = false, bool additive = false,
            Texture texture = null, Vector2? tiling = null, bool doubleSided = false)
        {
            Material m = GetMaterial(name, "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color);
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
                m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)(additive ? BlendMode.One : BlendMode.SrcAlpha));
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Geometry;
            }

            if (doubleSided) m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Particle(string name, Texture texture, Color color)
        {
            Material m = GetMaterial(name, "Universal Render Pipeline/Particles/Unlit");
            if (texture != null) m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size,
            Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!collider) RemoveCollider(go);
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!collider) RemoveCollider(go);
            return go;
        }

        public static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        public static void NoShadows(GameObject go)
        {
            if (go == null) return;
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize,
            TextAnchor alignment, Vector2 anchor, Vector2 anchoredPosition, Vector2 size, Color color, Font font,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = PivotFor(alignment);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        public static Text CreateStretchedText(Transform parent, string name, string content, int fontSize,
            TextAnchor alignment, Color color, Font font, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Vector2 PivotFor(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft: return new Vector2(0f, 1f);
                case TextAnchor.UpperCenter: return new Vector2(0.5f, 1f);
                case TextAnchor.UpperRight: return new Vector2(1f, 1f);
                case TextAnchor.MiddleLeft: return new Vector2(0f, 0.5f);
                case TextAnchor.MiddleCenter: return new Vector2(0.5f, 0.5f);
                case TextAnchor.MiddleRight: return new Vector2(1f, 0.5f);
                case TextAnchor.LowerLeft: return new Vector2(0f, 0f);
                case TextAnchor.LowerCenter: return new Vector2(0.5f, 0f);
                case TextAnchor.LowerRight: return new Vector2(1f, 0f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        public static Texture2D CreateGridTexture()
        {
            const int size = 256;
            const int cell = 32;
            string path = Textures + "/NeonGrid.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixels = new Color32[size * size];
            Color32 background = new Color32(0, 0, 0, 0);
            Color32 line = new Color32(65, 235, 255, 150);
            Color32 lineDim = new Color32(45, 120, 220, 70);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool major = x % cell == 0 || y % cell == 0;
                    bool minor = x % (cell / 2) == 0 || y % (cell / 2) == 0;
                    pixels[y * size + x] = major ? line : (minor ? lineDim : background);
                }
            }

            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();

            return WritePngAsset(texture, path, true);
        }

        public static Texture2D CreateSoftDotTexture()
        {
            const int size = 64;
            string path = Textures + "/SoftDot.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - radius);
                    alpha = alpha * alpha * alpha;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.Apply();

            return WritePngAsset(texture, path, false);
        }

        /// <summary>
        /// Writes a real PNG (Texture2D assets cannot be serialized as .png) and lets the
        /// importer produce a proper texture asset. The GUID in the existing .meta is kept,
        /// so materials referencing the texture stay connected across rebuilds.
        /// </summary>
        private static Texture2D WritePngAsset(Texture2D texture, string path, bool repeat)
        {
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.Combine(projectRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            bool validPng = IsPng(fullPath);
            if (!validPng && File.Exists(fullPath))
            {
                File.Delete(fullPath);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }
                if (!importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = true;
                    dirty = true;
                }
                TextureWrapMode wantedWrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (importer.wrapMode != wantedWrap)
                {
                    importer.wrapMode = wantedWrap;
                    dirty = true;
                }
                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    dirty = true;
                }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }
                if (dirty) importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static bool IsPng(string fullPath)
        {
            if (!File.Exists(fullPath)) return false;
            var header = new byte[8];
            using (FileStream stream = File.OpenRead(fullPath))
            {
                if (stream.Read(header, 0, header.Length) != header.Length) return false;
            }
            return header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                   header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        }

        public static Mesh CreatePickMesh()
        {
            string path = Meshes + "/PickMesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "PickMesh" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();

            var outline = new List<Vector2>();
            // Tapered point at the bottom.
            outline.Add(new Vector2(0f, 0f));
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                outline.Add(Quadratic(new Vector2(0f, 0f), new Vector2(-0.58f, 0.38f),
                    new Vector2(-0.55f, 0.8f), t));
            }
            for (int i = 1; i < 12; i++)
            {
                float angle = Mathf.PI - i / 12f * Mathf.PI;
                outline.Add(new Vector2(Mathf.Cos(angle) * 0.55f, 0.8f + Mathf.Sin(angle) * 0.55f));
            }
            for (int i = 1; i <= 10; i++)
            {
                float t = i / 10f;
                outline.Add(Quadratic(new Vector2(0.55f, 0.8f), new Vector2(0.58f, 0.38f),
                    new Vector2(0f, 0f), t));
            }

            const float halfDepth = 0.14f;
            int count = outline.Count;
            if (Vector2.Distance(outline[count - 1], outline[0]) < 0.001f)
                outline.RemoveAt(count - 1);
            count = outline.Count;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var center = new Vector2(0f, 0.62f);

            for (int i = 0; i < count; i++)
            {
                vertices.Add(new Vector3(outline[i].x, outline[i].y, -halfDepth));
                vertices.Add(new Vector3(outline[i].x, outline[i].y, halfDepth));
            }

            // Front and back fans.
            int frontCenter = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y, -halfDepth));
            int backCenter = vertices.Count;
            vertices.Add(new Vector3(center.x, center.y, halfDepth));

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int a = i * 2;
                int b = next * 2;
                triangles.Add(frontCenter);
                triangles.Add(a);
                triangles.Add(b);

                triangles.Add(backCenter);
                triangles.Add(b + 1);
                triangles.Add(a + 1);

                // Side wall
                triangles.Add(a);
                triangles.Add(b + 1);
                triangles.Add(b);
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(b + 1);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Vector2 Quadratic(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }
    }
}
