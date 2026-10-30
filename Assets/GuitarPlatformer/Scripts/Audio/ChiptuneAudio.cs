using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>
    /// Plays the procedural electric-guitar voice through a pool of spatial AudioSources.
    /// A new pad attack silences any ringing voices; chord intervals stacked in the same
    /// attack still sound together. Clips are cached per pitch/string and warmed a frame
    /// at a time at startup. Riff replays are rendered into one sample-accurate clip
    /// and scheduled on a dedicated 2D source.
    /// </summary>
    public class ChiptuneAudio : MonoBehaviour
    {
        public static ChiptuneAudio Instance { get; private set; }

        [SerializeField] private int voiceCount = 16;
        [SerializeField] private float masterVolume = 0.40f;
        [SerializeField] private float performanceGain = 1.15f;

        private AudioSource[] voices;
        private AudioSource performanceSource;
        private int nextVoice;
        private readonly Dictionary<int, AudioClip> clipCache = new Dictionary<int, AudioClip>();
        private readonly Dictionary<int, AudioClip> mutedClipCache = new Dictionary<int, AudioClip>();
        private readonly Dictionary<string, AudioClip> performanceCache = new Dictionary<string, AudioClip>();

        public int CachedClipCount => clipCache.Count;
        public int MutedCachedClipCount => mutedClipCache.Count;
        public bool LastPlayWasMuted { get; private set; }
        public bool PerformancePlaying => performanceSource != null && performanceSource.isPlaying;
        public float LastPerformanceSeconds { get; private set; }
        public int LastPerformanceEventCount { get; private set; }
        public float LastPerformancePeak { get; private set; }

        private bool AllMuted =>
            StringMuteManager.Instance != null && StringMuteManager.Instance.AllMuted;

        private void Awake()
        {
            Instance = this;
            BuildVoices();
        }

        private void Start()
        {
            StartCoroutine(PrewarmClips());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (AudioClip clip in performanceCache.Values)
            {
                if (clip != null) Destroy(clip);
            }
            performanceCache.Clear();
        }

        private void Update()
        {
            if (performanceSource == null || !performanceSource.isPlaying) return;
            performanceSource.volume = AllMuted ? 0f : masterVolume * performanceGain;
        }

        /// <summary>
        /// Generates clips a frame at a time so the first landing on a new pitch does not
        /// hitch. Open strings and the campaign's authored notes are warmed first, then
        /// the low frets players touch most often in free play.
        /// </summary>
        private IEnumerator PrewarmClips()
        {
            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                GetClip(GuitarTuning.OpenMidi[s], s);
                GetMutedClip(GuitarTuning.OpenMidi[s], s);
                yield return null;
            }

            RiffLevel[] levels = RiffLibrary.CreateLevels();
            for (int l = 0; l < levels.Length; l++)
            {
                RiffLevel level = levels[l];
                GetClip(GuitarTuning.MidiFor(level.StartString, level.StartFret), level.StartString);
                yield return null;

                for (int i = 0; i < level.Steps.Length; i++)
                {
                    RiffStep step = level.Steps[i];
                    GetClip(step.Midi, step.StringIndex);
                    yield return null;

                    int[] intervals = level.ChordIntervalsFor(step);
                    for (int j = 0; j < intervals.Length; j++)
                    {
                        int midi = Mathf.Clamp(step.Midi + intervals[j], 0, 127);
                        GetClip(midi, step.StringIndex);
                        yield return null;
                    }
                }
            }

            for (int fret = 1; fret <= 5; fret++)
            {
                for (int s = 0; s < GuitarTuning.StringCount; s++)
                {
                    GetClip(GuitarTuning.MidiFor(s, fret), s);
                    yield return null;
                }
            }
        }

        private void BuildVoices()
        {
            voiceCount = Mathf.Clamp(voiceCount, 4, 32);
            voices = new AudioSource[voiceCount];
            Transform root = new GameObject("Voices").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < voiceCount; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(root, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0.22f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 5f;
                source.maxDistance = 80f;
                source.dopplerLevel = 0f;
                source.volume = masterVolume;
                voices[i] = source;
            }

            var performanceGo = new GameObject("Riff Performance");
            performanceGo.transform.SetParent(transform, false);
            performanceSource = performanceGo.AddComponent<AudioSource>();
            performanceSource.playOnAwake = false;
            performanceSource.loop = false;
            performanceSource.spatialBlend = 0f;
            performanceSource.dopplerLevel = 0f;
            performanceSource.volume = masterVolume * performanceGain;
        }

        public void Play(NoteTile tile, float impact01)
        {
            if (tile == null) return;
            StopAllVoices();
            Vector3 position = tile.transform.position + Vector3.up * 0.9f;
            float volume = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(impact01));
            PlayNote(tile.stringIndex, tile.fret, position, volume);
        }

        public bool PlayNote(int stringIndex, int fret, Vector3 position, float volume)
        {
            return PlayMidi(GuitarTuning.MidiFor(stringIndex, fret), stringIndex, position, volume);
        }

        /// <summary>
        /// Plays an arbitrary pitch with a string's timbre, used by the chord intervals.
        /// Returns false when all strings are muted, so gameplay can keep counting while
        /// the instrument stays silent.
        /// </summary>
        public bool PlayMidi(int midi, int timbreString, Vector3 position, float volume)
        {
            LastPlayWasMuted = false;
            if (AllMuted || voices == null || voices.Length == 0) return false;

            midi = Mathf.Clamp(midi, 0, 127);
            timbreString = Mathf.Clamp(timbreString, 0, GuitarTuning.StringCount - 1);
            bool palmMuted = StringMuteManager.Instance != null && StringMuteManager.Instance.PalmMuted;
            AudioClip clip = palmMuted ? GetMutedClip(midi, timbreString) : GetClip(midi, timbreString);
            AudioSource source = GetVoice();
            source.transform.position = position;
            source.Stop();
            source.clip = clip;
            // Compensate for equal-loudness: low strings need a little extra level.
            float stringGain = timbreString == 0 ? 1.22f : (timbreString == 1 ? 1.1f : 1f);
            source.volume = masterVolume * Mathf.Clamp01(volume) * stringGain;
            source.pitch = 1f;
            source.Play();
            LastPlayWasMuted = palmMuted;
            return true;
        }

        /// <summary>Renders and caches the performance clip before the schedule is chosen.</summary>
        public bool PreparePerformance(RiffLevel level)
        {
            return level != null && GetPerformanceClip(level) != null;
        }

        /// <summary>
        /// Schedules the prepared performance clip at an absolute DSP time. The visuals
        /// read the same event grid, so they stay locked to the audio even when frames
        /// are uneven.
        /// </summary>
        public bool PlayPerformance(RiffLevel level, double dspStart)
        {
            if (level == null || performanceSource == null) return false;

            AudioClip clip = GetPerformanceClip(level);
            if (clip == null) return false;

            performanceSource.Stop();
            performanceSource.clip = clip;
            performanceSource.volume = AllMuted ? 0f : masterVolume * performanceGain;
            performanceSource.PlayScheduled(dspStart);
            return true;
        }

        public void StopPerformance()
        {
            if (performanceSource != null) performanceSource.Stop();
        }

        private AudioClip GetPerformanceClip(RiffLevel level)
        {
            string key = level.Title;
            if (performanceCache.TryGetValue(key, out AudioClip cached) && cached != null)
            {
                LastPerformanceSeconds = cached.length;
                LastPerformanceEventCount = RiffPerformance.EventCount(level);
                LastPerformancePeak = 0.9f;
                return cached;
            }

            float[] samples = ChiptuneSynth.RenderPerformance(level);
            if (samples == null || samples.Length == 0) return null;

            AudioClip clip = AudioClip.Create("riff_" + key, samples.Length, 1,
                ChiptuneSynth.SampleRate, false);
            clip.SetData(samples, 0);
            performanceCache[key] = clip;

            LastPerformanceSeconds = samples.Length / (float)ChiptuneSynth.SampleRate;
            LastPerformanceEventCount = RiffPerformance.EventCount(level);
            LastPerformancePeak = 0f;
            for (int i = 0; i < samples.Length; i++)
                LastPerformancePeak = Mathf.Max(LastPerformancePeak, Mathf.Abs(samples[i]));
            return clip;
        }

        public int ActiveVoiceCount
        {
            get
            {
                if (voices == null) return 0;
                int count = 0;
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i].isPlaying) count++;
                return count;
            }
        }

        /// <summary>Short dissonant cluster used when a riff step is missed.</summary>
        public void PlayError(Vector3 position)
        {
            StopAllVoices();
            PlayMidi(34, 0, position, 0.7f);
            PlayMidi(35, 1, position, 0.55f);
            PlayMidi(41, 2, position, 0.45f);
        }

        private void StopAllVoices()
        {
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null) voices[i].Stop();
            }
        }

        private AudioSource GetVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                int index = (nextVoice + i) % voices.Length;
                if (!voices[index].isPlaying)
                {
                    nextVoice = (index + 1) % voices.Length;
                    return voices[index];
                }
            }

            AudioSource stolen = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            return stolen;
        }

        private AudioClip GetClip(int midi, int stringIndex)
        {
            int key = (stringIndex + 1) * 1000 + midi;
            if (clipCache.TryGetValue(key, out AudioClip clip) && clip != null)
                return clip;

            clip = ChiptuneSynth.CreateClip(midi, stringIndex);
            clipCache[key] = clip;
            return clip;
        }

        private AudioClip GetMutedClip(int midi, int stringIndex)
        {
            int key = (stringIndex + 1) * 1000 + midi;
            if (mutedClipCache.TryGetValue(key, out AudioClip clip) && clip != null)
                return clip;

            clip = ChiptuneSynth.CreateMutedClip(midi, stringIndex);
            mutedClipCache[key] = clip;
            return clip;
        }
    }
}
