using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GuitarPlatformer
{
    /// <summary>
    /// Renders the main menu, challenge select, pause overlay and out-of-lives overlay.
    /// Navigation and game state live in LevelManager.
    /// </summary>
    public class MenuView : MonoBehaviour
    {
        public static MenuView Instance { get; private set; }

        [System.Serializable]
        public class MenuRow
        {
            public Image background;
            public Text label;
        }

        [Header("Screens")]
        [SerializeField] private CanvasGroup mainGroup;
        [SerializeField] private CanvasGroup challengeGroup;
        [SerializeField] private CanvasGroup pauseGroup;
        [SerializeField] private CanvasGroup outOfLivesGroup;
        [SerializeField] private CanvasGroup intervalGroup;

        [Header("Rows")]
        [SerializeField] private MenuRow[] mainRows;
        [SerializeField] private MenuRow[] challengeRows;
        [SerializeField] private MenuRow backRow;
        [SerializeField] private MenuRow[] difficultyRows;
        [SerializeField] private MenuRow[] pauseRows;
        [SerializeField] private MenuRow[] intervalRows;
        [SerializeField] private MenuRow intervalBackRow;

        [Header("Challenge text")]
        [SerializeField] private Text challengeStatus;
        [SerializeField] private Text difficultyDescription;
        [SerializeField] private Text intervalChordSummary;

        private static readonly Color NormalBg = new Color(0.02f, 0.015f, 0.05f, 0.9f);
        private static readonly Color SelectedBg = new Color(0.08f, 0.5f, 0.65f, 0.96f);
        private static readonly Color LockedBg = new Color(0.012f, 0.01f, 0.025f, 0.72f);
        private static readonly Color CompletedBg = new Color(0.12f, 0.03f, 0.12f, 0.9f);
        private static readonly Color NormalText = new Color(0.78f, 0.84f, 1f, 0.92f);
        private static readonly Color SelectedText = Color.white;
        private static readonly Color LockedText = new Color(0.42f, 0.45f, 0.56f, 0.8f);
        private static readonly Color CompletedText = new Color(1f, 0.55f, 0.9f, 1f);
        private static readonly Color DifficultySelectedBg = new Color(0.3f, 0.95f, 1f, 0.95f);
        private static readonly Color DifficultySelectedText = new Color(0.01f, 0.03f, 0.08f, 1f);
        private static readonly Color DifficultyText = new Color(0.63f, 0.72f, 0.9f, 0.9f);
        private static readonly Color Cyan = new Color(0.3f, 0.95f, 1f);
        private static readonly Color Dim = new Color(0.63f, 0.72f, 0.9f, 0.82f);

        private const float ChallengeRowHeight = 58f;
        private const float ChallengeRowWidth = 780f;
        private const float ChallengeListHeight = 300f;

        private readonly List<MenuRow> runtimeChallengeRows = new List<MenuRow>();
        private RectTransform challengeList;
        private Font menuFont;
        private bool challengeShellBuilt;

        private void Awake()
        {
            Instance = this;
            RebuildChallengeScreen();
            HideAll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(CanvasGroup main, CanvasGroup challenge, CanvasGroup pause,
            CanvasGroup outOfLives, MenuRow[] mainMenuRows, MenuRow[] challengeMenuRows, MenuRow challengeBackRow,
            MenuRow[] difficultyMenuRows, MenuRow[] pauseMenuRows, Text status, Text difficultyHelp)
        {
            mainGroup = main;
            challengeGroup = challenge;
            pauseGroup = pause;
            outOfLivesGroup = outOfLives;
            mainRows = mainMenuRows;
            challengeRows = challengeMenuRows;
            backRow = challengeBackRow;
            difficultyRows = difficultyMenuRows;
            pauseRows = pauseMenuRows;
            challengeStatus = status;
            difficultyDescription = difficultyHelp;
            HideAll();
        }

        public void ShowMainMenu(int selected)
        {
            HideAll();
            SetGroup(mainGroup, true);
            SetRows(mainRows, selected);
        }

        public void ShowChallengeSelect(string[] labels, bool[] locked, bool[] completed, int selected,
            Difficulty difficulty, string status)
        {
            HideAll();
            if (!challengeShellBuilt) RebuildChallengeScreen();
            SetGroup(challengeGroup, true);

            int count = labels != null ? labels.Length : 0;
            EnsureChallengeRows(count);

            float offset = ScrollOffset(selected, count);
            for (int i = 0; i < runtimeChallengeRows.Count; i++)
            {
                MenuRow row = runtimeChallengeRows[i];
                bool visible = i < count;
                if (row != null && row.background != null)
                    row.background.gameObject.SetActive(visible);
                if (!visible) continue;

                PlaceChallengeRow(row, i, offset);
                BindButton(row, MenuScreen.Challenge, i);
                bool isLocked = locked != null && i < locked.Length && locked[i];
                bool isCompleted = completed != null && i < completed.Length && completed[i];
                string label = labels != null && i < labels.Length ? labels[i] : "";
                SetRow(row, selected == i, isLocked, isCompleted, label);
            }

            BindButton(backRow, MenuScreen.Challenge, count);
            SetRow(backRow, selected == count, false, false, "BACK");
            SetDifficultyRows(difficulty);
            if (challengeStatus != null) challengeStatus.text = status ?? "";
            if (difficultyDescription != null) difficultyDescription.text = DifficultyHelp(difficulty);
        }

        public void ConfigureIntervals(CanvasGroup group, MenuRow[] rows, MenuRow back, Text summary)
        {
            intervalGroup = group;
            intervalRows = rows;
            intervalBackRow = back;
            intervalChordSummary = summary;
        }

        public void ShowIntervalMenu(int selected, bool[] active, string summary)
        {
            HideAll();
            SetGroup(intervalGroup, true);

            if (intervalRows != null && active != null)
            {
                for (int i = 0; i < intervalRows.Length; i++)
                {
                    bool isActive = i < active.Length && active[i];
                    bool isSelected = selected == i;
                    SetIntervalRow(intervalRows[i], isSelected, isActive);
                }
            }

            int backIndex = intervalRows != null ? intervalRows.Length : 0;
            SetRow(intervalBackRow, selected == backIndex, false, false, null);

            if (intervalChordSummary != null)
                intervalChordSummary.text = string.IsNullOrEmpty(summary)
                    ? "ACTIVE: NONE  \u00b7  SINGLE NOTES"
                    : "ACTIVE: " + summary;
        }

        public void ShowPause(int selected)
        {
            HideAll();
            SetGroup(pauseGroup, true);
            SetRows(pauseRows, selected);
        }

        public void ShowOutOfLives()
        {
            HideAll();
            SetGroup(outOfLivesGroup, true);
        }

        public void HideAll()
        {
            SetGroup(mainGroup, false);
            SetGroup(challengeGroup, false);
            SetGroup(pauseGroup, false);
            SetGroup(outOfLivesGroup, false);
            SetGroup(intervalGroup, false);
        }

        public void SetDifficulty(Difficulty difficulty)
        {
            SetDifficultyRows(difficulty);
            if (difficultyDescription != null) difficultyDescription.text = DifficultyHelp(difficulty);
        }

        private void RebuildChallengeScreen()
        {
            if (challengeGroup == null) return;

            menuFont = ResolveFont();
            Transform root = challengeGroup.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                child.name = child.name + "_Old";
                child.SetActive(false);
                Destroy(child);
            }

            runtimeChallengeRows.Clear();

            Transform card = CreatePanel(root, "Card", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1040f, 900f), new Color(0.018f, 0.012f, 0.05f, 0.97f), false);

            CreateLabel(card, "ChallengeTitle", "CHALLENGES", 56, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(960f, 68f), Cyan);
            CreateLabel(card, "ChallengeSubtitle", "PICK A RIFF", 18, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(960f, 24f), Dim);

            Transform list = CreatePanel(card, "ChallengeList", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 70f), new Vector2(840f, ChallengeListHeight),
                new Color(0.01f, 0.01f, 0.03f, 0.35f), true);
            list.gameObject.AddComponent<RectMask2D>();
            challengeList = list as RectTransform;

            CreateLabel(card, "DifficultyTitle", "DIFFICULTY", 18, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -175f), new Vector2(760f, 24f), Dim);

            difficultyRows = new[]
            {
                CreateRow(card, "DifficultyRow_0", "EASY", new Vector2(-250f, -230f),
                    new Vector2(220f, 52f), MenuScreen.Difficulty, 0, 22),
                CreateRow(card, "DifficultyRow_1", "MEDIUM", new Vector2(0f, -230f),
                    new Vector2(220f, 52f), MenuScreen.Difficulty, 1, 22),
                CreateRow(card, "DifficultyRow_2", "HARD", new Vector2(250f, -230f),
                    new Vector2(220f, 52f), MenuScreen.Difficulty, 2, 22)
            };

            challengeStatus = CreateLabel(card, "ChallengeStatus", "", 22, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -290f), new Vector2(960f, 32f), Color.white);
            difficultyDescription = CreateLabel(card, "DifficultyDescription", "", 16, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -324f), new Vector2(980f, 28f), Dim);

            backRow = CreateRow(card, "ChallengeBack", "BACK", new Vector2(0f, -380f),
                new Vector2(280f, 52f), MenuScreen.Challenge, 0, 22);

            CreateLabel(card, "ChallengeHint",
                "W / S  ·  ENTER / A  ·  ESC / B  ·  MOUSE  ·  ← / → DIFFICULTY", 16,
                TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 28f),
                new Vector2(980f, 24f), Dim);

            challengeShellBuilt = true;
        }

        private void EnsureChallengeRows(int count)
        {
            if (challengeList == null || count <= 0) return;

            while (runtimeChallengeRows.Count < count)
            {
                int index = runtimeChallengeRows.Count;
                runtimeChallengeRows.Add(CreateRow(challengeList, "ChallengeRow_" + index,
                    "CHALLENGE " + (index + 1).ToString("00"), Vector2.zero,
                    new Vector2(ChallengeRowWidth, ChallengeRowHeight - 8f),
                    MenuScreen.Challenge, index, 22));
            }
        }

        private static float ScrollOffset(int selected, int count)
        {
            float content = count * ChallengeRowHeight;
            float max = Mathf.Max(0f, content - ChallengeListHeight);
            if (max <= 0.001f || selected < 0 || selected >= count) return 0f;
            float selectedTop = selected * ChallengeRowHeight;
            return Mathf.Clamp(selectedTop - (ChallengeListHeight - ChallengeRowHeight) * 0.5f, 0f, max);
        }

        private static void PlaceChallengeRow(MenuRow row, int index, float offset)
        {
            if (row == null || row.background == null) return;
            RectTransform rect = row.background.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(ChallengeRowWidth, ChallengeRowHeight - 8f);
            rect.anchoredPosition = new Vector2(0f, -index * ChallengeRowHeight + offset);
        }

        private static void BindButton(MenuRow row, MenuScreen screen, int index)
        {
            if (row == null || row.background == null) return;
            var button = row.background.GetComponent<MenuButton>();
            if (button == null) return;
            button.screen = screen;
            button.index = index;
        }

        private void SetRows(MenuRow[] rows, int selected)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
                SetRow(rows[i], selected == i, false, false, null);
        }

        private void SetIntervalRow(MenuRow row, bool selected, bool active)
        {
            if (row == null) return;
            Color background = selected ? SelectedBg : active ? CompletedBg : NormalBg;
            Color foreground = selected ? SelectedText : active ? CompletedText : NormalText;
            if (row.background != null) row.background.color = background;
            if (row.label != null) row.label.color = foreground;
        }

        private static void SetRow(MenuRow row, bool selected, bool locked, bool completed, string label)
        {
            if (row == null) return;

            Color background = selected ? SelectedBg : locked ? LockedBg : completed ? CompletedBg : NormalBg;
            Color foreground = selected ? SelectedText : locked ? LockedText : completed ? CompletedText : NormalText;

            if (row.background != null) row.background.color = background;
            if (row.label != null)
            {
                if (label != null) row.label.text = label;
                row.label.color = foreground;
            }
        }

        private void SetDifficultyRows(Difficulty difficulty)
        {
            if (difficultyRows == null) return;
            for (int i = 0; i < difficultyRows.Length; i++)
            {
                bool selected = i == (int)difficulty;
                MenuRow row = difficultyRows[i];
                if (row == null) continue;
                if (row.background != null)
                    row.background.color = selected ? DifficultySelectedBg : NormalBg;
                if (row.label != null)
                    row.label.color = selected ? DifficultySelectedText : DifficultyText;
            }
        }

        private static string DifficultyHelp(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return "WRONG NOTES ARE DISABLED  ·  CHORDS STILL MATTER  ·  NO LIVES";
                case Difficulty.Hard:
                    return "NO WORLD MARKER  ·  FOLLOW THE HUD  ·  3 LIVES";
                default:
                    return "STANDARD RIFF RULES  ·  3 LIVES  ·  ANY MISTAKE COSTS ONE";
            }
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private Font ResolveFont()
        {
            if (mainRows != null)
            {
                for (int i = 0; i < mainRows.Length; i++)
                {
                    if (mainRows[i] != null && mainRows[i].label != null && mainRows[i].label.font != null)
                        return mainRows[i].label.font;
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        private MenuRow CreateRow(Transform parent, string name, string label, Vector2 position,
            Vector2 size, MenuScreen screen, int index, int fontSize)
        {
            Transform panel = CreatePanel(parent, name, new Vector2(0.5f, 0.5f), position, size, NormalBg, true);
            var button = panel.gameObject.AddComponent<MenuButton>();
            button.screen = screen;
            button.index = index;
            Text text = CreateLabel(panel, "Label", label, fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, NormalText);
            return new MenuRow
            {
                background = panel.GetComponent<Image>(),
                label = text
            };
        }

        private static Transform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position,
            Vector2 size, Color color, bool raycast)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private Text CreateLabel(Transform parent, string name, string content, int fontSize,
            TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = PivotFor(alignment);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = menuFont;
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Vector2 PivotFor(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperCenter: return new Vector2(0.5f, 1f);
                case TextAnchor.LowerCenter: return new Vector2(0.5f, 0f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }
    }
}
