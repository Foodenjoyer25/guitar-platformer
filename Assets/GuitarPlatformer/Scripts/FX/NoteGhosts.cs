using System.Collections.Generic;
using UnityEngine;

namespace GuitarPlatformer
{
    public enum GhostKind
    {
        Unison,
        OctaveUp,
        OctaveDown,
        Fifth
    }

    /// <summary>One pad that should receive a ghost copy of the player.</summary>
    public struct GhostTarget
    {
        public int stringIndex;
        public int fret;
        public int midi;
        public GhostKind kind;
        public Color color;
        public string label;
    }

    /// <summary>
    /// Shows translucent copies of the player pick on the pads that hold the same pitch,
    /// one octave up, one octave down, and the perfect fifth above the note just played.
    /// </summary>
    public class NoteGhosts : MonoBehaviour
    {
        public static NoteGhosts Instance { get; private set; }

        public static readonly Color FifthColor = new Color(0.2f, 0.62f, 1f);

        [SerializeField] private Material pickMaterial;
        [SerializeField] private Material eyeMaterial;
        [SerializeField] private Mesh pickMesh;
        [SerializeField] private Font font;
        [SerializeField] private int poolSize = 24;
        [SerializeField] private float lifetime = 2.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Ghost[] ghosts;
        private Camera mainCamera;

        private class Ghost
        {
            public GameObject root;
            public Transform pick;
            public Material bodyMaterial;
            public Material faceMaterial;
            public TextMesh label;
            public Color color;
            public GhostKind kind;
            public int targetString;
            public int targetFret;
            public float time;
            public float phase;
            public float baseScale;
            public bool active;
        }

        public int ActiveGhostCount
        {
            get
            {
                if (ghosts == null) return 0;
                int count = 0;
                for (int i = 0; i < ghosts.Length; i++)
                    if (ghosts[i].active) count++;
                return count;
            }
        }

