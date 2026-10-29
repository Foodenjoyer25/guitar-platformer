using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GuitarPlatformer.EditorTools
{
    /// <summary>
    /// Batch-mode tone audition: renders the dry amp/cab guitar voice to WAV files in
    /// /tmp/opencode/guitar-tone. The in-game echo and reverb add the room on top.
    /// </summary>
    public static class GuitarTonePreview
    {
        private const string OutputFolder = "/tmp/opencode/guitar-tone";
        private const int Rate = ChiptuneSynth.SampleRate;

        [MenuItem("Guitar Platformer/Render Guitar Tone Preview")]
        public static void Render()
        {
            Directory.CreateDirectory(OutputFolder);

            RenderOpenStrings();
            RenderPowerChords();
            RenderMutes();

            RiffLevel[] levels = RiffLibrary.CreateLevels();
            for (int i = 0; i < levels.Length; i++)
                RenderRiff(levels[i], (i + 4).ToString("00") + "_" + Slug(levels[i].Title));

            Debug.Log("Guitar tone preview written to " + OutputFolder);
        }

        private static void RenderOpenStrings()
        {
            const float spacing = 0.42f;
            int count = Mathf.RoundToInt(Rate * (spacing * GuitarTuning.StringCount + 1.5f));
            float[] mix = new float[count];

            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                float[] note = ChiptuneSynth.Generate(GuitarTuning.OpenMidi[s], s, Rate, 0.7f);
                Mix(mix, note, Mathf.RoundToInt(Rate * spacing * s), 0.75f);
            }

            WriteWav(Path.Combine(OutputFolder, "01_open_strings.wav"), mix);
        }

        private static void RenderPowerChords()
        {
            int count = Mathf.RoundToInt(Rate * 2.6f);
            float[] mix = new float[count];

            // E5: low E root plus the fifth, amped together so the chord crunches.
            float[] e5 = ChiptuneSynth.GenerateChord(GuitarTuning.OpenMidi[0], 0, new[] { 7 }, false, 1.5f);
            Mix(mix, e5, 0, 0.9f);

            // A5: open A plus E3.
            int offset = Mathf.RoundToInt(Rate * 1.2f);
            float[] a5 = ChiptuneSynth.GenerateChord(GuitarTuning.OpenMidi[1], 1, new[] { 7 }, false, 1.1f);
            Mix(mix, a5, offset, 0.9f);

            WriteWav(Path.Combine(OutputFolder, "02_power_chords.wav"), mix);
        }

        private static void RenderMutes()
        {
            int count = Mathf.RoundToInt(Rate * 4.2f);
            float[] mix = new float[count];

            float[] normal = ChiptuneSynth.Generate(GuitarTuning.OpenMidi[0], 0, Rate, 1.0f);
            Mix(mix, normal, Mathf.RoundToInt(Rate * 0.1f), 0.85f);

            float[] palm = ChiptuneSynth.GenerateMuted(GuitarTuning.OpenMidi[0], 0, Rate, 1.0f);
            Mix(mix, palm, Mathf.RoundToInt(Rate * 1.1f), 0.95f);

            float[] chord = ChiptuneSynth.GenerateChord(GuitarTuning.OpenMidi[0], 0, new[] { 7 }, false, 1.0f);
            Mix(mix, chord, Mathf.RoundToInt(Rate * 2.1f), 0.85f);

            float[] palmChord = ChiptuneSynth.GenerateChord(GuitarTuning.OpenMidi[0], 0, new[] { 7 }, true, 0.8f);
            Mix(mix, palmChord, Mathf.RoundToInt(Rate * 3.2f), 0.95f);

            WriteWav(Path.Combine(OutputFolder, "03_palm_mute.wav"), mix);
        }

        private static void RenderRiff(RiffLevel level, string fileName)
        {
            float[] mix = ChiptuneSynth.RenderPerformance(level);
            WriteWav(Path.Combine(OutputFolder, fileName + ".wav"), mix);
        }

        private static void Mix(float[] target, float[] source, int offset, float gain)
        {
            int count = Mathf.Min(source.Length, target.Length - offset);
            for (int i = 0; i < count; i++)
                target[offset + i] += source[i] * gain;
        }

        private static void WriteWav(string path, float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            float gain = peak > 1e-6f ? 0.89f / peak : 1f;

            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataSize = samples.Length * 2;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(Rate);
                writer.Write(Rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    float value = Mathf.Clamp(samples[i] * gain, -1f, 1f);
                    writer.Write((short)Mathf.RoundToInt(value * 32767f));
                }
            }

            Debug.Log("Wrote " + path + " (" + (samples.Length / (float)Rate).ToString("F1") + " s)");
        }

        private static string Slug(string title)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < title.Length; i++)
            {
                char c = char.ToLowerInvariant(title[i]);
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ') sb.Append('_');
            }
            return sb.ToString();
        }
    }
}
