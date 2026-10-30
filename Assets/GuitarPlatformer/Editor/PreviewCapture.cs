using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GuitarPlatformer.EditorTools
{
    /// <summary>
    /// Batch-mode visual check: opens the generated scene, poses the main camera at a few
    /// viewpoints and writes PNGs to /tmp/opencode. Nothing is saved back into the scene.
    /// </summary>
    public static class PreviewCapture
    {
        private const string OutputFolder = "/tmp/opencode";

        private struct Shot
        {
            public string name;
            public Vector3 position;
            public Vector3 target;
            public float fov;
            public bool hud;
            public bool pointLights;
            public bool allLights;
            public float roll;
            public string view;

            public Shot(string name, Vector3 position, Vector3 target, float fov, bool hud, bool pointLights = true,
                bool allLights = true, float roll = 0f, string view = null)
            {
                this.name = name;
                this.position = position;
                this.target = target;
                this.fov = fov;
                this.hud = hud;
                this.pointLights = pointLights;
                this.allLights = allLights;
                this.roll = roll;
                this.view = view;
            }
        }

        [MenuItem("Guitar Platformer/Capture Previews")]
        public static void Capture()
        {
            if (!File.Exists(BuilderUtil.ScenePath))
            {
                Debug.LogError("Preview capture: scene not found at " + BuilderUtil.ScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BuilderUtil.ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("Preview capture: no main camera in scene.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);
            Canvas hud = FindHudCanvas();
            PickVisual pick = Object.FindAnyObjectByType<PickVisual>();
            PopulateSampleHud(hud);

            var shots = new[]
            {
                new Shot("preview-start", new Vector3(-1.2f, 8.5f, -14.2f), new Vector3(-1.2f, 1.7f, -3.2f), 60f, true),
                new Shot("preview-player", new Vector3(2.5f, 4.2f, -8.5f), new Vector3(-1.2f, 0.8f, -2.6f), 42f, false),
                new Shot("preview-pads", new Vector3(-4.5f, 3.6f, 6.5f), new Vector3(-1.2f, 0.35f, 13f), 40f, false),
                new Shot("preview-emission", new Vector3(-4.5f, 3.6f, 6.5f), new Vector3(-1.2f, 0.35f, 13f), 40f, false, false),
                new Shot("preview-dark-start", new Vector3(-1.2f, 8.5f, -14.2f), new Vector3(-1.2f, 1.7f, -3.2f), 60f, false, false, false),
                new Shot("preview-dark-pads", new Vector3(-4.5f, 3.6f, 6.5f), new Vector3(-1.2f, 0.35f, 13f), 40f, false, false, false),
                new Shot("preview-overview", new Vector3(0f, 72f, 38f), new Vector3(0f, 0f, 44f), 55f, false),
                new Shot("preview-view-guitarist", new Vector3(1.2f, 8.52f, -14.17f),
                    new Vector3(1.2f, 1.63f, -3.15f), 60f, true, true, true, 0f, "GUITARIST"),
                new Shot("preview-view-first-person", new Vector3(1.2f, 1.93f, -3.15f),
                    new Vector3(1.2f, 1.55f, 6.85f), 72f, true, true, true, 0f, "FIRST PERSON"),
                new Shot("preview-view-orbit", new Vector3(-4.0f, 16.02f, -16.03f),
                    new Vector3(1.2f, 1.63f, -3.15f), 55f, true, true, true, 0f, "ORBIT"),
                new Shot("preview-view-playing-tilt", new Vector3(1.2f, 6.49f, -13.57f),
                    new Vector3(1.2f, 1.63f, -3.15f), 58f, true, true, true, 15f, "PLAYING TILT"),
                new Shot("preview-view-side", new Vector3(14.44f, 8.67f, -3.15f),
                    new Vector3(1.2f, 1.63f, -3.15f), 58f, true, true, true, 0f, "SIDE")
            };

            RenderTexture previous = camera.targetTexture;
            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();

            RenderMode previousMode = hud != null ? hud.renderMode : RenderMode.ScreenSpaceOverlay;
            Camera previousCanvasCamera = hud != null ? hud.worldCamera : null;
            float previousPlane = hud != null ? hud.planeDistance : 0f;
            Light[] pointLights = Object.FindObjectsByType<Light>();
            var pointLightStates = new bool[pointLights.Length];
            for (int i = 0; i < pointLights.Length; i++)
                pointLightStates[i] = pointLights[i].enabled;

            // URP needs one frame to initialise lights/volume data after opening a scene.
            camera.transform.position = shots[0].position;
            camera.transform.rotation = Quaternion.LookRotation(shots[0].target - shots[0].position, Vector3.up);
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;

            try
            {
                foreach (Shot shot in shots)
                {
                    for (int i = 0; i < pointLights.Length; i++)
                    {
                        if (pointLights[i].type == LightType.Point)
                            pointLights[i].enabled = shot.pointLights && pointLightStates[i];
                        else
                            pointLights[i].enabled = shot.allLights && pointLightStates[i];
                    }

                    camera.transform.position = shot.position;
                    camera.transform.rotation = Quaternion.LookRotation(shot.target - shot.position, Vector3.up) *
                                               Quaternion.Euler(0f, 0f, shot.roll);
                    camera.fieldOfView = shot.fov;

                    if (hud != null && !string.IsNullOrEmpty(shot.view))
                        SetText(hud, "ViewLabel", "VIEW  ·  " + shot.view, new Color(0.3f, 0.95f, 1f));
                    if (pick != null)
                        pick.SetVisible(shot.view != "FIRST PERSON");

                    if (hud != null)
                    {
                        hud.renderMode = shot.hud ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                        hud.worldCamera = camera;
                        hud.planeDistance = 1f;
                    }

                    camera.targetTexture = rt;
                    camera.Render();
                    WriteFrame(rt, Path.Combine(OutputFolder, shot.name + ".png"));
                    camera.targetTexture = null;

                    Debug.Log("Preview capture: wrote " + shot.name + ".png");
                }
            }
            finally
            {
                camera.targetTexture = previous;
                camera.fieldOfView = 60f;
                if (hud != null)
                {
                    hud.renderMode = previousMode;
                    hud.worldCamera = previousCanvasCamera;
                    hud.planeDistance = previousPlane;
                }
                for (int i = 0; i < pointLights.Length; i++)
                    pointLights[i].enabled = pointLightStates[i];
                if (pick != null) pick.SetVisible(true);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static void WriteFrame(RenderTexture rt, string path)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(tex);
        }

        private static Canvas FindHudCanvas()
        {
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>())
            {
                if (canvas.GetComponent<Hud>() != null) return canvas;
            }
            return null;
        }

        private static void PopulateSampleHud(Canvas hud)
        {
            if (hud == null) return;

            CanvasGroup group = null;
            foreach (CanvasGroup candidate in hud.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (candidate.gameObject.name == "NotePanel")
                {
                    group = candidate;
                    break;
                }
            }
            if (group != null) group.alpha = 1f;

            SetText(hud, "NoteName", "A4", new Color(0.69f, 0.32f, 1f));
            SetText(hud, "NoteDetail", "string 1  ·  fret 5  ·  440.0 Hz", new Color(0.75f, 0.82f, 0.95f, 0.9f));
            SetText(hud, "Count", "42", Color.white);
            SetText(hud, "Recent_0", "A4", new Color(0.69f, 0.32f, 1f));
            SetText(hud, "Recent_1", "E5", new Color(0.22f, 1f, 0.53f));
            SetText(hud, "Recent_2", "C#5", new Color(1f, 0.42f, 0.17f));
            SetText(hud, "Recent_3", "G3", new Color(0.23f, 0.52f, 1f));
            SetText(hud, "Recent_4", "B4", new Color(1f, 0.3f, 0.62f));
            SetText(hud, "ViewLabel", "VIEW  ·  GUITARIST", new Color(0.3f, 0.95f, 1f));
            SetText(hud, "ChordLabel", "CHORD  ·  m3 + P5", new Color(1f, 0.48f, 0.92f, 1f));
            SetText(hud, "BoostLabel", "CTRL · PLAY TILE", new Color(0.63f, 0.72f, 0.9f, 0.82f));
            SetText(hud, "MuteLabel", "STRINGS  ·  OFF", new Color(0.63f, 0.72f, 0.9f, 0.82f));
        }

        private static void SetText(Canvas hud, string name, string value, Color color)
        {
            Transform child = hud.transform.Find(name);
            if (child == null)
            {
                foreach (Transform t in hud.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == name)
                    {
                        child = t;
                        break;
                    }
                }
            }
            if (child == null) return;
            Text text = child.GetComponent<Text>();
            if (text == null) return;
            text.text = value;
            text.color = color;
        }
    }
}
