using UnityEngine;

namespace GuitarPlatformer
{
    /// <summary>Neon marker that sits on the pad the player must hit next.</summary>
    public class TargetBeacon : MonoBehaviour
    {
        public static TargetBeacon Instance { get; private set; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color BeamColor = new Color(0.2f, 1f, 0.92f);
        private static readonly Color CoreColor = new Color(1f, 0.55f, 0.95f);

        [SerializeField] private Renderer beamRenderer;
        [SerializeField] private Renderer baseRenderer;
        [SerializeField] private Renderer diamondRenderer;
        [SerializeField] private Transform diamond;

        private MaterialPropertyBlock beamBlock;
        private MaterialPropertyBlock baseBlock;
        private MaterialPropertyBlock diamondBlock;

        public NoteTile Target { get; private set; }
        public bool HasTarget => Target != null;

        private void Awake()
        {
            Instance = this;
            beamBlock = new MaterialPropertyBlock();
            baseBlock = new MaterialPropertyBlock();
            diamondBlock = new MaterialPropertyBlock();
            Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(Renderer beam, Renderer baseDisc, Transform diamondTransform, Renderer diamondFace)
        {
            beamRenderer = beam;
            baseRenderer = baseDisc;
            diamond = diamondTransform;
            diamondRenderer = diamondFace;
        }

        public void SetTarget(NoteTile tile)
        {
            Target = tile;
            if (tile == null)
            {
                Clear();
                return;
            }

            transform.position = new Vector3(tile.transform.position.x, FretboardLayout.PadHeight, tile.transform.position.z);
            gameObject.SetActive(true);
        }

        public void Clear()
        {
            Target = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Target == null) return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.6f);
            float fast = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.2f);

            Color beam = BeamColor * (1.4f + pulse * 2.2f);
            beam.a = 0.35f + pulse * 0.45f;
            if (beamRenderer != null)
            {
                beamBlock.SetColor(BaseColorId, beam);
                beamRenderer.SetPropertyBlock(beamBlock);
            }

            Color disc = BeamColor * (1.1f + fast * 1.8f);
            disc.a = 0.5f + fast * 0.4f;
            if (baseRenderer != null)
            {
                baseBlock.SetColor(BaseColorId, disc);
                baseRenderer.SetPropertyBlock(baseBlock);
            }

            Color core = CoreColor * (1.6f + fast * 2.4f);
            core.a = 1f;
            if (diamondRenderer != null)
            {
                diamondBlock.SetColor(BaseColorId, core);
                diamondRenderer.SetPropertyBlock(diamondBlock);
            }

            if (diamond != null)
            {
                diamond.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
                diamond.localPosition = new Vector3(0f, 4.25f + Mathf.Sin(Time.time * 3.1f) * 0.12f, 0f);
            }
        }
    }
}
