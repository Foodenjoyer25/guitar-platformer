using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuitarPlatformer
{
    public enum LevelState
    {
        MainMenu,
        ChallengeSelect,
        FreePlay,
        Playing,
        Replay,
        Reveal,
        Paused,
        OutOfLives,
        IntervalMenu
    }

    /// <summary>
    /// Runs the main menu, challenge select and the three-riff campaign: ordered target
    /// pads, manual chord validation, mistake teleport back to the last correct pad, a
    /// tempo-correct replay and the song reveal card.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public const int StartingLives = 3;

        public static LevelManager Instance { get; private set; }

        private RiffLevel[] levels;
        private bool[] completed;
        private PlayerController player;
        private Hud hud;
        private MenuView menu;
        private TargetBeacon beacon;
        private ChordManager chords;
        private NoteTile[] tiles;

        private LevelState state = LevelState.MainMenu;
        private Difficulty difficulty = Difficulty.Medium;
        private int levelIndex;
        private int stepIndex;
        private int mistakes;
        private int lives = StartingLives;
        private int mainSelection;
        private int challengeSelection;
        private int pauseSelection;
        private bool campaignActive;
        private bool savedLandingOnly;
        private NoteTile targetTile;
        private NoteTile lastCorrectTile;
        private Coroutine replayRoutine;
        private Coroutine outOfLivesRoutine;
        private bool replayUsesDsp;
        private double replayStart;
        private LevelState preIntervalState = LevelState.Playing;
        private int intervalSelection;

        public LevelState State => state;
        public Difficulty CurrentDifficulty => difficulty;
        public int CurrentLevelIndex => levelIndex;
        public int CurrentStepIndex => stepIndex;
        public int StepCount => CurrentLevel != null ? CurrentLevel.Steps.Length : 0;
        public int ReplayEventCount => CurrentLevel != null ? RiffPerformance.EventCount(CurrentLevel) : 0;
        public int MistakeCount => mistakes;
        public int Lives => lives;
        public int MainMenuSelection => mainSelection;
        public int ChallengeMenuSelection => challengeSelection;
        public int PauseMenuSelection => pauseSelection;
        public int IntervalMenuSelection => intervalSelection;
        public bool CampaignActive => campaignActive;
        public NoteTile TargetTile => targetTile;
        public bool AllowChordInput => state == LevelState.FreePlay || state == LevelState.Playing || state == LevelState.IntervalMenu;

        public RiffLevel CurrentLevel =>
            levels != null && levelIndex >= 0 && levelIndex < levels.Length ? levels[levelIndex] : null;

        public RiffStep CurrentStep
        {
            get
            {
                RiffLevel level = CurrentLevel;
                if (level == null || level.Steps == null || stepIndex < 0 || stepIndex >= level.Steps.Length)
                    return null;
                return level.Steps[stepIndex];
            }
        }

        public NoteTile StartPadTile
        {
            get
            {
                RiffLevel level = CurrentLevel;
                return level != null ? FindTile(level.StartString, level.StartFret) : null;
            }
        }

        public bool IsChallengeCompleted(int index)
        {
            return completed != null && index >= 0 && index < completed.Length && completed[index];
        }

        public bool IsChallengeUnlocked(int index)
        {
            return levels != null && index >= 0 && index < levels.Length;
        }

        /// <summary>The name shown in menus. Unfinished challenges stay hidden.</summary>
        public string ChallengeDisplayName(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length) return "???";
            return IsChallengeCompleted(index) ? levels[index].Title : "???";
        }

        private void Awake()
        {
            Instance = this;
            levels = RiffLibrary.CreateLevels();
            completed = new bool[levels.Length];
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (chords != null) chords.ActiveChanged -= OnActiveChanged;
        }

        private void Start()
        {
            player = PlayerController.Instance;
            if (player == null) player = Object.FindAnyObjectByType<PlayerController>();
            hud = Hud.Instance;
            if (hud == null) hud = Object.FindAnyObjectByType<Hud>();
            menu = MenuView.Instance;
            if (menu == null) menu = Object.FindAnyObjectByType<MenuView>();
            chords = ChordManager.Instance;
            if (chords == null) chords = Object.FindAnyObjectByType<ChordManager>();
            beacon = TargetBeacon.Instance;
            if (beacon == null) beacon = Object.FindAnyObjectByType<TargetBeacon>();
            tiles = Object.FindObjectsByType<NoteTile>();

            if (chords != null) chords.ActiveChanged += OnActiveChanged;
            ShowMainMenu();
            TryStartPendingCustomLevel();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            if (state == LevelState.Playing)
            {
                // Campaign levels use walk-on note triggers. Free play keeps the player's
                // L choice, so re-assert walk-on in case a stale scene value or an external
                // toggle tries to switch a live level to landing-only.
                if (player != null && player.LandingOnlyNotes)
                    player.SetLandingOnly(false);

                if (IntervalPressed(keyboard, gamepad))
                {
                    OpenIntervalMenu();
                    return;
                }

                if (PausePressed(keyboard, gamepad))
                    TogglePause();
                return;
            }

            switch (state)
            {
                case LevelState.FreePlay:
                    if (IntervalPressed(keyboard, gamepad))
                    {
                        OpenIntervalMenu();
                        return;
                    }

                    if (MenuPressed(keyboard, gamepad))
                    {
                        if (player != null) savedLandingOnly = player.LandingOnlyNotes;
                        ShowMainMenu();
                        return;
                    }

                    if (keyboard != null && keyboard.lKey.wasPressedThisFrame && player != null)
                    {
                        player.ToggleNoteMode();
                        UpdateNoteModeHud();
                    }
                    break;

                case LevelState.MainMenu:
                    HandleMainMenu(keyboard, gamepad);
                    break;

                case LevelState.ChallengeSelect:
                    if (IntervalPressed(keyboard, gamepad))
                    {
                        OpenIntervalMenu();
                        return;
                    }
                    HandleChallengeSelect(keyboard, gamepad);
                    break;

                case LevelState.Paused:
                    HandlePause(keyboard, gamepad);
                    break;

                case LevelState.Reveal:
                    if (ConfirmPressed(keyboard, gamepad))
                        AdvanceFromReveal();
                    break;

                case LevelState.IntervalMenu:
                    HandleIntervalMenu(keyboard, gamepad);
                    break;
            }
        }

        private void HandleMainMenu(Keyboard keyboard, Gamepad gamepad)
        {
            if (IntervalPressed(keyboard, gamepad))
            {
                OpenIntervalMenu();
                return;
            }

            int direction = NavigationDirection(keyboard, gamepad);
            if (direction != 0)
            {
                mainSelection = Wrap(mainSelection + direction, 3);
                if (menu != null) menu.ShowMainMenu(mainSelection);
            }

            if (ConfirmPressed(keyboard, gamepad))
                ActivateMenu(MenuScreen.Main, mainSelection);
        }

        private void HandleChallengeSelect(Keyboard keyboard, Gamepad gamepad)
        {
            int total = levels != null ? levels.Length + 1 : 1;
            int direction = NavigationDirection(keyboard, gamepad);
            if (direction != 0)
            {
                challengeSelection = Wrap(challengeSelection + direction, total);
                RefreshChallengeMenu();
            }

            int difficultyDirection = HorizontalDirection(keyboard, gamepad);
            if (difficultyDirection != 0)
                CycleDifficulty(difficultyDirection);

            if (ConfirmPressed(keyboard, gamepad))
            {
                if (challengeSelection >= (levels != null ? levels.Length : 0)) ShowMainMenu();
                else StartChallenge(challengeSelection);
            }
            else if (BackPressed(keyboard, gamepad))
            {
                ShowMainMenu();
            }
        }

        private void HandlePause(Keyboard keyboard, Gamepad gamepad)
        {
            if (PausePressed(keyboard, gamepad))
            {
                ClosePause();
                return;
            }

            if (IntervalPressed(keyboard, gamepad))
            {
                OpenIntervalMenu();
                return;
            }

            int direction = NavigationDirection(keyboard, gamepad);
            if (direction != 0)
            {
                pauseSelection = Wrap(pauseSelection + direction, 4);
                if (menu != null) menu.ShowPause(pauseSelection);
            }

            if (ConfirmPressed(keyboard, gamepad))
            {
                ActivateMenu(MenuScreen.Pause, pauseSelection);
            }
            else if (BackPressed(keyboard, gamepad))
            {
                ClosePause();
            }
        }

        /// <summary>Mouse bridge: hovering a row moves the keyboard/gamepad selection.</summary>
        public void HoverMenu(MenuScreen screen, int index)
        {
            switch (screen)
            {
                case MenuScreen.Main:
                    if (state != LevelState.MainMenu) return;
                    mainSelection = Mathf.Clamp(index, 0, 2);
                    if (menu != null) menu.ShowMainMenu(mainSelection);
                    break;

                case MenuScreen.Challenge:
                    if (state != LevelState.ChallengeSelect) return;
                    challengeSelection = Mathf.Clamp(index, 0, levels != null ? levels.Length : 0);
                    RefreshChallengeMenu();
                    break;

                case MenuScreen.Pause:
                    if (state != LevelState.Paused) return;
                    pauseSelection = Mathf.Clamp(index, 0, 3);
                    if (menu != null) menu.ShowPause(pauseSelection);
                    break;

                case MenuScreen.Interval:
                    if (state != LevelState.IntervalMenu) return;
                    intervalSelection = Mathf.Clamp(index, 0, ChordManager.Intervals.Length);
                    RefreshIntervalMenu();
                    break;
            }
        }

        /// <summary>Mouse bridge: activates the clicked row.</summary>
        public void ActivateMenu(MenuScreen screen, int index)
        {
            switch (screen)
            {
                case MenuScreen.Main:
                    if (state != LevelState.MainMenu) return;
                    if (index == 0) EnterFreePlay();
                    else if (index == 1) EnterChallengeSelect();
                    else if (index == 2) OpenIntervalMenu();
                    break;

                case MenuScreen.Challenge:
                    if (state != LevelState.ChallengeSelect) return;
                    if (index >= (levels != null ? levels.Length : 0)) ShowMainMenu();
                    else StartChallenge(index);
                    break;

                case MenuScreen.Difficulty:
                    if (state != LevelState.ChallengeSelect) return;
                    SetDifficulty((Difficulty)Mathf.Clamp(index, 0, 2));
                    break;

                case MenuScreen.Pause:
                    if (state != LevelState.Paused) return;
                    if (index == 0) ClosePause();
                    else if (index == 1) RestartChallenge();
                    else if (index == 2) EnterChallengeSelect();
                    else if (index == 3) OpenIntervalMenu();
                    break;

                case MenuScreen.Reveal:
                    if (state == LevelState.Reveal) AdvanceFromReveal();
                    break;

                case MenuScreen.Interval:
                    if (state != LevelState.IntervalMenu) return;
                    int closeIdx = ChordManager.Intervals.Length;
                    if (index == closeIdx) CloseIntervalMenu();
                    else if (index >= 0 && index < ChordManager.Intervals.Length)
                        ToggleIntervalFromMenu(index);
                    break;
            }
        }

        public void SetDifficulty(Difficulty value)
        {
            difficulty = value;
            if (state == LevelState.ChallengeSelect) RefreshChallengeMenu();
        }

        public void CycleDifficulty(int direction)
        {
            int next = ((int)difficulty + direction) % 3;
            if (next < 0) next += 3;
            SetDifficulty((Difficulty)next);
        }

        /// <summary>Starts challenge 01. Kept as a test/debug hook.</summary>
        public void StartCampaign()
        {
            StartChallenge(0);
        }

        /// <summary>
        /// Plays a Level Creator riff immediately. If a campaign riff already uses the
        /// same title it is replaced; otherwise the riff is appended as a custom challenge.
        /// </summary>
        public void StartCustomLevel(RiffLevel custom)
        {
            if (custom == null || player == null) return;
            if (levels == null) levels = new RiffLevel[0];

            int found = -1;
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] != null && levels[i].Title == custom.Title)
                {
                    found = i;
                    break;
                }
            }

            if (found >= 0)
            {
                levels[found] = custom;
            }
            else
            {
                var next = new RiffLevel[levels.Length + 1];
                for (int i = 0; i < levels.Length; i++) next[i] = levels[i];
                found = levels.Length;
                next[found] = custom;
                levels = next;

                var nextCompleted = new bool[levels.Length];
                if (completed != null)
                {
                    int copy = completed.Length < nextCompleted.Length ? completed.Length : nextCompleted.Length;
                    for (int i = 0; i < copy; i++) nextCompleted[i] = completed[i];
                }
                completed = nextCompleted;
            }

            StartLevel(found);
        }

        private void TryStartPendingCustomLevel()
        {
            RiffLevel pending = CustomLevelSession.Consume();
#if UNITY_EDITOR
            if (pending == null)
            {
                const string key = "GuitarPlatformer.PendingLevelJson";
                string json = UnityEditor.SessionState.GetString(key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    UnityEditor.SessionState.EraseString(key);
                    RiffLevelData data = JsonUtility.FromJson<RiffLevelData>(json);
                    if (data != null) pending = data.ToLevel();
                }
            }
#endif
            if (pending != null) StartCustomLevel(pending);
        }

        /// <summary>Test and debug hook: jump straight to a level, ignoring unlock state.</summary>
        public void StartLevel(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length || player == null) return;
            StopReplay();
            if (menu != null) menu.HideAll();
            if (hud != null) hud.HideReveal();
            if (!campaignActive) savedLandingOnly = player.LandingOnlyNotes;
            campaignActive = true;
            levelIndex = index;
            challengeSelection = Mathf.Clamp(index, 0, Mathf.Max(0, levels.Length - 1));
            LoadLevel();
        }

        /// <summary>Starts an unlocked challenge from the challenge select screen.</summary>
        public void StartChallenge(int index)
        {
            if (!IsChallengeUnlocked(index) || player == null) return;
            StopReplay();
            if (menu != null) menu.HideAll();
            if (hud != null) hud.HideReveal();
            if (!campaignActive) savedLandingOnly = player.LandingOnlyNotes;
            campaignActive = true;
            levelIndex = index;
            challengeSelection = Mathf.Clamp(index, 0, Mathf.Max(0, levels.Length - 1));
            LoadLevel();
        }

        /// <summary>Test hook: stop the authored replay and show the song card immediately.</summary>
        public bool SkipReplay()
        {
            if (state != LevelState.Replay) return false;
            StopReplay();
            EnterReveal();
            return true;
        }

        /// <summary>Advances the current screen. Space calls this on the reveal card; tests may too.</summary>
        public void Advance()
        {
            switch (state)
            {
                case LevelState.Reveal:
                    AdvanceFromReveal();
                    break;
                case LevelState.MainMenu:
                    ActivateMenu(MenuScreen.Main, mainSelection);
                    break;
                case LevelState.ChallengeSelect:
                    ActivateMenu(MenuScreen.Challenge, challengeSelection);
                    break;
                case LevelState.FreePlay:
                    ShowMainMenu();
                    break;
            }
        }

        public void OpenIntervalMenu()
        {
            if (state == LevelState.IntervalMenu || state == LevelState.Replay || state == LevelState.OutOfLives || state == LevelState.Reveal) return;
            preIntervalState = state;
            state = LevelState.IntervalMenu;
            intervalSelection = 0;
            if (player != null) player.SetInputLocked(true);
            RefreshIntervalMenu();
        }

        public void CloseIntervalMenu()
        {
            if (state != LevelState.IntervalMenu) return;
            state = preIntervalState;
            if (menu != null) menu.HideAll();
            if (player != null && (state == LevelState.Playing || state == LevelState.FreePlay))
                player.SetInputLocked(false);
            if (state == LevelState.Paused)
            {
                if (menu != null) menu.ShowPause(pauseSelection);
            }
            else if (state == LevelState.Playing)
            {
                UpdateHud();
            }
            else if (state == LevelState.ChallengeSelect)
            {
                RefreshChallengeMenu();
            }
            else if (state == LevelState.MainMenu)
            {
                if (menu != null) menu.ShowMainMenu(mainSelection);
            }
        }

        public void RefreshIntervalMenu()
        {
            if (menu == null || chords == null) return;
            var intervals = ChordManager.Intervals;
            var active = new bool[intervals.Length];
            for (int i = 0; i < intervals.Length; i++)
                active[i] = chords.IsActive(intervals[i].semitones);

            int count = chords.ActiveCount;
            string summary = "";
            if (count > 0)
            {
                string activeLabel = count <= 6 ? chords.ActiveLabel : count + " INTERVALS";
                summary = activeLabel + "  \u00b7  " + ChordTheory.Describe(chords.ActiveSemitones);
            }

            menu.ShowIntervalMenu(intervalSelection, active, summary);
        }

        public void ToggleIntervalFromMenu(int index)
        {
            if (chords == null || index < 0 || index >= ChordManager.Intervals.Length) return;
            chords.Toggle(ChordManager.Intervals[index].semitones);
            RefreshIntervalMenu();
        }

        private void HandleIntervalMenu(Keyboard keyboard, Gamepad gamepad)
        {
            if (IntervalPressed(keyboard, gamepad) || BackPressed(keyboard, gamepad))
            {
                CloseIntervalMenu();
                return;
            }

            int count = ChordManager.Intervals.Length;
            int totalRows = count + 1;
            int dir = NavigationDirection(keyboard, gamepad);
            if (dir != 0)
            {
                intervalSelection = Wrap(intervalSelection + dir, totalRows);
                RefreshIntervalMenu();
            }

            int hDir = HorizontalDirection(keyboard, gamepad);
            if (hDir != 0)
            {
                int simpleCount = ChordManager.SimpleIntervalCount;
                int compoundCount = count - simpleCount;
                if (intervalSelection < simpleCount && hDir > 0)
                {
                    int row = Mathf.Min(intervalSelection, compoundCount - 1);
                    intervalSelection = simpleCount + row;
                    RefreshIntervalMenu();
                }
                else if (intervalSelection >= simpleCount && intervalSelection < count && hDir < 0)
                {
                    int row = intervalSelection - simpleCount;
                    intervalSelection = Mathf.Min(row, simpleCount - 1);
                    RefreshIntervalMenu();
                }
            }

            if (ConfirmPressed(keyboard, gamepad))
            {
                if (intervalSelection == count)
                    CloseIntervalMenu();
                else if (intervalSelection >= 0 && intervalSelection < count)
                    ToggleIntervalFromMenu(intervalSelection);
            }
        }

        public void TogglePause()
        {
            if (state == LevelState.Playing) OpenPause();
            else if (state == LevelState.Paused) ClosePause();
        }

        private void OpenPause()
        {
            if (state != LevelState.Playing) return;
            state = LevelState.Paused;
            pauseSelection = 0;
            if (player != null) player.SetInputLocked(true);
            if (menu != null) menu.ShowPause(pauseSelection);
        }

        private void ClosePause()
        {
            if (state != LevelState.Paused) return;
            state = LevelState.Playing;
            if (menu != null) menu.HideAll();
            if (player != null) player.SetInputLocked(false);
            UpdateHud();
        }

        private void RestartChallenge()
        {
            if (menu != null) menu.HideAll();
            LoadLevel();
        }

        /// <summary>
        /// Easy disables wrong pads: only the authored target pitch or a safe pad may
        /// trigger a note. Medium and Hard keep the original free pad triggering so a
        /// wrong note can cost a life.
        /// </summary>
        public bool CanTriggerNote(NoteTile tile)
        {
            if (state != LevelState.Playing || difficulty != Difficulty.Easy || tile == null) return true;
            if (targetTile != null && tile.midi == targetTile.midi) return true;
            return IsSafePad(tile);
        }

        /// <summary>Called by NoteTile.Play after the note and manual chord have sounded.</summary>
        public void OnNotePlayed(NoteTile tile)
        {
            if (state != LevelState.Playing || tile == null) return;
            if (difficulty == Difficulty.Easy && !CanTriggerNote(tile)) return;

            // Any pad holding the target pitch counts, even on another string. The
            // authored pad still drives the beacon and HUD, so the equivalence is not
            // advertised; this only stops a valid pitch from registering as a mistake.
            if (targetTile != null && tile.midi == targetTile.midi)
            {
                if (ChordMatches())
                {
                    lastCorrectTile = tile;
                    AdvanceStep();
                }
                else
                    Mistake();
                return;
            }

            // The start pad and the pad that completed the previous step are always safe.
            if (IsSafePad(tile)) return;
            Mistake();
        }

        private bool ChordMatches()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return false;

            // A chord-free step accepts any held chord, so the player can keep P5
            // selected through the single-note open E without it counting as a mistake.
            RiffStep step = CurrentStep;
            if (step != null && step.IsChordFree) return true;

            return chords != null && chords.MatchesExactly(level.ChordIntervalsFor(step));
        }

        /// <summary>
        /// True when the manual chord voice should sound for this pad. Chord-free steps
        /// suppress the held voicing on their target pitch so they ring as single notes.
        /// </summary>
        public bool ShouldPlayChord(NoteTile tile)
        {
            if (state != LevelState.Playing || tile == null || targetTile == null) return true;

            RiffStep step = CurrentStep;
            if (step == null || !step.IsChordFree) return true;

            return tile.midi != targetTile.midi;
        }

        private bool IsSafePad(NoteTile tile)
        {
            RiffLevel level = CurrentLevel;
            if (level == null || tile == null) return false;

            if (tile.stringIndex == level.StartString && tile.fret == level.StartFret) return true;
            return tile == lastCorrectTile;
        }

        private void AdvanceStep()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return;

            stepIndex++;
            if (stepIndex >= level.Steps.Length)
            {
                BeginReplay();
                return;
            }

            RiffStep step = level.Steps[stepIndex];
            targetTile = FindTile(step.StringIndex, step.Fret);
            ShowBeaconTarget();
            UpdateHud();
        }

        private void Mistake()
        {
            mistakes++;
            if (hud != null) hud.FlashError();
            if (ChiptuneAudio.Instance != null && player != null)
                ChiptuneAudio.Instance.PlayError(player.transform.position);
            if (OrbitFollowCamera.Instance != null)
                OrbitFollowCamera.Instance.Shake(0.18f, 0.075f, 0.9f);

            if (campaignActive && difficulty != Difficulty.Easy)
            {
                lives = Mathf.Max(0, lives - 1);
                UpdateRunStatus();
                if (lives <= 0)
                {
                    BeginOutOfLives();
                    return;
                }
            }

            NoteTile safe = PreviousCorrectTile();
            if (safe != null && player != null)
                player.TeleportTo(PadTop(safe));
            if (player != null) player.StunMovement(0.5f);
        }

        private void BeginOutOfLives()
        {
            state = LevelState.OutOfLives;
            if (player != null) player.SetInputLocked(true);
            if (beacon != null) beacon.Clear();
            if (hud != null)
            {
                hud.SetStep("OUT OF LIVES");
                hud.SetTarget("RESTARTING CHALLENGE");
                hud.SetRunStatus("LIVES 0 / " + StartingLives, new Color(1f, 0.35f, 0.45f, 1f));
                hud.SetLevelVisible(false);
            }
            if (menu != null) menu.ShowOutOfLives();

            if (outOfLivesRoutine != null) StopCoroutine(outOfLivesRoutine);
            outOfLivesRoutine = StartCoroutine(OutOfLivesRoutine());
        }

        private IEnumerator OutOfLivesRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            outOfLivesRoutine = null;
            if (state != LevelState.OutOfLives) yield break;

            if (menu != null) menu.HideAll();
            if (hud != null) hud.SetLevelVisible(true);
            LoadLevel();
        }

        private NoteTile PreviousCorrectTile()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return null;
            if (lastCorrectTile != null) return lastCorrectTile;
            return FindTile(level.StartString, level.StartFret);
        }

        private void BeginReplay()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return;

            state = LevelState.Replay;
            if (player != null) player.SetInputLocked(true);
            if (beacon != null) beacon.Clear();
            if (hud != null)
            {
                hud.SetStep("REPLAY  ·  CHALLENGE " + (levelIndex + 1).ToString("00"));
                hud.SetTarget("RIFF COMPLETE  ·  " + level.Bpm + " BPM");
            }

            // Render once, then schedule the whole phrase on the audio clock. The
            // replay visuals read the same event grid below, so pad flashes and HUD
            // text follow the rendered audio instead of frame time. Preparing the clip
            // first keeps the render cost out of the schedule, which matters for the
            // 40-event Smells Like Teen Spirit replay.
            if (ChiptuneAudio.Instance != null)
                ChiptuneAudio.Instance.PreparePerformance(level);

            replayUsesDsp = AudioSettings.dspTime > 0.0;
            replayStart = (replayUsesDsp ? AudioSettings.dspTime : Time.timeAsDouble) + 0.3;
            if (ChiptuneAudio.Instance != null)
                ChiptuneAudio.Instance.PlayPerformance(level, replayStart);

            replayRoutine = StartCoroutine(ReplayRoutine(level, replayStart));
        }

        private IEnumerator ReplayRoutine(RiffLevel level, double start)
        {
            float secondsPerBeat = 60f / Mathf.Max(1, level.Bpm);
            RiffPerformanceNote[] notes = RiffPerformance.Build(level);
            int total = notes.Length;

            for (int i = 0; i < total; i++)
            {
                RiffPerformanceNote note = notes[i];
                double due = start + note.StartBeat * secondsPerBeat;
                while (ReplayNow < due) yield return null;

                NoteTile tile = FindTile(note.StringIndex, note.Fret);
                if (tile != null)
                    tile.PlayReplayVisual(note.Muted);

                if (hud != null)
                {
                    string position = tile != null
                        ? (tile.isOpen ? "OPEN" : "FRET " + tile.fret)
                        : (note.Fret == 0 ? "OPEN" : "FRET " + note.Fret);
                    hud.SetStep("REPLAY  ·  " + (i + 1) + " / " + total);
                    hud.SetTarget("REPLAY  ·  " + note.NoteName + "  ·  STRING " + note.StringLabel +
                                  "  ·  " + position + (note.Muted ? "  ·  PALM MUTE" : ""));
                }
            }

            double end = start + RiffPerformance.TotalBeats(level) * secondsPerBeat + 0.75;
            while (ReplayNow < end) yield return null;

            replayRoutine = null;
            EnterReveal();
        }

        private double ReplayNow => replayUsesDsp ? AudioSettings.dspTime : Time.timeAsDouble;

        private void EnterReveal()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return;

            state = LevelState.Reveal;
            completed[levelIndex] = true;
            if (beacon != null) beacon.Clear();
            if (hud != null)
            {
                hud.SetStep("RIFF COMPLETE");
                hud.SetTarget(level.Title.ToUpperInvariant() + "  ·  " + level.Artist);
                hud.ShowReveal(level.Title, level.Artist,
                    "SPACE / ENTER / A  ·  CHALLENGE MENU");
            }
        }

        private void AdvanceFromReveal()
        {
            if (state != LevelState.Reveal) return;
            if (hud != null) hud.HideReveal();
            challengeSelection = Mathf.Clamp(levelIndex + 1, 0, levels.Length - 1);
            EnterChallengeSelect();
        }

        private void LoadLevel()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return;

            StopReplay();
            if (outOfLivesRoutine != null)
            {
                StopCoroutine(outOfLivesRoutine);
                outOfLivesRoutine = null;
            }

            state = LevelState.Playing;
            stepIndex = 0;
            mistakes = 0;
            lives = StartingLives;

            NoteTile start = FindTile(level.StartString, level.StartFret);
            lastCorrectTile = start;
            targetTile = FindTile(level.Steps[0].StringIndex, level.Steps[0].Fret);

            if (player != null)
            {
                player.SetInputLocked(false);
                // Riff levels are walk-on: stepping onto a pad plays it.
                player.SetLandingOnly(false);
            }
            if (chords != null) chords.Clear();
            if (menu != null) menu.HideAll();

            if (start != null && player != null)
                player.TeleportTo(PadTop(start));

            ShowBeaconTarget();
            if (hud != null)
            {
                hud.HideReveal();
                hud.SetLevelVisible(true);
            }
            UpdateHud();
        }

        private void EnterFreePlay()
        {
            StopReplay();
            if (outOfLivesRoutine != null)
            {
                StopCoroutine(outOfLivesRoutine);
                outOfLivesRoutine = null;
            }

            state = LevelState.FreePlay;
            campaignActive = false;
            levelIndex = 0;
            stepIndex = 0;
            mistakes = 0;
            lives = StartingLives;
            targetTile = null;
            lastCorrectTile = null;

            if (menu != null) menu.HideAll();
            if (hud != null) hud.HideReveal();
            if (chords != null) chords.Clear();
            if (beacon != null) beacon.Clear();

            if (player != null)
            {
                player.SetInputLocked(false);
                player.SetLandingOnly(savedLandingOnly);
                player.TeleportTo(FreePlaySpawn());
            }

            ShowFreePlay();
        }

        private void ShowMainMenu()
        {
            StopReplay();
            if (outOfLivesRoutine != null)
            {
                StopCoroutine(outOfLivesRoutine);
                outOfLivesRoutine = null;
            }

            if (state == LevelState.FreePlay && player != null)
                savedLandingOnly = player.LandingOnlyNotes;

            state = LevelState.MainMenu;
            campaignActive = false;
            levelIndex = 0;
            stepIndex = 0;
            targetTile = null;
            lastCorrectTile = null;
            mainSelection = 0;

            if (player != null) player.SetInputLocked(true);
            if (chords != null) chords.Clear();
            if (beacon != null) beacon.Clear();
            if (hud != null)
            {
                hud.HideReveal();
                hud.SetLevelVisible(false);
                hud.SetRunStatus("", Color.white);
            }
            if (menu != null)
            {
                menu.HideAll();
                menu.ShowMainMenu(mainSelection);
            }
        }

        private void EnterChallengeSelect()
        {
            StopReplay();
            if (outOfLivesRoutine != null)
            {
                StopCoroutine(outOfLivesRoutine);
                outOfLivesRoutine = null;
            }

            state = LevelState.ChallengeSelect;
            campaignActive = false;
            stepIndex = 0;
            targetTile = null;
            lastCorrectTile = null;
            challengeSelection = Mathf.Clamp(challengeSelection, 0, Mathf.Max(0, levels.Length - 1));

            if (player != null) player.SetInputLocked(true);
            if (chords != null) chords.Clear();
            if (beacon != null) beacon.Clear();
            if (hud != null)
            {
                hud.HideReveal();
                hud.SetLevelVisible(false);
                hud.SetRunStatus("", Color.white);
            }
            RefreshChallengeMenu();
        }

        private void RefreshChallengeMenu()
        {
            if (menu == null || levels == null) return;

            int count = levels.Length;
            var labels = new string[count];
            var locked = new bool[count];
            var cleared = new bool[count];

            for (int i = 0; i < count; i++)
            {
                bool unlocked = IsChallengeUnlocked(i);
                bool done = IsChallengeCompleted(i);
                locked[i] = !unlocked;
                cleared[i] = done;

                string prefix = "CHALLENGE " + (i + 1).ToString("00");
                if (!unlocked) labels[i] = prefix + "  ·  LOCKED";
                else if (!done) labels[i] = prefix + "  ·  ???";
                else labels[i] = prefix + "  ·  " + levels[i].Title.ToUpperInvariant();
            }

            menu.ShowChallengeSelect(labels, locked, cleared, challengeSelection, difficulty,
                ChallengeStatus(challengeSelection));
        }

        private string ChallengeStatus(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length)
                return "DIFFICULTY APPLIES TO CHALLENGES ONLY";

            if (!IsChallengeUnlocked(index))
                return "LOCKED  ·  CLEAR CHALLENGE " + index.ToString("00") + " FIRST";

            RiffLevel level = levels[index];
            if (!IsChallengeCompleted(index))
                return "UNKNOWN RIFF  ·  " + level.Steps.Length + " NOTES  ·  " + level.Bpm + " BPM";

            return "CLEARED  ·  " + level.Title.ToUpperInvariant() + "  ·  " +
                   level.Artist.ToUpperInvariant();
        }

        private void ShowFreePlay()
        {
            if (hud != null)
            {
                hud.SetLevelVisible(true);
                hud.SetLevelHeader("FREE PLAY");
                hud.SetStep("");
                hud.SetTarget("PLAY ANY PAD", new Color(0.3f, 0.95f, 1f, 1f));
                hud.SetChordRequirement("", true);
                hud.SetRunStatus("", Color.white);
            }
            UpdateNoteModeHud();
        }

        private void UpdateHud()
        {
            RiffLevel level = CurrentLevel;
            if (hud == null || level == null) return;

            string name = IsChallengeCompleted(levelIndex) ? level.Title.ToUpperInvariant() : "???";
            hud.SetLevelVisible(true);
            hud.SetLevelHeader("CHALLENGE " + (levelIndex + 1).ToString("00") + "  ·  " + name);
            hud.SetStep("STEP " + (stepIndex + 1) + " / " + level.Steps.Length);

            if (targetTile != null)
            {
                string place = targetTile.isOpen ? "OPEN" : "FRET " + targetTile.fret;
                hud.SetTarget(targetTile.NoteName + "\nSTRING " + targetTile.StringLabel + "  ·  " + place,
                    targetTile.noteColor);
            }
            else
            {
                hud.SetTarget("?", Color.white);
            }

            UpdateChordRequirement();
            UpdateNoteModeHud();
            UpdateRunStatus();
        }

        private void UpdateRunStatus()
        {
            if (hud == null) return;

            if (!campaignActive || state == LevelState.FreePlay || state == LevelState.MainMenu ||
                state == LevelState.ChallengeSelect)
            {
                hud.SetRunStatus("", Color.white);
                return;
            }

            if (difficulty == Difficulty.Easy)
            {
                hud.SetRunStatus("EASY  ·  WRONG NOTES DISABLED", new Color(0.3f, 0.95f, 1f, 1f));
                return;
            }

            Color color = lives <= 1
                ? new Color(1f, 0.35f, 0.45f, 1f)
                : new Color(1f, 0.7f, 0.3f, 1f);
            hud.SetRunStatus(difficulty.ToString().ToUpperInvariant() + "  ·  LIVES " + lives +
                             " / " + StartingLives, color);
        }

        private void UpdateChordRequirement()
        {
            RiffLevel level = CurrentLevel;
            if (hud == null || level == null) return;

            RiffStep step = CurrentStep;
            if (step != null && step.IsChordFree)
            {
                hud.SetChordRequirement("NONE", true);
                return;
            }

            int[] required = level.ChordIntervalsFor(step);
            if (required == null || required.Length == 0)
            {
                hud.SetChordRequirement("NONE", true);
                return;
            }

            var label = new StringBuilder();
            for (int i = 0; i < required.Length; i++)
            {
                if (label.Length > 0) label.Append(" + ");
                label.Append(ChordManager.LabelFor(required[i]));
            }
            if (!string.IsNullOrEmpty(level.ChordKeyHint))
                label.Append("  ·  KEY ").Append(level.ChordKeyHint);

            bool ready = chords != null && chords.MatchesExactly(required);
            hud.SetChordRequirement(label.ToString(), ready);
        }

        private void UpdateNoteModeHud()
        {
            if (hud == null) return;
            bool landingOnly = player != null && player.LandingOnlyNotes;
            hud.SetNoteMode(landingOnly ? "LANDING ONLY" : "WALK-ON");
        }

        private void OnActiveChanged()
        {
            if (state == LevelState.Playing) UpdateChordRequirement();
            else if (state == LevelState.IntervalMenu) RefreshIntervalMenu();
        }

        private void ShowBeaconTarget()
        {
            if (beacon == null) return;
            if (difficulty == Difficulty.Hard) beacon.Clear();
            else beacon.SetTarget(targetTile);
        }

        private void StopReplay()
        {
            if (ChiptuneAudio.Instance != null) ChiptuneAudio.Instance.StopPerformance();
            if (replayRoutine == null) return;
            StopCoroutine(replayRoutine);
            replayRoutine = null;
        }

        private NoteTile FindTile(int stringIndex, int fret)
        {
            if (tiles == null) return null;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] != null && tiles[i].stringIndex == stringIndex && tiles[i].fret == fret)
                    return tiles[i];
            }
            return null;
        }

        private static Vector3 PadTop(NoteTile tile)
        {
            return new Vector3(tile.transform.position.x, FretboardLayout.PadHeight + 0.06f,
                tile.transform.position.z);
        }

        private static Vector3 FreePlaySpawn()
        {
            return new Vector3(FretboardLayout.LaneX(2), FretboardLayout.PadHeight + 0.06f,
                FretboardLayout.OpenPadCenterZ);
        }

        private static int Wrap(int value, int count)
        {
            if (count <= 0) return 0;
            value %= count;
            return value < 0 ? value + count : value;
        }

        private static bool IntervalPressed(Keyboard keyboard, Gamepad gamepad)
        {
            return (keyboard != null &&
                    (keyboard.iKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame)) ||
                   (gamepad != null && gamepad.buttonWest.wasPressedThisFrame);
        }

        private static bool PausePressed(Keyboard keyboard, Gamepad gamepad)
        {
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.startButton.wasPressedThisFrame);
        }

        private static bool MenuPressed(Keyboard keyboard, Gamepad gamepad)
        {
            return PausePressed(keyboard, gamepad) ||
                   (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                         keyboard.numpadEnterKey.wasPressedThisFrame));
        }

        private static bool ConfirmPressed(Keyboard keyboard, Gamepad gamepad)
        {
            return (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                         keyboard.numpadEnterKey.wasPressedThisFrame ||
                                         keyboard.spaceKey.wasPressedThisFrame)) ||
                   (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
        }

        private static bool BackPressed(Keyboard keyboard, Gamepad gamepad)
        {
            return (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                   (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        private static int NavigationDirection(Keyboard keyboard, Gamepad gamepad)
        {
            if (keyboard != null)
            {
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) return -1;
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) return 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.up.wasPressedThisFrame || gamepad.leftStick.up.wasPressedThisFrame) return -1;
                if (gamepad.dpad.down.wasPressedThisFrame || gamepad.leftStick.down.wasPressedThisFrame) return 1;
            }
            return 0;
        }

        private static int HorizontalDirection(Keyboard keyboard, Gamepad gamepad)
        {
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) return -1;
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) return 1;
            }
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame) return -1;
                if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame) return 1;
            }
            return 0;
        }
    }
}
