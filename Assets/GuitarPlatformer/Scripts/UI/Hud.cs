using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GuitarPlatformer
{
    public class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }

        [SerializeField] private Text noteText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text countText;
        [SerializeField] private Text controlsText;
        [SerializeField] private Text viewText;
        [SerializeField] private Text chordText;
        [SerializeField] private Text boostText;
        [SerializeField] private Text muteText;
        [SerializeField] private Text[] recentTexts;
        [SerializeField] private Image fadeImage;
        [SerializeField] private CanvasGroup noteGroup;

        [Header("Riff Campaign")]
        [SerializeField] private CanvasGroup levelGroup;
        [SerializeField] private Text levelText;
        [SerializeField] private Text stepText;
        [SerializeField] private Text targetText;
        [SerializeField] private Text chordRequirementText;
        [SerializeField] private Text noteModeText;
        [SerializeField] private Text runStatusText;
        [SerializeField] private Image errorImage;
        [SerializeField] private CanvasGroup revealGroup;
        [SerializeField] private Text revealTitle;
        [SerializeField] private Text revealArtist;
        [SerializeField] private Text revealPrompt;

        private int notesPlayed;
        private Coroutine noteRoutine;
        private Coroutine fadeRoutine;
        private Coroutine errorRoutine;
        private readonly List<NoteTile> recent = new List<NoteTile>();

        private void Awake()
        {
            Instance = this;
            if (fadeImage != null)
            {
                Color c = fadeImage.color;
                c.a = 0f;
                fadeImage.color = c;
            }
            if (noteGroup != null) noteGroup.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(Text note, Text detail, Text count, Text controls, Text view, Text chord,
            Text boost, Text mute, Text[] recentNotes, Image fade, CanvasGroup group)
        {
            noteText = note;
            detailText = detail;
            countText = count;
            controlsText = controls;
            viewText = view;
            chordText = chord;
            boostText = boost;
            muteText = mute;
            recentTexts = recentNotes;
            fadeImage = fade;
            noteGroup = group;
        }

        public void ConfigureCampaign(CanvasGroup levelCanvasGroup, Text level, Text step, Text target,
            Text chordRequirement, Text noteMode, Text runStatus, Image errorFlash, CanvasGroup revealCanvasGroup,
            Text songTitle, Text songArtist, Text prompt)
        {
            levelGroup = levelCanvasGroup;
            levelText = level;
            stepText = step;
            targetText = target;
            chordRequirementText = chordRequirement;
            noteModeText = noteMode;
            runStatusText = runStatus;
            errorImage = errorFlash;
            revealGroup = revealCanvasGroup;
            revealTitle = songTitle;
            revealArtist = songArtist;
            revealPrompt = prompt;

            if (errorImage != null)
            {
                Color c = errorImage.color;
                c.a = 0f;
                errorImage.color = c;
            }
            if (revealGroup != null)
            {
                revealGroup.alpha = 0f;
                revealGroup.blocksRaycasts = false;
                revealGroup.interactable = false;
            }
        }

        private void Start()
        {
            if (ChordManager.Instance != null)
                SetChordLabel(ChordManager.Instance.ActiveLabel);
            else
                SetChordLabel("");

            if (StringMuteManager.Instance != null)
                SetMuteLabel(StringMuteManager.Instance.Label);
            else
                SetMuteLabel("OFF");
        }

        public void SetViewLabel(string viewName)
        {
            if (viewText != null)
                viewText.text = "VIEW  ·  " + viewName;
        }

        public void SetChordLabel(string label)
        {
            if (chordText == null) return;
            bool active = !string.IsNullOrEmpty(label);
            chordText.text = active ? "CHORD  ·  " + label : "CHORD  ·  OFF";
            chordText.color = active
                ? new Color(1f, 0.48f, 0.92f, 1f)
                : new Color(0.63f, 0.72f, 0.9f, 0.82f);
        }

        public void SetMuteLabel(string label)
        {
            if (muteText == null) return;
            muteText.text = "STRINGS  ·  " + label;
            if (label == null || label == "OFF")
                muteText.color = new Color(0.63f, 0.72f, 0.9f, 0.82f);
            else if (label.Contains("ALL"))
                muteText.color = new Color(1f, 0.32f, 0.38f, 1f);
            else
                muteText.color = new Color(1f, 0.68f, 0.24f, 1f);
        }

        public void SetLevelVisible(bool visible)
        {
            if (levelGroup != null) levelGroup.alpha = visible ? 1f : 0f;
        }

        public void SetLevelHeader(string text)
        {
            if (levelText != null) levelText.text = text;
        }

        public void SetStep(string text)
        {
            if (stepText != null) stepText.text = text;
        }

        public void SetTarget(string text)
        {
            SetTarget(text, Color.white);
        }

        public void SetTarget(string text, Color color)
        {
            if (targetText == null) return;
            targetText.text = text ?? "";
            targetText.color = color;
        }

        public void SetChordRequirement(string text, bool ready)
        {
            if (chordRequirementText == null) return;
            CompactChordLabel();

            bool hide = string.IsNullOrEmpty(text) || text == "NONE" || text == "FREE";
            chordRequirementText.gameObject.SetActive(!hide);
            if (hide)
            {
                chordRequirementText.text = "";
                return;
            }

            chordRequirementText.text = "CHORD  ·  " + text;
            chordRequirementText.color = ready
                ? new Color(0.3f, 0.95f, 1f, 1f)
                : new Color(1f, 0.45f, 0.8f, 1f);
        }

        private void CompactChordLabel()
        {
            if (chordRequirementText == null) return;
            chordRequirementText.fontSize = 18;
            chordRequirementText.alignment = TextAnchor.LowerCenter;
            RectTransform rect = chordRequirementText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 48f);
            rect.sizeDelta = new Vector2(900f, 22f);
        }

        public void SetNoteMode(string mode)
        {
            if (noteModeText == null) return;
            noteModeText.text = "NOTES  ·  " + mode;
            noteModeText.color = mode != null && mode.Contains("LANDING")
                ? new Color(0.3f, 0.95f, 1f, 1f)
                : new Color(0.63f, 0.72f, 0.9f, 0.82f);
        }

        public void SetRunStatus(string text, Color color)
        {
            if (runStatusText == null) return;
            runStatusText.text = text ?? "";
            runStatusText.color = color;
        }

        public void ShowReveal(string title, string artist, string prompt)
        {
            if (revealGroup != null)
            {
                revealGroup.alpha = 1f;
                revealGroup.blocksRaycasts = true;
                revealGroup.interactable = true;
            }
            if (revealTitle != null) revealTitle.text = title;
            if (revealArtist != null) revealArtist.text = artist;
            if (revealPrompt != null) revealPrompt.text = prompt ?? "";
            if (levelGroup != null) levelGroup.alpha = 0f;
        }

        public void HideReveal()
        {
            if (revealGroup != null)
            {
                revealGroup.alpha = 0f;
                revealGroup.blocksRaycasts = false;
                revealGroup.interactable = false;
            }
        }

        public void FlashError()
        {
            if (errorImage == null) return;
            if (errorRoutine != null) StopCoroutine(errorRoutine);
            errorRoutine = StartCoroutine(ErrorRoutine());
        }

        private IEnumerator ErrorRoutine()
        {
            float t = 0f;
            while (t < 0.1f)
            {
                t += Time.deltaTime;
                SetErrorAlpha(Mathf.Lerp(0f, 0.32f, t / 0.1f));
                yield return null;
            }

            yield return new WaitForSeconds(0.12f);

            t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                SetErrorAlpha(Mathf.Lerp(0.32f, 0f, t / 0.45f));
                yield return null;
            }

            SetErrorAlpha(0f);
            errorRoutine = null;
        }

        private void SetErrorAlpha(float alpha)
        {
            if (errorImage == null) return;
            Color c = errorImage.color;
            c.a = alpha;
            errorImage.color = c;
        }

        private void Update()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null) return;

            if (boostText != null)
            {
                bool onPad = player.CurrentTile != null;
                boostText.text = "CTRL · PLAY TILE";
                boostText.color = onPad
                    ? new Color(0.85f, 0.92f, 1f, 0.95f)
                    : new Color(0.63f, 0.72f, 0.9f, 0.82f);
            }
        }

        public void ShowNote(NoteTile tile)
        {
            if (tile == null || noteText == null) return;

            notesPlayed++;
            if (countText != null) countText.text = notesPlayed.ToString("N0");

            noteText.text = tile.NoteName;
            noteText.color = tile.noteColor;
            if (detailText != null)
            {
                string position = tile.isOpen ? "open string" : "fret " + tile.fret;
                string chord = ChordManager.Instance != null ? ChordManager.Instance.ActiveLabel : "";
                detailText.text = "string " + tile.StringLabel + "  ·  " + position + "  ·  " +
                                  tile.Frequency.ToString("F1") + " Hz" +
                                  (string.IsNullOrEmpty(chord) ? "" : "  ·  " + chord);
            }

            recent.Insert(0, tile);
            if (recent.Count > 5) recent.RemoveAt(recent.Count - 1);
            UpdateRecent();

            if (noteGroup != null)
            {
                if (noteRoutine != null) StopCoroutine(noteRoutine);
                noteRoutine = StartCoroutine(NoteRoutine());
            }
        }

        private void UpdateRecent()
        {
            if (recentTexts == null) return;
            for (int i = 0; i < recentTexts.Length; i++)
            {
                if (recentTexts[i] == null) continue;
                if (i < recent.Count)
                {
                    recentTexts[i].text = recent[i].NoteName;
                    recentTexts[i].color = recent[i].noteColor;
                }
                else
                {
                    recentTexts[i].text = "";
                }
            }
        }

        private IEnumerator NoteRoutine()
        {
            noteGroup.alpha = 1f;
            RectTransform rect = noteText.rectTransform;
            rect.localScale = Vector3.one * 1.12f;

            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, t / 0.18f);
                yield return null;
            }
            rect.localScale = Vector3.one;

            yield return new WaitForSeconds(1.35f);

            float fade = 0f;
            while (fade < 0.35f)
            {
                fade += Time.deltaTime;
                noteGroup.alpha = 1f - fade / 0.35f;
                yield return null;
            }
            noteGroup.alpha = 0f;
            noteRoutine = null;
        }

        public IEnumerator FadeOut(float duration)
        {
            yield return FadeTo(1f, duration);
        }

        public IEnumerator FadeIn(float duration)
        {
            yield return FadeTo(0f, duration);
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            if (fadeImage == null) yield break;
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(target, duration));
            yield return fadeRoutine;
        }

        private IEnumerator FadeRoutine(float target, float duration)
        {
            float start = fadeImage.color.a;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.deltaTime;
                Color c = fadeImage.color;
                c.a = Mathf.Lerp(start, target, t / duration);
                fadeImage.color = c;
                yield return null;
            }
            Color final = fadeImage.color;
            final.a = target;
            fadeImage.color = final;
            fadeRoutine = null;
        }
    }
}
