namespace GuitarPlatformer
{
    /// <summary>
    /// One target pad in a riff. StartBeat and LengthBeats are authored on the riff's
    /// beat grid, so the replay can render the phrase with sample-accurate timing.
    /// Muted steps use the palm-muted chug voice in the replay. ChordIntervals is null
    /// when the step inherits the level's required voicing; an empty array makes the
    /// step chord-free, so it sounds as a single note even while a chord is held.
    /// </summary>
    public sealed class RiffStep
    {
        public readonly int StringIndex;
        public readonly int Fret;
        public readonly float StartBeat;
        public readonly float LengthBeats;
        public readonly bool Muted;
        public readonly int[] ChordIntervals;

        public RiffStep(int stringIndex, int fret, float startBeat, float lengthBeats, bool muted = false,
            int[] chordIntervals = null)
        {
            StringIndex = stringIndex;
            Fret = fret;
            StartBeat = startBeat;
            LengthBeats = lengthBeats;
            Muted = muted;
            ChordIntervals = chordIntervals;
        }

        public int Midi => GuitarTuning.MidiFor(StringIndex, Fret);
        public string NoteName => GuitarTuning.NoteName(Midi);
        public string StringLabel => GuitarTuning.GuitarStringNumbers[StringIndex];
        public bool IsChordFree => ChordIntervals != null && ChordIntervals.Length == 0;
    }

    /// <summary>
    /// A playable riff: an ordered pad sequence, the default manual chord voicing,
    /// per-step chord overrides (including chord-free steps), the authored beat grid
    /// used by the replay, and the tempo.
    /// </summary>
    public sealed class RiffLevel
    {
        public readonly string Title;
        public readonly string Artist;
        public readonly int Bpm;
        public readonly int StartString;
        public readonly int StartFret;
        public readonly RiffStep[] Steps;
        public readonly int[] RequiredIntervals;
        public readonly string ChordKeyHint;
        public readonly float LoopLengthBeats;
        public readonly int ReplayCycles;

        public RiffLevel(string title, string artist, int bpm, int startString, int startFret,
            RiffStep[] steps, float loopLengthBeats, int[] requiredIntervals, string chordKeyHint,
            int replayCycles = 1)
        {
            Title = title;
            Artist = artist;
            Bpm = bpm;
            StartString = startString;
            StartFret = startFret;
            Steps = steps;
            LoopLengthBeats = loopLengthBeats;
            RequiredIntervals = requiredIntervals ?? new int[0];
            ChordKeyHint = chordKeyHint ?? "";
            ReplayCycles = replayCycles < 1 ? 1 : replayCycles;
        }

        /// <summary>Effective voicing for a step; null steps fall back to the level default.</summary>
        public int[] ChordIntervalsFor(RiffStep step)
        {
            if (step == null) return RequiredIntervals;
            return step.ChordIntervals ?? RequiredIntervals;
        }
    }
}
