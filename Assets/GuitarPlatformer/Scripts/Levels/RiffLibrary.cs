using System;
using System.Collections.Generic;

namespace GuitarPlatformer
{
    /// <summary>The campaign riffs plus any extra custom levels saved by the Level Creator.</summary>
    public static class RiffLibrary
    {
        public static RiffLevel[] CreateCampaignLevels()
        {
            return new[]
            {
                SmokeOnTheWater(),
                SevenNationArmy(),
                SmellsLikeTeenSpirit(),
                BackInBlack(),
                DoIWannaKnow()
            };
        }

        public static RiffLevel[] CreateLevels()
        {
            RiffLevel[] campaign = CreateCampaignLevels();
            RiffLevel[] custom = CustomLevelLibrary.LoadAll();
            if (custom.Length == 0) return campaign;

            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var levels = new List<RiffLevel>(campaign.Length + custom.Length);
            for (int i = 0; i < campaign.Length; i++)
            {
                if (campaign[i] == null) continue;
                titles.Add(campaign[i].Title);
                levels.Add(campaign[i]);
            }

            for (int i = 0; i < custom.Length; i++)
            {
                if (custom[i] == null || titles.Contains(custom[i].Title)) continue;
                titles.Add(custom[i].Title);
                levels.Add(custom[i]);
            }

            return levels.ToArray();
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

        // The White Stripes - Seven Nation Army. Imported from the Level Creator JSON.
        private static RiffLevel SevenNationArmy()
        {
            return CustomLevelLibrary.LoadNamed("seven-nation-army") ?? SevenNationArmyFallback();
        }

        private static RiffLevel SevenNationArmyFallback()
        {
            int[] strings = new int[16];
            int[] frets = { 7, 7, 10, 7, 5, 3, 2, 7, 7, 10, 7, 5, 3, 5, 3, 2 };
            float[] starts =
            {
                0f, 1.5f, 2f, 2.75f, 3.5f, 4f, 6f, 8f, 9.5f, 10f, 10.75f, 11.5f, 12f, 12.75f, 13.5f, 14f
            };
            float[] lengths =
            {
                1.5f, 0.25f, 0.25f, 0.25f, 0.5f, 2f, 2f, 1.5f, 0.25f, 0.25f, 0.25f, 0.5f, 0.5f, 0.25f, 0.25f, 2f
            };
            for (int i = 0; i < strings.Length; i++) strings[i] = 1;

            return new RiffLevel(
                "Seven Nation Army",
                "The White Stripes",
                124,
                1, 3,
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

        // AC/DC - Back in Black. Imported from the Level Creator JSON; the fallback
        // matches that file if Resources/Levels/back-in-black.json is missing.
        private static RiffLevel BackInBlack()
        {
            return CustomLevelLibrary.LoadNamed("back-in-black") ?? BackInBlackFallback();
        }

        private static RiffLevel BackInBlackFallback()
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
                0f, 1.5f, 1.75f, 2f, 3.5f, 3.75f, 4f,
                6.25f, 6.5f, 6.75f, 7f, 7.25f, 7.43f, 7.5f, 7.75f,
                8f, 9.5f, 9.75f, 10f, 11.5f, 11.75f, 12f,
                13f, 13.25f, 13.75f, 14f, 14.5f, 14.75f, 15.25f, 15.5f
            };
            float[] lengths =
            {
                0.2f, 0.2f, 0.2f, 0.15f, 0.25f, 0.2f, 0.15f,
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
                16f,
                new[] { 7 },
                "E");
        }

        // Arctic Monkeys - Do I Wanna Know? Imported from the Level Creator JSON.
        private static RiffLevel DoIWannaKnow()
        {
            return CustomLevelLibrary.LoadNamed("do-i-wanna-know") ?? DoIWannaKnowFallback();
        }

        private static RiffLevel DoIWannaKnowFallback()
        {
            int[] strings =
            {
                0, 0, 1, 0, 0, 0, 1, 2, 2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0
            };
            int[] frets =
            {
                1, 3, 1, 3, 1, 3, 1, 3, 1, 5, 3, 1, 3, 5, 3, 1, 3, 5, 5, 3
            };
            float[] starts =
            {
                0f, 0.25f, 0.5f, 1f, 3.5f, 3.75f, 4f, 4.5f, 5f, 6.5f,
                7f, 8.75f, 9f, 9.25f, 9.5f, 11.25f, 11.5f, 11.75f, 12.25f, 12.75f
            };
            float[] lengths =
            {
                0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f,
                0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f
            };

            return new RiffLevel(
                "Do I Wanna Know?",
                "Arctic Monkeys",
                85,
                0, 0,
                BuildSteps(strings, frets, starts, lengths),
                14f,
                new int[0],
                "");
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
