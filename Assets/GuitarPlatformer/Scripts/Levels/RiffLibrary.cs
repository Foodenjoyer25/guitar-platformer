namespace GuitarPlatformer
{
    /// <summary>The four-riff campaign, in play order.</summary>
    public static class RiffLibrary
    {
        public static RiffLevel[] CreateLevels()
        {
            return new[]
            {
                SmokeOnTheWater(),
                SevenNationArmy(),
                SmellsLikeTeenSpirit(),
                BackInBlack()
            };
        }

        // Deep Purple - Smoke on the Water. Low E open is the safe start pad.
        // The riff is four bars: the last G rings into the turnaround.
        private static RiffLevel SmokeOnTheWater()
        {
            int[] strings = { 0, 1, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0 };
            int[] frets = { 3, 1, 3, 3, 1, 4, 3, 3, 1, 3, 1, 3 };
            float[] starts = { 0f, 1f, 2f, 3.5f, 4.5f, 5.5f, 6f, 8f, 9f, 10f, 11.5f, 12.5f };
            float[] lengths = { 0.75f, 0.5f, 1f, 0.5f, 0.5f, 0.5f, 1.5f, 0.75f, 0.5f, 1f, 0.5f, 2.5f };

            return new RiffLevel(
                "Smoke on the Water",
                "Deep Purple",
                112,
                0, 0,
                BuildSteps(strings, frets, starts, lengths),
                16f,
                new int[0],
                "");
        }

        // The White Stripes - Seven Nation Army. A open is the safe start pad.
        // Two 2-bar phrases of the E-E-G-E-D-C-B bass line.
        private static RiffLevel SevenNationArmy()
        {
            int[] strings = new int[14];
            int[] frets = { 7, 7, 10, 7, 5, 3, 2, 7, 7, 10, 7, 5, 3, 2 };
            float[] starts = { 0f, 1.5f, 2f, 2.75f, 3.5f, 4f, 6f, 8f, 9.5f, 10f, 10.75f, 11.5f, 12f, 14f };
            float[] lengths = { 1.5f, 0.25f, 0.25f, 0.25f, 0.5f, 2f, 2f, 1.5f, 0.25f, 0.25f, 0.25f, 0.5f, 2f, 2f };
            for (int i = 0; i < strings.Length; i++) strings[i] = 1;

            return new RiffLevel(
                "Seven Nation Army",
                "The White Stripes",
                124,
                1, 0,
                BuildSteps(strings, frets, starts, lengths),
                16f,
                new int[0],
                "");
        }

        // Nirvana - Smells Like Teen Spirit. The original riff roots are F, Bb, open E,
        // Ab and Db. The open low E steps are chord-free so they ring as single notes;
        // the power-chord steps keep the P5 voicing. The replay plays the phrase twice.
        private static RiffLevel SmellsLikeTeenSpirit()
        {
            int[] strings = { 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
            int[] frets = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 4, 4, 4, 4, 4, 4, 4, 4, 4, 0 };
            float[] starts =
            {
                0f, 0.75f, 1f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 3f, 3.5f,
                4f, 4.75f, 5f, 5.5f, 5.75f, 6f, 6.25f, 6.5f, 7f, 7.5f
            };
            float[] lengths =
            {
                0.75f, 0.25f, 0.5f, 0.125f, 0.125f, 0.125f, 0.125f, 0.5f, 0.5f, 0.5f,
                0.75f, 0.25f, 0.5f, 0.125f, 0.125f, 0.125f, 0.125f, 0.5f, 0.5f, 0.5f
            };
            bool[] muted =
            {
                false, false, false, true, true, true, true, false, false, false,
                false, false, false, true, true, true, true, false, false, false
            };
            bool[] chordFree =
            {
                false, false, false, false, false, false, false, false, false, true,
                false, false, false, false, false, false, false, false, false, true
            };

            return new RiffLevel(
                "Smells Like Teen Spirit",
                "Nirvana",
                117,
                0, 0,
                BuildSteps(strings, frets, starts, lengths, muted, chordFree),
                8f,
                new[] { 7 },
                "F",
                2);
        }

        // AC/DC - Back in Black. Low E open is the safe start pad.
        // Concert tempo is q=93 in 4/4. Phrase 1 and the chord hits keep the 1.75
        // rests with D/A as 16th down-up-downs and lick 1 as 3p0 3p0 2b3r2p0.
        // Lick 2: same 1.75 rest after the last A as lick 1, then B-pedal 4-5-6-7 with
        // each walk note a 16th after its B.
        // Open chords use P5; lick notes are chord-free.
        private static RiffLevel BackInBlack()
        {
            int[] strings =
            {
                0, 2, 2, 2, 1, 1, 1, 5, 5, 4, 4, 3, 3, 3, 3,
                0, 2, 2, 2, 1, 1, 1, 1, 0, 1, 0, 1, 0, 1, 0
            };
            int[] frets =
            {
                0, 0, 0, 0, 0, 0, 0, 3, 0, 3, 0, 2, 3, 2, 0,
                0, 0, 0, 0, 0, 0, 0, 2, 4, 2, 5, 2, 6, 2, 7
            };
            float[] starts =
            {
                0f, 1.75f, 2f, 2.25f, 4f, 4.25f, 4.5f,
                6.25f, 6.5f, 6.75f, 7f, 7.25f, 7.375f, 7.5f, 7.75f,
                8f, 9.75f, 10f, 10.25f, 12f, 12.25f, 12.5f,
                14.25f, 14.5f, 15.25f, 15.5f, 16.25f, 16.5f, 17.25f, 17.5f
            };
            float[] lengths =
            {
                0.2f, 0.2f, 0.2f, 0.15f, 0.2f, 0.2f, 0.15f,
                0.2f, 0.2f, 0.2f, 0.2f, 0.12f, 0.1f, 0.1f, 0.25f,
                0.2f, 0.2f, 0.2f, 0.15f, 0.2f, 0.2f, 0.15f,
                0.18f, 0.18f, 0.18f, 0.18f, 0.18f, 0.18f, 0.18f, 0.22f
            };
            bool[] muted = new bool[30];
            bool[] chordFree =
            {
                false, false, false, false, false, false, false,
                true, true, true, true, true, true, true, true,
                false, false, false, false, false, false, false,
                true, true, true, true, true, true, true, true
            };

            return new RiffLevel(
                "Back in Black",
                "AC/DC",
                93,
                0, 0,
                BuildSteps(strings, frets, starts, lengths, muted, chordFree),
                17.75f,
                new[] { 7 },
                "E");
        }

        private static RiffStep[] BuildSteps(int[] strings, int[] frets, float[] starts, float[] lengths,
            bool[] muted = null, bool[] chordFree = null)
        {
            var steps = new RiffStep[starts.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                bool isMuted = muted != null && i < muted.Length && muted[i];
                bool isChordFree = chordFree != null && i < chordFree.Length && chordFree[i];
                steps[i] = new RiffStep(strings[i], frets[i], starts[i], lengths[i], isMuted,
                    isChordFree ? new int[0] : null);
            }
            return steps;
        }
    }
}
