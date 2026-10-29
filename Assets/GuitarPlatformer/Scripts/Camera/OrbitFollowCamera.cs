using UnityEngine;
using UnityEngine.InputSystem;

namespace GuitarPlatformer
{
    public enum CameraView
    {
        Guitarist,
        FirstPerson,
        Orbit,
        PlayingTilt,
        Side
    }

    [RequireComponent(typeof(Camera))]
    public class OrbitFollowCamera : MonoBehaviour
    {
        public static OrbitFollowCamera Instance { get; private set; }

        [SerializeField] private Transform target;
        [SerializeField] private CameraView view = CameraView.Guitarist;
        [SerializeField] private float distance = 13f;
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 24f;
        [SerializeField] private float yaw;
        [SerializeField] private float pitch = 32f;
        [SerializeField] private float minPitch = 12f;
        [SerializeField] private float maxPitch = 68f;
        [SerializeField] private float lookHeight = 1.15f;
        [SerializeField] private float tiltRoll = 15f;

        private Camera cam;
        private PlayerController player;
        private InputAction viewAction;
        private float shakeTime;
        private float shakeDuration;
        private float shakeAmplitude;
        private float fovKick;
        private float baseFov = 60f;
        private float currentRoll;

        public CameraView CurrentView => view;
        public bool IsFirstPerson => view == CameraView.FirstPerson;
        public float ViewRoll => currentRoll;
        public float ViewTilt => view == CameraView.PlayingTilt ? tiltRoll : 0f;
        public string ViewName
        {
            get
            {
                switch (view)
                {
                    case CameraView.FirstPerson: return "FIRST PERSON";
                    case CameraView.Orbit: return "ORBIT";
                    case CameraView.PlayingTilt: return "PLAYING TILT";
                    case CameraView.Side: return "SIDE";
                    default: return "GUITARIST";
                }
            }
        }

        public Vector3 FlatForward
        {
            get
            {
                // Project the camera's actual view direction onto the board plane so movement
                // always follows the current camera angle, including the side preset.
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            player = followTarget != null ? followTarget.GetComponent<PlayerController>() : null;
        }

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            baseFov = cam.fieldOfView;

            viewAction = new InputAction("Camera View", InputActionType.Button);
            viewAction.AddBinding("<Keyboard>/v");
            viewAction.AddBinding("<Gamepad>/buttonNorth");
            viewAction.Enable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            viewAction?.Dispose();
        }

        private void Start()
        {
            if (target == null && PlayerController.Instance != null)
                target = PlayerController.Instance.transform;
            if (player == null && target != null)
                player = target.GetComponent<PlayerController>();

            ApplyViewDefaults();
            SetPickVisible(!IsFirstPerson);
            if (Hud.Instance != null) Hud.Instance.SetViewLabel(ViewName);
            Snap();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (viewAction != null && viewAction.WasPressedThisFrame())
                CycleView();

            float dt = Time.deltaTime;

            float lowPitch = IsFirstPerson ? -70f : minPitch;
            float highPitch = IsFirstPerson ? 75f : maxPitch;
            pitch = Mathf.Clamp(pitch, lowPitch, highPitch);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * lookHeight;
            Vector3 position = IsFirstPerson
                ? target.position + Vector3.up * 1.45f
                : pivot + orbitRotation * new Vector3(0f, 0f, -distance);

            if (shakeTime > 0f)
            {
                shakeTime = Mathf.Max(0f, shakeTime - dt);
                float strength = shakeDuration > 0f ? shakeTime / shakeDuration : 0f;
                strength *= strength;
                position += Random.insideUnitSphere * (shakeAmplitude * strength);
                fovKick = Mathf.Max(fovKick, 0.7f * strength);
            }

            currentRoll = view == CameraView.PlayingTilt ? tiltRoll : 0f;

            transform.position = position;
            Quaternion lookRotation = IsFirstPerson
                ? orbitRotation
                : Quaternion.LookRotation(pivot - position, Vector3.up);
            lookRotation *= Quaternion.Euler(0f, 0f, currentRoll);
            transform.rotation = lookRotation;

            if (cam != null)
            {
                fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 5f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + fovKick, 1f - Mathf.Exp(-8f * dt));
            }
        }

        public void CycleView()
        {
            int next = ((int)view + 1) % System.Enum.GetValues(typeof(CameraView)).Length;
            SetView((CameraView)next);
        }

        public void SetView(CameraView newView)
        {
            view = newView;
            ApplyViewDefaults();
            SetPickVisible(!IsFirstPerson);
            if (Hud.Instance != null) Hud.Instance.SetViewLabel(ViewName);
            Snap();
        }

        private void ApplyViewDefaults()
        {
            switch (view)
            {
                case CameraView.FirstPerson:
                    distance = 13f;
                    pitch = 4f;
                    yaw = 0f;
                    lookHeight = 1.45f;
                    baseFov = 72f;
                    currentRoll = 0f;
                    break;
                case CameraView.Orbit:
                    distance = 20f;
                    pitch = 46f;
                    yaw = 22f;
                    lookHeight = 1.3f;
                    baseFov = 55f;
                    currentRoll = 0f;
                    break;
                case CameraView.PlayingTilt:
                    distance = 11.5f;
                    pitch = 25f;
                    yaw = 0f;
                    lookHeight = 1.2f;
                    baseFov = 58f;
                    currentRoll = tiltRoll;
                    break;
                case CameraView.Side:
                    // Camera sits on the +X side looking across the board: the neck (-Z)
                    // projects to screen-left and the body end (+Z) to screen-right.
                    distance = 15f;
                    pitch = 28f;
                    yaw = -90f;
                    lookHeight = 1.15f;
                    baseFov = 58f;
                    currentRoll = 0f;
                    break;
                default:
                    distance = 13f;
                    pitch = 32f;
                    yaw = 0f;
                    lookHeight = 1.15f;
                    baseFov = 60f;
                    currentRoll = 0f;
                    break;
            }

            if (cam != null) cam.fieldOfView = baseFov;
        }

        private void SetPickVisible(bool visible)
        {
            if (player == null && PlayerController.Instance != null)
                player = PlayerController.Instance;
            if (player != null && player.Visual != null)
                player.Visual.SetVisible(visible);
        }

        public void Shake(float duration, float amplitude, float fov = 0.5f)
        {
            if (amplitude > shakeAmplitude || shakeTime <= 0f)
            {
                shakeDuration = duration;
                shakeTime = duration;
                shakeAmplitude = amplitude;
            }
            fovKick = Mathf.Max(fovKick, fov);
        }

        public void Snap()
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            if (IsFirstPerson)
            {
                transform.position = target.position + Vector3.up * 1.45f;
                transform.rotation = rotation * Quaternion.Euler(0f, 0f, currentRoll);
            }
            else
            {
                Vector3 pivot = target.position + Vector3.up * lookHeight;
                transform.position = pivot + rotation * new Vector3(0f, 0f, -distance);
                transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up) *
                                     Quaternion.Euler(0f, 0f, currentRoll);
            }

            if (cam != null) cam.fieldOfView = baseFov;
        }
    }
}
