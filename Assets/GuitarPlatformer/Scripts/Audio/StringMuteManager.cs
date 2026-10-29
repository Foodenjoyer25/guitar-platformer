using UnityEngine;
using UnityEngine.InputSystem;

namespace GuitarPlatformer
{
    /// <summary>
    /// Global string mute controls. M palm-mutes every note the player triggers, which
    /// swaps in the short chug voice. N mutes all string audio entirely while the game
    /// keeps counting notes and accepting riff progress. There are no per-string keys.
    /// </summary>
    public class StringMuteManager : MonoBehaviour
    {
        public static StringMuteManager Instance { get; private set; }

        public bool PalmMuted { get; private set; }
        public bool AllMuted { get; private set; }
        public bool AnyMuted => PalmMuted || AllMuted;

        public string Label
        {
            get
            {
                if (PalmMuted && AllMuted) return "PALM + ALL STRINGS";
                if (AllMuted) return "ALL STRINGS";
                if (PalmMuted) return "PALM";
                return "OFF";
            }
        }

        /// <summary>Raised whenever either mute state changes.</summary>
        public event System.Action Changed;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            RefreshHud();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.mKey.wasPressedThisFrame) TogglePalm();
            if (keyboard.nKey.wasPressedThisFrame) ToggleAll();
        }

        public void TogglePalm()
        {
            PalmMuted = !PalmMuted;
            Notify();
        }

        public void ToggleAll()
        {
            AllMuted = !AllMuted;
            Notify();
        }

        public void Clear()
        {
            if (!AnyMuted) return;
            PalmMuted = false;
            AllMuted = false;
            Notify();
        }

        private void Notify()
        {
            RefreshHud();
            Changed?.Invoke();
        }

        private void RefreshHud()
        {
            if (Hud.Instance != null)
                Hud.Instance.SetMuteLabel(Label);
        }
    }
}
