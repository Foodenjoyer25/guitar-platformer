namespace GuitarPlatformer
{
    /// <summary>
    /// One target pad in a riff. StartBeat and LengthBeats are authored on the riff's
    /// beat grid, so the replay can render the phrase with sample-accurate timing.
    /// Muted steps use the palm-muted chug voice in the replay.
    /// </summary>
    public sealed class RiffStep
    {
        public readonly int StringIndex;
        public readonly int Fret;
        public readonly float StartBeat;
        public readonly float LengthBeats;
        public readonly bool Muted;

        public RiffStep(int stringIndex, int fret, float startBeat, float lengthBeats, bool muted = false)
        {
            StringIndex = stringIndex;
            Fret = fret;
            StartBeat = startBeat;
            LengthBeats = lengthBeats;
            Muted = muted;
        }

        public int Midi => GuitarTuning.MidiFor(StringIndex, Fret);
        public string NoteName => GuitarTuning.NoteName(Midi);
        public string StringLabel => GuitarTuning.GuitarStringNumbers[StringIndex];
    }

    /// <summary>
    /// A playable riff: an ordered pad sequence, the manual chord voicing the player must
    /// hold for every step, the authored beat grid used by the replay, and the tempo.
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
        public readonly int[] ReplayIntervals;
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
            ReplayIntervals = RequiredIntervals;
            ChordKeyHint = chordKeyHint ?? "";
            ReplayCycles = replayCycles < 1 ? 1 : replayCycles;
        }
    }
}
