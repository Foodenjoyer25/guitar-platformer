using System;
using System.Text;
using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>
    /// Procedural electric-guitar voice. A Karplus-Strong plucked string feeds a
    /// classic-rock amp stage and a cabinet-style tone stack. Pitch is generated
    /// directly in the time domain, so every fret is exact equal temperament.
    /// </summary>
    public static class ChiptuneSynth
    {
        public const int SampleRate = 48000;
        public const float DefaultDuration = 1.5f;
        public const float DefaultMutedDuration = 0.5f;
        public const float PerformanceTailSeconds = 1.2f;

        private const float StringRms = 0.12f;
        private const float OutputPeak = 0.88f;

        public static float[] Generate(int midi, int stringIndex, int sampleRate = SampleRate,
            float duration = DefaultDuration)
        {
            float[] data = GenerateString(midi, stringIndex, sampleRate, duration);
            ApplyAmp(data, midi, stringIndex, sampleRate);
            return data;
        }

        /// <summary>Short, damped, mid-forward voice for palm-muted chugs.</summary>
        public static float[] GenerateMuted(int midi, int stringIndex, int sampleRate = SampleRate,
            float duration = DefaultMutedDuration)
        {
            float[] data = GenerateMutedString(midi, stringIndex, sampleRate, duration);
            ApplyMutedAmp(data, midi, stringIndex, sampleRate);
            return data;
        }

        /// <summary>
        /// Clean plucked-string model before amp distortion. Kept public so the tuning
        /// self-test can measure the string itself rather than the saturated output.
        /// </summary>
        public static float[] GenerateString(int midi, int stringIndex, int sampleRate = SampleRate,
            float duration = DefaultDuration)
        {
            midi = Mathf.Clamp(midi, 0, 127);
            stringIndex = Mathf.Clamp(stringIndex, 0, GuitarTuning.StringCount - 1);

            float freq = GuitarTuning.Frequency(midi);
            int count = Mathf.Max(1, Mathf.RoundToInt(sampleRate * duration));
            float[] data = new float[count];

            float brightness = Brightness(midi, stringIndex);
            float damping = Mathf.Lerp(0.16f, 0.42f, brightness);
            float t60 = Mathf.Lerp(3.6f, 1.6f, brightness);
            float period = sampleRate / freq;

            // The one-pole loop filter delays the loop. Subtract its phase delay at the
            // fundamental so the string stays in tune. Linear interpolation supplies the
            // fractional part of the delay with the same phase delay at low frequencies.
            float omega = 2f * Mathf.PI * freq / sampleRate;
            float phaseDelay = Mathf.Atan2(damping * Mathf.Sin(omega),
                1f - damping * Mathf.Cos(omega)) / Mathf.Max(1e-6f, omega);
            float loopMag = (1f - damping) /
                Mathf.Sqrt(1f - 2f * damping * Mathf.Cos(omega) + damping * damping);
            float delay = Mathf.Max(2f, period - phaseDelay);

            float desiredGain = Mathf.Pow(0.001f, 1f / Mathf.Max(1f, t60 * freq));
            float loopGain = Mathf.Clamp(desiredGain / Mathf.Max(0.2f, loopMag), 0.8f, 0.99995f);

            int lineLength = Mathf.CeilToInt(delay) + 4;
            float[] line = new float[lineLength];

            // Pick excitation: low-passed noise shaped by a pick-position comb.
            var rng = new System.Random(173 + midi * 31 + stringIndex * 977);
            float cutoff = Mathf.Lerp(2800f, 7500f, brightness);
            float coeff = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sampleRate);
            float lowPass = 0f;
            float[] noise = new float[lineLength];
            for (int i = 0; i < lineLength; i++)
            {
                float n = (float)rng.NextDouble() * 2f - 1f;
                lowPass += (n - lowPass) * coeff;
                noise[i] = lowPass;
            }

            int pickDelay = Mathf.Clamp(
                Mathf.RoundToInt(period * Mathf.Lerp(0.18f, 0.11f, brightness)),
                1, lineLength - 1);
            float mean = 0f;
            for (int i = 0; i < lineLength; i++)
            {
                float v = noise[i] - noise[(i - pickDelay + lineLength) % lineLength];
                line[i] = v;
                mean += v;
            }
            mean /= lineLength;

            float energy = 0f;
            for (int i = 0; i < lineLength; i++)
            {
                line[i] -= mean;
                energy += line[i] * line[i];
            }
            float excitationRms = Mathf.Sqrt(energy / lineLength);
            float excitationGain = excitationRms > 1e-6f ? 0.28f / excitationRms : 1f;
            for (int i = 0; i < lineLength; i++)
                line[i] *= excitationGain;

            int write = 0;
            float filterState = 0f;
            for (int i = 0; i < count; i++)
            {
                float read = write - delay;
                while (read < 0f) read += lineLength;
                int i0 = (int)read;
                if (i0 >= lineLength) i0 -= lineLength;
                int i1 = i0 + 1;
                if (i1 >= lineLength) i1 = 0;
                float frac = read - (int)read;
                float delayed = line[i0] * (1f - frac) + line[i1] * frac;

                filterState = (1f - damping) * delayed + damping * filterState;
                line[write] = filterState * loopGain;
                write++;
                if (write >= lineLength) write = 0;
                data[i] = filterState;
            }

            // Pick click: a short bright transient at the very start.
            int clickLength = Mathf.Min(count, Mathf.RoundToInt(sampleRate * 0.004f));
            for (int i = 0; i < clickLength; i++)
            {
                float decay = Mathf.Exp(-i / (sampleRate * 0.0012f));
                data[i] += (i % 2 == 0 ? 1f : -1f) * 0.08f * decay;
            }

            DcBlock(data);
            NormalizeRms(data, StringRms);
            FadeOut(data, sampleRate, 0.04f);
            return data;
        }

        /// <summary>Raw string model with the short palm-mute envelope and dulled top end.</summary>
        public static float[] GenerateMutedString(int midi, int stringIndex, int sampleRate = SampleRate,
            float duration = DefaultMutedDuration)
        {
            float[] data = GenerateString(midi, stringIndex, sampleRate, duration);
            float brightness = Brightness(midi, stringIndex);
            float tau = Mathf.Lerp(0.026f, 0.048f, brightness);
            Biquad lowPass = Biquad.LowPass(sampleRate, Mathf.Lerp(1100f, 2100f, brightness), 0.7071f);

            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)sampleRate;
                data[i] = lowPass.Process(data[i]) * Mathf.Exp(-t / tau);
            }

            DcBlock(data);
            NormalizeRms(data, StringRms * 1.15f);
            FadeOut(data, sampleRate, 0.02f);
            return data;
        }

        public static AudioClip CreateClip(int midi, int stringIndex, int sampleRate = SampleRate)
        {
            float[] samples = Generate(midi, stringIndex, sampleRate);
            AudioClip clip = AudioClip.Create(
                "guitar_" + GuitarTuning.NoteName(midi) + "_s" + stringIndex,
                samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateMutedClip(int midi, int stringIndex, int sampleRate = SampleRate)
        {
            float[] samples = GenerateMuted(midi, stringIndex, sampleRate);
            AudioClip clip = AudioClip.Create(
                "guitar_mute_" + GuitarTuning.NoteName(midi) + "_s" + stringIndex,
                samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Brightness(int midi, int stringIndex)
        {
            int fret = Mathf.Clamp(midi - GuitarTuning.OpenMidi[stringIndex], 0, 24);
            return Mathf.Clamp01(stringIndex / 5f * 0.65f + fret / 24f * 0.35f);
        }

        /// <summary>
        /// Chord-level amp rendering. The raw string voices are summed first and then
        /// driven through the amp once, which is how a real power chord generates its
        /// intermodulation crunch instead of sounding like two separate clean notes.
        /// </summary>
        public static float[] GenerateChord(int rootMidi, int stringIndex, int[] intervals, bool muted,
            float duration)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * Mathf.Max(0.05f, duration)));
            float[] chord = new float[count];
            int voices = 1 + (intervals != null ? intervals.Length : 0);
            float gain = 1f / Mathf.Sqrt(voices);

            AddVoice(chord, rootMidi, stringIndex, muted, gain);
            if (intervals != null)
            {
                for (int i = 0; i < intervals.Length; i++)
                {
                    int midi = Mathf.Clamp(rootMidi + intervals[i], 0, 127);
                    AddVoice(chord, midi, stringIndex, muted, gain);
                }
            }

            if (muted)
                ApplyMutedAmp(chord, rootMidi, stringIndex, SampleRate);
            else
                ApplyAmp(chord, rootMidi, stringIndex, SampleRate);
            return chord;
        }

        /// <summary>
        /// Renders an entire riff replay into one clip. Every event is placed on the
        /// authored beat grid with sample rounding. A new note fades out whatever is
        /// still ringing so attacks stay monophonic, matching live play. The mix then
        /// gets a final amp bus and soft limiter.
        /// </summary>
        public static float[] RenderPerformance(RiffLevel level)
        {
            if (level == null || level.Steps == null || level.Steps.Length == 0)
                return new float[0];

            RiffPerformanceNote[] notes = RiffPerformance.Build(level);
            float secondsPerBeat = 60f / Mathf.Max(1, level.Bpm);
            float totalBeats = RiffPerformance.TotalBeats(level);
            int count = Mathf.Max(1, Mathf.RoundToInt(
                SampleRate * (totalBeats * secondsPerBeat + PerformanceTailSeconds)));
            float[] mix = new float[count];
            int fadeSamples = Mathf.Max(1, SampleRate / 200);

            for (int i = 0; i < notes.Length; i++)
            {
                RiffPerformanceNote note = notes[i];
                int offset = Mathf.RoundToInt(SampleRate * note.StartBeat * secondsPerBeat);
                if (offset >= mix.Length) continue;

                CutOffAt(mix, offset, fadeSamples);
                float duration = Mathf.Max(0.08f, note.LengthBeats * secondsPerBeat + 0.45f);
                float[] chord = GenerateChord(note.Midi, note.StringIndex, note.ChordIntervals,
                    note.Muted, duration);
                MixInto(mix, chord, offset, 0.78f);
            }

            ApplyMixBus(mix);
            NormalizePeak(mix, 0.9f);
            return mix;
        }

        private static void AddVoice(float[] target, int midi, int stringIndex, bool muted, float gain)
        {
            float duration = target.Length / (float)SampleRate;
            float[] raw = muted
                ? GenerateMutedString(midi, stringIndex, SampleRate, duration)
                : GenerateString(midi, stringIndex, SampleRate, duration);
            int count = Mathf.Min(target.Length, raw.Length);
            for (int i = 0; i < count; i++)
                target[i] += raw[i] * gain;
        }

        private static void MixInto(float[] target, float[] source, int offset, float gain)
        {
            int count = Mathf.Min(source.Length, target.Length - offset);
            for (int i = 0; i < count; i++)
                target[offset + i] += source[i] * gain;
        }

        private static void CutOffAt(float[] mix, int offset, int fadeSamples)
        {
            int fadeStart = Mathf.Max(0, offset - fadeSamples);
            int fadeLength = offset - fadeStart;
            for (int i = 0; i < fadeLength; i++)
                mix[fadeStart + i] *= 1f - i / (float)fadeLength;
            for (int i = offset; i < mix.Length; i++)
                mix[i] = 0f;
        }

        private static void ApplyAmp(float[] data, int midi, int stringIndex, int sampleRate)
        {
            float brightness = Brightness(midi, stringIndex);
            float drive = Mathf.Lerp(8.0f, 11.0f, brightness);

            Biquad preLowPass = Biquad.LowPass(sampleRate, 6800f, 0.7071f);
            Biquad highPass = Biquad.HighPass(sampleRate, 85f, 0.7071f);
            Biquad lowResonance = Biquad.Peaking(sampleRate, 120f, 1.0f, 2.8f);
            Biquad midScoop = Biquad.Peaking(sampleRate, 700f, 1.0f, -1.4f);
            Biquad midrange = Biquad.Peaking(sampleRate, 1150f, 1.2f, 2.6f);
            Biquad presence = Biquad.Peaking(sampleRate, 2900f, 0.9f, 3.8f);
            Biquad cabLowPass = Biquad.LowPass(sampleRate, 5200f, 0.7071f);

            // Two-stage asymmetric saturation: the first bias adds even harmonics,
            // the second stage hardens the attack so power chords really bite.
            const float bias = 0.18f;
            float biasTanh = FastTanh(bias);
            const float bias2 = 0.06f;
            float bias2Tanh = FastTanh(bias2);

            for (int i = 0; i < data.Length; i++)
            {
                float x = preLowPass.Process(data[i]);
                float y = FastTanh(drive * x + bias) - biasTanh;
                y = FastTanh(2.6f * y + bias2) - bias2Tanh;
                data[i] = y;
            }

            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                x = highPass.Process(x);
                x = lowResonance.Process(x);
                x = midScoop.Process(x);
                x = midrange.Process(x);
                x = presence.Process(x);
                x = cabLowPass.Process(x);
                data[i] = x;
            }

            DcBlock(data);
            FadeOut(data, sampleRate, 0.06f);
            NormalizePeak(data, OutputPeak);
        }

        /// <summary>Darker, tighter amp for palm-muted chugs.</summary>
        private static void ApplyMutedAmp(float[] data, int midi, int stringIndex, int sampleRate)
        {
            float brightness = Brightness(midi, stringIndex);
            float drive = Mathf.Lerp(6.5f, 8.5f, brightness);

            Biquad preLowPass = Biquad.LowPass(sampleRate, 2400f, 0.7071f);
            Biquad highPass = Biquad.HighPass(sampleRate, 110f, 0.7071f);
            Biquad thump = Biquad.Peaking(sampleRate, 160f, 1.2f, 4.0f);
            Biquad midrange = Biquad.Peaking(sampleRate, 650f, 1.0f, 2.2f);
            Biquad cabLowPass = Biquad.LowPass(sampleRate, 3200f, 0.7071f);

            const float bias = 0.12f;
            float biasTanh = FastTanh(bias);

            for (int i = 0; i < data.Length; i++)
            {
                float x = preLowPass.Process(data[i]);
                float y = FastTanh(drive * x + bias) - biasTanh;
                data[i] = FastTanh(1.9f * y);
            }

            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                x = highPass.Process(x);
                x = thump.Process(x);
                x = midrange.Process(x);
                x = cabLowPass.Process(x);
                data[i] = x;
            }

            DcBlock(data);
            FadeOut(data, sampleRate, 0.04f);
            NormalizePeak(data, OutputPeak);
        }

        /// <summary>Final glue for a full riff: tighten lows, add presence, soft-limit.</summary>
        private static void ApplyMixBus(float[] data)
        {
            Biquad highPass = Biquad.HighPass(SampleRate, 55f, 0.7071f);
            Biquad midrange = Biquad.Peaking(SampleRate, 1200f, 1.0f, 1.6f);
            Biquad presence = Biquad.Peaking(SampleRate, 3200f, 0.9f, 1.8f);
            Biquad lowPass = Biquad.LowPass(SampleRate, 6200f, 0.7071f);
            float limiterNormal = FastTanh(1.05f);

            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                x = highPass.Process(x);
                x = midrange.Process(x);
                x = presence.Process(x);
                x = lowPass.Process(x);
                data[i] = FastTanh(1.05f * x) / limiterNormal;
            }

            DcBlock(data);
            FadeOut(data, SampleRate, 0.12f);
        }

        private static float FastTanh(float x)
        {
            if (x <= -3f) return -1f;
            if (x >= 3f) return 1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }

        private static void DcBlock(float[] data)
        {
            float previousIn = 0f;
            float previousOut = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float input = data[i];
                float output = input - previousIn + 0.995f * previousOut;
                previousIn = input;
                previousOut = output;
                data[i] = output;
            }
        }

        private static void NormalizeRms(float[] data, float target)
        {
            double sum = 0.0;
            for (int i = 0; i < data.Length; i++)
                sum += data[i] * (double)data[i];
            float rms = (float)Math.Sqrt(sum / Mathf.Max(1, data.Length));
            if (rms <= 1e-6f) return;
            float gain = target / rms;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        private static void NormalizePeak(float[] data, float target)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak <= 1e-6f) return;
            float gain = target / peak;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        private static void FadeOut(float[] data, int sampleRate, float seconds)
        {
            int fade = Mathf.Min(data.Length, Mathf.RoundToInt(sampleRate * seconds));
            for (int i = 0; i < fade; i++)
            {
                float t = (fade - i) / (float)fade;
                data[data.Length - fade + i] *= t;
            }
        }

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            return peak;
        }

        /// <summary>Estimate the fundamental with autocorrelation, for the tuning self-test.</summary>
        public static float EstimateFrequency(float[] samples, int sampleRate)
        {
            int start = Mathf.Min(samples.Length - 1, Mathf.RoundToInt(sampleRate * 0.055f));
            int window = Mathf.Min(4096, samples.Length - start);
            if (window < 256) return 0f;

            double mean = 0.0;
            for (int i = 0; i < window; i++) mean += samples[start + i];
            mean /= window;

            int minLag = Mathf.Max(2, Mathf.FloorToInt(sampleRate / 2000f));
            int maxLag = Mathf.Min(window / 2, Mathf.CeilToInt(sampleRate / 40f));
            float[] corr = new float[maxLag + 2];

            for (int lag = minLag; lag <= maxLag; lag++)
            {
                double xy = 0.0, xx = 0.0, yy = 0.0;
                int n = window - lag;
                for (int i = 0; i < n; i++)
                {
                    double a = samples[start + i] - mean;
                    double b = samples[start + i + lag] - mean;
                    xy += a * b;
                    xx += a * a;
                    yy += b * b;
                }
                corr[lag] = xx > 1e-12 && yy > 1e-12 ? (float)(xy / Math.Sqrt(xx * yy)) : 0f;
            }

            float best = 0f;
            for (int lag = minLag; lag <= maxLag; lag++)
                best = Mathf.Max(best, corr[lag]);
            if (best <= 0.05f) return 0f;

            int chosen = -1;
            for (int lag = minLag + 1; lag < maxLag; lag++)
            {
                if (corr[lag] >= 0.72f * best && corr[lag] >= corr[lag - 1] && corr[lag] >= corr[lag + 1])
                {
                    chosen = lag;
                    break;
                }
            }
            if (chosen < 0) return 0f;

            float y0 = corr[chosen - 1];
            float y1 = corr[chosen];
            float y2 = corr[chosen + 1];
            float denom = y0 - 2f * y1 + y2;
            float shift = Mathf.Abs(denom) > 1e-6f ? 0.5f * (y0 - y2) / denom : 0f;
            float lagFloat = chosen + Mathf.Clamp(shift, -0.5f, 0.5f);
            return sampleRate / lagFloat;
        }

        public static string TuningReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Guitar synth tuning check (48 kHz, clean string model):");
            int[] strings = { 0, 1, 2, 3, 4, 5, 0, 0, 5, 5 };
            int[] frets = { 0, 0, 0, 0, 0, 0, 5, 12, 12, 24 };
            for (int i = 0; i < strings.Length; i++)
            {
                int s = strings[i];
                int midi = GuitarTuning.MidiFor(s, frets[i]);
                float expected = GuitarTuning.Frequency(midi);
                float[] data = GenerateString(midi, s, SampleRate, 0.6f);
                float measured = EstimateFrequency(data, SampleRate);
                float cents = measured > 0f ? 1200f * Mathf.Log(measured / expected, 2f) : 9999f;
                sb.AppendLine($"  {GuitarTuning.NoteName(midi),-4} string {GuitarTuning.GuitarStringNumbers[s]} " +
                              $"fret {frets[i],2}  expected {expected,8:F2} Hz  measured {measured,8:F2} Hz  {cents,7:F2} cents");
            }
            return sb.ToString();
        }

        public static string LoudnessReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Guitar synth loudness check (open strings, amp + cab):");
            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                int midi = GuitarTuning.OpenMidi[s];
                float[] data = Generate(midi, s, SampleRate, 0.6f);
                float rms = Rms(data);
                float hf = HighFrequencyRatio(data, SampleRate);
                float peak = Peak(data);
                float crest = rms > 1e-6f ? peak / rms : 0f;
                float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-6f));
                float aWeighted = AWeightedLevel(data, SampleRate);
                sb.AppendLine($"  string {GuitarTuning.GuitarStringNumbers[s]} {GuitarTuning.OpenNoteNames[s],-3} " +
                              $"rms {rms:F4} ({db,6:F1} dBFS)  crest {crest,5:F2}  A {aWeighted,7:F1} dB  " +
                              $"energy>150Hz {hf,6:P1}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Rough A-weighted level of a clip, used to keep the six strings balanced by perceived
        /// loudness rather than raw RMS. Low notes need far more energy to sound as loud.
        /// </summary>
        public static float AWeightedLevel(float[] samples, int sampleRate)
        {
            int start = Mathf.Min(samples.Length - 1, Mathf.RoundToInt(sampleRate * 0.06f));
            int window = Mathf.Min(4096, samples.Length - start);
            if (window < 256) return -120f;

            double total = 0.0;
            for (float f = 40f; f < 4000f; f += 25f)
            {
                double re = 0.0;
                double im = 0.0;
                double w = 2.0 * Math.PI * f / sampleRate;
                for (int i = 0; i < window; i++)
                {
                    double hann = 0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (window - 1));
                    double v = samples[start + i] * hann;
                    re += v * Math.Cos(w * i);
                    im -= v * Math.Sin(w * i);
                }

                double mag2 = (re * re + im * im) / ((double)window * window);
                total += Math.Pow(10.0, AWeightDb(f) / 10.0) * mag2;
            }

            return 10f * Mathf.Log10(Mathf.Max((float)total, 1e-30f));
        }

        private static float AWeightDb(float frequency)
        {
            double f2 = (double)frequency * frequency;
            const double c1 = 20.6, c2 = 107.7, c3 = 737.9, c4 = 12194.0;
            double ra = (c4 * c4 * f2 * f2) /
                        ((f2 + c1 * c1) * Math.Sqrt((f2 + c2 * c2) * (f2 + c3 * c3)) * (f2 + c4 * c4));
            return (float)(20.0 * Math.Log10(Math.Max(ra, 1e-12)) + 2.0);
        }

        public static float Rms(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0f;
            double sum = 0.0;
            for (int i = 0; i < samples.Length; i++)
                sum += samples[i] * (double)samples[i];
            return (float)System.Math.Sqrt(sum / samples.Length);
        }

        /// <summary>Share of signal energy above the cutoff, used to check low-string presence.</summary>
        public static float HighFrequencyRatio(float[] samples, int sampleRate, float cutoffHz = 150f)
        {
            if (samples == null || samples.Length < 2) return 0f;
            float rc = 1f / (2f * Mathf.PI * cutoffHz);
            float alpha = rc / (rc + 1f / sampleRate);
            float previousIn = samples[0];
            float previousOut = 0f;
            double high = 0.0;
            double total = 0.0;

            for (int i = 0; i < samples.Length; i++)
            {
                float input = samples[i];
                float output = alpha * (previousOut + input - previousIn);
                previousIn = input;
                previousOut = output;
                high += output * (double)output;
                total += input * (double)input;
            }

            return total > 1e-12 ? (float)System.Math.Sqrt(high / total) : 0f;
        }

        /// <summary>Second-order RBJ biquad used by the amp tone stack.</summary>
        private struct Biquad
        {
            private float b0, b1, b2, a1, a2;
            private float x1, x2, y1, y2;

            private Biquad(float b0, float b1, float b2, float a1, float a2)
            {
                this.b0 = b0;
                this.b1 = b1;
                this.b2 = b2;
                this.a1 = a1;
                this.a2 = a2;
                x1 = 0f;
                x2 = 0f;
                y1 = 0f;
                y2 = 0f;
            }

            public static Biquad LowPass(float sampleRate, float frequency, float q)
            {
                float w0 = 2f * Mathf.PI * frequency / sampleRate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float cos = Mathf.Cos(w0);
                float a0 = 1f + alpha;
                return new Biquad(
                    (1f - cos) * 0.5f / a0,
                    (1f - cos) / a0,
                    (1f - cos) * 0.5f / a0,
                    -2f * cos / a0,
                    (1f - alpha) / a0);
            }

            public static Biquad HighPass(float sampleRate, float frequency, float q)
            {
                float w0 = 2f * Mathf.PI * frequency / sampleRate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float cos = Mathf.Cos(w0);
                float a0 = 1f + alpha;
                return new Biquad(
                    (1f + cos) * 0.5f / a0,
                    -(1f + cos) / a0,
                    (1f + cos) * 0.5f / a0,
                    -2f * cos / a0,
                    (1f - alpha) / a0);
            }

            public static Biquad Peaking(float sampleRate, float frequency, float q, float dbGain)
            {
                float a = Mathf.Pow(10f, dbGain / 40f);
                float w0 = 2f * Mathf.PI * frequency / sampleRate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float cos = Mathf.Cos(w0);
                float a0 = 1f + alpha / a;
                return new Biquad(
                    (1f + alpha * a) / a0,
                    -2f * cos / a0,
                    (1f - alpha * a) / a0,
                    -2f * cos / a0,
                    (1f - alpha / a) / a0);
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }
    }
}
