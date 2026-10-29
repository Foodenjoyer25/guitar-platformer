using System.Collections.Generic;
using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>
    /// Highlights the pads that hold the currently selected chord intervals. The root is the
    /// pad underfoot in free play and the current target pad during the riff campaign.
    /// </summary>
    public class ChordPreview : MonoBehaviour
    {
        public static ChordPreview Instance { get; private set; }

        private readonly HashSet<NoteTile> highlighted = new HashSet<NoteTile>();
        private readonly List<NoteTile> stale = new List<NoteTile>();
        private NoteTile[] tiles;

        public int HighlightedCount => highlighted.Count;

        public bool IsHighlighted(NoteTile tile)
        {
            return tile != null && highlighted.Contains(tile);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            tiles = Object.FindObjectsByType<NoteTile>();
        }

        private void LateUpdate()
        {
            ChordManager chords = ChordManager.Instance;
            PlayerController player = PlayerController.Instance;
            LevelManager level = LevelManager.Instance;

            NoteTile root = null;
            bool show = chords != null && chords.ActiveCount > 0 && player != null;

            if (show)
            {
                if (level != null && level.State == LevelState.Playing)
                    root = level.TargetTile;
                else if (level == null || level.State == LevelState.FreePlay)
                    root = player.CurrentTile;
                else
                    show = false;
            }

            if (!show || root == null)
            {
                ClearHighlights();
                return;
            }

            if (tiles == null || tiles.Length == 0)
                tiles = Object.FindObjectsByType<NoteTile>();

            var wanted = new HashSet<NoteTile>();
            foreach (int semitones in chords.ActiveSemitones)
            {
                int midi = root.midi + semitones;
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].midi == midi)
                        wanted.Add(tiles[i]);
                }
            }

            Apply(wanted);
        }

        private void Apply(HashSet<NoteTile> wanted)
        {
            if (highlighted.SetEquals(wanted)) return;

            stale.Clear();
            foreach (NoteTile tile in highlighted)
            {
                if (!wanted.Contains(tile)) stale.Add(tile);
            }
            for (int i = 0; i < stale.Count; i++)
                stale[i].SetHighlighted(false);

            foreach (NoteTile tile in wanted)
                tile.SetHighlighted(true);

            highlighted.Clear();
            foreach (NoteTile tile in wanted)
                highlighted.Add(tile);
        }

        private void ClearHighlights()
        {
            if (highlighted.Count == 0) return;
            foreach (NoteTile tile in highlighted)
            {
                if (tile != null) tile.SetHighlighted(false);
            }
            highlighted.Clear();
        }
    }
}
