using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        private const string PendingLevelKey = "GuitarPlatformer.PendingLevelJson";
        private const string GameScene = "Assets/GuitarPlatformer/Scenes/GuitarFretboard.unity";
        private const int TimelineBeats = 16;
        private const float BeatWidth = 30f;
        private const float RowHeight = 28f;
        private const float HeaderHeight = 24f;
        private const float LabelWidth = 54f;
        private const float Subdivision = 0.25f;
        private const float ResizeEdge = 7f;
        private const float FretCell = 22f;

        private enum DragKind
        {
            None,
            Move,
            ResizeStart,
            ResizeEnd
        }

        private static readonly string[] StringLabels = BuildStringLabels();
        private static readonly string[] IntervalLabels = BuildIntervalLabels();
        private static readonly int[] IntervalSemitones = BuildIntervalSemitones();
        private static readonly int[] MarkerFrets = { 3, 5, 7, 9, 12, 15, 17, 19, 21, 24 };
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
        private int placeFret;

        private Vector2 levelListScroll;
        private Vector2 stepListScroll;
        private Vector2 inspectorScroll;
        private Vector2 fretboardScroll;

        private string status = "";
        private MessageType statusType = MessageType.Info;

        private bool showRequired = true;
        private bool showTimeline = true;
        private bool showFretboard = true;
        private bool showSteps = true;
        private bool auditionOnSelect = true;

        private DragKind dragKind;
        private int dragStep = -1;
        private float dragStartBeat;
        private float dragLength;
        private float dragMouseBeat;

        private readonly List<string> levelPaths = new List<string>();

        [MenuItem("Guitar Platformer/Level Creator")]
        public static void Open()
        {
            var window = GetWindow<LevelCreatorWindow>("Level Creator");
            window.minSize = new Vector2(1120f, 720f);
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

        private void OnDisable()
        {
            EditorAudioPreview.Stop();
        }

        private void OnGUI()
        {
            if (level == null) NewLevel(false);
            HandleShortcuts();

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

        private void HandleShortcuts()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown) return;
            if (EditorGUIUtility.editingTextField) return;

            bool control = current.control || current.command;
            if (control && current.keyCode == KeyCode.S)
            {
                Save();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace)
            {
                DeleteStep(selectedStep);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.D)
            {
                DuplicateStep(selectedStep);
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.Space)
            {
                if (control) PlayRiff();
                else AuditionSelected();
                current.Use();
                return;
            }

            if (current.keyCode == KeyCode.LeftArrow)
            {
                NudgeStart(selectedStep, current.shift ? -1f : -Subdivision);
                current.Use();
            }
            else if (current.keyCode == KeyCode.RightArrow)
            {
                NudgeStart(selectedStep, current.shift ? 1f : Subdivision);
                current.Use();
            }
            else if (current.keyCode == KeyCode.UpArrow)
            {
                NudgeString(selectedStep, -1);
                current.Use();
            }
            else if (current.keyCode == KeyCode.DownArrow)
            {
                NudgeString(selectedStep, 1);
                current.Use();
            }
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
            auditionOnSelect = GUILayout.Toggle(auditionOnSelect, "Hear clicks", EditorStyles.toolbarButton,
                GUILayout.Width(88f));
            if (GUILayout.Button("Play Step", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                AuditionSelected();
            if (GUILayout.Button("Play Riff", EditorStyles.toolbarButton, GUILayout.Width(78f)))
                PlayRiff();
            if (GUILayout.Button("Play in Game", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                PlayInGame();
            if (GUILayout.Button("WAV", EditorStyles.toolbarButton, GUILayout.Width(44f)))
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
            showFretboard = EditorGUILayout.Foldout(showFretboard, "FRETBOARD  ·  click a pad to set string and fret",
                true);
            if (showFretboard) DrawFretboard();

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
                                       "  ·  click empty to add  ·  drag to move  ·  drag edges to resize  ·  right-click to delete",
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
                Rect rect = StepRect(content, step);
                if (rect.width <= 0f) continue;

                Color fill = StepColor(step);
                EditorGUI.DrawRect(rect, fill);
                if (i == selectedStep)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), Color.white);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Color.white);
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), Color.white);
                    EditorGUI.DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Color.white);
                }

                if (rect.width > 30f)
                    GUI.Label(new Rect(rect.x + 3f, rect.y, rect.width - 4f, rect.height),
                        NoteName(step), EditorStyles.miniLabel);
            }
        }

        private Rect StepRect(Rect content, RiffStepData step)
        {
            float start = Mathf.Max(step.startBeat, timelineStartBeat);
            float end = Mathf.Min(step.startBeat + step.lengthBeats, timelineStartBeat + TimelineBeats);
            if (end <= start) return new Rect(0f, 0f, 0f, 0f);

            float x = content.x + LabelWidth + (start - timelineStartBeat) * BeatWidth;
            int row = Mathf.Clamp(step.stringIndex, 0, GuitarTuning.StringCount - 1);
            float y = content.y + HeaderHeight + row * RowHeight + 1f;
            float w = Mathf.Max(5f, (end - start) * BeatWidth);
            return new Rect(x, y, w, RowHeight - 3f);
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
                        RiffStepData step = level.steps[hit];
                        Rect rect = StepRect(content, step);
                        dragKind = DragKind.Move;
                        if (current.mousePosition.x >= rect.xMax - ResizeEdge) dragKind = DragKind.ResizeEnd;
                        else if (current.mousePosition.x <= rect.xMin + ResizeEdge) dragKind = DragKind.ResizeStart;
                        dragStep = hit;
                        dragStartBeat = step.startBeat;
                        dragLength = step.lengthBeats;
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

            if (current.type == EventType.MouseDrag && dragKind != DragKind.None &&
                dragStep >= 0 && dragStep < level.steps.Length)
            {
                Vector2 local = current.mousePosition - content.position;
                float beat = timelineStartBeat + (local.x - LabelWidth) / BeatWidth;
                int stringIndex = Mathf.FloorToInt((local.y - HeaderHeight) / RowHeight);
                RiffStepData step = level.steps[dragStep];

                if (dragKind == DragKind.Move)
                {
                    float delta = Quantize(beat - dragMouseBeat);
                    step.startBeat = Mathf.Max(0f, dragStartBeat + delta);
                    if (stringIndex >= 0 && stringIndex < GuitarTuning.StringCount)
                        step.stringIndex = stringIndex;
                }
                else if (dragKind == DragKind.ResizeEnd)
                {
                    step.lengthBeats = Mathf.Max(Subdivision, Quantize(beat - step.startBeat));
                }
                else if (dragKind == DragKind.ResizeStart)
                {
                    float end = dragStartBeat + dragLength;
                    float start = Mathf.Max(0f, Quantize(beat));
                    if (start > end - Subdivision) start = end - Subdivision;
                    step.startBeat = Mathf.Max(0f, start);
                    step.lengthBeats = Mathf.Max(Subdivision, end - step.startBeat);
                }

                Repaint();
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp && dragKind != DragKind.None)
            {
                dragKind = DragKind.None;
                dragStep = -1;
                Repaint();
            }
        }

        private void DrawFretboard()
        {
            float width = LabelWidth + (GuitarTuning.FretCount + 1) * FretCell;
            float height = HeaderHeight + GuitarTuning.StringCount * FretCell;
            fretboardScroll = EditorGUILayout.BeginScrollView(fretboardScroll, GUILayout.Height(height + 8f));
            Rect content = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(content, new Color(0.08f, 0.07f, 0.05f, 1f));

            for (int fret = 0; fret <= GuitarTuning.FretCount; fret++)
            {
                float x = content.x + LabelWidth + fret * FretCell;
                bool marker = Array.IndexOf(MarkerFrets, fret) >= 0;
                GUI.Label(new Rect(x, content.y + 2f, FretCell, HeaderHeight - 2f),
                    fret == 0 ? "0" : fret.ToString(), EditorStyles.centeredGreyMiniLabel);
                if (marker)
                    EditorGUI.DrawRect(new Rect(x + FretCell * 0.5f - 1f, content.y + HeaderHeight, 2f,
                        content.height - HeaderHeight), new Color(1f, 1f, 1f, 0.18f));
            }

            RiffStepData selected = SelectedStepData();
            int selectedString = selected != null ? selected.stringIndex : -1;
            int selectedFret = selected != null ? selected.fret : placeFret;

            for (int row = 0; row < GuitarTuning.StringCount; row++)
            {
                float y = content.y + HeaderHeight + row * FretCell;
                GUI.Label(new Rect(content.x + 4f, y + 2f, LabelWidth - 6f, FretCell - 2f),
                    StringLabels[row], EditorStyles.miniLabel);

                for (int fret = 0; fret <= GuitarTuning.FretCount; fret++)
                {
                    Rect cell = new Rect(content.x + LabelWidth + fret * FretCell + 1f, y + 1f,
                        FretCell - 2f, FretCell - 2f);
                    Color color = GuitarTuning.ColorForMidi(GuitarTuning.MidiFor(row, fret));
                    color.a = row == selectedString && fret == selectedFret ? 1f : 0.35f;
                    EditorGUI.DrawRect(cell, color);
                    if (row == selectedString && fret == selectedFret)
                    {
                        EditorGUI.DrawRect(new Rect(cell.x, cell.y, cell.width, 2f), Color.white);
                        EditorGUI.DrawRect(new Rect(cell.x, cell.yMax - 2f, cell.width, 2f), Color.white);
                    }

                    if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition) &&
                        Event.current.button == 0)
                    {
                        placeFret = fret;
                        if (selected != null)
                        {
                            selected.stringIndex = row;
                            selected.fret = fret;
                            if (auditionOnSelect) AuditionStep(selected);
                        }
                        else
                        {
                            AddStepAt(row, NextFreeBeat(row, timelineStartBeat));
                            if (selectedStep >= 0)
                            {
                                level.steps[selectedStep].fret = fret;
                                if (auditionOnSelect) AuditionStep(level.steps[selectedStep]);
                            }
                        }

                        Event.current.Use();
                        Repaint();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("Brush fret " + placeFret + "  ·  new timeline notes use this fret",
                EditorStyles.miniLabel);
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
            EditorGUILayout.HelpBox(
                "Shortcuts: Space plays the step, Ctrl+Space the riff, Ctrl+S saves, D duplicates, Delete removes, arrows nudge.",
                MessageType.None);
            DrawValidation();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStepInspector(RiffStepData step)
        {
            int newString = EditorGUILayout.Popup("String", Mathf.Clamp(step.stringIndex, 0, 5), StringLabels);
            if (newString != step.stringIndex) step.stringIndex = newString;
            step.fret = EditorGUILayout.IntSlider("Fret", step.fret, 0, GuitarTuning.FretCount);
            placeFret = step.fret;
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
            if (GUILayout.Button("Hear")) AuditionStep(step);
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
            placeFret = 0;
            snapshot = JsonUtility.ToJson(level);
            ShowStatus("New level created. Click the fretboard to pick a pad, then click the timeline.",
                MessageType.Info);
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
                placeFret = SelectedStepData() != null ? SelectedStepData().fret : 0;
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
            placeFret = SelectedStepData() != null ? SelectedStepData().fret : 0;
            snapshot = JsonUtility.ToJson(level);
            ShowStatus("Imported '" + built.Title + "'. Save As to keep a copy.", MessageType.Info);
        }

        private void AuditionSelected()
        {
            RiffStepData step = SelectedStepData();
            if (step == null)
            {
                ShowStatus("Select a step to hear it.", MessageType.Warning);
                return;
            }

            AuditionStep(step);
        }

        private void AuditionStep(RiffStepData step)
        {
            if (step == null || level == null) return;
            if (step.stringIndex < 0 || step.stringIndex >= GuitarTuning.StringCount ||
                step.fret < 0 || step.fret > GuitarTuning.FretCount)
                return;

            int midi = GuitarTuning.MidiFor(step.stringIndex, step.fret);
            int[] intervals = step.overrideChord
                ? (step.chordFree ? new int[0] : step.chordIntervals)
                : level.requiredIntervals;
            float seconds = Mathf.Clamp(60f / Mathf.Max(1, level.bpm) * step.lengthBeats + 0.3f, 0.2f, 2.2f);
            float[] samples = ChiptuneSynth.GenerateChord(midi, step.stringIndex, intervals, step.muted, seconds);
            EditorAudioPreview.Play(samples, ChiptuneSynth.SampleRate);
            ShowStatus("Playing " + NoteName(step) + ".", MessageType.Info);
        }

        private void PlayRiff()
        {
            if (level == null) return;
            if (!RiffLevelData.TryValidate(level, out string message))
            {
                ShowStatus("Fix the level errors before playing:\n" + message, MessageType.Error);
                return;
            }

            try
            {
                float[] samples = ChiptuneSynth.RenderPerformance(level.ToLevel());
                EditorAudioPreview.Play(samples, ChiptuneSynth.SampleRate);
                ShowStatus("Playing the riff.", MessageType.Info);
            }
            catch (Exception exception)
            {
                ShowStatus("Could not play the riff: " + exception.Message, MessageType.Error);
            }
        }

        private void PlayInGame()
        {
            if (level == null) return;
            if (!RiffLevelData.TryValidate(level, out string message))
            {
                ShowStatus("Fix the level errors before playing in game:\n" + message, MessageType.Error);
                return;
            }

            RiffLevel riff = level.ToLevel();
            if (EditorApplication.isPlaying)
            {
                CustomLevelSession.Queue(riff);
                LevelManager manager = UnityEngine.Object.FindAnyObjectByType<LevelManager>();
                if (manager != null) manager.StartCustomLevel(CustomLevelSession.Consume());
                ShowStatus("Started '" + riff.Title + "' in the running game.", MessageType.Info);
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorSceneManager.GetActiveScene().path != GameScene)
                EditorSceneManager.OpenScene(GameScene);

            SessionState.SetString(PendingLevelKey, JsonUtility.ToJson(level));
            CustomLevelSession.Queue(riff);
            EditorApplication.isPlaying = true;
            ShowStatus("Entering Play Mode with '" + riff.Title + "'.", MessageType.Info);
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

        private RiffStepData SelectedStepData()
        {
            if (level == null || level.steps == null || selectedStep < 0 || selectedStep >= level.steps.Length)
                return null;
            return level.steps[selectedStep];
        }

        private void SelectStep(int index)
        {
            if (level == null || level.steps == null || level.steps.Length == 0)
            {
                selectedStep = -1;
                return;
            }

            selectedStep = Mathf.Clamp(index, 0, level.steps.Length - 1);
            RiffStepData step = level.steps[selectedStep];
            if (step != null) placeFret = step.fret;
            FocusStep(selectedStep);
            if (auditionOnSelect && step != null && Event.current != null &&
                Event.current.type != EventType.MouseDrag)
                AuditionStep(step);
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
                fret = Mathf.Clamp(placeFret, 0, GuitarTuning.FretCount),
                startBeat = Mathf.Max(0f, Quantize(beat)),
                lengthBeats = 0.25f
            };
            steps.Add(step);
            level.steps = steps.ToArray();
            selectedStep = level.steps.Length - 1;
            FocusStep(selectedStep);
            if (auditionOnSelect) AuditionStep(step);
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

        private void NudgeString(int index, int delta)
        {
            if (level == null || level.steps == null || index < 0 || index >= level.steps.Length) return;
            level.steps[index].stringIndex = Mathf.Clamp(level.steps[index].stringIndex + delta, 0,
                GuitarTuning.StringCount - 1);
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
