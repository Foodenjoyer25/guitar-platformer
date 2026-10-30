using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>
    /// Loads user-authored levels from Assets/GuitarPlatformer/Resources/Levels. The
    /// Level Creator writes that folder, so saved levels appear in the challenge menu
    /// alongside the built-in campaign. Invalid files are skipped with a warning instead
    /// of breaking the game.
    /// </summary>
    public static class CustomLevelLibrary
    {
        public const string ResourceFolder = "Levels";

        public static RiffLevel[] LoadAll()
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            if (assets == null || assets.Length == 0) return new RiffLevel[0];

            var levels = new List<RiffLevel>(assets.Length);
            for (int i = 0; i < assets.Length; i++)
            {
                TextAsset asset = assets[i];
                if (asset == null) continue;

                RiffLevel level = Load(asset);
                if (level != null) levels.Add(level);
            }

            levels.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
            return levels.ToArray();
        }

        public static RiffLevel Load(TextAsset asset)
        {
            if (asset == null) return null;

            RiffLevelData data;
            try
            {
                data = JsonUtility.FromJson<RiffLevelData>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Custom level '" + asset.name + "' is not valid JSON: " + exception.Message);
                return null;
            }

            if (!RiffLevelData.TryValidate(data, out string message))
            {
                Debug.LogWarning("Custom level '" + asset.name + "' was skipped: " + message);
                return null;
            }

            if (!string.IsNullOrEmpty(message))
                Debug.LogWarning("Custom level '" + asset.name + "' has warnings: " + message);

            return data.ToLevel();
        }
    }
}
