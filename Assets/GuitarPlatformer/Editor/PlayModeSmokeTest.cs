using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GuitarPlatformer.EditorTools
{
    /// <summary>
    /// Batch-mode smoke test: opens the generated scene, enters play mode, verifies the
    /// fretboard-only scene, mirrored string layout, all six synth voices, landing and
    /// walk-on note triggers, equivalent-note ghosts, five camera views, chord toggles,
    /// triple jump without a flip, tile retrigger and respawn behaviour.
    /// </summary>
    public static class PlayModeSmokeTest
    {
        private static double enteredPlayTime;
        private static double stageTime;
        private static int stage;
        private static bool ready;
        private static bool finished;
        private static NoteTile targetTile;
        private static NoteTile walkTile;

        [MenuItem("Guitar Platformer/Run Play Mode Smoke Test")]
        public static void Run()
        {
            if (!Application.isBatchMode)
            {
                Debug.LogWarning("Play mode smoke test is batch-mode only. Run it with " +
                                 "-executeMethod GuitarPlatformer.EditorTools.PlayModeSmokeTest.Run");
                return;
            }

            if (!System.IO.File.Exists(BuilderUtil.ScenePath))
            {
                Debug.LogError("SMOKE FAIL: scene missing.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(BuilderUtil.ScenePath, OpenSceneMode.Single);
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (finished) return;

            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - enteredPlayTime > 60.0)
                {
                    Fail("timed out waiting for play mode");
                }
                return;
            }

            if (!ready)
            {
                ready = true;
                enteredPlayTime = EditorApplication.timeSinceStartup;
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - enteredPlayTime;
            if (elapsed > 60.0)
            {
                Fail("timed out during play mode");
                return;
            }

            if (elapsed < 1.2) return;

            try
            {
                if (stage == 0)
                {
                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    OrbitFollowCamera camera = Object.FindAnyObjectByType<OrbitFollowCamera>();
                    Hud hud = Object.FindAnyObjectByType<Hud>();
                    ChiptuneAudio audio = Object.FindAnyObjectByType<ChiptuneAudio>();
                    NoteFX fx = Object.FindAnyObjectByType<NoteFX>();
                    NoteGhosts ghosts = Object.FindAnyObjectByType<NoteGhosts>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    MenuView menu = Object.FindAnyObjectByType<MenuView>();
                    EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
                    ChordPreview preview = Object.FindAnyObjectByType<ChordPreview>();
                    StringMuteManager mute = Object.FindAnyObjectByType<StringMuteManager>();
                    TargetBeacon beacon = TargetBeacon.Instance;
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();

                    Require(player != null, "player exists");
                    Require(camera != null, "camera exists");
                    Require(hud != null, "HUD exists");
                    Require(audio != null, "audio exists");
                    Require(fx != null, "FX exists");
                    Require(ghosts != null, "ghost system exists");
                    Require(chords != null, "chord manager exists");
                    Require(levels != null, "level manager exists");
                    Require(menu != null, "menu view exists");
                    Require(eventSystem != null, "menu event system exists");
                    Require(preview != null, "chord preview exists");
                    Require(mute != null, "string mute manager exists");
                    Require(beacon != null, "target beacon exists");
                    Require(chords.ActiveCount == 0, "chord intervals start off");
                    Require(!mute.PalmMuted && !mute.AllMuted, "strings start unmuted");
                    Require(mute.Label == "OFF", "mute label starts off (" + mute.Label + ")");
                    Require(levels.State == LevelState.MainMenu, "game starts on the main menu");
                    Require(!levels.CampaignActive, "no challenge is active at the main menu");
                    Require(player.InputLocked, "main menu freezes player input");
                    Require(levels.TargetTile == null, "no campaign target at the main menu");
                    Require(!beacon.HasTarget, "target beacon hidden at the main menu");
                    Require(tiles.Length == 150, "150 note pads, found " + tiles.Length);
                    Require(player.IsGrounded, "player grounded at spawn");
                    Require(camera.CurrentView == CameraView.Guitarist, "guitarist view is the default");

                    CanvasGroup mainGroup = GameObject.Find("MainMenu")?.GetComponent<CanvasGroup>();
                    CanvasGroup challengeGroup = GameObject.Find("ChallengeMenu")?.GetComponent<CanvasGroup>();
                    CanvasGroup levelGroup = GameObject.Find("LevelPanel")?.GetComponent<CanvasGroup>();
                    Text mainRow0 = GameObject.Find("MainRow_0")?.GetComponentInChildren<Text>();
                    Text mainRow1 = GameObject.Find("MainRow_1")?.GetComponentInChildren<Text>();
                    Text mainRow2 = GameObject.Find("MainRow_2")?.GetComponentInChildren<Text>();
                    Require(mainGroup != null && mainGroup.alpha > 0.99f, "main menu is visible");
                    Require(challengeGroup != null && challengeGroup.alpha < 0.01f,
                        "challenge menu starts hidden");
                    Require(levelGroup != null && levelGroup.alpha < 0.01f, "HUD level panel hides in menus");
                    Require(mainRow0 != null && mainRow0.text == "FREE PLAY", "main menu lists free play");
                    Require(mainRow1 != null && mainRow1.text == "CHALLENGES", "main menu lists challenges");
                    Require(mainRow2 != null && mainRow2.text == "INTERVAL SELECTION",
                        "main menu lists interval selection (" + mainRow2?.text + ")");
                    Require(GameObject.Find("MainRow_0")?.GetComponent<MenuButton>() != null &&
                            GameObject.Find("MainRow_2")?.GetComponent<MenuButton>() != null,
                        "main menu rows accept mouse input");
                    Require(GameObject.Find("Reveal")?.transform.Find("Dim")?.GetComponent<MenuButton>() != null,
                        "reveal card accepts mouse input");

                    // Challenge select starts with every challenge unlocked and unnamed.
                    levels.HoverMenu(MenuScreen.Main, 1);
                    Require(levels.MainMenuSelection == 1, "mouse hover moves the menu selection");
                    levels.ActivateMenu(MenuScreen.Main, 1);
                    Require(levels.State == LevelState.ChallengeSelect, "challenges open from the menu");
                    Require(levels.IsChallengeUnlocked(0), "challenge 1 is unlocked");
                    Require(levels.IsChallengeUnlocked(1), "challenge 2 is unlocked");
                    Require(levels.IsChallengeUnlocked(2), "challenge 3 is unlocked");
                    Require(levels.IsChallengeUnlocked(3), "challenge 4 is unlocked");
                    Require(levels.IsChallengeUnlocked(4), "challenge 5 is unlocked");
                    Text challengeRow0 = GameObject.Find("ChallengeRow_0")?.GetComponentInChildren<Text>();
                    Text challengeRow1 = GameObject.Find("ChallengeRow_1")?.GetComponentInChildren<Text>();
                    Text challengeRow2 = GameObject.Find("ChallengeRow_2")?.GetComponentInChildren<Text>();
                    Text challengeRow3 = GameObject.Find("ChallengeRow_3")?.GetComponentInChildren<Text>();
                    Text challengeRow4 = GameObject.Find("ChallengeRow_4")?.GetComponentInChildren<Text>();
                    Require(challengeRow0 != null && challengeRow0.text.Contains("???") &&
                            !challengeRow0.text.Contains("SMOKE ON THE WATER"),
                        "challenge 1 name stays hidden (" + challengeRow0?.text + ")");
                    Require(challengeRow1 != null && challengeRow1.text.Contains("???") &&
                            !challengeRow1.text.Contains("SEVEN NATION ARMY") &&
                            !challengeRow1.text.Contains("LOCKED"),
                        "challenge 2 name stays hidden (" + challengeRow1?.text + ")");
                    Require(challengeRow2 != null && challengeRow2.text.Contains("???") &&
                            !challengeRow2.text.Contains("SMELLS LIKE TEEN SPIRIT") &&
                            !challengeRow2.text.Contains("LOCKED"),
                        "challenge 3 name stays hidden (" + challengeRow2?.text + ")");
                    Require(challengeRow3 != null && challengeRow3.text.Contains("???") &&
                            !challengeRow3.text.Contains("BACK IN BLACK") &&
                            !challengeRow3.text.Contains("LOCKED"),
                        "challenge 4 name stays hidden (" + challengeRow3?.text + ")");
                    Require(challengeRow4 != null && challengeRow4.text.Contains("???") &&
                            !challengeRow4.text.Contains("DO I WANNA KNOW") &&
                            !challengeRow4.text.Contains("LOCKED"),
                        "challenge 5 name stays hidden (" + challengeRow4?.text + ")");
                    levels.HoverMenu(MenuScreen.Challenge, 1);
                    Require(levels.ChallengeMenuSelection == 1,
                        "mouse hover moves the challenge selection");
                    MenuButton backButton = GameObject.Find("ChallengeBack")?.GetComponent<MenuButton>();
                    Require(backButton != null && backButton.index == RiffLibrary.CreateLevels().Length,
                        "BACK sits after the last challenge (index " + backButton?.index + ")");
                    levels.ActivateMenu(MenuScreen.Challenge, RiffLibrary.CreateLevels().Length);
                    Require(levels.State == LevelState.MainMenu, "back returns to the main menu");

                    // The challenge list grows for custom levels loaded from Resources.
                    levels.ActivateMenu(MenuScreen.Main, 1);
                    Require(levels.State == LevelState.ChallengeSelect,
                        "challenges reopen for the custom-row check");
                    var menuView = Object.FindAnyObjectByType<MenuView>();
                    Require(menuView != null, "menu view is available");
                    var customLabels = new[]
                    {
                        "CUSTOM 01", "CUSTOM 02", "CUSTOM 03",
                        "CUSTOM 04", "CUSTOM 05", "CUSTOM 06"
                    };
                    menuView.ShowChallengeSelect(customLabels, new bool[6], new bool[6], 0,
                        Difficulty.Medium, "CUSTOM");
                    GameObject extraRow = GameObject.Find("ChallengeRow_5");
                    Require(extraRow != null,
                        "challenge list clones rows for custom levels");
                    levels.HoverMenu(MenuScreen.Challenge, 0);
                    Require(extraRow != null && !extraRow.activeSelf,
                        "extra challenge rows hide again for the built-in campaign");
                    levels.ActivateMenu(MenuScreen.Challenge, RiffLibrary.CreateLevels().Length);
                    Require(levels.State == LevelState.MainMenu, "back returns to the main menu again");

                    // JSON levels round-trip through the same shape the creator saves.
                    RiffLevel builtIn = RiffLibrary.CreateCampaignLevels()[0];
                    RiffLevelData roundTrip = RiffLevelData.FromLevel(builtIn);
                    RiffLevel rebuilt = roundTrip.ToLevel();
                    Require(rebuilt.Title == builtIn.Title && rebuilt.Steps.Length == builtIn.Steps.Length &&
                            rebuilt.Steps[0].StringIndex == builtIn.Steps[0].StringIndex &&
                            rebuilt.Steps[0].Fret == builtIn.Steps[0].Fret,
                        "riff data round-trips through the JSON shape");
                    Require(RiffLevelData.TryValidate(roundTrip, out _), "built-in level data validates");
                    Require(!RiffLevelData.TryValidate(new RiffLevelData(), out _),
                        "empty level data is rejected");
                    var customData = RiffLevelData.FromLevel(builtIn);
                    customData.title = "Smoke Custom";
                    var customAsset = new TextAsset(JsonUtility.ToJson(customData));
                    RiffLevel customLevel = CustomLevelLibrary.Load(customAsset);
                    Require(customLevel != null && customLevel.Title == "Smoke Custom",
                        "custom level JSON loads at runtime");
                    Require(CustomLevelLibrary.LoadAll().Length >= 0,
                        "custom level folder loads without errors");
                    RiffLevel[] campaign = RiffLibrary.CreateCampaignLevels();
                    Require(campaign.Length == 5, "campaign has five riffs");
                    Require(campaign[3].Title == "Back in Black", "challenge 4 is the imported Back in Black");
                    Require(campaign[4].Title == "Do I Wanna Know?",
                        "challenge 5 is the imported Do I Wanna Know");
                    CustomLevelSession.Queue(customLevel);
                    Require(CustomLevelSession.HasPending, "custom session queues a riff");
                    Require(CustomLevelSession.Consume() == customLevel,
                        "custom session returns the queued riff");
                    Require(!CustomLevelSession.HasPending, "custom session clears after consume");

                    // The interval menu opens from the main menu and returns there.
                    levels.HoverMenu(MenuScreen.Main, 2);
                    Require(levels.MainMenuSelection == 2, "mouse hover selects the interval row");
                    levels.ActivateMenu(MenuScreen.Main, 2);
                    Require(levels.State == LevelState.IntervalMenu,
                        "interval menu opens from the main menu");
                    CanvasGroup intervalFromMain = GameObject.Find("IntervalMenu")?.GetComponent<CanvasGroup>();
                    Require(intervalFromMain != null && intervalFromMain.alpha > 0.99f,
                        "interval menu is visible from the main menu");
                    levels.ActivateMenu(MenuScreen.Interval, ChordManager.Intervals.Length);
                    Require(levels.State == LevelState.MainMenu,
                        "closing the interval menu returns to the main menu");

                    // Free play keeps the original sandbox and the L note-mode toggle.
                    levels.ActivateMenu(MenuScreen.Main, 0);
                    Require(levels.State == LevelState.FreePlay, "free play starts from the menu");
                    Require(!levels.CampaignActive, "free play is not a challenge");
                    Require(!player.InputLocked, "free play restores player input");
                    Require(mainGroup.alpha < 0.01f, "main menu hides for free play");
                    Require(levelGroup != null && levelGroup.alpha > 0.99f, "HUD returns for free play");

                    // The scene is the fretboard alone: no neck, body, headstock or strings.
                    Require(GameObject.Find("Fretboard") != null, "fretboard slab exists");
                    Require(GameObject.Find("Neck") == null, "neck model removed");
                    Require(GameObject.Find("Body") == null, "body model removed");
                    Require(GameObject.Find("Headstock") == null, "headstock model removed");
                    Require(GameObject.Find("Strings") == null, "string objects removed");
                    Require(Object.FindObjectsByType<LineRenderer>().Length == 0, "no string line renderers");

                    // Requested chord toggles, plus the suggested layout.
                    Require(ChordManager.Intervals.Any(i => i.key == Key.F && i.semitones == 7),
                        "F toggles the perfect fifth");
                    Require(ChordManager.Intervals.Any(i => i.key == Key.J && i.semitones == -7),
                        "J toggles the lower fifth");
                    Require(ChordManager.Intervals.Any(i => i.key == Key.E && i.semitones == 3),
                        "E toggles the minor third");
                    Require(ChordManager.Intervals.Any(i => i.key == Key.R && i.semitones == 4),
                        "R toggles the major third");
                    Require(ChordManager.Intervals.Length == 25, "25 intervals are available");
                    Require(ChordManager.SimpleIntervalCount == 13, "13 simple intervals");
                    Require(ChordManager.Intervals.Any(i => i.semitones == 13 && i.key == Key.Digit1),
                        "1 toggles the minor ninth");
                    Require(ChordManager.Intervals.Any(i => i.semitones == 24 && i.key == Key.Equals),
                        "= toggles the perfect fifteenth");
                    Require(ChordManager.Intervals.All(i => !string.IsNullOrEmpty(i.fullName)),
                        "every interval has a full name");
                    Require(ChordTheory.Describe(new int[0]) == "SINGLE NOTES",
                        "no active intervals reads as single notes");
                    Require(ChordTheory.Describe(new[] { 7 }) == "POWER CHORD",
                        "P5 reads as a power chord");
                    Require(ChordTheory.Describe(new[] { 4, 7 }) == "MAJOR",
                        "M3 + P5 reads as major");
                    Require(ChordTheory.Describe(new[] { 3, 7 }) == "MINOR",
                        "m3 + P5 reads as minor");
                    Require(ChordTheory.Describe(new[] { 5, 7 }) == "SUS4",
                        "P4 + P5 reads as sus4");
                    Require(ChordTheory.Describe(new[] { 4, 7, 10 }) == "DOMINANT 7",
                        "M3 + P5 + m7 reads as dominant 7");
                    Require(ChordTheory.Describe(new[] { 4, 7, 11 }) == "MAJOR 7",
                        "M3 + P5 + M7 reads as major 7");
                    Require(ChordTheory.Describe(new[] { 3, 7, 10 }) == "MINOR 7",
                        "m3 + P5 + m7 reads as minor 7");
                    Require(ChordTheory.Describe(new[] { 13 }) == "MINOR 9TH",
                        "m9 reads as a minor ninth");
                    Require(ChordTheory.Describe(new[] { 7, 12 }).Contains("POWER CHORD") &&
                            ChordTheory.Describe(new[] { 7, 12 }).Contains("OCTAVE"),
                        "P5 + octave reads as a power chord with an octave");
                    var chordKeys = ChordManager.Intervals.Select(i => i.key).ToArray();
                    Require(chordKeys.Distinct().Count() == chordKeys.Length, "chord keys are unique");
                    Require(player.RespawnBinding.Contains("backspace"),
                        "respawn moved to Backspace (" + player.RespawnBinding + ")");

                    Text levelHeader = GameObject.Find("LevelHeader")?.GetComponent<Text>();
                    Text levelMode = GameObject.Find("LevelMode")?.GetComponent<Text>();
                    Text muteLabel = GameObject.Find("MuteLabel")?.GetComponent<Text>();
                    Text controlsLabel = GameObject.Find("Controls")?.GetComponent<Text>();
                    Require(levelHeader != null && levelHeader.text.Contains("FREE PLAY"),
                        "HUD starts in free play (" + levelHeader?.text + ")");
                    Require(levelMode != null && levelMode.text.Contains("WALK-ON"),
                        "free play defaults to walk-on (" + levelMode?.text + ")");
                    Require(muteLabel != null && muteLabel.text.Contains("OFF"),
                        "HUD shows strings off (" + muteLabel?.text + ")");
                    Require(controlsLabel != null && controlsLabel.text.Contains("M") &&
                            controlsLabel.text.Contains("N") &&
                            controlsLabel.text.Contains("palm mute") &&
                            controlsLabel.text.Contains("mute all strings") &&
                            controlsLabel.text.Contains("TAB") &&
                            controlsLabel.text.Contains("interval selection"),
                        "controls document the mute and interval keys");
                    Transform pauseMenu = GameObject.Find("PauseMenu")?.transform;
                    Require(pauseMenu != null && controlsLabel.transform.IsChildOf(pauseMenu),
                        "controls only appear inside the pause menu");

                    // Right-handed layout: high E on screen-left (negative X), low E on screen-right.
                    Require(FretboardLayout.LaneX(5) < FretboardLayout.LaneX(0),
                        "high E lane left of low E lane");
                    NoteTile highE = tiles.First(t => t.stringIndex == 5 && t.isOpen);
                    NoteTile lowE = tiles.First(t => t.stringIndex == 0 && t.isOpen);
                    Require(highE.transform.position.x < lowE.transform.position.x,
                        "open high E pad left of open low E pad (" +
                        highE.transform.position.x.ToString("F2") + " < " + lowE.transform.position.x.ToString("F2") + ")");

                    // Every open string must generate audible, bright-enough audio.
                    for (int s = 0; s < GuitarTuning.StringCount; s++)
                    {
                        int midi = GuitarTuning.OpenMidi[s];
                        float[] samples = ChiptuneSynth.Generate(midi, s, ChiptuneSynth.SampleRate, 0.6f);
                        float rms = ChiptuneSynth.Rms(samples);
                        float hf = ChiptuneSynth.HighFrequencyRatio(samples, ChiptuneSynth.SampleRate);
                        float peak = samples.Max(v => Mathf.Abs(v));
                        float crest = peak / Mathf.Max(0.0001f, rms);
                        Require(rms > 0.05f, "string " + GuitarTuning.GuitarStringNumbers[s] + " " +
                                             GuitarTuning.OpenNoteNames[s] + " audible (rms " + rms.ToString("F3") + ")");
                        Require(samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v)),
                            "string " + GuitarTuning.OpenNoteNames[s] + " has finite samples");
                        Require(peak <= 1.001f, "string " + GuitarTuning.OpenNoteNames[s] +
                                               " does not clip (peak " + peak.ToString("F3") + ")");
                        Require(crest < 5f, "string " + GuitarTuning.OpenNoteNames[s] +
                                            " is amp-compressed (crest " + crest.ToString("F2") + ")");
                        if (s <= 2)
                            Require(hf > 0.3f, "low string " + GuitarTuning.OpenNoteNames[s] +
                                               " has upper harmonics (" + hf.ToString("P0") + ")");

                        float measured = ChiptuneSynth.EstimateFrequency(
                            ChiptuneSynth.GenerateString(midi, s, ChiptuneSynth.SampleRate, 0.6f),
                            ChiptuneSynth.SampleRate);
                        float cents = measured > 0f
                            ? 1200f * Mathf.Log(measured / GuitarTuning.Frequency(midi), 2f)
                            : 9999f;
                        Require(Mathf.Abs(cents) < 5f, "string " + GuitarTuning.OpenNoteNames[s] +
                                                       " is in tune (" + cents.ToString("F1") + " cents)");
                    }

                    var voiceTimer = System.Diagnostics.Stopwatch.StartNew();
                    ChiptuneSynth.Generate(GuitarTuning.OpenMidi[0], 0, ChiptuneSynth.SampleRate, 0.6f);
                    voiceTimer.Stop();
                    Require(voiceTimer.ElapsedMilliseconds < 100,
                        "guitar voice generates quickly (" + voiceTimer.ElapsedMilliseconds + " ms)");

                    // D3 used to be the only pure-triangle voice, which made it ~6 dB quieter
                    // than E2/A2 by perceived level. It must now sit with them.
                    float[] d3Samples = ChiptuneSynth.Generate(GuitarTuning.OpenMidi[2], 2,
                        ChiptuneSynth.SampleRate, 0.6f);
                    float[] a2Samples = ChiptuneSynth.Generate(GuitarTuning.OpenMidi[1], 1,
                        ChiptuneSynth.SampleRate, 0.6f);
                    float d3Rms = ChiptuneSynth.Rms(d3Samples);
                    float d3Level = ChiptuneSynth.AWeightedLevel(d3Samples, ChiptuneSynth.SampleRate);
                    float a2Level = ChiptuneSynth.AWeightedLevel(a2Samples, ChiptuneSynth.SampleRate) +
                                    20f * Mathf.Log10(1.1f);
                    Require(d3Rms > 0.27f, "D3 loud enough (rms " + d3Rms.ToString("F3") + ")");
                    Require(Mathf.Abs(d3Level - a2Level) < 2.5f,
                        "D3 perceived level matches A2 (" + d3Level.ToString("F1") + " vs " +
                        a2Level.ToString("F1") + " dB)");

                    float[] openLowE = ChiptuneSynth.Generate(GuitarTuning.OpenMidi[0], 0,
                        ChiptuneSynth.SampleRate, 0.6f);
                    float[] palmLowE = ChiptuneSynth.GenerateMuted(GuitarTuning.OpenMidi[0], 0,
                        ChiptuneSynth.SampleRate, 0.6f);
                    float palmPeak = palmLowE.Max(v => Mathf.Abs(v));
                    Require(palmLowE.All(v => !float.IsNaN(v) && !float.IsInfinity(v)) &&
                            palmPeak <= 1.001f,
                        "palm-muted voice is finite and does not clip (peak " + palmPeak.ToString("F3") + ")");
                    float palmTail = RmsRange(palmLowE, 0.4f, 0.6f);
                    float openTail = RmsRange(openLowE, 0.4f, 0.6f);
                    Require(palmTail < openTail * 0.6f,
                        "palm mute decays faster than the open string (" + palmTail.ToString("F3") +
                        " vs " + openTail.ToString("F3") + ")");

                    Text viewLabel = GameObject.Find("ViewLabel")?.GetComponent<Text>();
                    Require(viewLabel != null && viewLabel.text.Contains("GUITARIST"),
                        "HUD shows the guitarist view");

                    NoteTile target = tiles.First(t => !t.isOpen && t.fret == 5 && t.stringIndex == 5);
                    Require(target != null, "found target pad");
                    targetTile = target;

                    // Drop the player onto the pad from above and let the controller land normally.
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = new Vector3(target.transform.position.x,
                        FretboardLayout.PadHeight + 3f, target.transform.position.z);
                    controller.enabled = true;

                    stage = 1;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 1)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.75) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    OrbitFollowCamera camera = Object.FindAnyObjectByType<OrbitFollowCamera>();
                    ChiptuneAudio audio = Object.FindAnyObjectByType<ChiptuneAudio>();
                    NoteFX fx = Object.FindAnyObjectByType<NoteFX>();
                    NoteGhosts ghosts = Object.FindAnyObjectByType<NoteGhosts>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile target = tiles.First(t => !t.isOpen && t.fret == 5 && t.stringIndex == 5);

                    Require(player.IsGrounded, "player landed on pad");
                    Text noteText = GameObject.Find("NoteName")?.GetComponent<Text>();
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    Require(noteText != null, "note text found");
                    Require(countText != null, "count text found");
                    Require(noteText.text == target.NoteName, "HUD shows " + target.NoteName + " (was " + noteText.text + ")");
                    Require(countText.text == "1", "landing played exactly one note (was " + countText.text + ")");

                    AudioSource[] voices = audio.GetComponentsInChildren<AudioSource>(true);
                    Require(voices.Any(v => v.isPlaying), "a synth voice is playing");

                    Transform ripples = fx.transform.Find("Ripples");
                    Require(ripples != null, "ripple pool exists");
                    Require(ripples.Cast<Transform>().Any(t => t.gameObject.activeSelf), "ripple FX active");

                    // A4 on high E fret 5: 4 unison, 2 octave-up, 4 octave-down, 1 fifth.
                    var ghostTargets = NoteGhosts.BuildTargets(target.stringIndex, target.fret, target.midi);
                    Require(ghostTargets.Count == 11,
                        "A4 has 11 ghost targets (found " + ghostTargets.Count + ")");
                    Require(ghostTargets.Count(t => t.kind == GhostKind.Unison) == 4,
                        "A4 has 4 unison ghosts (" +
                        ghostTargets.Count(t => t.kind == GhostKind.Unison) + ")");
                    Require(ghostTargets.Count(t => t.kind == GhostKind.OctaveUp) == 2,
                        "A4 has 2 octave-up ghosts (" +
                        ghostTargets.Count(t => t.kind == GhostKind.OctaveUp) + ")");
                    Require(ghostTargets.Count(t => t.kind == GhostKind.OctaveDown) == 4,
                        "A4 has 4 octave-down ghosts (" +
                        ghostTargets.Count(t => t.kind == GhostKind.OctaveDown) + ")");
                    Require(ghostTargets.Count(t => t.kind == GhostKind.Fifth) == 1,
                        "A4 has 1 fifth ghost (" +
                        ghostTargets.Count(t => t.kind == GhostKind.Fifth) + ")");
                    Require(!ghostTargets.Any(t => t.stringIndex == target.stringIndex &&
                                                   t.fret == target.fret),
                        "no ghost target on the source pad");
                    Require(ghostTargets.Where(t => t.kind == GhostKind.Fifth)
                            .All(t => t.color == NoteGhosts.FifthColor),
                        "fifth targets use the blue colour");

                    Require(ghosts.ActiveGhostCount == 11,
                        "ghost pool shows all 11 targets (" + ghosts.ActiveGhostCount + ")");
                    Require(ghosts.CountActive(GhostKind.Unison) == 4,
                        "4 active unison ghosts (" + ghosts.CountActive(GhostKind.Unison) + ")");
                    Require(ghosts.CountActive(GhostKind.OctaveUp) == 2,
                        "2 active octave-up ghosts (" + ghosts.CountActive(GhostKind.OctaveUp) + ")");
                    Require(ghosts.CountActive(GhostKind.OctaveDown) == 4,
                        "4 active octave-down ghosts (" + ghosts.CountActive(GhostKind.OctaveDown) + ")");
                    Require(ghosts.CountActive(GhostKind.Fifth) == 1,
                        "1 active fifth ghost (" + ghosts.CountActive(GhostKind.Fifth) + ")");
                    Color fifthColor = ghosts.FirstActiveColor(GhostKind.Fifth);
                    Require(Mathf.Abs(fifthColor.r - 0.2f) < 0.01f &&
                            Mathf.Abs(fifthColor.g - 0.62f) < 0.01f &&
                            Mathf.Abs(fifthColor.b - 1f) < 0.01f,
                        "active fifth ghost is blue (" + fifthColor + ")");
                    Require(!ghosts.HasActiveAt(target.stringIndex, target.fret),
                        "no active ghost on the source pad");

                    // E4 on D3 fret 14 gets the fifth on both D and G strings.
                    var e4Targets = NoteGhosts.BuildTargets(2, 14, GuitarTuning.OpenMidi[2] + 14);
                    var e4Fifths = e4Targets.Where(t => t.kind == GhostKind.Fifth).ToList();
                    Require(e4Fifths.Count == 2,
                        "E4 on D3 has two fifth ghosts (" + e4Fifths.Count + ")");
                    Require(e4Fifths.Any(t => t.stringIndex == 2 && t.fret == 21) &&
                            e4Fifths.Any(t => t.stringIndex == 3 && t.fret == 16),
                        "E4 fifths at D fret 21 and G fret 16");

                    // Rigid follow: exact preset offset and angle from the player, no lag.
                    Vector3 pivot = player.transform.position + Vector3.up * 1.15f;
                    Vector3 expectedCamera = pivot +
                                             Quaternion.Euler(32f, 0f, 0f) * new Vector3(0f, 0f, -13f);
                    float cameraError = Vector3.Distance(camera.transform.position, expectedCamera);
                    Require(cameraError < 0.02f,
                        "camera holds the rigid guitarist offset (" + cameraError.ToString("F3") + ")");
                    Quaternion expectedRotation = Quaternion.LookRotation(pivot - expectedCamera, Vector3.up);
                    Require(Quaternion.Angle(camera.transform.rotation, expectedRotation) < 0.1f,
                        "camera holds the rigid guitarist angle");

                    // Four camera views, first person hides the pick, tilt only rolls the camera.
                    camera.SetView(CameraView.FirstPerson);
                    Require(camera.CurrentView == CameraView.FirstPerson, "camera switched to first person");
                    Require(camera.IsFirstPerson, "first-person flag set");
                    Require(player.Visual != null && !player.Visual.IsVisible, "pick hidden in first person");
                    float headDistance = Vector3.Distance(camera.transform.position,
                        player.transform.position + Vector3.up * 1.45f);
                    Require(headDistance < 0.1f, "first-person camera at the player head (" +
                                                 headDistance.ToString("F2") + ")");

                    camera.SetView(CameraView.Orbit);
                    Require(camera.CurrentView == CameraView.Orbit, "camera switched to orbit");
                    camera.SetView(CameraView.PlayingTilt);
                    Require(camera.CurrentView == CameraView.PlayingTilt, "camera switched to playing tilt");
                    Require(Mathf.Abs(camera.ViewRoll - 15f) < 0.01f,
                        "playing tilt rolls the camera 15 degrees (" + camera.ViewRoll.ToString("F1") + ")");

                    // Side preset: camera on +X, looking across the board. The neck (-Z)
                    // must project to screen-left and the body end (+Z) to screen-right.
                    camera.SetView(CameraView.Side);
                    Require(camera.CurrentView == CameraView.Side, "camera switched to side");
                    Require(camera.ViewName == "SIDE", "side view is named SIDE");
                    Vector3 sidePivot = player.transform.position + Vector3.up * 1.15f;
                    Vector3 sideExpected = sidePivot +
                                           Quaternion.Euler(28f, -90f, 0f) * new Vector3(0f, 0f, -15f);
                    float sideError = Vector3.Distance(camera.transform.position, sideExpected);
                    Require(sideError < 0.02f,
                        "camera holds the rigid side offset (" + sideError.ToString("F3") + ")");
                    Vector3 sideRight = camera.transform.right;
                    Require(Vector3.Dot(sideRight, Vector3.forward) > 0.99f,
                        "side camera screen-right points down the neck (+Z)");
                    Require(Vector3.Dot(Vector3.back, sideRight) < 0f,
                        "neck projects to screen-left");
                    Require(Vector3.Dot(Vector3.forward, sideRight) > 0f,
                        "body end projects to screen-right");
                    Vector3 sideFlat = camera.FlatForward;
                    Require(Vector3.Dot(sideFlat, Vector3.right) < -0.99f,
                        "side view W moves away from the camera (-X)");
                    Vector3 sideMoveRight = Vector3.Cross(Vector3.up, sideFlat).normalized;
                    Require(Vector3.Dot(sideMoveRight, Vector3.forward) > 0.99f,
                        "side view D moves toward the body (+Z)");
                    Text sideLabel = GameObject.Find("ViewLabel")?.GetComponent<Text>();
                    Require(sideLabel != null && sideLabel.text.Contains("SIDE"),
                        "HUD shows the side view");

                    camera.SetView(CameraView.Guitarist);
                    Require(camera.CurrentView == CameraView.Guitarist, "camera cycled back to guitarist");
                    Require(player.Visual != null && player.Visual.IsVisible, "pick visible again");
                    Text viewLabel = GameObject.Find("ViewLabel")?.GetComponent<Text>();
                    Require(viewLabel != null && viewLabel.text.Contains("GUITARIST"), "HUD view label updated");

                    // Ctrl restrikes the pad underfoot without leaving it.
                    Require(player.TryRetriggerTile(), "retrigger plays the pad again");
                    Require(countText.text == "2",
                        "retrigger added exactly one note (was " + countText.text + ")");
                    Require(player.TryRetriggerTile(), "retrigger works again on the same pad");
                    Require(countText.text == "3",
                        "second retrigger added one more note (was " + countText.text + ")");

                    // Teleport one fret forward without leaving the ground: walk-on trigger.
                    NoteTile walk = tiles.First(t => !t.isOpen && t.fret == 6 && t.stringIndex == 5);
                    walkTile = walk;
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = new Vector3(walk.transform.position.x,
                        FretboardLayout.PadHeight + 0.03f, walk.transform.position.z);
                    controller.enabled = true;

                    stage = 2;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 2)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.8) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile walk = tiles.First(t => !t.isOpen && t.fret == 6 && t.stringIndex == 5);
                    Text noteText = GameObject.Find("NoteName")?.GetComponent<Text>();
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();

                    Require(player.IsGrounded, "player still grounded after walking to the next pad");
                    Require(noteText != null && noteText.text == walk.NoteName,
                        "walk-on triggered " + walk.NoteName + " (was " + noteText?.text + ")");
                    Require(countText != null && countText.text == "4",
                        "walk-on added exactly one note (was " + countText?.text + ")");

                    Require(player.TryRetriggerTile(), "retrigger works after walking onto a pad");
                    Require(countText.text == "5",
                        "walk-on retrigger added one note (was " + countText?.text + ")");

                    // Fall off the edge; respawn should return to the walk-on checkpoint without replaying it.
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = new Vector3(0f, -20f, 0f);
                    controller.enabled = true;

                    stage = 3;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 3)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 1.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    Vector3 position = player.transform.position;
                    Require(player.IsGrounded, "player respawned grounded");
                    Require(Mathf.Abs(position.x - walkTile.transform.position.x) < 0.25f,
                        "respawn x at walk-on checkpoint (" + position.x.ToString("F2") + ")");
                    Require(Mathf.Abs(position.z - walkTile.transform.position.z) < 0.25f,
                        "respawn z at walk-on checkpoint (" + position.z.ToString("F2") + ")");
                    Require(position.y > 0.35f && position.y < 0.8f,
                        "respawn height on pad (" + position.y.ToString("F2") + ")");

                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    Require(countText != null && countText.text == "5",
                        "respawn did not retrigger a note (count " + countText?.text + ")");

                    // Land on the bare board beside the pads; no note should fire.
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = new Vector3(7.55f, FretboardLayout.PadHeight + 2f, 40f);
                    controller.enabled = true;

                    stage = 4;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 4)
                {
                    // Long enough for the walk-on note's ghosts to expire before the chord check.
                    if (EditorApplication.timeSinceStartup - stageTime < 1.5) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    Require(player.IsGrounded, "player landed on bare board");
                    Require(countText != null && countText.text == "5",
                        "bare board landing did not play a note (count " + countText?.text + ")");

                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    ChiptuneAudio audio = Object.FindAnyObjectByType<ChiptuneAudio>();
                    NoteGhosts ghosts = Object.FindAnyObjectByType<NoteGhosts>();
                    StringMuteManager mute = Object.FindAnyObjectByType<StringMuteManager>();
                    Require(chords != null, "chord manager available");
                    Require(audio != null, "audio available for the mute test");
                    Require(mute != null, "string mute manager available");
                    Require(chords.ActiveCount == 0, "chord intervals still off");

                    // M palm-mutes every string; N silences all string audio. Both keep
                    // gameplay running, and the pads dim to show the instrument state.
                    Require(!mute.AnyMuted, "strings are unmuted before the mute test");
                    Text muteLabel = GameObject.Find("MuteLabel")?.GetComponent<Text>();
                    mute.ToggleAll();
                    Require(mute.AllMuted && mute.Label == "ALL STRINGS",
                        "N mutes all strings (" + mute.Label + ")");
                    Require(muteLabel != null && muteLabel.text.Contains("ALL STRINGS"),
                        "HUD shows all strings muted (" + muteLabel?.text + ")");
                    Require(!audio.PlayMidi(40, 0, Vector3.zero, 1f),
                        "all-muted strings stay silent");
                    targetTile.RefreshVisual();
                    Require(targetTile.DimFactor < 0.5f,
                        "pads dim when all strings are muted (" + targetTile.DimFactor.ToString("F2") + ")");

                    mute.TogglePalm();
                    Require(mute.PalmMuted && mute.AllMuted && mute.Label == "PALM + ALL STRINGS",
                        "palm mute stacks with all strings (" + mute.Label + ")");
                    mute.ToggleAll();
                    Require(mute.PalmMuted && !mute.AllMuted && mute.Label == "PALM",
                        "all-string mute toggles back off (" + mute.Label + ")");
                    Require(audio.PlayMidi(40, 0, Vector3.zero, 1f),
                        "palm-muted string still sounds");
                    Require(audio.LastPlayWasMuted, "palm mute swaps in the chug voice");
                    targetTile.RefreshVisual();
                    Require(targetTile.DimFactor > 0.5f && targetTile.DimFactor < 0.8f,
                        "pads dim slightly for palm mute (" + targetTile.DimFactor.ToString("F2") + ")");

                    mute.TogglePalm();
                    Require(!mute.AnyMuted && mute.Label == "OFF",
                        "mute state returns to off (" + mute.Label + ")");
                    Require(audio.PlayMidi(40, 0, Vector3.zero, 1f), "unmuted string sounds");
                    Require(!audio.LastPlayWasMuted, "unmuted string uses the normal guitar");
                    targetTile.RefreshVisual();
                    Require(targetTile.DimFactor > 0.99f,
                        "pads return to full brightness (" + targetTile.DimFactor.ToString("F2") + ")");

                    // Chord toggles: the main note plus two intervals sound together, but the
                    // chord voices must not add note counts or recursively stack ghost sets.
                    chords.Toggle(3);
                    chords.Toggle(7);
                    Require(chords.IsActive(3) && chords.IsActive(7),
                        "minor third and fifth toggled on");
                    Require(chords.ActiveLabel.Contains("m3") && chords.ActiveLabel.Contains("P5"),
                        "active chord label lists m3 + P5 (" + chords.ActiveLabel + ")");

                    Text chordLabel = GameObject.Find("ChordLabel")?.GetComponent<Text>();
                    Require(chordLabel != null && chordLabel.text.Contains("m3") &&
                            chordLabel.text.Contains("P5"),
                        "HUD shows the active chord (" + chordLabel?.text + ")");

                    int chordPlays = chords.ChordPlayCount;
                    int ghostsBefore = ghosts != null ? ghosts.ActiveGhostCount : 0;
                    Require(ghostsBefore == 0, "ghosts cleared before the chord test");
                    targetTile.Play(0.6f);
                    Require(chords.ChordPlayCount == chordPlays + 1,
                        "note play triggers the chord");
                    Require(chords.LastChordSize == 2,
                        "two interval voices played (" + chords.LastChordSize + ")");
                    Require(audio != null && audio.ActiveVoiceCount >= 3,
                        "main note + two intervals sound together (" +
                        (audio != null ? audio.ActiveVoiceCount : 0) + " voices)");
                    Require(countText.text == "6",
                        "chord did not add extra note counts (count " + countText.text + ")");
                    int ghostsAfter = ghosts != null ? ghosts.ActiveGhostCount : 0;
                    Require(ghostsAfter == 11,
                        "chord spawns only the played note's 11 ghosts (" + ghostsAfter + ")");

                    chords.Toggle(3);
                    chords.Toggle(7);
                    Require(chords.ActiveCount == 0, "chord toggles reset");
                    Require(chordLabel != null && chordLabel.text.Contains("OFF"),
                        "HUD clears the chord label");

                    // Triple jump: ground -> double -> triple, then no fourth jump.
                    Require(player.IsGrounded, "player grounded before the jump chain");
                    Require(player.TryJump(), "ground jump starts");
                    Require(player.JumpsUsed == 1, "one jump used after the ground jump");
                    Require(player.TryJump(), "double jump starts");
                    Require(player.JumpsUsed == 2, "two jumps used after the double jump");
                    Require(player.TryJump(), "triple jump starts");
                    Require(player.JumpsUsed == 3, "three jumps used after the triple jump");
                    Require(player.VerticalVelocity > 5f,
                        "triple jump has upward velocity (" + player.VerticalVelocity.ToString("F2") + ")");
                    Require(!player.TryJump(), "no fourth jump");
                    Require(player.JumpsUsed == 3, "jump count stays at three");

                    stage = 5;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 5)
                {
                    // Sample mid-flight, where the old 360-degree flip would be clearly visible.
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    Require(!player.IsGrounded, "player is airborne after the triple jump");
                    Require(player.JumpsUsed == 3, "triple jump state persists in the air");

                    Transform pick = player.Visual != null ? player.Visual.transform : null;
                    Require(pick != null, "pick visual found for the flip check");
                    float pickPitch = Mathf.DeltaAngle(0f, pick.localEulerAngles.x);
                    Require(Mathf.Abs(pickPitch) < 25f,
                        "pick does not flip during the triple jump (pitch " +
                        pickPitch.ToString("F1") + ")");

                    stage = 6;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 6)
                {
                    // Wait for the triple jump to land, then stage the landing-only check.
                    if (EditorApplication.timeSinceStartup - stageTime < 1.3) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile pad = tiles.First(t => t.stringIndex == 5 && t.fret == 5);
                    Require(player.IsGrounded, "player landed after the triple jump");

                    player.TeleportTo(new Vector3(pad.transform.position.x, FretboardLayout.PadHeight + 0.06f,
                        pad.transform.position.z));
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    Require(countText != null, "count text found before the note-mode test");
                    player.SetLandingOnly(true);
                    Require(player.LandingOnlyNotes, "landing-only mode turns on");
                    Text modeLabel = GameObject.Find("LevelMode")?.GetComponent<Text>();
                    Require(modeLabel != null && modeLabel.text.Contains("LANDING ONLY"),
                        "HUD shows landing-only mode (" + modeLabel?.text + ")");

                    stage = 7;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 7)
                {
                    // Walk sideways onto the neighbouring pad without jumping. Landing-only
                    // mode must ignore the new pad.
                    if (EditorApplication.timeSinceStartup - stageTime < 0.55) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile next = tiles.First(t => t.stringIndex == 5 && t.fret == 6);

                    player.transform.position = new Vector3(next.transform.position.x,
                        FretboardLayout.PadHeight + 0.03f, next.transform.position.z);

                    stage = 8;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 8)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.45) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    int count = int.Parse(countText.text);
                    Require(player.IsGrounded, "player grounded on the neighbouring pad");
                    Require(count == 6,
                        "landing-only ignores walking onto a pad (count " + count + ")");

                    // Jump straight up and land on the same pad; this must trigger.
                    Require(player.TryJump(), "landing-only test jump starts");
                    stage = 9;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 9)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 1.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    int count = int.Parse(countText.text);
                    Require(player.IsGrounded, "player landed after the landing-only jump");
                    Require(count == 7, "landing-only triggers on a real landing (count " + count + ")");

                    stage = 90;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 90)
                {
                    // Drop onto a pad without jumping. Landing-only mode must ignore it,
                    // even though the fall is long enough to pass the landing grace timer.
                    if (EditorApplication.timeSinceStartup - stageTime < 0.55) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile fallPad = tiles.First(t => t.stringIndex == 5 && t.fret == 7);
                    Require(player.LandingOnlyNotes, "fall check runs in landing-only mode");

                    player.TeleportTo(new Vector3(fallPad.transform.position.x,
                        FretboardLayout.PadHeight + 3f, fallPad.transform.position.z));

                    stage = 91;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 91)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 1.4) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();
                    int count = int.Parse(countText.text);
                    Require(player.IsGrounded, "player landed after the non-jump fall");
                    Require(count == 7,
                        "non-jump fall does not trigger a note (count " + count + ")");
                    Require(levels.CurrentStepIndex == 0,
                        "non-jump fall leaves progress untouched (" + levels.CurrentStepIndex + ")");

                    // Step onto bare board first; otherwise restoring walk-on would
                    // immediately play the pad the fall test is standing on.
                    player.TeleportTo(new Vector3(7.55f, FretboardLayout.PadHeight + 0.06f, 40f));
                    player.SetLandingOnly(false);
                    Require(!player.LandingOnlyNotes, "walk-on mode restores");
                    Text modeLabel = GameObject.Find("LevelMode")?.GetComponent<Text>();
                    Require(modeLabel != null && modeLabel.text.Contains("WALK-ON"),
                        "HUD shows walk-on mode again (" + modeLabel?.text + ")");

                    stage = 10;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 10)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    TargetBeacon beacon = TargetBeacon.Instance;
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();

                    levels.SetDifficulty(Difficulty.Medium);
                    levels.StartLevel(0);
                    Require(levels.State == LevelState.Playing, "Smoke on the Water starts");
                    Require(levels.CurrentDifficulty == Difficulty.Medium, "challenge runs on medium");
                    Require(levels.Lives == LevelManager.StartingLives, "medium starts with three lives");
                    Require(levels.CurrentLevelIndex == 0, "campaign starts at level 1");
                    Require(levels.CurrentLevel.Title == "Smoke on the Water",
                        "level 1 is Smoke on the Water");
                    Require(levels.CurrentLevel.Artist == "Deep Purple", "level 1 artist is Deep Purple");
                    Require(levels.CurrentLevel.Bpm == 112, "level 1 runs at 112 BPM");
                    Require(levels.CurrentStepIndex == 0, "level 1 starts at step 1");
                    Require(levels.StepCount == 12, "Smoke on the Water has 12 steps");
                    RiffPerformanceNote[] smokeNotes = RiffPerformance.Build(levels.CurrentLevel);
                    Require(smokeNotes.Length == 12, "Smoke performance has 12 events");
                    Require(Mathf.Abs(smokeNotes[0].StartBeat) < 0.001f,
                        "Smoke performance starts on beat 1");
                    Require(Mathf.Abs(smokeNotes[11].StartBeat - 12.5f) < 0.001f,
                        "Smoke performance lands the last G on beat 12.5 (" +
                        smokeNotes[11].StartBeat.ToString("F2") + ")");
                    Require(Mathf.Abs(levels.CurrentLevel.LoopLengthBeats - 16f) < 0.001f,
                        "Smoke loop is four bars");
                    Require(levels.ReplayEventCount == 12, "Smoke replay reports 12 events");
                    Require(!player.LandingOnlyNotes, "campaign uses walk-on notes");
                    Require(chords.ActiveCount == 0, "campaign clears manual chords");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 0 &&
                            levels.TargetTile.fret == 3,
                        "first target is low E fret 3 (G2)");
                    Require(beacon != null && beacon.HasTarget && beacon.Target == levels.TargetTile,
                        "beacon marks the first target");
                    Require(levels.TargetTile.transform.position.x ==
                            tiles.First(t => t.stringIndex == 0 && t.fret == 3).transform.position.x,
                        "target pad identity is stable");

                    NoteTile start = tiles.First(t => t.stringIndex == 0 && t.isOpen);
                    Require(Mathf.Abs(player.transform.position.x - start.transform.position.x) < 0.25f,
                        "level 1 teleports to the low E open start pad");
                    Require(Mathf.Abs(player.transform.position.z - start.transform.position.z) < 0.25f,
                        "level 1 start pad Z is correct");
                    Require(!player.InputLocked, "player input is live during the level");

                    Text header = GameObject.Find("LevelHeader")?.GetComponent<Text>();
                    Text step = GameObject.Find("LevelStep")?.GetComponent<Text>();
                    Text target = GameObject.Find("LevelTarget")?.GetComponent<Text>();
                    Text chord = GameObject.Find("LevelChord")?.GetComponent<Text>();
                    Text mode = GameObject.Find("LevelMode")?.GetComponent<Text>();
                    Text runStatus = GameObject.Find("LevelStatus")?.GetComponent<Text>();
                    Require(header != null && header.text.Contains("CHALLENGE 01") &&
                            !header.text.Contains("SMOKE ON THE WATER"),
                        "HUD hides the challenge 1 name before completion (" + header?.text + ")");
                    Require(step != null && step.text.Contains("1 / 12"),
                        "HUD shows step 1 of 12 (" + step?.text + ")");
                    Require(target != null && target.text.Contains("G2"),
                        "HUD shows the G2 target (" + target?.text + ")");
                    Require(chord != null && !chord.gameObject.activeSelf,
                        "HUD hides the unused next-chord line (" + chord?.text + ")");
                    Require(mode != null && mode.text.Contains("WALK-ON"),
                        "HUD shows walk-on during the level (" + mode?.text + ")");
                    Require(runStatus != null && runStatus.text.Contains("MEDIUM") &&
                            runStatus.text.Contains("LIVES 3 / 3"),
                        "HUD shows medium and three lives (" + runStatus?.text + ")");

                    // A level must not be switchable to landing-only mid-challenge.
                    player.SetLandingOnly(true);

                    stage = 11;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 11)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile wrong = tiles.First(t => t.stringIndex == 5 && t.fret == 5);
                    NoteTile start = tiles.First(t => t.stringIndex == 0 && t.isOpen);
                    int stepBefore = levels.CurrentStepIndex;
                    Require(!player.LandingOnlyNotes,
                        "campaign re-asserts walk-on after a mid-level toggle");
                    Text modeLabel = GameObject.Find("LevelMode")?.GetComponent<Text>();
                    Require(modeLabel != null && modeLabel.text.Contains("WALK-ON"),
                        "HUD returns to walk-on after a mid-level toggle (" + modeLabel?.text + ")");

                    wrong.Play(0.6f);
                    Require(levels.CurrentStepIndex == stepBefore, "wrong note keeps step progress");
                    Require(levels.MistakeCount == 1, "wrong note counts as one mistake");
                    Require(levels.Lives == 2, "wrong note costs one life");
                    Require(player.MovementLocked, "wrong note locks movement for a beat");
                    Text status = GameObject.Find("LevelStatus")?.GetComponent<Text>();
                    Require(status != null && status.text.Contains("LIVES 2 / 3"),
                        "HUD counts down to two lives (" + status?.text + ")");
                    Require(Mathf.Abs(player.transform.position.x - start.transform.position.x) < 0.25f &&
                            Mathf.Abs(player.transform.position.z - start.transform.position.z) < 0.25f,
                        "wrong note teleports back to the level start pad");
                    Require(levels.TargetTile != null && levels.TargetTile.fret == 3,
                        "wrong note keeps the same target");

                    stage = 12;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 12)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    NoteTile target = levels.TargetTile;
                    Require(target != null, "current target found for the correct-note test");

                    player.TeleportTo(new Vector3(target.transform.position.x,
                        FretboardLayout.PadHeight + 0.06f, target.transform.position.z));
                    target.Play(0.6f);
                    Require(levels.CurrentStepIndex == 1, "correct note advances one step");
                    Require(levels.MistakeCount == 1, "correct note does not add a mistake");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 1 &&
                            levels.TargetTile.fret == 1,
                        "next target is A string fret 1 (Bb2)");
                    Text step = GameObject.Find("LevelStep")?.GetComponent<Text>();
                    Require(step != null && step.text.Contains("2 / 12"),
                        "HUD advances to step 2 (" + step?.text + ")");

                    // Fall off the board. The checkpoint must stay on the correct pad.
                    CharacterController controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    player.transform.position = new Vector3(0f, -20f, 0f);
                    controller.enabled = true;

                    stage = 20;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 20)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 1.4) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile lastCorrect = tiles.First(t => t.stringIndex == 0 && t.fret == 3);
                    Text countText = GameObject.Find("Count")?.GetComponent<Text>();

                    Require(player.IsGrounded, "player respawned after falling in level 1");
                    Require(Mathf.Abs(player.transform.position.x - lastCorrect.transform.position.x) < 0.25f &&
                            Mathf.Abs(player.transform.position.z - lastCorrect.transform.position.z) < 0.25f,
                        "fall respawns at the last correct pad");
                    Require(levels.CurrentStepIndex == 1, "fall keeps level progress");
                    Require(levels.TargetTile != null && levels.TargetTile.fret == 1,
                        "fall keeps the current target");
                    Require(levels.MistakeCount == 1, "fall does not add a mistake");
                    Require(countText != null && countText.text == "9",
                        "fall respawn does not retrigger a note (count " + countText?.text + ")");

                    stage = 13;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 13)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile target = levels.TargetTile;
                    NoteTile previous = tiles.First(t => t.stringIndex == 0 && t.fret == 3);

                    chords.Toggle(7);
                    Require(chords.ActiveCount == 1, "wrong-chord test activates P5");
                    player.TeleportTo(new Vector3(target.transform.position.x,
                        FretboardLayout.PadHeight + 0.06f, target.transform.position.z));
                    target.Play(0.6f);

                    Require(levels.CurrentStepIndex == 1, "wrong chord keeps step progress");
                    Require(levels.MistakeCount == 2, "wrong chord counts as a mistake");
                    Require(levels.Lives == 1, "wrong chord costs one life");
                    Require(Mathf.Abs(player.transform.position.x - previous.transform.position.x) < 0.25f &&
                            Mathf.Abs(player.transform.position.z - previous.transform.position.z) < 0.25f,
                        "wrong chord teleports to the previous correct pad");
                    Require(levels.TargetTile != null && levels.TargetTile.fret == 1,
                        "wrong chord keeps the same target");

                    chords.Clear();
                    stage = 21;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 21)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    TargetBeacon beacon = TargetBeacon.Instance;
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile authored = levels.TargetTile;
                    NoteTile equivalent = tiles.First(t => t.stringIndex == 0 && t.fret == 6);
                    NoteTile wrong = tiles.First(t => t.stringIndex == 2 && t.fret == 5);

                    Require(authored != null && authored.stringIndex == 1 && authored.fret == 1,
                        "same-pitch test starts on authored A string fret 1");
                    Require(equivalent.midi == authored.midi,
                        "test pad repeats the target pitch on another string");
                    Require(wrong.midi != authored.midi, "wrong-pad test uses a different pitch");

                    player.TeleportTo(new Vector3(equivalent.transform.position.x,
                        FretboardLayout.PadHeight + 0.06f, equivalent.transform.position.z));
                    equivalent.Play(0.6f);

                    Require(levels.CurrentStepIndex == 2,
                        "same pitch on another string advances the step");
                    Require(levels.MistakeCount == 2,
                        "same pitch on another string is not a mistake");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 1 &&
                            levels.TargetTile.fret == 3,
                        "next target stays on the authored A string fret 3");
                    Require(beacon != null && beacon.Target == levels.TargetTile,
                        "beacon stays on the authored target pad");
                    Text targetText = GameObject.Find("LevelTarget")?.GetComponent<Text>();
                    Require(targetText != null && targetText.text.Contains("STRING 5") &&
                            targetText.text.Contains("FRET 3") && !targetText.text.Contains("FRET 6"),
                        "HUD keeps pointing at the authored pad (" + targetText?.text + ")");

                    // The next mistake empties the last life. The challenge must restart
                    // from step 1 with a fresh set of three lives.
                    wrong.Play(0.6f);
                    Require(levels.MistakeCount == 3,
                        "wrong note after an equivalent note counts a mistake");
                    Require(levels.Lives == 0, "the third mistake empties the lives");
                    Require(levels.State == LevelState.OutOfLives,
                        "zero lives opens the out-of-lives screen");
                    Require(player.InputLocked, "out-of-lives freezes player input");
                    CanvasGroup outOfLives = GameObject.Find("OutOfLives")?.GetComponent<CanvasGroup>();
                    Require(outOfLives != null && outOfLives.alpha > 0.99f,
                        "out-of-lives overlay is visible");
                    Text outTitle = GameObject.Find("OutTitle")?.GetComponent<Text>();
                    Require(outTitle != null && outTitle.text == "OUT OF LIVES",
                        "out-of-lives title shows (" + outTitle?.text + ")");

                    stage = 22;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 22)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 1.9) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile start = tiles.First(t => t.stringIndex == 0 && t.isOpen);

                    Require(levels.State == LevelState.Playing,
                        "out-of-lives restarts the same challenge");
                    Require(levels.CurrentLevelIndex == 0, "the restart keeps challenge 1");
                    Require(levels.CurrentStepIndex == 0, "the restart returns to step 1");
                    Require(levels.Lives == LevelManager.StartingLives,
                        "the restart restores three lives");
                    Require(levels.MistakeCount == 0, "the restart clears the mistake count");
                    Require(!player.InputLocked, "the restart restores player input");
                    Require(Mathf.Abs(player.transform.position.x - start.transform.position.x) < 0.25f &&
                            Mathf.Abs(player.transform.position.z - start.transform.position.z) < 0.25f,
                        "the restart teleports to the challenge start pad");
                    CanvasGroup outOfLives = GameObject.Find("OutOfLives")?.GetComponent<CanvasGroup>();
                    Require(outOfLives != null && outOfLives.alpha < 0.01f,
                        "out-of-lives overlay hides after the restart");
                    Text status = GameObject.Find("LevelStatus")?.GetComponent<Text>();
                    Require(status != null && status.text.Contains("LIVES 3 / 3"),
                        "HUD shows three lives after the restart (" + status?.text + ")");

                    stage = 14;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 14)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    TargetBeacon beacon = TargetBeacon.Instance;

                    int guard = 0;
                    while (levels.State == LevelState.Playing && guard++ < 32)
                    {
                        NoteTile target = levels.TargetTile;
                        Require(target != null, "target exists while completing Smoke");
                        levels.OnNotePlayed(target);
                    }
                    Require(levels.State == LevelState.Replay, "completing Smoke starts the replay");
                    Require(player.InputLocked, "replay freezes player input");
                    Require(beacon != null && !beacon.HasTarget, "replay hides the target beacon");
                    Require(!levels.AllowChordInput, "replay disables manual chord input");
                    Text step = GameObject.Find("LevelStep")?.GetComponent<Text>();
                    Require(step != null && step.text.Contains("REPLAY"),
                        "HUD announces the replay (" + step?.text + ")");

                    Require(levels.SkipReplay(), "replay can be skipped in tests");
                    Require(levels.State == LevelState.Reveal, "skip moves to the reveal card");
                    Require(player.InputLocked, "reveal keeps player input frozen");
                    Text title = GameObject.Find("RevealTitle")?.GetComponent<Text>();
                    Text artist = GameObject.Find("RevealArtist")?.GetComponent<Text>();
                    Text prompt = GameObject.Find("RevealPrompt")?.GetComponent<Text>();
                    CanvasGroup reveal = GameObject.Find("Reveal")?.GetComponent<CanvasGroup>();
                    Require(title != null && title.text == "Smoke on the Water",
                        "reveal shows Smoke on the Water (" + title?.text + ")");
                    Require(artist != null && artist.text == "Deep Purple",
                        "reveal shows Deep Purple (" + artist?.text + ")");
                    Require(prompt != null && prompt.text.Contains("CHALLENGE MENU"),
                        "reveal prompts for the challenge menu (" + prompt?.text + ")");
                    Require(reveal != null && reveal.alpha > 0.99f, "reveal card is visible");
                    Require(levels.IsChallengeCompleted(0), "challenge 1 is marked complete");
                    Require(levels.IsChallengeUnlocked(1), "challenge 2 stays unlocked");
                    Require(levels.IsChallengeUnlocked(2), "challenge 3 stays unlocked");

                    stage = 15;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 15)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();

                    levels.Advance();
                    Require(levels.State == LevelState.ChallengeSelect,
                        "the challenge 1 reveal returns to the challenge menu");
                    Require(levels.IsChallengeCompleted(0), "challenge 1 stays complete");
                    Require(levels.IsChallengeUnlocked(1), "challenge 2 is unlocked");
                    Require(levels.IsChallengeUnlocked(2), "challenge 3 is unlocked");
                    CanvasGroup challengeGroup = GameObject.Find("ChallengeMenu")?.GetComponent<CanvasGroup>();
                    Require(challengeGroup != null && challengeGroup.alpha > 0.99f,
                        "challenge select is visible");
                    Text row0 = GameObject.Find("ChallengeRow_0")?.GetComponentInChildren<Text>();
                    Text row1 = GameObject.Find("ChallengeRow_1")?.GetComponentInChildren<Text>();
                    Text row2 = GameObject.Find("ChallengeRow_2")?.GetComponentInChildren<Text>();
                    Require(row0 != null && row0.text.Contains("SMOKE ON THE WATER"),
                        "the cleared challenge reveals its name (" + row0?.text + ")");
                    Require(row1 != null && row1.text.Contains("???") &&
                            !row1.text.Contains("SEVEN NATION ARMY"),
                        "the unlocked challenge stays unnamed (" + row1?.text + ")");
                    Require(row2 != null && row2.text.Contains("???") &&
                            !row2.text.Contains("SMELLS LIKE TEEN SPIRIT") &&
                            !row2.text.Contains("LOCKED"),
                        "the uncleared challenge 3 stays unnamed (" + row2?.text + ")");

                    levels.ActivateMenu(MenuScreen.Challenge, 1);
                    Require(levels.State == LevelState.Playing, "challenge 2 starts from the menu");
                    Require(levels.CurrentLevelIndex == 1, "challenge 2 is active");
                    Require(levels.CurrentLevel.Title == "Seven Nation Army",
                        "challenge 2 is Seven Nation Army");
                    Require(levels.CurrentLevel.Artist == "The White Stripes",
                        "challenge 2 artist is The White Stripes");
                    Require(levels.CurrentLevel.Bpm == 124, "challenge 2 runs at 124 BPM");
                    Require(levels.StepCount == 16, "Seven Nation Army has 16 steps");
                    RiffPerformanceNote[] sevenNotes = RiffPerformance.Build(levels.CurrentLevel);
                    Require(sevenNotes.Length == 16, "Seven Nation Army performance has 16 events");
                    Require(Mathf.Abs(sevenNotes[0].StartBeat) < 0.001f,
                        "Seven Nation Army performance starts on beat 1");
                    Require(Mathf.Abs(sevenNotes[15].StartBeat - 14f) < 0.001f,
                        "Seven Nation Army performance lands the last B on beat 14 (" +
                        sevenNotes[15].StartBeat.ToString("F2") + ")");
                    Require(levels.ReplayEventCount == 16, "Seven Nation Army replay reports 16 events");
                    Require(levels.CurrentStepIndex == 0, "challenge 2 starts at step 1");
                    Require(levels.Lives == LevelManager.StartingLives, "challenge 2 starts with three lives");
                    Require(!player.LandingOnlyNotes, "challenge 2 keeps walk-on notes");
                    Require(chords.ActiveCount == 0, "challenge 2 clears manual chords");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 1 &&
                            levels.TargetTile.fret == 7,
                        "first Seven Nation Army target is A fret 7 (E3)");
                    NoteTile start = tiles.First(t => t.stringIndex == 1 && t.fret == 3);
                    Require(Mathf.Abs(player.transform.position.x - start.transform.position.x) < 0.25f &&
                            Mathf.Abs(player.transform.position.z - start.transform.position.z) < 0.25f,
                        "challenge 2 teleports to the A fret 3 start pad");
                    CanvasGroup reveal = GameObject.Find("Reveal")?.GetComponent<CanvasGroup>();
                    Require(reveal != null && reveal.alpha < 0.01f, "reveal card hides for challenge 2");
                    Text header = GameObject.Find("LevelHeader")?.GetComponent<Text>();
                    Require(header != null && header.text.Contains("CHALLENGE 02") &&
                            !header.text.Contains("SEVEN NATION ARMY"),
                        "HUD hides the challenge 2 name (" + header?.text + ")");
                    Text runStatus = GameObject.Find("LevelStatus")?.GetComponent<Text>();
                    Require(runStatus != null && runStatus.text.Contains("LIVES 3 / 3"),
                        "HUD shows three lives for challenge 2 (" + runStatus?.text + ")");

                    int guard = 0;
                    while (levels.State == LevelState.Playing && guard++ < 40)
                    {
                        NoteTile target = levels.TargetTile;
                        Require(target != null, "target exists while completing Seven Nation Army");
                        levels.OnNotePlayed(target);
                    }
                    Require(levels.State == LevelState.Replay, "challenge 2 replay starts");
                    Require(levels.SkipReplay(), "challenge 2 replay skips");
                    Require(levels.State == LevelState.Reveal, "challenge 2 reveal shows");

                    Text title = GameObject.Find("RevealTitle")?.GetComponent<Text>();
                    Text artist = GameObject.Find("RevealArtist")?.GetComponent<Text>();
                    Text prompt = GameObject.Find("RevealPrompt")?.GetComponent<Text>();
                    Require(title != null && title.text == "Seven Nation Army",
                        "reveal shows Seven Nation Army (" + title?.text + ")");
                    Require(artist != null && artist.text == "The White Stripes",
                        "reveal shows The White Stripes (" + artist?.text + ")");
                    Require(prompt != null && prompt.text.Contains("CHALLENGE MENU"),
                        "challenge 2 reveal prompts for the menu (" + prompt?.text + ")");
                    Require(levels.IsChallengeCompleted(1), "challenge 2 is marked complete");
                    Require(levels.IsChallengeUnlocked(2), "challenge 3 stays unlocked after the clear");

                    stage = 16;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 16)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();

                    levels.Advance();
                    Require(levels.State == LevelState.ChallengeSelect,
                        "the challenge 2 reveal returns to the challenge menu");
                    Require(levels.IsChallengeCompleted(1), "challenge 2 stays complete");
                    Require(levels.IsChallengeUnlocked(2), "challenge 3 is unlocked");
                    Text row1 = GameObject.Find("ChallengeRow_1")?.GetComponentInChildren<Text>();
                    Text row2 = GameObject.Find("ChallengeRow_2")?.GetComponentInChildren<Text>();
                    Require(row1 != null && row1.text.Contains("SEVEN NATION ARMY"),
                        "the cleared challenge 2 reveals its name (" + row1?.text + ")");
                    Require(row2 != null && row2.text.Contains("???") &&
                            !row2.text.Contains("SMELLS LIKE TEEN SPIRIT"),
                        "the unlocked challenge 3 stays unnamed (" + row2?.text + ")");

                    levels.ActivateMenu(MenuScreen.Challenge, 2);
                    Require(levels.State == LevelState.Playing, "challenge 3 starts from the menu");
                    Require(levels.CurrentLevelIndex == 2, "challenge 3 is active");
                    Require(levels.CurrentLevel.Title == "Smells Like Teen Spirit",
                        "challenge 3 is Smells Like Teen Spirit");
                    Require(levels.CurrentLevel.Artist == "Nirvana", "challenge 3 artist is Nirvana");
                    Require(levels.CurrentLevel.Bpm == 117, "challenge 3 runs at 117 BPM");
                    Require(levels.StepCount == 20, "Smells Like Teen Spirit has 20 steps");
                    Require(levels.Lives == LevelManager.StartingLives, "challenge 3 starts with three lives");
                    RiffPerformanceNote[] teenNotes = RiffPerformance.Build(levels.CurrentLevel);
                    Require(teenNotes.Length == 40, "SLTS performance plays the two-bar phrase twice (40 events)");
                    Require(teenNotes.Count(n => n.Muted) == 16,
                        "SLTS performance includes the palm-muted chugs (" +
                        teenNotes.Count(n => n.Muted) + ")");
                    Require(Mathf.Abs(teenNotes[0].StartBeat) < 0.001f,
                        "SLTS performance starts on beat 1");
                    Require(Mathf.Abs(teenNotes[teenNotes.Length - 1].StartBeat - 15.5f) < 0.001f,
                        "SLTS second pass ends on beat 15.5 (" +
                        teenNotes[teenNotes.Length - 1].StartBeat.ToString("F2") + ")");
                    Require(Mathf.Abs(levels.CurrentLevel.LoopLengthBeats - 8f) < 0.001f,
                        "SLTS loop is two bars");
                    Require(levels.CurrentLevel.ReplayCycles == 2, "SLTS replay plays two cycles");
                    Require(levels.ReplayEventCount == 40, "SLTS replay reports 40 events");
                    Require(teenNotes.Take(7).All(n => n.Midi == 41),
                        "SLTS opens with three F hits and four muted F chugs");
                    Require(teenNotes[7].StringIndex == 1 && teenNotes[7].Fret == 1 &&
                            teenNotes[8].Midi == 46,
                        "SLTS moves to Bb on the A string fret 1");
                    Require(teenNotes[9].Midi == 40 && teenNotes[9].StringIndex == 0 &&
                            teenNotes[9].Fret == 0,
                        "SLTS bar 1 ends on the open low E");
                    Require(teenNotes.Skip(10).Take(7).All(n => n.Midi == 44),
                        "SLTS second bar opens with three Ab hits and four muted Ab chugs");
                    Require(teenNotes[17].StringIndex == 1 && teenNotes[17].Fret == 4 &&
                            teenNotes[18].Midi == 49,
                        "SLTS moves to Db on the A string fret 4");
                    Require(teenNotes[19].Midi == 40 && teenNotes[19].StringIndex == 0 &&
                            teenNotes[19].Fret == 0,
                        "SLTS bar 2 ends on the open low E");
                    Require(Mathf.Abs(teenNotes[9].StartBeat - 3.5f) < 0.001f &&
                            Mathf.Abs(teenNotes[19].StartBeat - 7.5f) < 0.001f,
                        "SLTS open E lands on the turnaround beats");
                    Require(teenNotes.Take(20).Where(n => n.Midi != 40)
                            .All(n => n.ChordIntervals.SequenceEqual(new[] { 7 })),
                        "every SLTS power-chord step keeps the P5 voicing");
                    Require(teenNotes.Take(20).Where(n => n.Midi == 40)
                            .All(n => n.ChordIntervals.Length == 0),
                        "both SLTS open E steps are chord-free");
                    Require(teenNotes[29].Midi == 40 && teenNotes[39].Midi == 40 &&
                            teenNotes[29].ChordIntervals.Length == 0 &&
                            teenNotes[39].ChordIntervals.Length == 0,
                        "second SLTS cycle repeats the chord-free open E");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 0 &&
                            levels.TargetTile.fret == 1,
                        "first SLTS target is low E fret 1 (F2)");
                    Require(!player.LandingOnlyNotes, "challenge 3 keeps walk-on notes");
                    Require(chords.ActiveCount == 0, "challenge 3 clears manual chords");
                    Text header = GameObject.Find("LevelHeader")?.GetComponent<Text>();
                    Require(header != null && header.text.Contains("CHALLENGE 03") &&
                            !header.text.Contains("SMELLS LIKE TEEN SPIRIT"),
                        "HUD hides the challenge 3 name (" + header?.text + ")");

                    Text chord = GameObject.Find("LevelChord")?.GetComponent<Text>();
                    Require(chord != null && chord.text.Contains("P5") && chord.text.Contains("KEY F"),
                        "HUD requires P5 on F (" + chord?.text + ")");
                    Require(!chords.MatchesExactly(new[] { 7 }),
                        "P5 requirement is not satisfied without the chord");

                    NoteTile target = levels.TargetTile;
                    NoteTile start = tiles.First(t => t.stringIndex == 0 && t.isOpen);
                    player.TeleportTo(new Vector3(target.transform.position.x,
                        FretboardLayout.PadHeight + 0.06f, target.transform.position.z));
                    target.Play(0.6f);
                    Require(levels.CurrentStepIndex == 0, "missing P5 on SLTS keeps the step");
                    Require(levels.MistakeCount == 1, "missing P5 counts as a mistake");
                    Require(levels.Lives == 2, "missing P5 costs one life");
                    Require(Mathf.Abs(player.transform.position.x - start.transform.position.x) < 0.25f &&
                            Mathf.Abs(player.transform.position.z - start.transform.position.z) < 0.25f,
                        "missing P5 teleports to the level start pad");

                    chords.Toggle(7);
                    Require(chords.MatchesExactly(new[] { 7 }), "P5 satisfies the SLTS chord rule");
                    Require(chords.ActiveLabel.Contains("P5"), "active chord label shows P5");
                    chord = GameObject.Find("LevelChord")?.GetComponent<Text>();
                    Require(chord != null && chord.text.Contains("P5"), "HUD reflects the active P5");

                    stage = 17;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 17)
                {
                    // Let LateUpdate run the preview once with P5 selected.
                    if (EditorApplication.timeSinceStartup - stageTime < 0.15) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    ChordPreview preview = Object.FindAnyObjectByType<ChordPreview>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile target = levels.TargetTile;
                    Require(target != null, "SLTS target found for the preview test");

                    Require(preview != null && preview.HighlightedCount > 0,
                        "chord preview highlights interval pads");
                    Require(preview.IsHighlighted(tiles.First(t => t.midi == target.midi + 7)),
                        "preview highlights the P5 pad relative to the target");
                    Require(tiles.Where(t => t.midi == target.midi + 7).Any(t => t.IsHighlighted),
                        "P5 pads carry the highlight flag");

                    // J is the lower fifth, stacked under the P5 selection.
                    chords.Toggle(-7);
                    Require(chords.IsActive(-7), "J activates the lower fifth");
                    Require(chords.ActiveLabel.Contains("P5"), "label still names the lower fifth");
                    Require(chords.MatchesExactly(new[] { 7, -7 }), "P5 and the lower fifth coexist");
                    chords.Toggle(-7);
                    Require(chords.MatchesExactly(new[] { 7 }), "lower fifth toggles back off");

                    // Advance to the last P5 chord (step 19, Db3) so its lower fifth is F#2.
                    for (int i = 0; i < 18; i++)
                        levels.OnNotePlayed(levels.TargetTile);
                    Require(levels.CurrentStepIndex == 18, "SLTS advances to the last P5 chord");
                    target = levels.TargetTile;
                    Require(target != null && target.midi == 49, "last SLTS P5 root is Db3");

                    chords.Toggle(-7);
                    stage = 18;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 18)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.15) return;

                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    ChordPreview preview = Object.FindAnyObjectByType<ChordPreview>();
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile target = levels.TargetTile;

                    Require(preview != null && preview.HighlightedCount > 0,
                        "lower-fifth preview highlights pads");
                    Require(tiles.Where(t => t.midi == target.midi - 7).Any(t => t.IsHighlighted),
                        "lower-fifth pad below the target is highlighted");
                    chords.Toggle(-7);
                    Require(chords.MatchesExactly(new[] { 7 }), "lower fifth toggles back off");

                    stage = 19;
                    stageTime = EditorApplication.timeSinceStartup;
                    return;
                }

                if (stage == 19)
                {
                    if (EditorApplication.timeSinceStartup - stageTime < 0.2) return;

                    PlayerController player = Object.FindAnyObjectByType<PlayerController>();
                    LevelManager levels = Object.FindAnyObjectByType<LevelManager>();
                    ChordManager chords = Object.FindAnyObjectByType<ChordManager>();
                    TargetBeacon beacon = TargetBeacon.Instance;

                    NoteTile dbTarget = levels.TargetTile;
                    Require(dbTarget != null && dbTarget.midi == 49,
                        "SLTS step 19 is the Db3 P5 chord");
                    Require(levels.ShouldPlayChord(dbTarget), "P5 voice plays on the Db step");
                    int chordPlays = chords.ChordPlayCount;
                    dbTarget.Play(0.6f);
                    Require(chords.ChordPlayCount == chordPlays + 1,
                        "Db step sounds the held P5 chord");
                    Require(levels.CurrentStepIndex == 19, "SLTS advances to the open E");

                    NoteTile openTarget = levels.TargetTile;
                    Require(openTarget != null && openTarget.midi == 40 &&
                            openTarget.stringIndex == 0 && openTarget.fret == 0,
                        "final SLTS target is the open low E");
                    Text levelChord = GameObject.Find("LevelChord")?.GetComponent<Text>();
                    Require(levelChord != null && !levelChord.gameObject.activeSelf,
                        "open E hides the unused next-chord line (" + levelChord?.text + ")");
                    Require(chords.MatchesExactly(new[] { 7 }), "P5 remains held on the open E");
                    Require(!levels.ShouldPlayChord(openTarget),
                        "open E suppresses the held P5 voice");
                    int openChordPlays = chords.ChordPlayCount;
                    openTarget.Play(0.6f);
                    Require(chords.ChordPlayCount == openChordPlays,
                        "open E plays as a single note");
                    Require(levels.State == LevelState.Replay, "SLTS replay starts");
                    Require(player.InputLocked, "SLTS replay freezes input");
                    Require(beacon != null && !beacon.HasTarget, "SLTS replay hides the beacon");
                    Require(!levels.AllowChordInput, "SLTS replay disables manual chords");
                    Require(chords.MatchesExactly(new[] { 7 }), "replay keeps the authored P5 set");
                    ChiptuneAudio audio = Object.FindAnyObjectByType<ChiptuneAudio>();
                    Require(audio != null, "audio available for the replay render");
                    Require(audio.LastPerformanceEventCount == 40,
                        "SLTS performance clip contains 40 events (" +
                        audio.LastPerformanceEventCount + ")");
                    Require(audio.LastPerformanceSeconds > 9f,
                        "SLTS performance clip has the full length (" +
                        audio.LastPerformanceSeconds.ToString("F2") + " s)");
                    Require(audio.LastPerformancePeak > 0.5f && audio.LastPerformancePeak <= 1.001f,
                        "SLTS performance clip is audible and does not clip (peak " +
                        audio.LastPerformancePeak.ToString("F3") + ")");

                    Require(levels.SkipReplay(), "SLTS replay skips");
                    Require(levels.State == LevelState.Reveal, "SLTS reveal shows");
                    Text title = GameObject.Find("RevealTitle")?.GetComponent<Text>();
                    Text artist = GameObject.Find("RevealArtist")?.GetComponent<Text>();
                    Text prompt = GameObject.Find("RevealPrompt")?.GetComponent<Text>();
                    Require(title != null && title.text == "Smells Like Teen Spirit",
                        "reveal shows Smells Like Teen Spirit (" + title?.text + ")");
                    Require(artist != null && artist.text == "Nirvana",
                        "reveal shows Nirvana (" + artist?.text + ")");
                    Require(prompt != null && prompt.text.Contains("CHALLENGE MENU"),
                        "final reveal prompts for the challenge menu (" + prompt?.text + ")");
                    Require(levels.IsChallengeCompleted(2), "challenge 3 is marked complete");

                    levels.Advance();
                    Require(levels.State == LevelState.ChallengeSelect,
                        "the challenge 3 reveal returns to the challenge menu");
                    Require(levels.IsChallengeCompleted(2), "challenge 3 remains complete");
                    Require(levels.IsChallengeUnlocked(3), "challenge 4 stays unlocked after challenge 3");
                    Text row2 = GameObject.Find("ChallengeRow_2")?.GetComponentInChildren<Text>();
                    Text row3 = GameObject.Find("ChallengeRow_3")?.GetComponentInChildren<Text>();
                    Require(row2 != null && row2.text.Contains("SMELLS LIKE TEEN SPIRIT"),
                        "the cleared challenge 3 reveals its name (" + row2?.text + ")");
                    Require(row3 != null && row3.text.Contains("???") &&
                            !row3.text.Contains("BACK IN BLACK"),
                        "the unlocked challenge 4 stays unnamed (" + row3?.text + ")");

                    // Start Challenge 4 (Back in Black)
                    levels.ActivateMenu(MenuScreen.Challenge, 3);
                    Require(levels.State == LevelState.Playing, "challenge 4 starts from the menu");
                    Require(levels.CurrentLevelIndex == 3, "challenge 4 is active");
                    Require(levels.CurrentLevel.Title == "Back in Black", "challenge 4 is Back in Black");
                    Require(levels.CurrentLevel.Artist == "AC/DC", "challenge 4 artist is AC/DC");
                    Require(levels.CurrentLevel.Bpm == 93, "challenge 4 runs at 93 BPM");
                    Require(levels.StepCount == 30, "Back in Black has 30 steps");
                    Require(levels.Lives == LevelManager.StartingLives, "challenge 4 starts with three lives");
                    RiffPerformanceNote[] bibNotes = RiffPerformance.Build(levels.CurrentLevel);
                    Require(bibNotes.Length == 30, "Back in Black performance plays both phrases (30 events)");
                    Require(levels.ReplayEventCount == 30, "Back in Black replay reports 30 events");
                    Require(Mathf.Abs(levels.CurrentLevel.LoopLengthBeats - 16f) < 0.001f,
                        "Back in Black loops at 16 beats");
                    Require(levels.CurrentLevel.ReplayCycles == 1, "Back in Black plays both licks in one pass");
                    Require(Mathf.Abs(bibNotes[0].StartBeat) < 0.001f,
                        "Back in Black starts on beat 1");
                    Require(Mathf.Abs(bibNotes[1].StartBeat - 1.5f) < 0.001f,
                        "D5 group starts 1.5 beats after E5");
                    Require(Mathf.Abs(bibNotes[4].StartBeat - 3.5f) < 0.001f,
                        "A5 group starts 1.5 beats after the last D5");
                    Require(Mathf.Abs(bibNotes[7].StartBeat - 6.25f) < 0.001f,
                        "lick 1 starts after the A5 group");
                    Require(Mathf.Abs(bibNotes[2].StartBeat - bibNotes[1].StartBeat - 0.25f) < 0.001f &&
                            Mathf.Abs(bibNotes[3].StartBeat - bibNotes[2].StartBeat - 0.25f) < 0.001f &&
                            Mathf.Abs(bibNotes[5].StartBeat - bibNotes[4].StartBeat - 0.25f) < 0.001f &&
                            Mathf.Abs(bibNotes[6].StartBeat - bibNotes[5].StartBeat - 0.25f) < 0.001f,
                        "D5 and A5 use the same 16th down-up-down");
                    Require(Mathf.Abs(bibNotes[6].StartBeat - 4f) < 0.001f,
                        "third A5 lands on beat 4");
                    Require(Mathf.Abs(bibNotes[14].StartBeat - 7.75f) < 0.001f,
                        "lick pull-off to open G lands on beat 7.75");
                    Require(Mathf.Abs(bibNotes[15].StartBeat - 8f) < 0.001f,
                        "second phrase starts on beat 8");
                    Require(Mathf.Abs(bibNotes[22].StartBeat - 13f) < 0.001f,
                        "walk-up starts on beat 13");
                    Require(Mathf.Abs(bibNotes[bibNotes.Length - 1].StartBeat - 15.5f) < 0.02f,
                        "walk-up lick ends on beat 15.5 (" +
                        bibNotes[bibNotes.Length - 1].StartBeat.ToString("F2") + ")");
                    Require(bibNotes[0].StringIndex == 0 && bibNotes[0].Fret == 0,
                        "bar 1 opens on open low E");
                    Require(bibNotes.Skip(1).Take(3).All(n => n.StringIndex == 2 && n.Fret == 0),
                        "three D5 hits follow the E5 rest");
                    Require(bibNotes.Skip(4).Take(3).All(n => n.StringIndex == 1 && n.Fret == 0),
                        "three A5 hits follow the matching rest");
                    Require(bibNotes[7].StringIndex == 5 && bibNotes[7].Fret == 3 &&
                            bibNotes[8].StringIndex == 5 && bibNotes[8].Fret == 0 &&
                            bibNotes[9].StringIndex == 4 && bibNotes[9].Fret == 3 &&
                            bibNotes[10].StringIndex == 4 && bibNotes[10].Fret == 0 &&
                            bibNotes[11].StringIndex == 3 && bibNotes[11].Fret == 2 &&
                            bibNotes[12].StringIndex == 3 && bibNotes[12].Fret == 3 &&
                            bibNotes[13].StringIndex == 3 && bibNotes[13].Fret == 2 &&
                            bibNotes[14].StringIndex == 3 && bibNotes[14].Fret == 0,
                        "first lick is 3p0, 3p0, 2b3r2p0 finishing with the little bend");
                    Require(bibNotes[22].StringIndex == 1 && bibNotes[22].Fret == 2 &&
                            bibNotes[23].StringIndex == 0 && bibNotes[23].Fret == 4 &&
                            bibNotes[24].StringIndex == 1 && bibNotes[24].Fret == 2 &&
                            bibNotes[25].StringIndex == 0 && bibNotes[25].Fret == 5 &&
                            bibNotes[26].StringIndex == 1 && bibNotes[26].Fret == 2 &&
                            bibNotes[27].StringIndex == 0 && bibNotes[27].Fret == 6 &&
                            bibNotes[28].StringIndex == 1 && bibNotes[28].Fret == 2 &&
                            bibNotes[29].StringIndex == 0 && bibNotes[29].Fret == 7,
                        "second lick is the B pedal walk-up 4-5-6-7 on the low E");
                    Require(bibNotes.Take(7).All(n => n.HasChord && n.ChordIntervals.SequenceEqual(new[] { 7 })) &&
                            bibNotes.Skip(7).Take(8).All(n => !n.HasChord) &&
                            bibNotes.Skip(15).Take(7).All(n => n.HasChord && n.ChordIntervals.SequenceEqual(new[] { 7 })) &&
                            bibNotes.Skip(22).Take(8).All(n => !n.HasChord),
                        "chord hits keep P5; both licks are single notes");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 0 &&
                            levels.TargetTile.fret == 0,
                        "first Back in Black target is open low E");
                    chords.Toggle(7);
                    Require(chords.MatchesExactly(new[] { 7 }), "P5 chord active for Back in Black open chords");

                    // Complete Back in Black
                    int bibGuard = 0;
                    while (levels.State == LevelState.Playing && bibGuard++ < 40)
                    {
                        NoteTile t = levels.TargetTile;
                        Require(t != null, "target exists while completing Back in Black");
                        levels.OnNotePlayed(t);
                    }
                    Require(levels.State == LevelState.Replay, "challenge 4 replay starts");
                    Require(levels.SkipReplay(), "challenge 4 replay skips");
                    Require(levels.State == LevelState.Reveal, "challenge 4 reveal shows");
                    Text bibTitle = GameObject.Find("RevealTitle")?.GetComponent<Text>();
                    Text bibArtist = GameObject.Find("RevealArtist")?.GetComponent<Text>();
                    Require(bibTitle != null && bibTitle.text == "Back in Black",
                        "reveal shows Back in Black (" + bibTitle?.text + ")");
                    Require(bibArtist != null && bibArtist.text == "AC/DC",
                        "reveal shows AC/DC (" + bibArtist?.text + ")");
                    Require(levels.IsChallengeCompleted(3), "challenge 4 is marked complete");

                    levels.Advance();
                    Require(levels.State == LevelState.ChallengeSelect,
                        "the challenge 4 reveal returns to the challenge menu");
                    row3 = GameObject.Find("ChallengeRow_3")?.GetComponentInChildren<Text>();
                    Require(row3 != null && row3.text.Contains("BACK IN BLACK"),
                        "the cleared challenge 4 reveals its name (" + row3?.text + ")");

                    Text row4 = GameObject.Find("ChallengeRow_4")?.GetComponentInChildren<Text>();
                    Require(levels.IsChallengeUnlocked(4), "challenge 5 stays unlocked after challenge 4");
                    Require(row4 != null && row4.text.Contains("???") &&
                            !row4.text.Contains("DO I WANNA KNOW"),
                        "the unlocked challenge 5 stays unnamed (" + row4?.text + ")");

                    levels.ActivateMenu(MenuScreen.Challenge, 4);
                    Require(levels.State == LevelState.Playing, "challenge 5 starts from the menu");
                    Require(levels.CurrentLevelIndex == 4, "challenge 5 is active");
                    Require(levels.CurrentLevel.Title == "Do I Wanna Know?",
                        "challenge 5 is Do I Wanna Know");
                    Require(levels.CurrentLevel.Artist == "Arctic Monkeys",
                        "challenge 5 artist is Arctic Monkeys");
                    Require(levels.CurrentLevel.Bpm == 85, "challenge 5 runs at 85 BPM");
                    Require(levels.StepCount == 20, "Do I Wanna Know has 20 steps");
                    Require(Mathf.Abs(levels.CurrentLevel.LoopLengthBeats - 14f) < 0.001f,
                        "Do I Wanna Know loops at 14 beats");
                    Require(levels.TargetTile != null && levels.TargetTile.stringIndex == 0 &&
                            levels.TargetTile.fret == 1,
                        "first Do I Wanna Know target is low-E fret 1");

                    int diwkGuard = 0;
                    while (levels.State == LevelState.Playing && diwkGuard++ < 30)
                    {
                        NoteTile t = levels.TargetTile;
                        Require(t != null, "target exists while completing Do I Wanna Know");
                        levels.OnNotePlayed(t);
                    }
                    Require(levels.State == LevelState.Replay, "challenge 5 replay starts");
                    Require(levels.SkipReplay(), "challenge 5 replay skips");
                    Require(levels.State == LevelState.Reveal, "challenge 5 reveal shows");
                    Text diwkTitle = GameObject.Find("RevealTitle")?.GetComponent<Text>();
                    Text diwkArtist = GameObject.Find("RevealArtist")?.GetComponent<Text>();
                    Require(diwkTitle != null && diwkTitle.text == "Do I Wanna Know?",
                        "reveal shows Do I Wanna Know (" + diwkTitle?.text + ")");
                    Require(diwkArtist != null && diwkArtist.text == "Arctic Monkeys",
                        "reveal shows Arctic Monkeys (" + diwkArtist?.text + ")");
                    Require(levels.IsChallengeCompleted(4), "challenge 5 is marked complete");

                    levels.Advance();
                    Require(levels.State == LevelState.ChallengeSelect,
                        "the challenge 5 reveal returns to the challenge menu");
                    row4 = GameObject.Find("ChallengeRow_4")?.GetComponentInChildren<Text>();
                    Require(row4 != null && row4.text.Contains("DO I WANNA KNOW"),
                        "the cleared challenge 5 reveals its name (" + row4?.text + ")");

                    // Interval Selection Menu tests
                    levels.OpenIntervalMenu();
                    Require(levels.State == LevelState.IntervalMenu, "interval menu opens");
                    Require(player.InputLocked, "interval menu freezes player input");
                    CanvasGroup intervalMenuGrp = GameObject.Find("IntervalMenu")?.GetComponent<CanvasGroup>();
                    Require(intervalMenuGrp != null && intervalMenuGrp.alpha > 0.99f,
                        "interval menu is visible");
                    Require(GameObject.Find("IntervalRow_0") != null && GameObject.Find("IntervalRow_24") != null,
                        "interval menu shows all 25 rows");
                    Text intervalSummary = GameObject.Find("IntervalSummary")?.GetComponent<Text>();
                    Require(intervalSummary != null && intervalSummary.text.Contains("SINGLE NOTES"),
                        "interval summary starts with single notes (" + intervalSummary?.text + ")");

                    // Toggle P5 and M3 from the menu; the summary names the chord.
                    int p5Index = ChordManager.IndexOf(7);
                    int majorThirdIndex = ChordManager.IndexOf(4);
                    levels.ActivateMenu(MenuScreen.Interval, p5Index);
                    Require(chords.MatchesExactly(new[] { 7 }), "manual interval toggle works from menu");
                    Require(intervalSummary != null && intervalSummary.text.Contains("POWER CHORD"),
                        "interval summary names the power chord (" + intervalSummary?.text + ")");
                    levels.ActivateMenu(MenuScreen.Interval, majorThirdIndex);
                    Require(chords.MatchesExactly(new[] { 7, 4 }), "menu stacks the major third");
                    Require(intervalSummary != null && intervalSummary.text.Contains("MAJOR"),
                        "interval summary names the major chord (" + intervalSummary?.text + ")");
                    levels.ActivateMenu(MenuScreen.Interval, majorThirdIndex);
                    levels.ActivateMenu(MenuScreen.Interval, p5Index);
                    Require(chords.ActiveCount == 0, "menu toggles clear the active intervals");
                    Require(intervalSummary != null && intervalSummary.text.Contains("SINGLE NOTES"),
                        "interval summary returns to single notes (" + intervalSummary?.text + ")");

                    // Close interval menu
                    levels.ActivateMenu(MenuScreen.Interval, ChordManager.Intervals.Length);
                    Require(levels.State != LevelState.IntervalMenu, "interval menu closes");

                    // Easy: wrong pads are disabled, but the chord rule still applies.
                    levels.SetDifficulty(Difficulty.Easy);
                    Require(levels.CurrentDifficulty == Difficulty.Easy, "easy difficulty selected");
                    levels.StartLevel(0);
                    Require(levels.State == LevelState.Playing, "easy challenge starts");
                    Require(levels.Lives == LevelManager.StartingLives, "easy keeps the life counter");
                    NoteTile[] tiles = Object.FindObjectsByType<NoteTile>();
                    NoteTile easyWrong = tiles.First(t => t.stringIndex == 5 && t.fret == 5);
                    NoteTile easyTarget = levels.TargetTile;
                    Require(easyTarget != null, "easy target found");
                    Require(!levels.CanTriggerNote(easyWrong), "easy rejects a wrong pad");
                    easyWrong.Play(0.6f);
                    Require(levels.CurrentStepIndex == 0, "easy wrong note keeps the step");
                    Require(levels.MistakeCount == 0, "easy wrong note is disabled");
                    Require(levels.Lives == LevelManager.StartingLives, "easy wrong note costs no life");
                    Text easyStatus = GameObject.Find("LevelStatus")?.GetComponent<Text>();
                    Require(easyStatus != null && easyStatus.text.Contains("EASY") &&
                            easyStatus.text.Contains("WRONG NOTES DISABLED"),
                        "HUD explains easy rules (" + easyStatus?.text + ")");
                    easyTarget.Play(0.6f);
                    Require(levels.CurrentStepIndex == 1, "easy target note advances");

                    levels.StartLevel(2);
                    chords.Clear();
                    Require(levels.CurrentStepIndex == 0, "easy SLTS starts at step 1");
                    levels.TargetTile.Play(0.6f);
                    Require(levels.CurrentStepIndex == 0, "easy still requires the P5 chord");
                    Require(levels.MistakeCount == 1, "easy wrong chord is a mistake");
                    Require(levels.Lives == LevelManager.StartingLives, "easy wrong chord costs no life");
                    chords.Clear();

                    // Hard: the beacon is hidden, but the HUD keeps pointing at the pad.
                    levels.SetDifficulty(Difficulty.Hard);
                    Require(levels.CurrentDifficulty == Difficulty.Hard, "hard difficulty selected");
                    levels.StartLevel(0);
                    Require(levels.State == LevelState.Playing, "hard challenge starts");
                    Require(!beacon.HasTarget, "hard hides the world beacon");
                    Text hardTarget = GameObject.Find("LevelTarget")?.GetComponent<Text>();
                    Require(hardTarget != null && hardTarget.text.Contains("G2"),
                        "hard HUD still names the target (" + hardTarget?.text + ")");
                    Require(levels.Lives == LevelManager.StartingLives, "hard starts with three lives");
                    NoteTile hardWrong = tiles.First(t => t.stringIndex == 5 && t.fret == 5);
                    hardWrong.Play(0.6f);
                    Require(levels.MistakeCount == 1, "hard wrong note is a mistake");
                    Require(levels.Lives == 2, "hard wrong note costs a life");

                    // Pause overlay: resume and quit back to the challenge menu.
                    levels.TogglePause();
                    Require(levels.State == LevelState.Paused, "pause opens during a challenge");
                    Require(player.InputLocked, "pause freezes player input");
                    CanvasGroup pauseGroup = GameObject.Find("PauseMenu")?.GetComponent<CanvasGroup>();
                    Require(pauseGroup != null && pauseGroup.alpha > 0.99f, "pause overlay is visible");
                    Text pauseRow3 = GameObject.Find("PauseRow_3")?.GetComponentInChildren<Text>();
                    Require(pauseRow3 != null && pauseRow3.text == "INTERVAL SELECTION",
                        "pause menu lists interval selection (" + pauseRow3?.text + ")");
                    levels.HoverMenu(MenuScreen.Pause, 3);
                    Require(levels.PauseMenuSelection == 3, "mouse hover selects the pause interval row");
                    levels.ActivateMenu(MenuScreen.Pause, 3);
                    Require(levels.State == LevelState.IntervalMenu,
                        "interval menu opens from the pause menu");
                    Require(player.InputLocked, "pause interval menu keeps player input locked");
                    levels.ActivateMenu(MenuScreen.Interval, ChordManager.Intervals.Length);
                    Require(levels.State == LevelState.Paused,
                        "closing the interval menu returns to the pause overlay");
                    Require(pauseGroup.alpha > 0.99f, "pause overlay returns after the interval menu");
                    levels.ActivateMenu(MenuScreen.Pause, 0);
                    Require(levels.State == LevelState.Playing, "resume returns to the challenge");
                    Require(!player.InputLocked, "resume restores player input");
                    levels.TogglePause();
                    levels.ActivateMenu(MenuScreen.Pause, 2);
                    Require(levels.State == LevelState.ChallengeSelect,
                        "pause can quit to the challenge menu");

                    // Back to the main menu, then free play.
                    levels.ActivateMenu(MenuScreen.Challenge, RiffLibrary.CreateLevels().Length);
                    Require(levels.State == LevelState.MainMenu, "back returns to the main menu");
                    levels.ActivateMenu(MenuScreen.Main, 0);
                    Require(levels.State == LevelState.FreePlay, "free play starts again from the menu");
                    Require(!levels.CampaignActive, "free play is not a challenge");
                    Require(!player.InputLocked, "free play restores player input");
                    Require(!player.LandingOnlyNotes, "free play restores the saved walk-on mode");
                    Require(chords.ActiveCount == 0, "free play clears manual chords");
                    Require(levels.TargetTile == null, "free play clears the target");
                    Require(beacon != null && !beacon.HasTarget, "free play hides the beacon");
                    Text header = GameObject.Find("LevelHeader")?.GetComponent<Text>();
                    Text mode = GameObject.Find("LevelMode")?.GetComponent<Text>();
                    Require(header != null && header.text.Contains("FREE PLAY"),
                        "HUD returns to free play (" + header?.text + ")");
                    Require(mode != null && mode.text.Contains("WALK-ON"),
                        "HUD returns to walk-on (" + mode?.text + ")");

                    Debug.Log("SMOKE PASS: fretboard-only scene, mirrored layout, six audible strings, " +
                              "crunchy chord-level amp, palm-muted voice, landing + walk-on notes, ghosts, " +
                              "five camera views, chord toggles, triple jump without a flip, " +
                              "Ctrl tile retrigger, global palm/all-string mutes, respawn, " +
                              "free-play landing-only toggle, main menu, all challenges unlocked, hidden names, " +
                              "easy/medium/hard rules, three lives, out-of-lives restart, pause overlay, " +
                              "sample-accurate replays, same-pitch notes on other strings, mistake teleport, " +
                              "chord preview and song reveals verified.");
                    Finish(0);
                }
            }
            catch (System.Exception e)
            {
                Fail(e.Message + "\n" + e.StackTrace);
            }
        }

        private static float RmsRange(float[] samples, float startSeconds, float endSeconds)
        {
            if (samples == null || samples.Length == 0) return 0f;
            int start = Mathf.Clamp(Mathf.RoundToInt(startSeconds * ChiptuneSynth.SampleRate), 0, samples.Length);
            int end = Mathf.Clamp(Mathf.RoundToInt(endSeconds * ChiptuneSynth.SampleRate), start, samples.Length);
            if (end <= start) return 0f;
            double sum = 0.0;
            for (int i = start; i < end; i++)
                sum += samples[i] * (double)samples[i];
            return Mathf.Sqrt((float)(sum / (end - start)));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new System.Exception("SMOKE FAIL: " + message);
            Debug.Log("SMOKE OK: " + message);
        }

        private static void Fail(string message)
        {
            Debug.LogError("SMOKE FAIL: " + message);
            Finish(1);
        }

        private static void Finish(int code)
        {
            finished = true;
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(code);
        }
    }
}
