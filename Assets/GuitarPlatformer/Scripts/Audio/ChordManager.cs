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
            public readonly string fullName;

            public ChordInterval(Key key, int semitones, string label, string fullName)
            {
                this.key = key;
                this.semitones = semitones;
                this.label = label;
                this.fullName = fullName;
            }
        }

        /// <summary>Intervals from m2 through the octave; the compound list follows.</summary>
        public const int SimpleIntervalCount = 13;

        // Simple intervals first, then the compound intervals. Keys avoid movement,
        // jump, sprint, boost, camera, respawn, mute and menu bindings.
        public static readonly ChordInterval[] Intervals =
        {
            new ChordInterval(Key.Z, 1, "m2", "MINOR 2ND"),
            new ChordInterval(Key.Y, 2, "M2", "MAJOR 2ND"),
            new ChordInterval(Key.E, 3, "m3", "MINOR 3RD"),
            new ChordInterval(Key.R, 4, "M3", "MAJOR 3RD"),
            new ChordInterval(Key.T, 5, "P4", "PERFECT 4TH"),
            new ChordInterval(Key.X, 6, "TT", "TRITONE"),
            new ChordInterval(Key.F, 7, "P5", "PERFECT 5TH"),
            new ChordInterval(Key.J, -7, "P5\u2193", "LOWER 5TH"),
            new ChordInterval(Key.C, 8, "m6", "MINOR 6TH"),
            new ChordInterval(Key.G, 9, "M6", "MAJOR 6TH"),
            new ChordInterval(Key.H, 10, "m7", "MINOR 7TH"),
            new ChordInterval(Key.B, 11, "M7", "MAJOR 7TH"),
            new ChordInterval(Key.Q, 12, "8va", "OCTAVE"),
            new ChordInterval(Key.Digit1, 13, "m9", "MINOR 9TH"),
            new ChordInterval(Key.Digit2, 14, "M9", "MAJOR 9TH"),
            new ChordInterval(Key.Digit3, 15, "m10", "MINOR 10TH"),
            new ChordInterval(Key.Digit4, 16, "M10", "MAJOR 10TH"),
            new ChordInterval(Key.Digit5, 17, "P11", "PERFECT 11TH"),
            new ChordInterval(Key.Digit6, 18, "A11", "AUGMENTED 11TH"),
            new ChordInterval(Key.Digit7, 19, "P12", "PERFECT 12TH"),
            new ChordInterval(Key.Digit8, 20, "m13", "MINOR 13TH"),
            new ChordInterval(Key.Digit9, 21, "M13", "MAJOR 13TH"),
            new ChordInterval(Key.Digit0, 22, "m14", "MINOR 14TH"),
            new ChordInterval(Key.Minus, 23, "M14", "MAJOR 14TH"),
            new ChordInterval(Key.Equals, 24, "P15", "PERFECT 15TH")
        };

        private readonly HashSet<int> active = new HashSet<int>();

        public int ActiveCount => active.Count;
        public int LastChordSize { get; private set; }
        public int ChordPlayCount { get; private set; }
        public IEnumerable<int> ActiveSemitones => active;

        /// <summary>Raised whenever the manual interval set changes.</summary>
        public event System.Action ActiveChanged;

        public static int IndexOf(int semitones)
        {
            for (int i = 0; i < Intervals.Length; i++)
            {
                if (Intervals[i].semitones == semitones)
                    return i;
            }
            return -1;
        }

        public static string LabelFor(int semitones)
        {
            int index = IndexOf(semitones);
            if (index >= 0) return Intervals[index].label;
            return semitones > 0 ? "+" + semitones : semitones.ToString();
        }

        public static string KeyLabel(Key key)
        {
            switch (key)
            {
                case Key.Digit0: return "0";
                case Key.Digit1: return "1";
                case Key.Digit2: return "2";
                case Key.Digit3: return "3";
                case Key.Digit4: return "4";
                case Key.Digit5: return "5";
                case Key.Digit6: return "6";
                case Key.Digit7: return "7";
                case Key.Digit8: return "8";
                case Key.Digit9: return "9";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                default: return key.ToString();
            }
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
