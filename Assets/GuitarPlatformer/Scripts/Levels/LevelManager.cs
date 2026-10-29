using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuitarPlatformer
{
    public enum LevelState
    {
        FreePlay,
        Playing,
        Replay,
        Reveal
    }

    /// <summary>
    /// Runs the three-riff campaign: ordered target pads, manual chord validation,
    /// mistake teleport back to the last correct pad, a tempo-correct replay and the
    /// song reveal card.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        private RiffLevel[] levels;
        private PlayerController player;
        private Hud hud;
        private TargetBeacon beacon;
        private ChordManager chords;
        private NoteTile[] tiles;

        private LevelState state = LevelState.FreePlay;
        private int levelIndex;
        private int stepIndex;
        private int mistakes;
        private bool campaignActive;
        private bool savedLandingOnly;
        private NoteTile targetTile;
        private NoteTile lastCorrectTile;
        private Coroutine replayRoutine;
        private bool replayUsesDsp;
        private double replayStart;

        public LevelState State => state;
        public int CurrentLevelIndex => levelIndex;
        public int CurrentStepIndex => stepIndex;
        public int StepCount => CurrentLevel != null ? CurrentLevel.Steps.Length : 0;
        public int ReplayEventCount => CurrentLevel != null ? RiffPerformance.EventCount(CurrentLevel) : 0;
        public int MistakeCount => mistakes;
        public bool CampaignActive => campaignActive;
        public NoteTile TargetTile => targetTile;
        public bool AllowChordInput => state == LevelState.FreePlay || state == LevelState.Playing;

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

        private void Awake()
        {
            Instance = this;
            levels = RiffLibrary.CreateLevels();
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
            chords = ChordManager.Instance;
            if (chords == null) chords = Object.FindAnyObjectByType<ChordManager>();
            beacon = TargetBeacon.Instance;
            if (beacon == null) beacon = Object.FindAnyObjectByType<TargetBeacon>();
            tiles = Object.FindObjectsByType<NoteTile>();

            if (chords != null) chords.ActiveChanged += OnActiveChanged;
            ShowFreePlay();
        }

        private void Update()
        {
            // Campaign levels use walk-on note triggers. Free play keeps the player's
            // L choice, so re-assert walk-on in case a stale scene value or an external
            // toggle tries to switch a live level to landing-only.
            if (state == LevelState.Playing && player != null && player.LandingOnlyNotes)
                player.SetLandingOnly(false);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (state == LevelState.FreePlay)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    StartCampaign();
                    return;
                }

                if (keyboard.lKey.wasPressedThisFrame && player != null)
                {
                    player.ToggleNoteMode();
                    UpdateNoteModeHud();
                }
                return;
            }

            if (state == LevelState.Reveal && keyboard.spaceKey.wasPressedThisFrame)
                AdvanceFromReveal();
        }

        /// <summary>Starts the campaign from free play. Enter calls this.</summary>
        public void StartCampaign()
        {
            if (state != LevelState.FreePlay || player == null) return;
            savedLandingOnly = player.LandingOnlyNotes;
            campaignActive = true;
            levelIndex = 0;
            LoadLevel();
        }

        /// <summary>Test and debug hook: jump straight to a level.</summary>
        public void StartLevel(int index)
        {
            if (levels == null || index < 0 || index >= levels.Length || player == null) return;
            StopReplay();
            if (hud != null) hud.HideReveal();
            if (!campaignActive) savedLandingOnly = player.LandingOnlyNotes;
            campaignActive = true;
            levelIndex = index;
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

        /// <summary>Advances the reveal card. Space calls this; tests may too.</summary>
        public void Advance()
        {
            if (state == LevelState.Reveal) AdvanceFromReveal();
            else if (state == LevelState.FreePlay) StartCampaign();
        }

        /// <summary>Called by NoteTile.Play after the note and manual chord have sounded.</summary>
        public void OnNotePlayed(NoteTile tile)
        {
            if (state != LevelState.Playing || tile == null) return;

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
            if (beacon != null) beacon.SetTarget(targetTile);
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

            NoteTile safe = PreviousCorrectTile();
            if (safe != null && player != null)
                player.TeleportTo(PadTop(safe));
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
                hud.SetStep("REPLAY  ·  " + level.Title.ToUpperInvariant());
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
            if (beacon != null) beacon.Clear();
            if (hud != null)
            {
                hud.SetStep("RIFF COMPLETE");
                hud.SetTarget(level.Title.ToUpperInvariant() + "  ·  " + level.Artist);
                hud.ShowReveal(level.Title, level.Artist, levelIndex == levels.Length - 1);
            }
        }

        private void AdvanceFromReveal()
        {
            if (state != LevelState.Reveal) return;
            if (hud != null) hud.HideReveal();

            if (levelIndex + 1 < levels.Length)
            {
                levelIndex++;
                LoadLevel();
            }
            else
            {
                ReturnToFreePlay();
            }
        }

        private void LoadLevel()
        {
            RiffLevel level = CurrentLevel;
            if (level == null) return;

            StopReplay();
            state = LevelState.Playing;
            stepIndex = 0;
            mistakes = 0;

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

            if (start != null && player != null)
                player.TeleportTo(PadTop(start));

            if (beacon != null) beacon.SetTarget(targetTile);
            if (hud != null)
            {
                hud.HideReveal();
                hud.SetLevelVisible(true);
            }
            UpdateHud();
        }

        private void ReturnToFreePlay()
        {
            StopReplay();
            state = LevelState.FreePlay;
            campaignActive = false;
            levelIndex = 0;
            stepIndex = 0;
            targetTile = null;
            lastCorrectTile = null;

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

        private void ShowFreePlay()
        {
            if (hud != null)
            {
                hud.SetLevelVisible(true);
                hud.SetLevelHeader("FREE PLAY  ·  ENTER STARTS THE RIFF CAMPAIGN");
                hud.SetStep("");
                hud.SetTarget("L TOGGLES WALK-ON / LANDING ONLY  ·  FREE PLAY");
                hud.SetChordRequirement("", true);
            }
            UpdateNoteModeHud();
        }

        private void UpdateHud()
        {
            RiffLevel level = CurrentLevel;
            if (hud == null || level == null) return;

            hud.SetLevelVisible(true);
            hud.SetLevelHeader("LEVEL " + (levelIndex + 1) + " / " + levels.Length +
                               "  ·  " + level.Title.ToUpperInvariant());
            hud.SetStep("STEP " + (stepIndex + 1) + " / " + level.Steps.Length);

            if (targetTile != null)
            {
                hud.SetTarget("TARGET  ·  " + targetTile.NoteName + "  ·  STRING " +
                              targetTile.StringLabel + "  ·  " +
                              (targetTile.isOpen ? "OPEN" : "FRET " + targetTile.fret));
            }
            else
            {
                hud.SetTarget("TARGET  ·  ?");
            }

            UpdateChordRequirement();
            UpdateNoteModeHud();
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
    }
}
