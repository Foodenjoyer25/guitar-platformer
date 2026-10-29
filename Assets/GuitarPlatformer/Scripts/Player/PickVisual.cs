using UnityEngine;

namespace GuitarPlatformer
{
    public class PickVisual : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Color baseTrailColor = new Color(0.25f, 0.9f, 1f);

        private PlayerController player;
        private Light glow;
        private Vector3 scale = Vector3.one;
        private float boostFlash;
        private bool visible = true;

        public bool IsVisible => visible;

        private void Awake()
        {
            player = GetComponentInParent<PlayerController>();
            glow = GetComponentInChildren<Light>(true);
            SetTrailColor(baseTrailColor);
        }

        public void Configure(TrailRenderer trailRenderer)
        {
            trail = trailRenderer;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (glow == null) glow = GetComponentInChildren<Light>(true);
            foreach (Renderer child in GetComponentsInChildren<Renderer>(true))
                child.enabled = value;
            if (glow != null) glow.enabled = value;
            if (trail != null) trail.emitting = value;
        }

        public void OnBoost()
        {
            scale = new Vector3(0.82f, 1.38f, 0.82f);
            boostFlash = 1f;
        }

        public void OnJump()
        {
            scale = new Vector3(0.78f, 1.24f, 0.78f);
        }

        public void OnDoubleJump()
        {
            scale = new Vector3(0.68f, 1.34f, 0.68f);
        }

        public void OnTripleJump()
        {
            scale = new Vector3(0.62f, 1.42f, 0.62f);
        }

        public void OnLand(float impact01)
        {
            float k = Mathf.Clamp01(impact01);
            scale = new Vector3(1f + 0.38f * k, 1f - 0.3f * k, 1f + 0.38f * k);
        }

        public void SetTrailColor(Color color)
        {
            if (trail == null) return;
            trail.startColor = Color.Lerp(color, Color.white, 0.25f);
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            scale = Vector3.Lerp(scale, Vector3.one, 1f - Mathf.Exp(-13f * dt));
            boostFlash = Mathf.MoveTowards(boostFlash, 0f, dt * 3.2f);

            float bob = player != null && player.IsGrounded ? Mathf.Sin(Time.time * 6.5f) * 0.028f : 0f;
            Vector3 targetPosition = new Vector3(0f, bob, 0f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, 1f - Mathf.Exp(-14f * dt));

            float lean = player != null ? Mathf.Clamp(player.CurrentSpeed * 1.35f, 0f, 18f) : 0f;
            Quaternion pose = Quaternion.Euler(lean, 0f, 0f);
            // In the side view the pick would be edge-on to the camera, hiding its face.
            // Billboard it there so the character stays readable; other views keep the
            // original local pose.
            OrbitFollowCamera follow = OrbitFollowCamera.Instance;
            Camera cam = follow != null && follow.CurrentView == CameraView.Side ? Camera.main : null;
            if (cam != null)
                transform.rotation = cam.transform.rotation * pose;
            else
                transform.localRotation = pose;
            transform.localScale = scale;

            if (glow != null)
                glow.intensity = 4.5f + boostFlash * 7f;
            if (trail != null)
                trail.widthMultiplier = 0.16f + boostFlash * 0.13f;
        }
    }
}
