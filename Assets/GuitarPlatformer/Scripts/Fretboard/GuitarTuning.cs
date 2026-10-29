using UnityEngine;

namespace GuitarPlatformer
{
    public static class GuitarTuning
    {
        public const int StringCount = 6;
        public const int FretCount = 24;

        /// <summary>Low E to high E, standard tuning.</summary>
        public static readonly int[] OpenMidi = { 40, 45, 50, 55, 59, 64 };
        public static readonly string[] OpenNoteNames = { "E2", "A2", "D3", "G3", "B3", "E4" };
        public static readonly string[] OpenToneNames = { "E", "A", "D", "G", "B", "E" };
        public static readonly string[] GuitarStringNumbers = { "6", "5", "4", "3", "2", "1" };

        private static readonly string[] NoteNames =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
        };

        /// <summary>Neon palette indexed by pitch class, C..B.</summary>
        public static readonly Color[] PitchColors =
        {
            new Color(1.00f, 0.18f, 0.39f), // C
            new Color(1.00f, 0.42f, 0.17f), // C#
            new Color(1.00f, 0.79f, 0.24f), // D
            new Color(0.78f, 1.00f  , 0.24f), // D#
            new Color(0.22f, 1.00f, 0.53f), // E
            new Color(0.17f, 1.00f, 0.82f), // F
            new Color(0.17f, 0.85f, 1.00f), // F#
            new Color(0.23f, 0.52f, 1.00f), // G
            new Color(0.42f, 0.36f, 1.00f), // G#
            new Color(0.69f, 0.32f, 1.00f), // A
            new Color(1.00f, 0.28f, 0.95f), // A#
            new Color(1.00f, 0.30f, 0.62f)  // B
        };

        public static int PitchClass(int midi)
        {
            return ((midi % 12) + 12) % 12;
        }

        public static string NoteName(int midi)
        {
            int octave = Mathf.FloorToInt(midi / 12f) - 1;
            return NoteNames[PitchClass(midi)] + octave;
        }

        public static Color ColorForMidi(int midi)
        {
            return PitchColors[PitchClass(midi)];
        }

        public static float Frequency(int midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        }

        public static int MidiFor(int stringIndex, int fret)
        {
            return OpenMidi[stringIndex] + fret;
        }

        public static float FretDistance(int fret, float scaleLength)
        {
            if (fret <= 0) return 0f;
            return scaleLength * (1f - Mathf.Pow(2f, -fret / 12f));
        }
    }
}
