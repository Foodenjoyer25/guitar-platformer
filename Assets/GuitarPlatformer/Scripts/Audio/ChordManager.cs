using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuitarPlatformer
{
    /// <summary>
    /// Toggleable harmony intervals. When a pad is played, every active interval is
    /// synthesized above the pad's pitch, producing a chord without re-triggering pads,
    /// ghosts or the note counter.
    /// </summary>
    public class ChordManager : MonoBehaviour
    {
        public static ChordManager Instance { get; private set; }

        public readonly struct ChordInterval
        {
            public readonly Key key;
            public readonly int semitones;
            public readonly string label;

            public ChordInterval(Key key, int semitones, string label)
            {
                this.key = key;
                this.semitones = semitones;
                this.label = label;
            }
        }

        // Requested toggles first, then a suggested layout for the remaining intervals.
        // No key collides with movement, jump, sprint, boost, camera or respawn.
        public static readonly ChordInterval[] Intervals =
        {
            new ChordInterval(Key.F, 7, "P5"),
            new ChordInterval(Key.J, -7, "P5\u2193"),
            new ChordInterval(Key.E, 3, "m3"),
            new ChordInterval(Key.R, 4, "M3"),
            new ChordInterval(Key.T, 5, "P4"),
            new ChordInterval(Key.Y, 2, "M2"),
            new ChordInterval(Key.C, 8, "m6"),
            new ChordInterval(Key.G, 9, "M6"),
            new ChordInterval(Key.H, 10, "m7"),
            new ChordInterval(Key.Z, 1, "m2"),
            new ChordInterval(Key.X, 6, "TT"),
            new ChordInterval(Key.B, 11, "M7"),
            new ChordInterval(Key.Q, 12, "8va")
        };

        private readonly HashSet<int> active = new HashSet<int>();

        public int ActiveCount => active.Count;
        public int LastChordSize { get; private set; }
        public int ChordPlayCount { get; private set; }
        public IEnumerable<int> ActiveSemitones => active;

        /// <summary>Raised whenever the manual interval set changes.</summary>
        public event System.Action ActiveChanged;

        public static string LabelFor(int semitones)
        {
            for (int i = 0; i < Intervals.Length; i++)
            {
                if (Intervals[i].semitones == semitones)
                    return Intervals[i].label;
            }
            return semitones > 0 ? "+" + semitones : semitones.ToString();
        }

        /// <summary>True when the active set is exactly the requested voicing.</summary>
        public bool MatchesExactly(int[] semitones)
        {
            int wanted = semitones != null ? semitones.Length : 0;
            if (active.Count != wanted) return false;
            if (wanted == 0) return true;
            for (int i = 0; i < semitones.Length; i++)
            {
                if (!active.Contains(semitones[i])) return false;
            }
            return true;
        }

        public string ActiveLabel
        {
            get
            {
                if (active.Count == 0) return "";
                var label = new StringBuilder();
                foreach (ChordInterval interval in Intervals)
                {
                    if (!active.Contains(interval.semitones)) continue;
                    if (label.Length > 0) label.Append(" + ");
                    label.Append(interval.label);
                }
                return label.ToString();
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            RefreshHud();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Replay and the reveal card freeze manual chord input.
            if (LevelManager.Instance != null && !LevelManager.Instance.AllowChordInput) return;

            for (int i = 0; i < Intervals.Length; i++)
            {
                if (keyboard[Intervals[i].key].wasPressedThisFrame)
                    Toggle(Intervals[i].semitones);
            }
        }

        public bool IsActive(int semitones)
        {
            return active.Contains(semitones);
        }

        public void Toggle(int semitones)
        {
            if (!active.Remove(semitones))
                active.Add(semitones);
            RefreshHud();
            ActiveChanged?.Invoke();
        }

        public void Clear()
        {
            if (active.Count == 0) return;
            active.Clear();
            RefreshHud();
            ActiveChanged?.Invoke();
        }

        /// <summary>Called by NoteTile.Play after the pad's own note starts.</summary>
        public void PlayChord(NoteTile source, float impact01)
        {
            LastChordSize = 0;
            if (source == null || active.Count == 0 || ChiptuneAudio.Instance == null) return;

            Vector3 position = source.transform.position + Vector3.up * 0.9f;
            float volume = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(impact01));
            // Keep dense chords from clipping while leaving a single interval prominent.
            float gain = 0.78f / Mathf.Sqrt(active.Count);

            foreach (ChordInterval interval in Intervals)
            {
                if (!active.Contains(interval.semitones)) continue;
                int midi = Mathf.Clamp(source.midi + interval.semitones, 0, 127);
                ChiptuneAudio.Instance.PlayMidi(midi, source.stringIndex, position, volume * gain);
                LastChordSize++;
            }

            ChordPlayCount++;
        }

        /// <summary>
        /// Plays an authored voicing regardless of the manual interval set. Used by the
        /// tempo-correct riff replay, which must sound exactly as written.
        /// </summary>
        public void PlayIntervals(NoteTile source, float impact01, int[] semitones)
        {
            LastChordSize = 0;
            if (source == null || semitones == null || semitones.Length == 0 ||
                ChiptuneAudio.Instance == null) return;

            Vector3 position = source.transform.position + Vector3.up * 0.9f;
            float volume = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(impact01));
            float gain = 0.78f / Mathf.Sqrt(semitones.Length);

            for (int i = 0; i < semitones.Length; i++)
            {
                int midi = Mathf.Clamp(source.midi + semitones[i], 0, 127);
                ChiptuneAudio.Instance.PlayMidi(midi, source.stringIndex, position, volume * gain);
                LastChordSize++;
            }
        }

        private void RefreshHud()
        {
            if (Hud.Instance != null)
                Hud.Instance.SetChordLabel(ActiveLabel);
        }
    }
}
