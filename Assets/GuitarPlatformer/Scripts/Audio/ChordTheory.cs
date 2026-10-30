using System.Collections.Generic;
using System.Text;

namespace GuitarPlatformer
{
    /// <summary>
    /// Names the chord formed by the active interval set. The played pad is always the
    /// root; every active interval is stacked above it. Compound intervals that repeat a
    /// pitch class are reported as voicing extensions instead of separate chord tones.
    /// </summary>
    public static class ChordTheory
    {
        private static readonly Dictionary<string, string> Templates = new Dictionary<string, string>
        {
            { "0,7", "POWER CHORD" },
            { "0,4,7", "MAJOR" },
            { "0,3,7", "MINOR" },
            { "0,2,7", "SUS2" },
            { "0,5,7", "SUS4" },
            { "0,3,6", "DIMINISHED" },
            { "0,4,8", "AUGMENTED" },
            { "0,7,10", "POWER 7" },
            { "0,7,11", "POWER MAJ7" },
            { "0,4,7,11", "MAJOR 7" },
            { "0,3,7,10", "MINOR 7" },
            { "0,4,7,10", "DOMINANT 7" },
            { "0,3,6,10", "HALF-DIMINISHED 7" },
            { "0,3,6,9", "DIMINISHED 7" },
            { "0,3,7,11", "MINOR-MAJOR 7" },
            { "0,4,8,11", "AUGMENTED MAJOR 7" },
            { "0,4,8,10", "AUGMENTED 7" },
            { "0,4,7,9", "MAJOR 6" },
            { "0,3,7,9", "MINOR 6" },
            { "0,5,7,10", "7SUS4" },
            { "0,2,7,10", "7SUS2" },
            { "0,5,7,11", "MAJOR 7SUS4" },
            { "0,2,4,7", "ADD9" },
            { "0,2,3,7", "MINOR ADD9" },
            { "0,2,4,7,9", "6/9" },
            { "0,2,4,7,11", "MAJOR 9" },
            { "0,2,4,7,10", "DOMINANT 9" },
            { "0,2,3,7,10", "MINOR 9" },
            { "0,2,4,5,7,10", "DOMINANT 11" },
            { "0,2,4,5,7,11", "MAJOR 11" },
            { "0,2,3,5,7,10", "MINOR 11" },
            { "0,2,4,5,7,9,10", "DOMINANT 13" },
            { "0,2,4,5,7,9,11", "MAJOR 13" },
            { "0,2,3,5,7,9,10", "MINOR 13" }
        };

        public static string Describe(IEnumerable<int> semitones)
        {
            if (semitones == null) return "SINGLE NOTES";

            var list = new List<int>();
            foreach (int semitone in semitones)
                list.Add(semitone);
            if (list.Count == 0) return "SINGLE NOTES";

            bool lowerFifth = list.Contains(-7);
            bool octave = list.Contains(12);
            bool twoOctaves = list.Contains(24);

            if (list.Count == 1)
            {
                int only = list[0];
                if (only == 7) return "POWER CHORD";
                if (only == -7) return "POWER CHORD (LOWER 5TH)";
                return FullName(only);
            }

            var pitchClasses = new HashSet<int> { 0 };
            foreach (int semitone in list)
            {
                if (semitone == -7)
                {
                    pitchClasses.Add(7);
                    continue;
                }
                pitchClasses.Add(((semitone % 12) + 12) % 12);
            }

            string quality;
            if (pitchClasses.Count == 1)
            {
                quality = twoOctaves ? "OCTAVES" : "OCTAVE";
            }
            else if (!Templates.TryGetValue(SortedKey(pitchClasses), out quality))
            {
                quality = "CUSTOM VOICING";
            }

            if (lowerFifth) quality += "  \u00b7  LOWER 5TH";
            if (twoOctaves) quality += "  \u00b7  2 OCTAVES";
            else if (octave) quality += "  \u00b7  OCTAVE";
            return quality;
        }

        private static string FullName(int semitones)
        {
            int index = ChordManager.IndexOf(semitones);
            if (index >= 0) return ChordManager.Intervals[index].fullName;
            return semitones > 0 ? "+" + semitones : semitones.ToString();
        }

        private static string SortedKey(HashSet<int> pitchClasses)
        {
            var sorted = new List<int>(pitchClasses);
            sorted.Sort();
            var key = new StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) key.Append(',');
                key.Append(sorted[i]);
            }
            return key.ToString();
        }
    }
}
