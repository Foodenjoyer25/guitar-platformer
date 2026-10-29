using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GuitarPlatformer.EditorTools
{
    public static partial class FretboardSceneBuilder
    {
        private static GameObject BuildPlayer(BuilderAssets a, Transform root)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(root, false);
            player.transform.position = new Vector3(FretboardLayout.LaneX(2), FretboardLayout.PadHeight + 0.03f,
                FretboardLayout.OpenPadCenterZ);
            player.layer = 2;

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.5f;
            controller.radius = 0.42f;
            controller.center = new Vector3(0f, 0.75f, 0f);
            controller.slopeLimit = 55f;
            controller.stepOffset = 0.2f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            player.AddComponent<PlayerController>();

            var visualGo = new GameObject("Pick");
            visualGo.transform.SetParent(player.transform, false);
            visualGo.layer = 2;
            var visual = visualGo.AddComponent<PickVisual>();

            var filter = visualGo.AddComponent<MeshFilter>();
            filter.sharedMesh = BuilderUtil.CreatePickMesh();
            var renderer = visualGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = a.Pick;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // Eyes, highlights and mouth on both faces of the pick.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int eye = -1; eye <= 1; eye += 2)
                {
                    CreatePrimitiveChild("Eye", visualGo.transform, PrimitiveType.Sphere,
                        new Vector3(eye * 0.205f, 0.9f, side * 0.155f),
                        new Vector3(0.13f, 0.18f, 0.06f), a.PickEye, 2);
                    CreatePrimitiveChild("EyeHighlight", visualGo.transform, PrimitiveType.Sphere,
                        new Vector3(eye * 0.175f, 0.935f, side * 0.188f),
                        new Vector3(0.035f, 0.05f, 0.025f), a.PickHighlight, 2);
                }
                CreatePrimitiveChild("Mouth", visualGo.transform, PrimitiveType.Cube,
                    new Vector3(0f, 0.68f, side * 0.155f),
                    new Vector3(0.22f, 0.035f, 0.02f), a.PickEye, 2);
            }

            var glow = new GameObject("PickGlow").AddComponent<Light>();
            glow.transform.SetParent(visualGo.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(0.3f, 0.9f, 1f);
            glow.intensity = 4.5f;
            glow.range = 5f;
            glow.shadows = LightShadows.None;

            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(player.transform, false);
            trailGo.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.sharedMaterial = a.Trail;
            trail.time = 0.3f;
            trail.widthMultiplier = 0.16f;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 2;
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = true;
            trail.startColor = new Color(0.3f, 0.95f, 1f, 0.9f);
            trail.endColor = new Color(0.3f, 0.95f, 1f, 0f);
            visual.Configure(trail);

            return player;
        }

        private static void CreatePrimitiveChild(string name, Transform parent, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.layer = layer;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            BuilderUtil.RemoveCollider(go);
        }

        private static void BuildCamera(BuilderAssets a, Transform root, Transform player)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(root, false);

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.012f, 0.008f, 0.03f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.08f;
            cam.farClipPlane = 500f;
            cam.allowHDR = true;
            cam.allowMSAA = true;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;

            var pivot = player.position + Vector3.up * 1.15f;
            var rotation = Quaternion.Euler(32f, 0f, 0f);
            go.transform.position = pivot + rotation * new Vector3(0f, 0f, -13f);
            go.transform.rotation = Quaternion.LookRotation(pivot - go.transform.position, Vector3.up);

            go.AddComponent<AudioListener>();

            var echo = go.AddComponent<AudioEchoFilter>();
            echo.delay = 185f;
            echo.decayRatio = 0.28f;
            echo.dryMix = 0.92f;
            echo.wetMix = 0.12f;

            var reverb = go.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.User;
            reverb.dryLevel = 0f;
            reverb.room = -1200f;
            reverb.roomHF = -600f;
            reverb.roomLF = -1500f;
            reverb.decayTime = 1.4f;
            reverb.decayHFRatio = 0.55f;
            reverb.reflectionsLevel = -1600f;
            reverb.reflectionsDelay = 0.015f;
            reverb.reverbLevel = -900f;
            reverb.reverbDelay = 0.035f;
            reverb.diffusion = 70f;
            reverb.density = 80f;
            reverb.hfReference = 6000f;
            reverb.lfReference = 250f;

            var follow = go.AddComponent<OrbitFollowCamera>();
            follow.Configure(player);
        }

        private static Hud BuildHud(BuilderAssets a, Transform root)
        {
            var canvasGo = new GameObject("HUD");
            canvasGo.transform.SetParent(root, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var hud = canvasGo.AddComponent<Hud>();

            var fadeGo = new GameObject("Fade");
            fadeGo.transform.SetParent(canvasGo.transform, false);
            var fadeRect = fadeGo.AddComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            var fade = fadeGo.AddComponent<Image>();
            fade.color = new Color(0f, 0f, 0f, 0f);
            fade.raycastTarget = false;

            Color cyan = new Color(0.3f, 0.95f, 1f);
            Color dim = new Color(0.63f, 0.72f, 0.9f, 0.82f);

            BuilderUtil.CreateText(canvasGo.transform, "Title", "FRET HOPPER", 46,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(48f, -34f), new Vector2(720f, 60f),
                cyan, a.Font, FontStyle.Bold);

            BuilderUtil.CreateText(canvasGo.transform, "Subtitle", "a playable neon fretboard", 20,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(50f, -84f), new Vector2(720f, 30f),
                dim, a.Font);

            Text controls = BuilderUtil.CreateText(canvasGo.transform, "Controls",
                "WASD / left stick   move\n" +
                "Space / A   jump + double + triple\n" +
                "Shift / L3   sprint      Ctrl / RT   boost\n" +
                "V / Y   camera view      Backspace / Select   respawn\n" +
                "L   free play: walk-on / landing only\n" +
                "M   palm mute      N   mute all strings\n" +
                "Esc / Start   menu      Enter / A   confirm\n" +
                "\n" +
                "CHORDS  F P5   J P5\u2193   E m3   R M3\n" +
                "        T P4   Y M2   C m6   G M6\n" +
                "        H m7   Z m2   X TT   B M7   Q 8va", 17,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(50f, -126f), new Vector2(680f, 350f),
                new Color(0.78f, 0.84f, 1f, 0.88f), a.Font);
            controls.lineSpacing = 1.12f;

            Text view = BuilderUtil.CreateText(canvasGo.transform, "ViewLabel", "VIEW  ·  GUITARIST", 18,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-48f, -146f), new Vector2(320f, 28f),
                cyan, a.Font, FontStyle.Bold);

            Text chord = BuilderUtil.CreateText(canvasGo.transform, "ChordLabel", "CHORD  ·  OFF", 18,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-48f, -174f), new Vector2(420f, 28f),
                dim, a.Font, FontStyle.Bold);

            Text mute = BuilderUtil.CreateText(canvasGo.transform, "MuteLabel", "STRINGS  ·  OFF", 18,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-48f, -202f), new Vector2(420f, 28f),
                dim, a.Font, FontStyle.Bold);

            var levelPanel = new GameObject("LevelPanel");
            levelPanel.transform.SetParent(canvasGo.transform, false);
            var levelRect = levelPanel.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.5f, 1f);
            levelRect.anchorMax = new Vector2(0.5f, 1f);
            levelRect.pivot = new Vector2(0.5f, 1f);
            levelRect.anchoredPosition = new Vector2(0f, -24f);
            levelRect.sizeDelta = new Vector2(760f, 158f);
            var levelImage = levelPanel.AddComponent<Image>();
            levelImage.color = new Color(0.015f, 0.01f, 0.04f, 0.72f);
            levelImage.raycastTarget = false;
            var levelGroup = levelPanel.AddComponent<CanvasGroup>();
            levelGroup.alpha = 1f;

            Text levelHeader = BuilderUtil.CreateText(levelPanel.transform, "LevelHeader",
                "FREE PLAY  ·  ENTER STARTS THE RIFF CAMPAIGN", 20,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -9f), new Vector2(730f, 28f),
                cyan, a.Font, FontStyle.Bold);
            Text levelStep = BuilderUtil.CreateText(levelPanel.transform, "LevelStep", "", 30,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(730f, 34f),
                Color.white, a.Font, FontStyle.Bold);
            Text levelTarget = BuilderUtil.CreateText(levelPanel.transform, "LevelTarget", "", 19,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(730f, 26f),
                dim, a.Font);
            Text levelChord = BuilderUtil.CreateText(levelPanel.transform, "LevelChord", "", 16,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(24f, -102f), new Vector2(430f, 24f),
                dim, a.Font, FontStyle.Bold);
            Text levelMode = BuilderUtil.CreateText(levelPanel.transform, "LevelMode", "", 16,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-24f, -102f), new Vector2(300f, 24f),
                dim, a.Font, FontStyle.Bold);
            Text levelStatus = BuilderUtil.CreateText(levelPanel.transform, "LevelStatus", "", 17,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(730f, 24f),
                new Color(1f, 0.7f, 0.3f, 1f), a.Font, FontStyle.Bold);

            BuilderUtil.CreateText(canvasGo.transform, "CountLabel", "NOTES PLAYED", 16,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-48f, -38f), new Vector2(320f, 26f),
                dim, a.Font);
            Text count = BuilderUtil.CreateText(canvasGo.transform, "Count", "0", 54,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-48f, -62f), new Vector2(320f, 70f),
                Color.white, a.Font, FontStyle.Bold);

            Text boostLabel = BuilderUtil.CreateText(canvasGo.transform, "BoostLabel", "CTRL BOOST · READY", 16,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(50f, 40f), new Vector2(300f, 22f),
                dim, a.Font, FontStyle.Bold);

            var panel = new GameObject("NotePanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 82f);
            panelRect.sizeDelta = new Vector2(820f, 190f);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.015f, 0.01f, 0.04f, 0.72f);
            panelImage.raycastTarget = false;
            var noteGroup = panel.AddComponent<CanvasGroup>();
            noteGroup.alpha = 0f;

            Text note = BuilderUtil.CreateText(panel.transform, "NoteName", "A4", 104,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(800f, 120f),
                Color.white, a.Font, FontStyle.Bold);
            Text detail = BuilderUtil.CreateText(panel.transform, "NoteDetail", "", 25,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(800f, 34f),
                new Color(0.75f, 0.82f, 0.95f, 0.9f), a.Font);

            var recent = new Text[5];
            for (int i = 0; i < recent.Length; i++)
            {
                recent[i] = BuilderUtil.CreateText(canvasGo.transform, "Recent_" + i, "", 20,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0f),
                    new Vector2(-300f + i * 150f, 32f), new Vector2(145f, 28f),
                    new Color(0.6f, 0.7f, 0.9f, 0.5f), a.Font, FontStyle.Bold);
            }

            var errorGo = new GameObject("ErrorFlash");
            errorGo.transform.SetParent(canvasGo.transform, false);
            var errorRect = errorGo.AddComponent<RectTransform>();
            errorRect.anchorMin = Vector2.zero;
            errorRect.anchorMax = Vector2.one;
            errorRect.offsetMin = Vector2.zero;
            errorRect.offsetMax = Vector2.zero;
            var errorImage = errorGo.AddComponent<Image>();
            errorImage.color = new Color(1f, 0.12f, 0.22f, 0f);
            errorImage.raycastTarget = false;

            var revealGo = new GameObject("Reveal");
            revealGo.transform.SetParent(canvasGo.transform, false);
            var revealRect = revealGo.AddComponent<RectTransform>();
            revealRect.anchorMin = Vector2.zero;
            revealRect.anchorMax = Vector2.one;
            revealRect.offsetMin = Vector2.zero;
            revealRect.offsetMax = Vector2.zero;
            var revealGroup = revealGo.AddComponent<CanvasGroup>();
            revealGroup.alpha = 0f;
            revealGroup.blocksRaycasts = false;
            revealGroup.interactable = false;

            var revealDimGo = new GameObject("Dim");
            revealDimGo.transform.SetParent(revealGo.transform, false);
            var revealDimRect = revealDimGo.AddComponent<RectTransform>();
            revealDimRect.anchorMin = Vector2.zero;
            revealDimRect.anchorMax = Vector2.one;
            revealDimRect.offsetMin = Vector2.zero;
            revealDimRect.offsetMax = Vector2.zero;
            var revealDim = revealDimGo.AddComponent<Image>();
            revealDim.color = new Color(0.005f, 0.004f, 0.015f, 0.84f);
            revealDim.raycastTarget = true;
            var revealButton = revealDimGo.AddComponent<MenuButton>();
            revealButton.screen = MenuScreen.Reveal;

            var cardGo = new GameObject("RevealCard");
            cardGo.transform.SetParent(revealGo.transform, false);
            var cardRect = cardGo.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(980f, 420f);
            var cardImage = cardGo.AddComponent<Image>();
            cardImage.color = new Color(0.018f, 0.012f, 0.05f, 0.96f);
            cardImage.raycastTarget = false;

            Text revealComplete = BuilderUtil.CreateText(cardGo.transform, "RevealComplete", "RIFF COMPLETE", 26,
                TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(900f, 32f),
                cyan, a.Font, FontStyle.Bold);
            Text revealTitle = BuilderUtil.CreateText(cardGo.transform, "RevealTitle", "", 64,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(920f, 90f),
                Color.white, a.Font, FontStyle.Bold);
            Text revealArtist = BuilderUtil.CreateText(cardGo.transform, "RevealArtist", "", 30,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -46f), new Vector2(920f, 44f),
                new Color(1f, 0.45f, 0.85f, 1f), a.Font, FontStyle.Bold);
            Text revealPrompt = BuilderUtil.CreateText(cardGo.transform, "RevealPrompt", "", 22,
                TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(900f, 30f),
                dim, a.Font, FontStyle.Bold);

            MenuView menu = BuildMenu(canvasGo.transform, a);

            hud.Configure(note, detail, count, controls, view, chord, boostLabel, mute, recent, fade,
                noteGroup);
            hud.ConfigureCampaign(levelGroup, levelHeader, levelStep, levelTarget, levelChord, levelMode,
                levelStatus, errorImage, revealGroup, revealTitle, revealArtist, revealPrompt);
            return hud;
        }

        private static MenuView BuildMenu(Transform canvas, BuilderAssets a)
        {
            Color cyan = new Color(0.3f, 0.95f, 1f);
            Color dim = new Color(0.63f, 0.72f, 0.9f, 0.82f);
            Font font = a.Font;

            var menu = canvas.gameObject.AddComponent<MenuView>();

            CanvasGroup mainGroup = CreateMenuScreen(canvas, "MainMenu", new Color(0.005f, 0.004f, 0.015f, 0.86f),
                out Transform mainRoot);
            CanvasGroup challengeGroup = CreateMenuScreen(canvas, "ChallengeMenu",
                new Color(0.005f, 0.004f, 0.015f, 0.9f), out Transform challengeRoot);
            CanvasGroup pauseGroup = CreateMenuScreen(canvas, "PauseMenu",
                new Color(0.005f, 0.004f, 0.015f, 0.78f), out Transform pauseRoot);
            CanvasGroup outGroup = CreateMenuScreen(canvas, "OutOfLives",
                new Color(0.005f, 0.004f, 0.015f, 0.9f), out Transform outRoot);

            // Main menu.
            Transform mainCard = CreateMenuCard(mainRoot, new Vector2(860f, 560f),
                new Color(0.018f, 0.012f, 0.05f, 0.96f));
            BuilderUtil.CreateText(mainCard, "MenuTitle", "FRET HOPPER", 74, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(800f, 90f), cyan, font, FontStyle.Bold);
            BuilderUtil.CreateText(mainCard, "MenuSubtitle", "RIFF CHALLENGES", 25, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(800f, 34f), dim, font,
                FontStyle.Bold);
            MenuView.MenuRow[] mainRows =
            {
                CreateMenuRow(mainCard, "MainRow_0", "FREE PLAY", new Vector2(0f, 34f),
                    new Vector2(520f, 78f), MenuScreen.Main, 0, font),
                CreateMenuRow(mainCard, "MainRow_1", "CHALLENGES", new Vector2(0f, -58f),
                    new Vector2(520f, 78f), MenuScreen.Main, 1, font)
            };
            BuilderUtil.CreateText(mainCard, "MenuHint",
                "W / S  ·  LEFT STICK  ·  ENTER / A  ·  MOUSE", 18, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(800f, 26f), dim, font);

            // Challenge select.
            Transform challengeCard = CreateMenuCard(challengeRoot, new Vector2(1080f, 880f),
                new Color(0.018f, 0.012f, 0.05f, 0.97f));
            BuilderUtil.CreateText(challengeCard, "ChallengeTitle", "CHALLENGES", 58, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(980f, 70f), cyan, font,
                FontStyle.Bold);

            MenuView.MenuRow[] challengeRows =
            {
                CreateMenuRow(challengeCard, "ChallengeRow_0", "CHALLENGE 01  ·  ???", new Vector2(0f, 190f),
                    new Vector2(760f, 72f), MenuScreen.Challenge, 0, font, 25),
                CreateMenuRow(challengeCard, "ChallengeRow_1", "CHALLENGE 02  ·  LOCKED", new Vector2(0f, 100f),
                    new Vector2(760f, 72f), MenuScreen.Challenge, 1, font, 25),
                CreateMenuRow(challengeCard, "ChallengeRow_2", "CHALLENGE 03  ·  LOCKED", new Vector2(0f, 10f),
                    new Vector2(760f, 72f), MenuScreen.Challenge, 2, font, 25)
            };

            BuilderUtil.CreateText(challengeCard, "DifficultyTitle", "DIFFICULTY  ·  ← / → OR CLICK", 20,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -78f),
                new Vector2(760f, 28f), dim, font, FontStyle.Bold);

            MenuView.MenuRow[] difficultyRows =
            {
                CreateMenuRow(challengeCard, "DifficultyRow_0", "EASY", new Vector2(-260f, -138f),
                    new Vector2(230f, 56f), MenuScreen.Difficulty, 0, font, 22),
                CreateMenuRow(challengeCard, "DifficultyRow_1", "MEDIUM", new Vector2(0f, -138f),
                    new Vector2(230f, 56f), MenuScreen.Difficulty, 1, font, 22),
                CreateMenuRow(challengeCard, "DifficultyRow_2", "HARD", new Vector2(260f, -138f),
                    new Vector2(230f, 56f), MenuScreen.Difficulty, 2, font, 22)
            };

            Text challengeStatus = BuilderUtil.CreateText(challengeCard, "ChallengeStatus", "", 23,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -212f),
                new Vector2(960f, 34f), Color.white, font, FontStyle.Bold);
            Text difficultyDescription = BuilderUtil.CreateText(challengeCard, "DifficultyDescription", "", 17,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -252f),
                new Vector2(980f, 30f), dim, font, FontStyle.Bold);

            MenuView.MenuRow backRow = CreateMenuRow(challengeCard, "ChallengeBack", "BACK", new Vector2(0f, -330f),
                new Vector2(300f, 56f), MenuScreen.Challenge, 3, font, 22);
            BuilderUtil.CreateText(challengeCard, "ChallengeHint",
                "W / S  ·  LEFT STICK  ·  ENTER / A  ·  ESC / B  ·  MOUSE", 17, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(980f, 26f), dim, font);

            // Pause.
            Transform pauseCard = CreateMenuCard(pauseRoot, new Vector2(660f, 500f),
                new Color(0.018f, 0.012f, 0.05f, 0.97f));
            BuilderUtil.CreateText(pauseCard, "PauseTitle", "PAUSED", 56, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(600f, 70f), cyan, font,
                FontStyle.Bold);
            MenuView.MenuRow[] pauseRows =
            {
                CreateMenuRow(pauseCard, "PauseRow_0", "RESUME", new Vector2(0f, 62f),
                    new Vector2(420f, 64f), MenuScreen.Pause, 0, font, 24),
                CreateMenuRow(pauseCard, "PauseRow_1", "RESTART CHALLENGE", new Vector2(0f, -22f),
                    new Vector2(420f, 64f), MenuScreen.Pause, 1, font, 24),
                CreateMenuRow(pauseCard, "PauseRow_2", "CHALLENGE MENU", new Vector2(0f, -106f),
                    new Vector2(420f, 64f), MenuScreen.Pause, 2, font, 24)
            };
            BuilderUtil.CreateText(pauseCard, "PauseHint", "ESC / START  ·  RESUME", 17,
                TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(600f, 24f),
                dim, font);

            // Out of lives.
            Transform outCard = CreateMenuCard(outRoot, new Vector2(780f, 360f),
                new Color(0.05f, 0.008f, 0.025f, 0.98f));
            BuilderUtil.CreateText(outCard, "OutTitle", "OUT OF LIVES", 66, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(720f, 80f),
                new Color(1f, 0.32f, 0.45f, 1f), font, FontStyle.Bold);
            BuilderUtil.CreateText(outCard, "OutSubtitle", "RESTARTING CHALLENGE FROM STEP 1", 23,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(720f, 34f),
                Color.white, font, FontStyle.Bold);
            BuilderUtil.CreateText(outCard, "OutHint", "3 LIVES RESTORED", 18, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(720f, 28f), dim, font);

            menu.Configure(mainGroup, challengeGroup, pauseGroup, outGroup, mainRows, challengeRows,
                backRow, difficultyRows, pauseRows, challengeStatus, difficultyDescription);
            return menu;
        }

        private static CanvasGroup CreateMenuScreen(Transform canvas, string name, Color dimColor,
            out Transform root)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var group = go.AddComponent<CanvasGroup>();
            var dim = go.AddComponent<Image>();
            dim.color = dimColor;
            dim.raycastTarget = false;
            root = go.transform;
            return group;
        }

        private static Transform CreateMenuCard(Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go.transform;
        }

        private static MenuView.MenuRow CreateMenuRow(Transform parent, string name, string label,
            Vector2 position, Vector2 size, MenuScreen screen, int index, Font font, int fontSize = 28)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.02f, 0.015f, 0.05f, 0.9f);
            image.raycastTarget = true;

            var button = go.AddComponent<MenuButton>();
            button.screen = screen;
            button.index = index;

            Text text = BuilderUtil.CreateText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, size,
                new Color(0.78f, 0.84f, 1f, 0.92f), font, FontStyle.Bold);

            return new MenuView.MenuRow { background = image, label = text };
        }

        private static void BuildGame(BuilderAssets a, Transform root)
        {
            var go = new GameObject("Game");
            go.transform.SetParent(root, false);
            go.AddComponent<ChiptuneAudio>();
            go.AddComponent<ChordManager>();
            go.AddComponent<StringMuteManager>();
            var fx = go.AddComponent<NoteFX>();
            fx.Configure(a.Ring, a.Spark, a.Font);
            var ghosts = go.AddComponent<NoteGhosts>();
            ghosts.Configure(a.GhostPick, a.GhostEye, BuilderUtil.CreatePickMesh(), a.Font);
            go.AddComponent<GameRoot>();

            BuildTargetBeacon(a, root);
            go.AddComponent<LevelManager>();
            go.AddComponent<ChordPreview>();

            var volumeGo = new GameObject("Global Volume");
            volumeGo.transform.SetParent(root, false);
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = a.Profile;
        }

        private static void BuildEventSystem(Transform root)
        {
            var go = new GameObject("EventSystem");
            go.transform.SetParent(root, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static TargetBeacon BuildTargetBeacon(BuilderAssets a, Transform root)
        {
            var go = new GameObject("TargetBeacon");
            go.transform.SetParent(root, false);
            var beacon = go.AddComponent<TargetBeacon>();

            GameObject beam = BuilderUtil.Cylinder("Beam", go.transform, new Vector3(0f, 2.1f, 0f),
                new Vector3(0.3f, 2.1f, 0.3f), a.Beacon);
            BuilderUtil.NoShadows(beam);

            GameObject disc = BuilderUtil.Cylinder("Base", go.transform, new Vector3(0f, 0.035f, 0f),
                new Vector3(1.2f, 0.012f, 1.2f), a.Beacon);
            BuilderUtil.NoShadows(disc);

            GameObject diamond = BuilderUtil.Box("Diamond", go.transform, new Vector3(0f, 4.25f, 0f),
                new Vector3(0.55f, 0.55f, 0.55f), a.Beacon, false);
            diamond.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            BuilderUtil.NoShadows(diamond);

            beacon.Configure(beam.GetComponent<Renderer>(), disc.GetComponent<Renderer>(), diamond.transform,
                diamond.GetComponent<Renderer>());
            return beacon;
        }
    }
}