        public int CountActive(GhostKind kind)
        {
            if (ghosts == null) return 0;
            int count = 0;
            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i].active && ghosts[i].kind == kind) count++;
            return count;
        }

        public Color FirstActiveColor(GhostKind kind)
        {
            if (ghosts == null) return Color.clear;
            for (int i = 0; i < ghosts.Length; i++)
                if (ghosts[i].active && ghosts[i].kind == kind) return ghosts[i].color;
            return Color.clear;
        }

        public bool HasActiveAt(int stringIndex, int fret)
        {
            if (ghosts == null) return false;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i].active && ghosts[i].targetString == stringIndex &&
                    ghosts[i].targetFret == fret) return true;
            }
            return false;
        }

        private void Awake()
        {
            Instance = this;
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildPool();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ghosts == null) return;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i].bodyMaterial != null) Destroy(ghosts[i].bodyMaterial);
                if (ghosts[i].faceMaterial != null) Destroy(ghosts[i].faceMaterial);
            }
        }

        public void Configure(Material ghostPick, Material ghostEye, Mesh mesh, Font labelFont)
        {
            pickMaterial = ghostPick;
            eyeMaterial = ghostEye;
            pickMesh = mesh;
            font = labelFont;
        }

        private void BuildPool()
        {
            if (pickMaterial == null || pickMesh == null) return;
            poolSize = Mathf.Clamp(poolSize, 8, 32);
            ghosts = new Ghost[poolSize];
            Transform root = new GameObject("PlayerGhosts").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Ghost " + i);
                go.transform.SetParent(root, false);

                var pickGo = new GameObject("Pick");
                pickGo.transform.SetParent(go.transform, false);
                pickGo.AddComponent<MeshFilter>().sharedMesh = pickMesh;
                var bodyRenderer = pickGo.AddComponent<MeshRenderer>();
                var bodyMaterial = new Material(pickMaterial) { name = "GhostPick " + i };
                bodyRenderer.sharedMaterial = bodyMaterial;
                bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bodyRenderer.receiveShadows = false;

                // The ghost always billboards to the camera, so only the front face needs eyes.
                var faceMaterial = new Material(eyeMaterial) { name = "GhostEye " + i };
                for (int eye = -1; eye <= 1; eye += 2)
                {
                    CreateFacePart("Eye", pickGo.transform, PrimitiveType.Sphere,
                        new Vector3(eye * 0.205f, 0.9f, -0.155f),
                        new Vector3(0.13f, 0.18f, 0.06f), faceMaterial);
                }
                CreateFacePart("Mouth", pickGo.transform, PrimitiveType.Cube,
                    new Vector3(0f, 0.68f, -0.155f),
                    new Vector3(0.22f, 0.035f, 0.02f), faceMaterial);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.18f, 0f);
                var label = labelGo.AddComponent<TextMesh>();
                label.font = font;
                label.fontSize = 56;
                label.characterSize = 0.075f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.fontStyle = FontStyle.Bold;
                label.richText = false;
                var labelRenderer = labelGo.GetComponent<MeshRenderer>();
                if (font != null) labelRenderer.sharedMaterial = font.material;
                labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                labelRenderer.receiveShadows = false;

                go.SetActive(false);
                ghosts[i] = new Ghost
                {
                    root = go,
                    pick = pickGo.transform,
                    bodyMaterial = bodyMaterial,
                    faceMaterial = faceMaterial,
                    label = label,
                    active = false
                };
            }
        }

        private Renderer CreateFacePart(string name, Transform parent, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return renderer;
        }

        /// <summary>
        /// Pads that should show a ghost for the given note: unison on the other strings,
        /// +12 and -12 semitones on every string, and the perfect fifth (+7) on the same
        /// string plus the next string toward the high E. The source pad is excluded.
        /// </summary>
        public static List<GhostTarget> BuildTargets(int stringIndex, int fret, int midi)
        {
            var targets = new List<GhostTarget>(20);
            var seen = new HashSet<int>();

            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                if (s == stringIndex) continue;
                int f = midi - GuitarTuning.OpenMidi[s];
                AddTarget(targets, seen, s, f, stringIndex, fret, GhostKind.Unison,
                    GuitarTuning.ColorForMidi(midi));
            }

            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                AddTarget(targets, seen, s, midi + 12 - GuitarTuning.OpenMidi[s], stringIndex, fret,
                    GhostKind.OctaveUp, GuitarTuning.ColorForMidi(midi + 12));
                AddTarget(targets, seen, s, midi - 12 - GuitarTuning.OpenMidi[s], stringIndex, fret,
                    GhostKind.OctaveDown, GuitarTuning.ColorForMidi(midi - 12));
            }

            AddTarget(targets, seen, stringIndex, fret + 7, stringIndex, fret,
                GhostKind.Fifth, FifthColor);
            int higher = stringIndex + 1;
            if (higher < GuitarTuning.StringCount)
                AddTarget(targets, seen, higher, midi + 7 - GuitarTuning.OpenMidi[higher],
                    stringIndex, fret, GhostKind.Fifth, FifthColor);

            return targets;
        }

        private static void AddTarget(List<GhostTarget> targets, HashSet<int> seen,
            int stringIndex, int fret, int sourceString, int sourceFret, GhostKind kind, Color color)
        {
            if (stringIndex < 0 || stringIndex >= GuitarTuning.StringCount) return;
            if (fret < 0 || fret > FretboardLayout.FretCount) return;
            if (stringIndex == sourceString && fret == sourceFret) return;
            if (!seen.Add(stringIndex * 100 + fret)) return;

            int midi = GuitarTuning.OpenMidi[stringIndex] + fret;
            string suffix;
            switch (kind)
            {
                case GhostKind.OctaveUp: suffix = "  +1"; break;
                case GhostKind.OctaveDown: suffix = "  -1"; break;
                case GhostKind.Fifth: suffix = "  5th"; break;
                default: suffix = ""; break;
            }

            targets.Add(new GhostTarget
            {
                stringIndex = stringIndex,
                fret = fret,
                midi = midi,
                kind = kind,
                color = color,
                label = GuitarTuning.NoteName(midi) + suffix
            });
        }

        public void Show(NoteTile source)
        {
            if (source == null || ghosts == null) return;

            List<GhostTarget> targets = BuildTargets(source.stringIndex, source.fret, source.midi);
            for (int i = 0; i < targets.Count; i++)
            {
                Ghost ghost = GetGhost();
                if (ghost == null) return;

                GhostTarget target = targets[i];
                Vector3 pad = FretboardLayout.PadCenter(target.stringIndex, target.fret, target.fret == 0);

                ghost.active = true;
                ghost.time = 0f;
                ghost.phase = Random.value * 12f;
                ghost.color = target.color;
                ghost.kind = target.kind;
                ghost.targetString = target.stringIndex;
                ghost.targetFret = target.fret;
                ghost.baseScale = target.fret == 0 ? 0.72f : 0.64f;
                ghost.root.transform.position = new Vector3(pad.x, FretboardLayout.PadHeight + 0.5f, pad.z);
                ghost.pick.localScale = Vector3.one * (ghost.baseScale * 0.7f);
                ghost.pick.localPosition = Vector3.zero;
                ghost.bodyMaterial.SetColor(BaseColorId, Color.Lerp(target.color, Color.white, 0.06f) * 0.92f);
                ghost.faceMaterial.SetColor(BaseColorId, new Color(0.015f, 0.02f, 0.045f, 0.9f));
                ghost.label.text = target.label;
                ghost.label.color = Color.Lerp(target.color, Color.white, 0.15f) * 0.95f;
                ghost.root.SetActive(true);
            }
        }

        private Ghost GetGhost()
        {
            Ghost oldest = null;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (!ghosts[i].active) return ghosts[i];
                if (oldest == null || ghosts[i].time > oldest.time) oldest = ghosts[i];
            }
            return oldest;
        }

        private void Update()
        {
            if (ghosts == null) return;
            if (mainCamera == null) mainCamera = Camera.main;

            float dt = Time.deltaTime;
            for (int i = 0; i < ghosts.Length; i++)
            {
                Ghost ghost = ghosts[i];
                if (!ghost.active) continue;

                ghost.time += dt;
                float k = Mathf.Clamp01(ghost.time / lifetime);
                float fade = Mathf.Pow(1f - k, 1.35f);
                float appear = Mathf.Clamp01(ghost.time / 0.16f);
                float bob = Mathf.Sin(ghost.time * 2.6f + ghost.phase) * 0.06f;
                float sway = Mathf.Sin(ghost.time * 1.9f + ghost.phase * 0.7f) * 3f;

                float scale = ghost.baseScale * Mathf.Lerp(0.7f, 1f, appear) * Mathf.Lerp(1f, 1.07f, k);
                ghost.pick.localScale = Vector3.one * scale;
                ghost.pick.localPosition = new Vector3(0f, bob, 0f);

                if (mainCamera != null)
                    ghost.root.transform.rotation = mainCamera.transform.rotation *
                                                    Quaternion.Euler(0f, 0f, sway);

                Color body = Color.Lerp(ghost.color, Color.white, 0.06f) * 0.92f;
                body.a = Mathf.Clamp01(0.8f * fade);
                ghost.bodyMaterial.SetColor(BaseColorId, body);

                Color face = new Color(0.015f, 0.02f, 0.045f, Mathf.Clamp01(0.9f * fade));
                ghost.faceMaterial.SetColor(BaseColorId, face);

                Color labelColor = Color.Lerp(ghost.color, Color.white, 0.15f) * 0.95f;
                labelColor.a = Mathf.Clamp01(fade * 1.2f);
                ghost.label.color = labelColor;

                if (k >= 1f)
                {
                    ghost.active = false;
                    ghost.root.SetActive(false);
                }
            }
        }
    }
}
