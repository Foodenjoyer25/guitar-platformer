using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace GuitarPlatformer.EditorTools
{
    public static partial class FretboardSceneBuilder
    {
        [MenuItem("Guitar Platformer/Rebuild Scene")]
        public static void RebuildSceneMenu()
        {
            Build();
        }

        /// <summary>Batch-mode entry point: -executeMethod GuitarPlatformer.EditorTools.FretboardSceneBuilder.Build</summary>
        public static void Build()
        {
            BuilderUtil.EnsureFolders();
            Debug.Log(ChiptuneSynth.TuningReport());
            Debug.Log(ChiptuneSynth.LoudnessReport());

            BuilderAssets assets = BuilderAssets.Build();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureRenderSettings();

            var root = new GameObject("Fret Hopper");
            BuildEnvironment(assets, root.transform, out Transform notesRoot);
            BuildNotes(assets, notesRoot);
            GameObject player = BuildPlayer(assets, root.transform);
            BuildCamera(assets, root.transform, player.transform);
            Hud hud = BuildHud(assets, root.transform);
            BuildEventSystem(root.transform);
            BuildGame(assets, root.transform);
            BuildLighting(root.transform);
            BuildAtmosphere(assets, root.transform);

            EditorSceneManager.SaveScene(scene, BuilderUtil.ScenePath);
            AddSceneToBuildSettings();

            int tileCount = Object.FindObjectsByType<NoteTile>().Length;
            Debug.Log($"Guitar Platformer: scene generated with {tileCount} note pads. " +
                      $"Player at {player.transform.position}, HUD={(hud != null)}.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureRenderSettings()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.055f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.015f, 0.01f, 0.035f);
            RenderSettings.fogStartDistance = 75f;
            RenderSettings.fogEndDistance = 260f;
        }

        private static void BuildEnvironment(BuilderAssets a, Transform root, out Transform notesRoot)
        {
            Transform env = new GameObject("Environment").transform;
            env.SetParent(root, false);

            float boardLength = FretboardLayout.NeckEndZ - FretboardLayout.NeckStartZ;
            float boardCenter = (FretboardLayout.NeckStartZ + FretboardLayout.NeckEndZ) * 0.5f;

            // The fretboard is the only guitar geometry left: a thin slab with frets and inlays.
            BuilderUtil.Box("Fretboard", env, new Vector3(0f, -0.18f, boardCenter),
                new Vector3(FretboardLayout.BoardWidth, 0.36f, boardLength), a.Board);

            BuilderUtil.Box("NeonStripLeft", env,
                new Vector3(-FretboardLayout.BoardWidth * 0.5f, 0.02f, boardCenter),
                new Vector3(0.07f, 0.06f, boardLength), a.NeonCyan, false);
            BuilderUtil.Box("NeonStripRight", env,
                new Vector3(FretboardLayout.BoardWidth * 0.5f, 0.02f, boardCenter),
                new Vector3(0.07f, 0.06f, boardLength), a.NeonPink, false);
            BuilderUtil.NoShadows(GameObject.Find("NeonStripLeft"));
            BuilderUtil.NoShadows(GameObject.Find("NeonStripRight"));

            // Nut
            BuilderUtil.Box("Nut", env, new Vector3(0f, 0.1f, -0.22f),
                new Vector3(FretboardLayout.BoardWidth - 0.4f, 0.22f, 0.3f), a.Nut, false);

            // Fret wires
            for (int fret = 1; fret <= FretboardLayout.FretCount; fret++)
            {
                float z = FretboardLayout.FretPosition(fret);
                GameObject wire = BuilderUtil.Box("Fret_" + fret, env, new Vector3(0f, 0.02f, z),
                    new Vector3(FretboardLayout.BoardWidth - 0.6f, 0.045f, 0.08f), a.Fret, false);
                BuilderUtil.NoShadows(wire);
            }

            // Side marker dots on both edges so the side view keeps its inlays.
            int[] markerFrets = { 3, 5, 7, 9, 12, 15, 17, 19, 21, 24 };
            foreach (int fret in markerFrets)
            {
                float z = FretboardLayout.FretPosition(fret);
                bool doubleDot = fret == 12 || fret == 24;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 7.45f;
                    if (doubleDot)
                    {
                        CreateDot(env, a, x, z - 0.32f);
                        CreateDot(env, a, x, z + 0.32f);
                    }
                    else
                    {
                        CreateDot(env, a, x, z);
                    }
                }
            }

            notesRoot = new GameObject("Notes").transform;
            notesRoot.SetParent(root, false);

            // Grid floor far below the fretboard.
            GameObject grid = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grid.name = "SynthwaveGrid";
            grid.transform.SetParent(env, false);
            grid.transform.localPosition = new Vector3(0f, -30f, 40f);
            grid.transform.localScale = new Vector3(42f, 1f, 42f);
            grid.GetComponent<Renderer>().sharedMaterial = a.Grid;
            BuilderUtil.RemoveCollider(grid);
            BuilderUtil.NoShadows(grid);
        }

        private static void CreateDot(Transform parent, BuilderAssets a, float x, float z)
        {
            GameObject dot = BuilderUtil.Cylinder("FretDot", parent, new Vector3(x, 0.02f, z),
                new Vector3(0.2f, 0.01f, 0.2f), a.Dot);
            BuilderUtil.NoShadows(dot);
        }

        private static void BuildNotes(BuilderAssets a, Transform notesRoot)
        {
            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                for (int fret = 0; fret <= FretboardLayout.FretCount; fret++)
                {
                    bool open = fret == 0;
                    int midi = GuitarTuning.MidiFor(s, fret);
                    Color color = GuitarTuning.ColorForMidi(midi);
                    Vector3 position = FretboardLayout.PadCenter(s, fret, open);
                    Vector3 size = FretboardLayout.PadSize(fret, open);
                    Material material = a.NotePads[GuitarTuning.PitchClass(midi)];

                    GameObject rim = BuilderUtil.Box(open ? $"OpenRim_{s}" : $"Rim_{s}_{fret}", notesRoot,
                        new Vector3(position.x, FretboardLayout.PadHeight - 0.025f, position.z),
                        new Vector3(size.x + 0.12f, 0.035f, size.z + 0.12f),
                        a.PadRims[GuitarTuning.PitchClass(midi)], false);
                    BuilderUtil.NoShadows(rim);

                    GameObject pad = BuilderUtil.Box(open ? $"Open_{s}" : $"Pad_{s}_{fret}", notesRoot,
                        position, size, material);

                    var tile = pad.AddComponent<NoteTile>();
                    tile.Configure(s, fret, midi, open, color, pad.GetComponent<Renderer>(),
                        rim.GetComponent<Renderer>());
                }
            }
        }

        private static void BuildLighting(Transform root)
        {
            Transform lights = new GameObject("Lights").transform;
            lights.SetParent(root, false);

            var key = new GameObject("KeyLight");
            key.transform.SetParent(lights, false);
            key.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            var keyLight = key.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(0.62f, 0.73f, 1f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.8f;

            var fill = new GameObject("FillLight");
            fill.transform.SetParent(lights, false);
            fill.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
            var fillLight = fill.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(1f, 0.3f, 0.72f);
            fillLight.intensity = 0.32f;
            fillLight.shadows = LightShadows.None;

            AddPointLight(lights, "CyanGlowA", new Vector3(-9f, 12f, 8f), new Color(0.1f, 0.8f, 1f), 12f, 24f);
            AddPointLight(lights, "PinkGlowA", new Vector3(9f, 12f, 30f), new Color(1f, 0.15f, 0.75f), 12f, 24f);
            AddPointLight(lights, "CyanGlowB", new Vector3(-9f, 13f, 55f), new Color(0.1f, 0.8f, 1f), 12f, 24f);
            AddPointLight(lights, "PinkGlowB", new Vector3(9f, 14f, 84f), new Color(1f, 0.15f, 0.75f), 14f, 26f);
            AddPointLight(lights, "BoardEndGlow", new Vector3(0f, 12f, 76f), new Color(0.6f, 0.3f, 1f), 4f, 24f);
        }

        private static void AddPointLight(Transform parent, string name, Vector3 position, Color color,
            float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static void BuildAtmosphere(BuilderAssets a, Transform root)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 8f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.04f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.35f, 0.85f, 1f, 0.55f), new Color(1f, 0.4f, 0.9f, 0.35f));
            main.maxParticles = 450;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 26f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, 8f, 42f);
            shape.scale = new Vector3(28f, 14f, 130f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = a.Spark;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == BuilderUtil.ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(BuilderUtil.ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
