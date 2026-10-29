using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>Shared level dimensions used by both the scene builder and runtime code.</summary>
    public static class FretboardLayout
    {
        public const int FretCount = 24;
        public const int StringCount = 6;

        public const float ScaleLength = 96f;
        public const float StringSpacing = 2.4f;
        public const float BoardWidth = 16f;
        public const float BoardMargin = 0.8f;
        public const float PadHeight = 0.45f;
        public const float PadGap = 0.18f;

        public const float NeckStartZ = -5f;
        public const float NeckEndZ = 78f;
        public const float OpenPadStartZ = -4.8f;
        public const float OpenPadEndZ = -1.5f;
        public const float OpenPadCenterZ = (OpenPadStartZ + OpenPadEndZ) * 0.5f;
        public const float OpenPadLength = OpenPadEndZ - OpenPadStartZ;
        public const float NutZ = 0f;

        public static float LaneX(int stringIndex)
        {
            // High E on screen-left, low E on screen-right (right-handed player view).
            return ((StringCount - 1) * 0.5f - stringIndex) * StringSpacing;
        }

        public static float FretPosition(int fret)
        {
            if (fret <= 0) return 0f;
            return ScaleLength * (1f - Mathf.Pow(2f, -fret / 12f));
        }

        /// <summary>Start/end Z of the pad for the given fret (1..24).</summary>
        public static Vector2 FretCell(int fret)
        {
            return new Vector2(FretPosition(fret - 1), FretPosition(fret));
        }

        public static Vector3 PadCenter(int stringIndex, int fret, bool isOpen)
        {
            float z = isOpen ? OpenPadCenterZ : (FretCell(fret).x + FretCell(fret).y) * 0.5f;
            return new Vector3(LaneX(stringIndex), PadHeight * 0.5f, z);
        }

        public static Vector3 PadSize(int fret, bool isOpen)
        {
            float length = isOpen ? OpenPadLength : FretCell(fret).y - FretCell(fret).x;
            return new Vector3(StringSpacing - PadGap, PadHeight, length - PadGap);
        }
    }
}
