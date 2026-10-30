using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GuitarPlatformer.EditorTools
{
    /// <summary>
    /// Verifies the Level Creator's save format end to end: write JSON into
    /// Resources/Levels, load it through the runtime catalog, confirm it merges into the
    /// challenge list, then remove the temporary file.
    /// </summary>
    public static class LevelCreatorSmokeTest
    {
        private const string LevelsFolder = "Assets/GuitarPlatformer/Resources/Levels";

        [MenuItem("Guitar Platformer/Run Level Creator Smoke Test")]
        public static void Run()
        {
            bool ok = false;
            string path = null;
            try
            {
                Directory.CreateDirectory(LevelsFolder);
                path = LevelsFolder + "/__level_creator_smoke_" + Guid.NewGuid().ToString("N") + ".json";

                RiffLevel source = RiffLibrary.CreateCampaignLevels()[3];
                RiffLevelData data = RiffLevelData.FromLevel(source);
                data.title = "Creator Smoke Riff " + Guid.NewGuid().ToString("N").Substring(0, 6);
                data.steps = data.steps.Take(Mathf.Min(4, data.steps.Length)).ToArray();
                data.loopLengthBeats = 4f;

                if (!RiffLevelData.TryValidate(data, out string validation))
                {
                    Debug.LogError("LEVEL CREATOR SMOKE FAIL: generated level is invalid: " + validation);
                    return;
                }

                File.WriteAllText(path, JsonUtility.ToJson(data, true));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                RiffLevel[] custom = CustomLevelLibrary.LoadAll();
                bool loaded = custom.Any(level => level.Title == data.title);
                RiffLevel[] all = RiffLibrary.CreateLevels();
                bool merged = all.Any(level => level.Title == data.title);
                bool invalidRejected = !RiffLevelData.TryValidate(new RiffLevelData(), out _);

                if (!loaded || !merged || !invalidRejected)
                {
                    Debug.LogError("LEVEL CREATOR SMOKE FAIL: loaded=" + loaded + " merged=" + merged +
                                   " invalidRejected=" + invalidRejected + " custom=" + custom.Length +
                                   " all=" + all.Length);
                    return;
                }

                Debug.Log("LEVEL CREATOR SMOKE OK: custom=" + custom.Length + " all=" + all.Length +
                          " title=" + data.title);
                ok = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("LEVEL CREATOR SMOKE FAIL: " + exception);
            }
            finally
            {
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.Refresh();
                }

                if (Application.isBatchMode)
                    EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
