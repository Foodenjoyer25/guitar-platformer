using System.Collections;
using UnityEngine;

namespace GuitarPlatformer
{
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }

        [SerializeField] private ChiptuneAudio audioPlayer;
        [SerializeField] private float introDelay = 0.75f;

        private void Awake()
        {
            Instance = this;
            if (audioPlayer == null) audioPlayer = GetComponent<ChiptuneAudio>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(introDelay);
            if (audioPlayer == null) yield break;

            // Quick open-string strum to introduce the instrument.
            for (int s = 0; s < GuitarTuning.StringCount; s++)
            {
                Vector3 position = new Vector3(FretboardLayout.LaneX(s), 0.9f, FretboardLayout.OpenPadCenterZ);
                audioPlayer.PlayNote(s, 0, position, 0.55f);
                yield return new WaitForSeconds(0.075f);
            }
        }
    }
}
