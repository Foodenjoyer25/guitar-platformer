using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GuitarPlatformer.EditorTools
{
    /// <summary>
    /// Standalone GUI for authoring riff levels. It edits the same JSON format the game
    /// loads from Resources/Levels, so saved levels show up in the challenge menu without
    /// touching any C# arrays.
    /// </summary>
    public sealed class LevelCreatorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/GuitarPlatformer/Resources/Levels";
        private const string PreviewFolder = "/tmp/opencode/level-preview";
        private const int TimelineBeats = 16;
        private const float BeatWidth = 30f;
        private const float RowHeight = 28f;
        private const float HeaderHeight = 24f;
        private const float LabelWidth = 54f;
        private const float Subdivision = 0.25f;

        private static readonly string[] StringLabels = BuildStringLabels();
        private static readonly string[] IntervalLabels = BuildIntervalLabels();
        private static readonly int[] IntervalSemitones = BuildIntervalSemitones();
        private static readonly string[] ChordModeLabels =
        {
            "Inherit level chords",
            "Chord-free (single note)",
            "Custom voicing"
        };

        private RiffLevelData level;
        private string currentPath;
        private string snapshot;
        private int selectedStep = -1;
        private int timelineStartBeat;

        private Vector2 levelListScroll;
        private Vector2 stepListScroll;
        private Vector2 inspectorScroll;

        private string status = "";
        private MessageType statusType = MessageType.Info;

        private bool showRequired = true;
        private bool showTimeline = true;
        private bool showSteps = true;

        private bool dragging;
        private int dragStep = -1;
        private float dragStartBeat;
        private float dragMouseBeat;

        private readonly List<string> levelPaths = new List<string>();

        [MenuItem("Guitar Platformer/Level Creator")]
        public static void Open()
        {
            var window = GetWindow<LevelCreatorWindow>("Level Creator");
            window.minSize = new Vector2(1120f, 680f);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureFolder();
            RefreshLevelList();
            if (levelPaths.Count > 0)
                LoadLevel(levelPaths[0], false);
            else
                NewLevel(false);
        }

        private void OnGUI()
        {
            if (level == null) NewLevel(false);

            DrawToolbar();
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            DrawLevelList();
            GUILayout.Space(6f);
            DrawMainPanel();
            GUILayout.Space(6f);
            DrawInspector();
            EditorGUILayout.EndHorizontal();

            DrawStatusBar();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                NewLevel(true);
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(54f)))
                Save();
            if (GUILayout.Button("Save As", EditorStyles.toolbarButton, GUILayout.Width(66f)))
                SaveAs();
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                DeleteLevel();

            GUILayout.Space(12f);
            if (GUILayout.Button("Reload List", EditorStyles.toolbarButton, GUILayout.Width(82f)))
                RefreshLevelList();
            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(84f)))
                OpenFolder();
            if (GUILayout.Button("Import Built-in", EditorStyles.toolbarButton, GUILayout.Width(102f)))
                ShowImportMenu();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Render Preview WAV", EditorStyles.toolbarButton, GUILayout.Width(132f)))
                RenderPreview();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLevelList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(232f));
            EditorGUILayout.LabelField("LEVELS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(levelPaths.Count + " JSON file(s) in Resources/Levels", EditorStyles.miniLabel);

            levelListScroll = EditorGUILayout.BeginScrollView(levelListScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < levelPaths.Count; i++)
            {
                string path = levelPaths[i];
                bool selected = string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase);
                string label = Path.GetFileNameWithoutExtension(path);
                GUIStyle style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button(label, style, GUILayout.Height(24f)))
                    LoadLevel(path, true);
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add empty level", GUILayout.Height(26f)))
                NewLevel(true);
            if (GUILayout.Button("Duplicate current as new", GUILayout.Height(26f)))
                DuplicateAsNew();
            EditorGUILayout.EndVertical();
        }

        private void DrawMainPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField("LEVEL SETTINGS", EditorStyles.boldLabel);
            level.title = EditorGUILayout.TextField("Title", level.title);
            level.artist = EditorGUILayout.TextField("Artist", level.artist);

            EditorGUILayout.BeginHorizontal();
            level.bpm = EditorGUILayout.IntSlider("BPM", level.bpm, 40, 240, GUILayout.Width(220f));
            level.startString = EditorGUILayout.Popup("Start string", level.startString, StringLabels,
                GUILayout.Width(190f));
            level.startFret = EditorGUILayout.IntSlider("Start fret", level.startFret, 0, GuitarTuning.FretCount,
                GUILayout.Width(190f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            level.loopLengthBeats = EditorGUILayout.FloatField("Loop beats", level.loopLengthBeats,
                GUILayout.Width(170f));
            level.replayCycles = EditorGUILayout.IntSlider("Repeats", level.replayCycles, 1, 8,
                GUILayout.Width(190f));
            level.chordKeyHint = EditorGUILayout.TextField("Chord hint", level.chordKeyHint);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            showTimeline = EditorGUILayout.Foldout(showTimeline, "TIMELINE", true);
            if (showTimeline) DrawTimeline();

            EditorGUILayout.Space(6f);
            showSteps = EditorGUILayout.Foldout(showSteps, "STEPS", true);
            if (showSteps) DrawStepList();

            EditorGUILayout.EndVertical();
        }

        private void DrawTimeline()
        {
            ClampTimelineStart();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(28f)))
            {
                timelineStartBeat = Mathf.Max(0, timelineStartBeat - 4);
                Repaint();
            }

            int firstBar = timelineStartBeat / 4 + 1;
            int lastBar = (timelineStartBeat + TimelineBeats - 1) / 4 + 1;
            EditorGUILayout.LabelField("Bars " + firstBar + "–" + lastBar +
                                       "  ·  click empty grid to add  ·  drag a step to move  ·  right-click to delete",
                EditorStyles.miniLabel);

            if (GUILayout.Button("▶", GUILayout.Width(28f)))
            {
                timelineStartBeat = Mathf.Min(MaxTimelineStart, timelineStartBeat + 4);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            float width = LabelWidth + TimelineBeats * BeatWidth;
            float height = HeaderHeight + GuitarTuning.StringCount * RowHeight;
            Rect content = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            DrawTimelineContent(content);
            HandleTimelineInput(content);
        }

        private void DrawTimelineContent(Rect content)
        {
            EditorGUI.DrawRect(content, new Color(0.10f, 0.11f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(content.x, content.y, content.width, HeaderHeight),
                new Color(0.06f, 0.07f, 0.11f, 1f));

            for (int row = 0; row < GuitarTuning.StringCount; row++)
            {
                float y = content.y + HeaderHeight + row * RowHeight;
                if (row % 2 == 0)
                    EditorGUI.DrawRect(new Rect(content.x, y, content.width, RowHeight),
                        new Color(1f, 1f, 1f, 0.025f));
                EditorGUI.DrawRect(new Rect(content.x + LabelWidth, y, content.width - LabelWidth, 1f),
                    new Color(1f, 1f, 1f, 0.07f));
                GUI.Label(new Rect(content.x + 4f, y + 4f, LabelWidth - 8f, RowHeight - 4f),
                    StringLabels[row], EditorStyles.miniLabel);
            }

            for (int beat = 0; beat <= TimelineBeats; beat++)
            {
                int absolute = timelineStartBeat + beat;
                float x = content.x + LabelWidth + beat * BeatWidth;
                bool bar = absolute % 4 == 0;
                EditorGUI.DrawRect(new Rect(x, content.y, bar ? 2f : 1f, content.height),
                    bar ? new Color(0.35f, 0.95f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.09f));
                GUI.Label(new Rect(x + 2f, content.y + 3f, 34f, HeaderHeight), (absolute + 1).ToString(),
                    EditorStyles.miniLabel);
            }

            for (int sub = 0; sub < TimelineBeats * 4; sub++)
            {
                if (sub % 4 == 0) continue;
                float x = content.x + LabelWidth + sub * (BeatWidth / 4f);
                EditorGUI.DrawRect(new Rect(x, content.y + HeaderHeight, 1f, content.height - HeaderHeight),
                    new Color(1f, 1f, 1f, 0.035f));
            }

            float loopX = content.x + LabelWidth + (level.loopLengthBeats - timelineStartBeat) * BeatWidth;
            if (loopX >= content.x + LabelWidth && loopX <= content.xMax)
            {
                EditorGUI.DrawRect(new Rect(loopX - 1f, content.y, 2f, content.height),
                    new Color(1f, 0.32f, 0.55f, 0.9f));
                GUI.Label(new Rect(loopX + 3f, content.y + 3f, 70f, HeaderHeight), "LOOP",
                    EditorStyles.miniLabel);
            }

            if (level.steps == null) return;
            for (int i = 0; i < level.steps.Length; i++)
            {
                RiffStepData step = level.steps[i];
                if (step == null) continue;
                float start = Mathf.Max(step.startBeat, timelineStartBeat);
                float end = Mathf.Min(step.startBeat + step.lengthBeats, timelineStartBeat + TimelineBeats);
                if (end <= start) continue;

                float x = content.x + LabelWidth + (start - timelineStartBeat) * BeatWidth;
                int row = Mathf.Clamp(step.stringIndex, 0, GuitarTuning.StringCount - 1);
                float y = content.y + HeaderHeight + row * RowHeight + 1f;
                float w = Mathf.Max(5f, (end - start) * BeatWidth);
                Rect rect = new Rect(x, y, w, RowHeight - 3f);

                Color fill = StepColor(step);
                EditorGUI.DrawRect(rect, fill);
                if (i == selectedStep)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), Color.white);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Color.white);
                }

                if (w > 30f)
                    GUI.Label(new Rect(rect.x + 3f, rect.y, rect.width - 4f, rect.height),
                        NoteName(step), EditorStyles.miniLabel);
            }
        }

        private void HandleTimelineInput(Rect content)
        {
            Event current = Event.current;
            if (current == null) return;

            if (current.type == EventType.MouseDown && content.Contains(current.mousePosition))
            {
                Vector2 local = current.mousePosition - content.position;
                int stringIndex = Mathf.FloorToInt((local.y - HeaderHeight) / RowHeight);
                float beat = timelineStartBeat + (local.x - LabelWidth) / BeatWidth;
                if (stringIndex < 0 || stringIndex >= GuitarTuning.StringCount || beat < 0f) return;

                int hit = FindStepAt(stringIndex, beat);
                if (current.button == 1)
                {
                    if (hit >= 0) DeleteStep(hit);
                    current.Use();
                    return;
                }

                if (current.button == 0)
                {
                    if (hit >= 0)
                    {
                        SelectStep(hit);
                        dragging = true;
                        dragStep = hit;
                        dragStartBeat = level.steps[hit].startBeat;
                        dragMouseBeat = beat;
                    }
                    else
                    {
                        AddStepAt(stringIndex, Quantize(beat));
                    }
                    current.Use();
                }
                return;
            }

            if (current.type == EventType.MouseDrag && dragging && dragStep >= 0 &&
                dragStep < level.steps.Length)
            {
                Vector2 local = current.mousePosition - content.position;
                float beat = timelineStartBeat + (local.x - LabelWidth) / BeatWidth;
                float delta = Quantize(beat - dragMouseBeat);
                level.steps[dragStep].startBeat = Mathf.Max(0f, dragStartBeat + delta);
                Repaint();
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp && dragging)
            {
                dragging = false;
                dragStep = -1;
                Repaint();
            }
        }

        private void DrawStepList()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Step", GUILayout.Width(90f)))
                AddStepAt(0, NextFreeBeat(0, timelineStartBeat));
            if (GUILayout.Button("Sort by beat", GUILayout.Width(100f)))
                SortSteps();
            if (GUILayout.Button("Delete selected", GUILayout.Width(110f)))
                DeleteStep(selectedStep);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(level.steps != null ? level.steps.Length + " steps" : "0 steps",
                EditorStyles.miniLabel, GUILayout.Width(80f));
            EditorGUILayout.EndHorizontal();

            stepListScroll = EditorGUILayout.BeginScrollView(stepListScroll, GUILayout.Height(160f));
            if (level.steps != null)
            {
                for (int i = 0; i < level.steps.Length; i++)
                {
                    RiffStepData step = level.steps[i];
                    if (step == null) continue;
                    string chord = step.overrideChord
                        ? (step.chordFree ? "single" : "custom")
                        : "level";
                    string line = (i + 1).ToString("00") + "   beat " +
                                  step.startBeat.ToString("0.##") + "   len " +
                                  step.lengthBeats.ToString("0.##") + "   " +
                                  StringLabels[Mathf.Clamp(step.stringIndex, 0, 5)] + "   fret " +
                                  step.fret + "   " + NoteName(step) + "   " + chord +
                                  (step.muted ? "   MUTE" : "");
                    GUIStyle style = i == selectedStep ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    if (GUILayout.Button(line, style, GUILayout.Height(22f)))
                        SelectStep(i);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(318f));
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUILayout.ExpandHeight(true));

            showRequired = EditorGUILayout.Foldout(showRequired, "REQUIRED CHORDS", true);
            if (showRequired)
            {
                int mask = MaskFromIntervals(level.requiredIntervals);
                int newMask = EditorGUILayout.MaskField("Voicing", mask, IntervalLabels);
                if (newMask != mask)
                    level.requiredIntervals = IntervalsFromMask(newMask);
                EditorGUILayout.LabelField("Reads as", ChordTheory.Describe(level.requiredIntervals));
                if (level.requiredIntervals != null && level.requiredIntervals.Length > 0)
                    EditorGUILayout.LabelField(level.requiredIntervals.Length + " interval(s)",
                        EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("SELECTED STEP", EditorStyles.boldLabel);
            if (level.steps == null || selectedStep < 0 || selectedStep >= level.steps.Length)
            {
                EditorGUILayout.HelpBox("Select a step on the timeline or in the step list, or add one.",
                    MessageType.Info);
            }
            else
            {
                DrawStepInspector(level.steps[selectedStep]);
            }

            EditorGUILayout.Space(8f);
            DrawValidation();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStepInspector(RiffStepData step)
        {
            int newString = EditorGUILayout.Popup("String", Mathf.Clamp(step.stringIndex, 0, 5), StringLabels);
            if (newString != step.stringIndex) step.stringIndex = newString;
            step.fret = EditorGUILayout.IntSlider("Fret", step.fret, 0, GuitarTuning.FretCount);
            step.startBeat = Mathf.Max(0f, EditorGUILayout.FloatField("Start beat", step.startBeat));
            step.lengthBeats = Mathf.Max(0.01f, EditorGUILayout.FloatField("Length (beats)", step.lengthBeats));
            step.muted = EditorGUILayout.Toggle("Palm mute", step.muted);

            int mode = step.overrideChord ? (step.chordFree ? 1 : 2) : 0;
            int newMode = EditorGUILayout.Popup("Chord", mode, ChordModeLabels);
            if (newMode != mode)
            {
                step.overrideChord = newMode != 0;
                step.chordFree = newMode == 1;
                if (newMode == 2 && (step.chordIntervals == null || step.chordIntervals.Length == 0))
                    step.chordIntervals = new[] { 7 };
            }

            if (step.overrideChord && !step.chordFree)
            {
                int mask = MaskFromIntervals(step.chordIntervals);
                int newMask = EditorGUILayout.MaskField("Voicing", mask, IntervalLabels);
                if (newMask != mask)
                    step.chordIntervals = IntervalsFromMask(newMask);
                EditorGUILayout.LabelField("Reads as", ChordTheory.Describe(step.chordIntervals));
            }

            EditorGUILayout.LabelField("Note", NoteName(step));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Duplicate")) DuplicateStep(selectedStep);
            if (GUILayout.Button("Delete")) DeleteStep(selectedStep);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("-1/16")) NudgeStart(selectedStep, -Subdivision);
            if (GUILayout.Button("+1/16")) NudgeStart(selectedStep, Subdivision);
            if (GUILayout.Button("-1 beat")) NudgeStart(selectedStep, -1f);
            if (GUILayout.Button("+1 beat")) NudgeStart(selectedStep, 1f);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawValidation()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            level.Validate(errors, warnings);

            if (errors.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
            if (warnings.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", warnings), MessageType.Warning);
            if (errors.Count == 0 && warnings.Count == 0)
                EditorGUILayout.HelpBox("Level is valid and ready to save.", MessageType.Info);
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            MessageType type = statusType;
            Color previous = GUI.color;
            GUI.color = type == MessageType.Error
                ? new Color(1f, 0.55f, 0.55f)
                : type == MessageType.Warning
                    ? new Color(1f, 0.85f, 0.45f)
                    : Color.white;
            EditorGUILayout.LabelField(string.IsNullOrEmpty(status) ? "Ready." : status);
            GUI.color = previous;
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField((IsDirty ? "● UNSAVED" : "SAVED") + "  ·  " +
                                       (string.IsNullOrEmpty(currentPath) ? "not saved yet" : currentPath),
                EditorStyles.miniLabel, GUILayout.Width(460f));
            EditorGUILayout.EndHorizontal();
        }

        private void NewLevel(bool confirm)
        {
            if (confirm && !ConfirmDiscard()) return;

            level = new RiffLevelData
            {
                steps = new[]
                {
                    new RiffStepData
                    {
                        stringIndex = 0,
                        fret = 0,
                        startBeat = 0f,
                        lengthBeats = 0.5f
                    }
                }
            };
            currentPath = null;
            selectedStep = 0;
            timelineStartBeat = 0;
            snapshot = JsonUtility.ToJson(level);
            ShowStatus("New level created. Click the timeline to add steps.", MessageType.Info);
        }

        private void DuplicateAsNew()
        {
            if (level == null || !ConfirmDiscard()) return;
            level = CloneData(level);
            currentPath = null;
            level.title = string.IsNullOrEmpty(level.title) ? "New Riff" : level.title + " Copy";
            selectedStep = level.steps != null && level.steps.Length > 0 ? 0 : -1;
            timelineStartBeat = 0;
            snapshot = JsonUtility.ToJson(level);
            ShowStatus("Duplicated the current level. Save As to give it a new file.", MessageType.Info);
        }

        private void LoadLevel(string path, bool confirm)
        {
            if (confirm && !ConfirmDiscard()) return;

            try
            {
                string json = File.ReadAllText(path);
                RiffLevelData data = JsonUtility.FromJson<RiffLevelData>(json);
                if (data == null) throw new InvalidDataException("The file did not contain a level.");

                level = Normalize(data);
                currentPath = path;
                selectedStep = level.steps.Length > 0 ? 0 : -1;
                timelineStartBeat = 0;
                snapshot = JsonUtility.ToJson(level);
                ShowStatus("Loaded " + Path.GetFileName(path) + ".", MessageType.Info);
            }
            catch (Exception exception)
            {
                ShowStatus("Could not load " + Path.GetFileName(path) + ": " + exception.Message,
                    MessageType.Error);
            }
        }

        private void Save()
        {
            if (level == null) return;
            if (!RiffLevelData.TryValidate(level, out string message))
            {
                ShowStatus("Fix the level errors before saving:\n" + message, MessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(currentPath))
            {
                SaveAs();
                return;
            }

            WriteLevel(currentPath);
        }

        private void SaveAs()
        {
            if (level == null) return;
            if (!RiffLevelData.TryValidate(level, out string message))
            {
                ShowStatus("Fix the level errors before saving:\n" + message, MessageType.Error);
                return;
            }

            EnsureFolder();
            string suggested = Slug(string.IsNullOrEmpty(level.title) ? "level" : level.title);
            string path = EditorUtility.SaveFilePanelInProject("Save Riff Level", suggested, "json",
                "Save the riff as JSON", LevelsFolder);
            if (string.IsNullOrEmpty(path)) return;

            path = path.Replace('\\', '/');
            string directory = Path.GetDirectoryName(path);
            if (directory != null) directory = directory.Replace('\\', '/');
            if (!string.Equals(directory, LevelsFolder, StringComparison.OrdinalIgnoreCase))
            {
                ShowStatus("Save directly inside " + LevelsFolder + " so the game can load the level.",
                    MessageType.Error);
                return;
            }

            WriteLevel(path);
        }

        private void WriteLevel(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? LevelsFolder);
                File.WriteAllText(fullPath, JsonUtility.ToJson(level, true));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                currentPath = path.Replace('\\', '/');
                snapshot = JsonUtility.ToJson(level);
                RefreshLevelList();
                ShowStatus("Saved " + Path.GetFileName(currentPath) +
                           ". Restart Play Mode to see it in Challenges.", MessageType.Info);
            }
            catch (Exception exception)
            {
                ShowStatus("Could not save the level: " + exception.Message, MessageType.Error);
            }
        }

        private void DeleteLevel()
        {
            if (level == null) return;

            if (string.IsNullOrEmpty(currentPath))
            {
                if (!EditorUtility.DisplayDialog("Discard level?",
                        "This level has not been saved yet. Discard it?", "Discard", "Cancel"))
                    return;
                NewLevel(false);
                return;
            }

            string name = Path.GetFileNameWithoutExtension(currentPath);
            if (!EditorUtility.DisplayDialog("Delete level?",
                    "Delete '" + name + "' and its .meta file?\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            AssetDatabase.DeleteAsset(currentPath);
            AssetDatabase.Refresh();
            RefreshLevelList();
            if (levelPaths.Count > 0) LoadLevel(levelPaths[0], false);
            else NewLevel(false);
            ShowStatus("Deleted " + name + ".", MessageType.Info);
        }

        private void ShowImportMenu()
        {
            var menu = new GenericMenu();
            RiffLevel[] campaign = RiffLibrary.CreateCampaignLevels();
            for (int i = 0; i < campaign.Length; i++)
            {
                RiffLevel built = campaign[i];
                menu.AddItem(new GUIContent(built.Title), false, () => ImportBuiltIn(built));
            }
            menu.ShowAsContext();
        }

        private void ImportBuiltIn(RiffLevel built)
        {
            if (built == null || !ConfirmDiscard()) return;
            level = Normalize(RiffLevelData.FromLevel(built));
            currentPath = null;
            selectedStep = level.steps.Length > 0 ? 0 : -1;
            timelineStartBeat = 0;
            snapshot = JsonUtility.ToJson(level);
            ShowStatus("Imported '" + built.Title + "'. Save As to keep a copy.", MessageType.Info);
        }

        private void RenderPreview()
        {
            if (level == null) return;
            if (!RiffLevelData.TryValidate(level, out string message))
            {
                ShowStatus("Fix the level errors before rendering:\n" + message, MessageType.Error);
                return;
            }

            try
            {
                RiffLevel riff = level.ToLevel();
                float[] samples = ChiptuneSynth.RenderPerformance(riff);
                Directory.CreateDirectory(PreviewFolder);
                string path = Path.Combine(PreviewFolder, Slug(level.title) + ".wav");
                GuitarTonePreview.WriteWavFile(path, samples);
                ShowStatus("Preview rendered to " + path, MessageType.Info);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            {
                ShowStatus("Could not render the preview: " + exception.Message, MessageType.Error);
            }
        }

        private void OpenFolder()
        {
            EnsureFolder();
            EditorUtility.RevealInFinder(Path.GetFullPath(LevelsFolder));
        }

        private void RefreshLevelList()
        {
            levelPaths.Clear();
            if (!Directory.Exists(LevelsFolder)) return;

            string[] files = Directory.GetFiles(LevelsFolder, "*.json");
            for (int i = 0; i < files.Length; i++)
                levelPaths.Add(files[i].Replace('\\', '/'));
            levelPaths.Sort(StringComparer.OrdinalIgnoreCase);
        }

        private void EnsureFolder()
        {
            if (Directory.Exists(LevelsFolder)) return;
            Directory.CreateDirectory(LevelsFolder);
            AssetDatabase.Refresh();
        }

        private void SelectStep(int index)
        {
            if (level == null || level.steps == null || level.steps.Length == 0)
            {
                selectedStep = -1;
                return;
            }

            selectedStep = Mathf.Clamp(index, 0, level.steps.Length - 1);
            FocusStep(selectedStep);
            Repaint();
        }

        private void FocusStep(int index)
        {
            if (level == null || level.steps == null || index < 0 || index >= level.steps.Length) return;
            float beat = level.steps[index].startBeat;
            if (beat < timelineStartBeat || beat >= timelineStartBeat + TimelineBeats)
                timelineStartBeat = Mathf.Clamp(Mathf.FloorToInt(beat / 4f) * 4, 0, MaxTimelineStart);
        }

        private void AddStepAt(int stringIndex, float beat)
        {
            if (level == null) return;
            var steps = new List<RiffStepData>(level.steps ?? new RiffStepData[0]);
            var step = new RiffStepData
            {
                stringIndex = Mathf.Clamp(stringIndex, 0, GuitarTuning.StringCount - 1),
                fret = 0,
                startBeat = Mathf.Max(0f, Quantize(beat)),
                lengthBeats = 0.25f
            };
            steps.Add(step);
            level.steps = steps.ToArray();
            selectedStep = level.steps.Length - 1;
            FocusStep(selectedStep);
            Repaint();
        }

        private void DuplicateStep(int index)
        {
            if (level == null || level.steps == null || index < 0 || index >= level.steps.Length) return;
            RiffStepData copy = level.steps[index].Clone();
            copy.startBeat += copy.lengthBeats;
            var steps = new List<RiffStepData>(level.steps);
            steps.Insert(index + 1, copy);
            level.steps = steps.ToArray();
            selectedStep = index + 1;
            FocusStep(selectedStep);
            Repaint();
        }

        private void DeleteStep(int index)
        {
            if (level == null || level.steps == null || index < 0 || index >= level.steps.Length) return;
            var steps = new List<RiffStepData>(level.steps);
            steps.RemoveAt(index);
            level.steps = steps.ToArray();
            selectedStep = level.steps.Length == 0 ? -1 : Mathf.Min(index, level.steps.Length - 1);
            Repaint();
        }

        private void NudgeStart(int index, float delta)
        {
            if (level == null || level.steps == null || index < 0 || index >= level.steps.Length) return;
            level.steps[index].startBeat = Mathf.Max(0f, level.steps[index].startBeat + delta);
            FocusStep(index);
            Repaint();
        }

        private void SortSteps()
        {
            if (level == null || level.steps == null) return;
            Array.Sort(level.steps, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;
                int beat = a.startBeat.CompareTo(b.startBeat);
                return beat != 0 ? beat : a.stringIndex.CompareTo(b.stringIndex);
            });
            selectedStep = level.steps.Length > 0 ? Mathf.Clamp(selectedStep, 0, level.steps.Length - 1) : -1;
            Repaint();
        }

        private int FindStepAt(int stringIndex, float beat)
        {
            if (level == null || level.steps == null) return -1;
            for (int i = level.steps.Length - 1; i >= 0; i--)
            {
                RiffStepData step = level.steps[i];
                if (step == null || step.stringIndex != stringIndex) continue;
                if (beat >= step.startBeat && beat <= step.startBeat + step.lengthBeats)
                    return i;
            }
            return -1;
        }

        private float NextFreeBeat(int stringIndex, float start)
        {
            float beat = Mathf.Max(0f, Quantize(start));
            for (int guard = 0; guard < 512; guard++)
            {
                if (FindStepAt(stringIndex, beat) < 0) return beat;
                beat += Subdivision;
            }
            return beat;
        }

        private void ClampTimelineStart()
        {
            if (level == null) return;
            timelineStartBeat = Mathf.Clamp(timelineStartBeat, 0, MaxTimelineStart);
        }

        private int MaxTimelineStart
        {
            get
            {
                int total = TotalBeats();
                return Mathf.Max(0, total - TimelineBeats);
            }
        }

        private int TotalBeats()
        {
            float total = level != null ? Mathf.Max(4f, level.loopLengthBeats) : 4f;
            if (level != null && level.steps != null)
            {
                for (int i = 0; i < level.steps.Length; i++)
                {
                    RiffStepData step = level.steps[i];
                    if (step != null)
                        total = Mathf.Max(total, step.startBeat + step.lengthBeats);
                }
            }
            return Mathf.CeilToInt(total);
        }

        private bool IsDirty
        {
            get
            {
                if (level == null) return false;
                if (snapshot == null) return true;
                return !string.Equals(JsonUtility.ToJson(level), snapshot, StringComparison.Ordinal);
            }
        }

        private bool ConfirmDiscard()
        {
            if (!IsDirty) return true;
            return EditorUtility.DisplayDialog("Unsaved changes",
                "Discard changes to '" + (level != null ? level.title : "level") + "'?",
                "Discard", "Cancel");
        }

        private void ShowStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
            Repaint();
        }

        private static RiffLevelData Normalize(RiffLevelData data)
        {
            if (data == null) data = new RiffLevelData();
            if (data.steps == null) data.steps = new RiffStepData[0];
            if (data.requiredIntervals == null) data.requiredIntervals = new int[0];
            for (int i = 0; i < data.steps.Length; i++)
            {
                if (data.steps[i] == null) data.steps[i] = new RiffStepData();
                if (data.steps[i].chordIntervals == null)
                    data.steps[i].chordIntervals = new int[0];
            }
            return data;
        }

        private static RiffLevelData CloneData(RiffLevelData source)
        {
            if (source == null) return new RiffLevelData();
            return Normalize(RiffLevelData.FromLevel(source.ToLevel()));
        }

        private static float Quantize(float beat)
        {
            return Mathf.Round(beat / Subdivision) * Subdivision;
        }

        private static string NoteName(RiffStepData step)
        {
            if (step == null || step.stringIndex < 0 || step.stringIndex >= GuitarTuning.StringCount ||
                step.fret < 0 || step.fret > GuitarTuning.FretCount)
                return "—";
            return GuitarTuning.NoteName(GuitarTuning.MidiFor(step.stringIndex, step.fret));
        }

        private static Color StepColor(RiffStepData step)
        {
            if (step.muted) return new Color(1f, 0.55f, 0.2f, 0.9f);
            if (step.overrideChord && step.chordFree) return new Color(1f, 0.3f, 0.85f, 0.9f);
            if (step.overrideChord) return new Color(0.62f, 0.42f, 1f, 0.9f);
            if (step.stringIndex < 0 || step.stringIndex >= GuitarTuning.StringCount ||
                step.fret < 0 || step.fret > GuitarTuning.FretCount)
                return new Color(0.5f, 0.5f, 0.5f, 0.9f);

            Color color = GuitarTuning.ColorForMidi(GuitarTuning.MidiFor(step.stringIndex, step.fret));
            color.a = 0.9f;
            return color;
        }

        private static string[] BuildStringLabels()
        {
            var labels = new string[GuitarTuning.StringCount];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = GuitarTuning.GuitarStringNumbers[i] + "  ·  " + GuitarTuning.OpenNoteNames[i];
            return labels;
        }

        private static string[] BuildIntervalLabels()
        {
            var labels = new string[ChordManager.Intervals.Length];
            for (int i = 0; i < labels.Length; i++)
                labels[i] = ChordManager.Intervals[i].label + "  " + ChordManager.Intervals[i].fullName;
            return labels;
        }

        private static int[] BuildIntervalSemitones()
        {
            var values = new int[ChordManager.Intervals.Length];
            for (int i = 0; i < values.Length; i++)
                values[i] = ChordManager.Intervals[i].semitones;
            return values;
        }

        private static int MaskFromIntervals(int[] intervals)
        {
            if (intervals == null) return 0;
            int mask = 0;
            for (int i = 0; i < IntervalSemitones.Length; i++)
            {
                for (int j = 0; j < intervals.Length; j++)
                {
                    if (intervals[j] == IntervalSemitones[i])
                    {
                        mask |= 1 << i;
                        break;
                    }
                }
            }
            return mask;
        }

        private static int[] IntervalsFromMask(int mask)
        {
            var values = new List<int>();
            for (int i = 0; i < IntervalSemitones.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                    values.Add(IntervalSemitones[i]);
            }
            return values.ToArray();
        }

        private static string Slug(string value)
        {
            if (string.IsNullOrEmpty(value)) return "level";
            var builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(c)) builder.Append(c);
                else if (c == ' ' || c == '-' || c == '_') builder.Append('-');
            }

            string slug = builder.ToString().Trim('-');
            return string.IsNullOrEmpty(slug) ? "level" : slug;
        }
    }
}
