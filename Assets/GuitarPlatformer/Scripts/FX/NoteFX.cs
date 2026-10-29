using UnityEngine;

namespace GuitarPlatformer
{
    public class NoteFX : MonoBehaviour
    {
        public static NoteFX Instance { get; private set; }

        [SerializeField] private Material ringMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Font font;
        [SerializeField] private int ringCount = 12;
        [SerializeField] private int popupCount = 10;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private RingFx[] rings;
        private PopupFx[] popups;
        private ParticleSystem sparks;
        private Camera mainCamera;

        private struct RingFx
        {
            public Transform transform;
            public Renderer renderer;
            public MaterialPropertyBlock block;
            public float time;
            public bool active;
            public Color color;
        }

        private struct PopupFx
        {
            public Transform transform;
            public TextMesh text;
            public float time;
            public bool active;
            public Color color;
        }

        private void Awake()
        {
            Instance = this;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateRings();
            CreateSparks();
            CreatePopups();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(Material rippleMaterial, Material particlesMaterial, Font popupFont)
        {
            ringMaterial = rippleMaterial;
            sparkMaterial = particlesMaterial;
            font = popupFont;
        }

        private void CreateRings()
        {
            if (ringMaterial == null) return;
            Mesh mesh = BuildRingMesh();
            rings = new RingFx[ringCount];
            Transform root = new GameObject("Ripples").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < ringCount; i++)
            {
                var go = new GameObject("Ripple " + i);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = ringMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);

                rings[i] = new RingFx
                {
                    transform = go.transform,
                    renderer = renderer,
                    block = new MaterialPropertyBlock(),
                    active = false
                };
            }
        }

        private static Mesh BuildRingMesh()
        {
            const int segments = 64;
            const float inner = 0.72f;
            const float outer = 1f;
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(cos * outer, 0f, sin * outer);
                vertices[i * 2 + 1] = new Vector3(cos * inner, 0f, sin * inner);
            }

            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 3;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "NoteRipple" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateSparks()
        {
            if (sparkMaterial == null) return;
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparks.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 900;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.75f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.16f);
            main.gravityModifier = 0.9f;
            main.startColor = Color.white;

            var emission = sparks.emission;
            emission.enabled = false;

            var colorOverLifetime = sparks.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.35f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            sparks.Play();
        }

        private void CreatePopups()
        {
            if (font == null) return;
            popups = new PopupFx[popupCount];
            Transform root = new GameObject("NotePopups").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < popupCount; i++)
            {
                var go = new GameObject("Popup " + i);
                go.transform.SetParent(root, false);
                var text = go.AddComponent<TextMesh>();
                text.font = font;
                text.fontSize = 72;
                text.characterSize = 0.28f;
                text.anchor = TextAnchor.LowerCenter;
                text.alignment = TextAlignment.Center;
                text.fontStyle = FontStyle.Bold;
                text.richText = false;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = font.material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                go.SetActive(false);

                popups[i] = new PopupFx
                {
                    transform = go.transform,
                    text = text,
                    active = false
                };
            }
        }

        public void Land(NoteTile tile, float impact01)
        {
            if (tile == null) return;
            Vector3 top = new Vector3(tile.transform.position.x, FretboardLayout.PadHeight + 0.02f,
                tile.transform.position.z);

            SpawnRing(top, tile.noteColor);
            SpawnSparks(top + Vector3.up * 0.12f, tile.noteColor, impact01);
            SpawnPopup(top + Vector3.up * 1.05f, tile.NoteName, tile.noteColor);
        }

        private void SpawnRing(Vector3 position, Color color)
        {
            if (rings == null) return;
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i].active) continue;
                rings[i].active = true;
                rings[i].time = 0f;
                rings[i].color = color;
                rings[i].transform.position = position;
                rings[i].transform.localScale = Vector3.one * 1.1f;
                rings[i].renderer.enabled = true;
                rings[i].transform.gameObject.SetActive(true);
                return;
            }
        }

        private void SpawnSparks(Vector3 position, Color color, float impact01)
        {
            if (sparks == null) return;
            int count = Mathf.RoundToInt(Mathf.Lerp(8f, 20f, Mathf.Clamp01(impact01)));
            for (int i = 0; i < count; i++)
            {
                Vector2 circle = Random.insideUnitCircle * Random.Range(0.5f, 2.2f);
                var emit = new ParticleSystem.EmitParams
                {
                    position = position + Random.insideUnitSphere * 0.1f,
                    velocity = new Vector3(circle.x, Random.Range(1.4f, 4.2f), circle.y),
                    startLifetime = Random.Range(0.3f, 0.75f),
                    startSize = Random.Range(0.05f, 0.15f),
                    startColor = new Color32(
                        (byte)Mathf.Clamp(color.r * 255f * 1.7f, 0f, 255f),
                        (byte)Mathf.Clamp(color.g * 255f * 1.7f, 0f, 255f),
                        (byte)Mathf.Clamp(color.b * 255f * 1.7f, 0f, 255f),
                        255)
                };
                sparks.Emit(emit, 1);
            }
        }

        private void SpawnPopup(Vector3 position, string text, Color color)
        {
            if (popups == null) return;
            for (int i = 0; i < popups.Length; i++)
            {
                if (popups[i].active) continue;
                popups[i].active = true;
                popups[i].time = 0f;
                popups[i].color = color;
                popups[i].text.text = text;
                popups[i].text.color = color;
                popups[i].transform.position = position;
                popups[i].transform.localScale = Vector3.one * 0.85f;
                popups[i].transform.gameObject.SetActive(true);
                return;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateRings(dt);
            UpdatePopups(dt);
        }

        private void UpdateRings(float dt)
        {
            if (rings == null || ringMaterial == null) return;
            for (int i = 0; i < rings.Length; i++)
            {
                if (!rings[i].active) continue;
                rings[i].time += dt;
                float k = Mathf.Clamp01(rings[i].time / 0.55f);
                float scale = Mathf.Lerp(1.05f, 5.2f, 1f - Mathf.Pow(1f - k, 3f));
                rings[i].transform.localScale = Vector3.one * scale;
                float alpha = Mathf.Pow(1f - k, 1.4f);
                Color color = rings[i].color * 2.2f;
                color.a = alpha;
                rings[i].block.SetColor(BaseColorId, color);
                rings[i].renderer.SetPropertyBlock(rings[i].block);

                if (k >= 1f)
                {
                    rings[i].active = false;
                    rings[i].transform.gameObject.SetActive(false);
                }
            }
        }

        private void UpdatePopups(float dt)
        {
            if (popups == null) return;
            if (mainCamera == null) mainCamera = Camera.main;

            for (int i = 0; i < popups.Length; i++)
            {
                if (!popups[i].active) continue;
                popups[i].time += dt;
                float t = popups[i].time;
                popups[i].transform.position += Vector3.up * (dt * 1.1f);
                if (mainCamera != null)
                    popups[i].transform.rotation = mainCamera.transform.rotation;

                float alpha = 1f - Mathf.Clamp01((t - 0.35f) / 0.7f);
                Color color = popups[i].color;
                color.a = alpha;
                popups[i].text.color = color;
                popups[i].transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.05f, Mathf.Clamp01(t / 0.15f));

                if (t >= 1.05f)
                {
                    popups[i].active = false;
                    popups[i].transform.gameObject.SetActive(false);
                }
            }
        }
    }
}
