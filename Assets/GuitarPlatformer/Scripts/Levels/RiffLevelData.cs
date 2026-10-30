using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>
    /// JSON-friendly copy of a riff step. The level creator edits this shape and the
    /// runtime converts it back into an immutable <see cref="RiffStep"/>.
    /// </summary>
    [Serializable]
    public sealed class RiffStepData
    {
        public int stringIndex;
        public int fret;
        public float startBeat;
        public float lengthBeats = 0.25f;
        public bool muted;

        // overrideChord=false inherits the level's required voicing. overrideChord=true
        // with chordFree=true sounds the pad as a single note. Otherwise chordIntervals
        // is the authored voicing for this step.
        public bool overrideChord;
        public bool chordFree;
        public int[] chordIntervals = new int[0];

        public RiffStep ToStep()
        {
            int[] intervals = null;
            if (overrideChord)
                intervals = chordFree ? new int[0] : Copy(chordIntervals);

            return new RiffStep(stringIndex, fret, startBeat, lengthBeats, muted, intervals);
        }

        public static RiffStepData FromStep(RiffStep step)
        {
            var data = new RiffStepData
            {
                stringIndex = step.StringIndex,
                fret = step.Fret,
                startBeat = step.StartBeat,
                lengthBeats = step.LengthBeats,
                muted = step.Muted,
                overrideChord = step.ChordIntervals != null,
                chordFree = step.ChordIntervals != null && step.ChordIntervals.Length == 0,
                chordIntervals = step.ChordIntervals != null ? Copy(step.ChordIntervals) : new int[0]
            };
            return data;
        }

        public RiffStepData Clone()
        {
            return new RiffStepData
            {
                stringIndex = stringIndex,
                fret = fret,
                startBeat = startBeat,
                lengthBeats = lengthBeats,
                muted = muted,
                overrideChord = overrideChord,
                chordFree = chordFree,
                chordIntervals = Copy(chordIntervals)
            };
        }

        private static int[] Copy(int[] values)
        {
            if (values == null) return new int[0];
            var copy = new int[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }
    }

    /// <summary>
    /// Serializable riff definition used by the Level Creator and by custom levels in
    /// Resources/Levels. Keeping the format independent of the runtime classes makes
    /// levels easy to save, diff and share.
    /// </summary>
    [Serializable]
    public sealed class RiffLevelData
    {
        public string title = "New Riff";
        public string artist = "You";
        public int bpm = 120;
        public int startString;
        public int startFret;
        public float loopLengthBeats = 8f;
        public int replayCycles = 1;
        public int[] requiredIntervals = new int[0];
        public string chordKeyHint = "";
        public RiffStepData[] steps = new RiffStepData[0];

        public RiffLevel ToLevel()
        {
            int count = steps != null ? steps.Length : 0;
            var built = new RiffStep[count];
            for (int i = 0; i < count; i++)
                built[i] = steps[i] != null ? steps[i].ToStep() : new RiffStep(0, 0, 0f, 0.25f);

            return new RiffLevel(title, artist, bpm, startString, startFret, built,
                loopLengthBeats, requiredIntervals, chordKeyHint, replayCycles);
        }

        public static RiffLevelData FromLevel(RiffLevel level)
        {
            if (level == null) return new RiffLevelData();

            int count = level.Steps != null ? level.Steps.Length : 0;
            var steps = new RiffStepData[count];
            for (int i = 0; i < count; i++)
                steps[i] = RiffStepData.FromStep(level.Steps[i]);

            return new RiffLevelData
            {
                title = level.Title,
                artist = level.Artist,
                bpm = level.Bpm,
                startString = level.StartString,
                startFret = level.StartFret,
                loopLengthBeats = level.LoopLengthBeats,
                replayCycles = level.ReplayCycles,
                requiredIntervals = Copy(level.RequiredIntervals),
                chordKeyHint = level.ChordKeyHint,
                steps = steps
            };
        }

        /// <summary>
        /// Checks the authored data. Errors make the level unplayable; warnings are
        /// things the creator should look at but that still render.
        /// </summary>
        public void Validate(List<string> errors, List<string> warnings)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (warnings == null) throw new ArgumentNullException(nameof(warnings));

            if (string.IsNullOrWhiteSpace(title))
                errors.Add("Title is required.");
            if (string.IsNullOrWhiteSpace(artist))
                errors.Add("Artist is required.");
            if (bpm < 30 || bpm > 300)
                errors.Add("BPM must be between 30 and 300.");
            if (startString < 0 || startString >= GuitarTuning.StringCount)
                errors.Add("Start string must be between 1 and 6.");
            if (startFret < 0 || startFret > GuitarTuning.FretCount)
                errors.Add("Start fret must be between 0 and " + GuitarTuning.FretCount + ".");
            if (loopLengthBeats <= 0f)
                errors.Add("Loop length must be greater than zero.");
            if (loopLengthBeats > 256f)
                errors.Add("Loop length must be 256 beats or fewer.");
            if (replayCycles < 1 || replayCycles > 16)
                errors.Add("Repeats must be between 1 and 16.");

            if (requiredIntervals != null)
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < requiredIntervals.Length; i++)
                {
                    int interval = requiredIntervals[i];
                    if (interval < -24 || interval > 36)
                        warnings.Add("Required interval " + interval + " is outside the usual -24..+36 range.");
                    if (!seen.Add(interval))
                        warnings.Add("Required interval " + interval + " is listed more than once.");
                }
            }

            if (steps == null || steps.Length == 0)
            {
                errors.Add("Add at least one step.");
                return;
            }

            for (int i = 0; i < steps.Length; i++)
            {
                RiffStepData step = steps[i];
                string prefix = "Step " + (i + 1) + ": ";
                if (step == null)
                {
                    errors.Add(prefix + "is missing.");
                    continue;
                }

                if (step.stringIndex < 0 || step.stringIndex >= GuitarTuning.StringCount)
                    errors.Add(prefix + "string must be between 1 and 6.");
                if (step.fret < 0 || step.fret > GuitarTuning.FretCount)
                    errors.Add(prefix + "fret must be between 0 and " + GuitarTuning.FretCount + ".");
                if (step.startBeat < 0f)
                    errors.Add(prefix + "start beat cannot be negative.");
                if (step.lengthBeats <= 0f)
                    errors.Add(prefix + "length must be greater than zero.");
                if (step.startBeat >= loopLengthBeats)
                    warnings.Add(prefix + "starts at or after the loop end.");
                else if (step.startBeat + step.lengthBeats > loopLengthBeats + 0.001f)
                    warnings.Add(prefix + "rings past the loop end.");

                if (step.overrideChord && !step.chordFree && step.chordIntervals != null)
                {
                    for (int c = 0; c < step.chordIntervals.Length; c++)
                    {
                        int interval = step.chordIntervals[c];
                        if (interval < -24 || interval > 36)
                            warnings.Add(prefix + "chord interval " + interval + " is outside the usual range.");
                    }
                }
            }

            // Two steps on the same string at the same instant are almost always a
            // mistake; flag them without blocking the save.
            for (int i = 0; i < steps.Length; i++)
            {
                RiffStepData a = steps[i];
                if (a == null) continue;
                for (int j = i + 1; j < steps.Length; j++)
                {
                    RiffStepData b = steps[j];
                    if (b == null || a.stringIndex != b.stringIndex) continue;
                    if (Mathf.Abs(a.startBeat - b.startBeat) < 0.001f)
                        warnings.Add("Steps " + (i + 1) + " and " + (j + 1) +
                                     " start together on the same string.");
                }
            }

            if (Mathf.Abs(loopLengthBeats - Mathf.Round(loopLengthBeats * 4f) / 4f) > 0.0001f)
                warnings.Add("Loop length is not on a 16th-note boundary.");
        }

        public static bool TryValidate(RiffLevelData data, out string message)
        {
            if (data == null)
            {
                message = "Level data is null.";
                return false;
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            data.Validate(errors, warnings);
            if (errors.Count == 0)
            {
                message = warnings.Count > 0 ? string.Join("\n", warnings) : "";
                return true;
            }

            message = string.Join("\n", errors);
            return false;
        }

        private static int[] Copy(int[] values)
        {
            if (values == null) return new int[0];
            var copy = new int[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }
    }
}
