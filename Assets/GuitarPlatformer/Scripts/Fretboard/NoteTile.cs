using System.Collections;
using UnityEngine;

namespace GuitarPlatformer
{
    public class NoteTile : MonoBehaviour
    {
        public int stringIndex;
        public int fret;
        public int midi;
        public bool isOpen;
        public Color noteColor = Color.white;
        public Renderer tileRenderer;
        public Renderer rimRenderer;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color HighlightColor = new Color(0.55f, 1f, 0.95f);

        private MaterialPropertyBlock block;
        private MaterialPropertyBlock rimBlock;
        private Coroutine flashRoutine;
        private bool highlighted;
        private bool lastMuted;

        public string NoteName => GuitarTuning.NoteName(midi);
        public string StringLabel => GuitarTuning.GuitarStringNumbers[stringIndex];
        public string FretLabel => isOpen ? "open" : fret.ToString();
        public float Frequency => GuitarTuning.Frequency(midi);
        public string OpenStringName => GuitarTuning.OpenNoteNames[stringIndex];
        public bool IsHighlighted => highlighted;

        /// <summary>Brightness multiplier applied by the global mute state, for tests and HUD polish.</summary>
        public float DimFactor { get; private set; } = 1f;

        public void Configure(int stringIndex, int fret, int midi, bool isOpen, Color color, Renderer renderer,
            Renderer rim = null)
        {
            this.stringIndex = stringIndex;
            this.fret = fret;
            this.midi = midi;
            this.isOpen = isOpen;
            noteColor = color;
            tileRenderer = renderer;
            rimRenderer = rim;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (block != null) return;
            if (tileRenderer == null) tileRenderer = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            rimBlock = new MaterialPropertyBlock();
            ApplyVisual(0f);
        }

        public void OnLanded(float impact01)
        {
            Play(impact01);
        }

        /// <summary>Single path for landing and walk-on triggers: audio, FX, HUD, ghosts and flash.</summary>
        public void Play(float impact01)
        {
            // Easy challenges disable wrong pads entirely. Medium and Hard still let
            // them sound so the mistake can cost a life.
            if (LevelManager.Instance != null && !LevelManager.Instance.CanTriggerNote(this)) return;

            PlayInternal(impact01);
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnNotePlayed(this);
        }

        private void PlayInternal(float impact01)
        {
            Initialize();
            if (ChiptuneAudio.Instance != null)
                ChiptuneAudio.Instance.Play(this, impact01);
            if (ChordManager.Instance != null &&
                (LevelManager.Instance == null || LevelManager.Instance.ShouldPlayChord(this)))
                ChordManager.Instance.PlayChord(this, impact01);

            Flash();

            if (NoteFX.Instance != null)
                NoteFX.Instance.Land(this, impact01);
            if (NoteGhosts.Instance != null)
                NoteGhosts.Instance.Show(this);
            if (Hud.Instance != null)
                Hud.Instance.ShowNote(this);
        }

        /// <summary>
        /// Visual half of an authored replay event. The soundtrack is one pre-rendered
        /// clip, so this only flashes the pad and spawns the landing FX in sync.
        /// </summary>
        public void PlayReplayVisual(bool muted)
        {
            Flash(muted ? 0.45f : 1f);
            if (NoteFX.Instance != null)
                NoteFX.Instance.Land(this, muted ? 0.35f : 0.9f);
        }

        /// <summary>Restarts the bright pad kick used by notes and the riff replay.</summary>
        public void Flash()
        {
            Flash(1f);
        }

        public void Flash(float strength)
        {
            Initialize();
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine(Mathf.Clamp01(strength)));
        }

        /// <summary>Highlights pads that belong to the current chord voicing.</summary>
        public void SetHighlighted(bool value)
        {
            if (highlighted == value) return;
            highlighted = value;
            Initialize();
            ApplyVisual(0f);
        }

        /// <summary>Forces a visual refresh, used when the mute state changes.</summary>
        public void RefreshVisual()
        {
            Initialize();
            ApplyVisual(0f);
        }

        private void Update()
        {
            bool muted = StringMuteManager.Instance != null && StringMuteManager.Instance.AnyMuted;
            if (muted != lastMuted)
            {
                lastMuted = muted;
                ApplyVisual(0f);
            }
            else if (highlighted)
            {
                ApplyVisual(0f);
            }
        }

        private IEnumerator FlashRoutine(float strength)
        {
            const float duration = 0.65f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float kick = Mathf.Exp(-(time / duration) * 7f) * strength;
                ApplyVisual(kick);
                yield return null;
            }
            ApplyVisual(0f);
            flashRoutine = null;
        }

        private void ApplyVisual(float flash)
        {
            float dim = 1f;
            if (StringMuteManager.Instance != null)
            {
                if (StringMuteManager.Instance.AllMuted) dim = 0.28f;
                else if (StringMuteManager.Instance.PalmMuted) dim = 0.62f;
            }
            DimFactor = dim;

            if (tileRenderer == null || block == null) return;

            float c = Mathf.Clamp01(flash);
            float pulse = highlighted ? 0.5f + 0.5f * Mathf.Sin(Time.time * 7f) : 0f;
            float hi = highlighted ? 0.48f + 0.32f * pulse : 0f;

            Color baseColor = Color.Lerp(noteColor * 0.08f, Color.white, Mathf.Max(c * 0.72f, hi));
            baseColor.a = 1f;
            Color emission = noteColor * (0.5f + c * 6f);
            if (highlighted)
                emission += HighlightColor * (1.35f + pulse * 1.6f);
            emission.a = 1f;

            block.SetColor(BaseColorId, ScaleRgb(baseColor, dim));
            block.SetColor(EmissionColorId, ScaleRgb(emission, dim));
            tileRenderer.SetPropertyBlock(block);

            if (rimRenderer != null)
            {
                Color rimColor = noteColor * (1.7f + c * 5f);
                if (highlighted)
                    rimColor += HighlightColor * (1.1f + pulse * 1.8f);
                rimColor.a = 1f;
                rimBlock.SetColor(BaseColorId, ScaleRgb(rimColor, dim));
                rimRenderer.SetPropertyBlock(rimBlock);
            }
        }

        private static Color ScaleRgb(Color color, float scale)
        {
            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
        }
    }
}
