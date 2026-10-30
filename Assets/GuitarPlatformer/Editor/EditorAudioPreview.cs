using UnityEditor;
using UnityEngine;

namespace GuitarPlatformer.EditorTools
{
    /// <summary>Plays a generated float buffer through a hidden editor AudioSource.</summary>
    internal static class EditorAudioPreview
    {
        private static AudioSource source;
        private static AudioClip clip;
        private static double stopAt;

        public static void Play(float[] samples, int sampleRate)
        {
            Stop();
            if (samples == null || samples.Length == 0 || sampleRate <= 0) return;

            clip = AudioClip.Create("LevelCreatorPreview", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);

            GameObject go = EditorUtility.CreateGameObjectWithHideFlags("LevelCreatorPreview",
                HideFlags.HideAndDontSave, typeof(AudioSource));
            source = go.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = clip;
            source.Play();

            stopAt = EditorApplication.timeSinceStartup + samples.Length / (double)sampleRate + 0.15;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            if (source != null)
            {
                source.Stop();
                Object.DestroyImmediate(source.gameObject);
                source = null;
            }

            if (clip != null)
            {
                Object.DestroyImmediate(clip);
                clip = null;
            }
        }

        private static void Tick()
        {
            if (source == null || (!source.isPlaying && EditorApplication.timeSinceStartup >= stopAt))
                Stop();
        }
    }
}
