using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>
    /// One scheduled note event in a riff performance, already unrolled across cycles.
    /// ChordIntervals is the effective voicing for the event; empty means a single note.
    /// </summary>
    public readonly struct RiffPerformanceNote
    {
        public readonly int StepIndex;
        public readonly int Cycle;
        public readonly float StartBeat;
        public readonly float LengthBeats;
        public readonly int StringIndex;
        public readonly int Fret;
        public readonly bool Muted;
        public readonly int[] ChordIntervals;

        public RiffPerformanceNote(int stepIndex, int cycle, float startBeat, float lengthBeats,
            int stringIndex, int fret, bool muted, int[] chordIntervals)
        {
            StepIndex = stepIndex;
            Cycle = cycle;
            StartBeat = startBeat;
            LengthBeats = lengthBeats;
            StringIndex = stringIndex;
            Fret = fret;
            Muted = muted;
            ChordIntervals = chordIntervals;
        }

        public int Midi => GuitarTuning.MidiFor(StringIndex, Fret);
        public string NoteName => GuitarTuning.NoteName(Midi);
        public string StringLabel => GuitarTuning.GuitarStringNumbers[StringIndex];
        public bool HasChord => ChordIntervals != null && ChordIntervals.Length > 0;
    }

    /// <summary>
    /// Builds the exact event list used by both the rendered replay audio and the replay
    /// visuals. Keeping one source of truth means the pad flashes and the soundtrack can
    /// never drift apart.
    /// </summary>
    public static class RiffPerformance
    {
        public static RiffPerformanceNote[] Build(RiffLevel level)
        {
            if (level == null || level.Steps == null || level.Steps.Length == 0)
                return new RiffPerformanceNote[0];

            int cycles = Mathf.Max(1, level.ReplayCycles);
            var notes = new RiffPerformanceNote[level.Steps.Length * cycles];
            int index = 0;
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                float offset = cycle * Mathf.Max(0f, level.LoopLengthBeats);
                for (int i = 0; i < level.Steps.Length; i++)
                {
                    RiffStep step = level.Steps[i];
                    notes[index++] = new RiffPerformanceNote(i, cycle, offset + step.StartBeat,
                        step.LengthBeats, step.StringIndex, step.Fret, step.Muted,
                        level.ChordIntervalsFor(step));
                }
            }
            return notes;
        }

        public static float TotalBeats(RiffLevel level)
        {
            if (level == null) return 0f;
            return Mathf.Max(0f, level.LoopLengthBeats) * Mathf.Max(1, level.ReplayCycles);
        }

        public static int EventCount(RiffLevel level)
        {
            if (level == null || level.Steps == null) return 0;
            return level.Steps.Length * Mathf.Max(1, level.ReplayCycles);
        }
    }
}
