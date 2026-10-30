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

        private void Awake()
        {
            Instance = this;
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
            SetGroup(challengeGroup, true);

            if (challengeRows != null)
            {
                for (int i = 0; i < challengeRows.Length; i++)
                {
                    bool isLocked = locked != null && i < locked.Length && locked[i];
                    bool isCompleted = completed != null && i < completed.Length && completed[i];
                    string label = labels != null && i < labels.Length ? labels[i] : "";
                    SetChallengeRow(challengeRows[i], label, isLocked, isCompleted, selected == i);
                }
            }

            int backIdx = challengeRows != null ? challengeRows.Length : 3;
            SetRow(backRow, selected == backIdx, false, false, null);
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

        private void SetIntervalRow(MenuRow row, bool selected, bool active)
        {
            if (row == null) return;
            Color background = selected ? SelectedBg : active ? CompletedBg : NormalBg;
            Color foreground = selected ? SelectedText : active ? CompletedText : NormalText;

            if (row.background != null) row.background.color = background;
            if (row.label != null) row.label.color = foreground;
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

        private void SetRows(MenuRow[] rows, int selected)
        {
            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
                SetRow(rows[i], selected == i, false, false, null);
        }

        private void SetChallengeRow(MenuRow row, string label, bool locked, bool completed, bool selected)
        {
            SetRow(row, selected, locked, completed, label);
        }

        private void SetRow(MenuRow row, bool selected, bool locked, bool completed, string label)
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
    }
}
