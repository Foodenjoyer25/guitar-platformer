using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuitarPlatformer
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float sprintSpeed = 11.5f;
        [SerializeField] private float groundAcceleration = 75f;
        [SerializeField] private float airAcceleration = 32f;
        [SerializeField] private float jumpHeight = 2.5f;
        [SerializeField] private float doubleJumpHeight = 1.9f;
        [SerializeField] private float tripleJumpHeight = 2.1f;
        [SerializeField] private float gravity = -28f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.14f;
        [SerializeField] private float turnSpeed = 15f;

        [Header("Respawn")]
        [SerializeField] private float fallY = -12f;
        [SerializeField] private float spawnGrace = 0.5f;

        [Header("Note Triggering")]
        [SerializeField] private bool landingOnlyNotes;

        [Header("Boost")]
        [SerializeField] private float boostMultiplier = 1.8f;
        [SerializeField] private float boostDuration = 0.7f;

        public PickVisual Visual { get; set; }
        public bool IsGrounded { get; private set; }
        public int JumpsUsed => jumpsUsed;
        public float VerticalVelocity => verticalVelocity;
        public float CurrentSpeed => horizontalVelocity.magnitude;
        public Vector3 HorizontalVelocity => horizontalVelocity;
        public bool BoostActive => boostRemaining > 0f;
        public float BoostMultiplier => BoostActive ? boostMultiplier : 1f;
        public bool LandingOnlyNotes => landingOnlyNotes;
        public bool InputLocked => inputLocked;
        public NoteTile CurrentTile => currentTile;
        public string RespawnBinding => respawnAction != null && respawnAction.bindings.Count > 0
            ? respawnAction.bindings[0].path
            : "";

        private CharacterController controller;
        private OrbitFollowCamera gameCamera;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private bool wasGrounded;
        private float coyoteTimer;
        private float jumpBufferTimer;
        private int jumpsUsed;
        private float airTime;
        private float graceTimer;
        private bool respawning;
        private Vector3 checkpoint;

        private NoteTile currentTile;
        private NoteTile lastPlayedTile;
        private float noteDebounce;
        private float noteSuppressTimer;
        private float boostRemaining;
        private bool inputLocked;
        private bool jumpedSinceGrounded;
        private int jumpSuppressFrame = -1;

        private InputAction moveAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction boostAction;
        private InputAction respawnAction;

        private void Awake()
        {
            Instance = this;
            controller = GetComponent<CharacterController>();
            Visual = GetComponentInChildren<PickVisual>();
            CreateInput();
            checkpoint = transform.position;
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
            sprintAction?.Enable();
            boostAction?.Enable();
            respawnAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            jumpAction?.Disable();
            sprintAction?.Disable();
            boostAction?.Disable();
            respawnAction?.Disable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            moveAction?.Dispose();
            jumpAction?.Dispose();
            sprintAction?.Dispose();
            boostAction?.Dispose();
            respawnAction?.Dispose();
        }

        private void Start()
        {
            gameCamera = Object.FindAnyObjectByType<OrbitFollowCamera>();
            wasGrounded = true;
            IsGrounded = true;
            graceTimer = spawnGrace;
            noteSuppressTimer = 0.3f;
            jumpedSinceGrounded = false;
        }

        private void CreateInput()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            moveAction.AddBinding("<Gamepad>/leftStick");

            jumpAction = new InputAction("Jump", InputActionType.Button);
            jumpAction.AddBinding("<Keyboard>/space");
            jumpAction.AddBinding("<Gamepad>/buttonSouth");

            sprintAction = new InputAction("Sprint", InputActionType.Button);
            sprintAction.AddBinding("<Keyboard>/leftShift");
            sprintAction.AddBinding("<Gamepad>/leftStickPress");

            boostAction = new InputAction("Boost", InputActionType.Button);
            boostAction.AddBinding("<Keyboard>/leftCtrl");
            boostAction.AddBinding("<Keyboard>/rightCtrl");
            boostAction.AddBinding("<Gamepad>/rightTrigger");

            // R is the major-third chord toggle now; respawn moved to Backspace / Select.
            respawnAction = new InputAction("Respawn", InputActionType.Button);
            respawnAction.AddBinding("<Keyboard>/backspace");
            respawnAction.AddBinding("<Gamepad>/select");
        }

        private void Update()
        {
            if (respawning) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            graceTimer = Mathf.Max(0f, graceTimer - dt);
            noteDebounce = Mathf.Max(0f, noteDebounce - dt);
            noteSuppressTimer = Mathf.Max(0f, noteSuppressTimer - dt);
            boostRemaining = Mathf.Max(0f, boostRemaining - dt);

            // Riff replay and the reveal card freeze the character completely.
            if (inputLocked)
            {
                horizontalVelocity = Vector3.zero;
                verticalVelocity = 0f;
                return;
            }

            if (respawnAction.WasPressedThisFrame())
            {
                StartCoroutine(RespawnRoutine());
                return;
            }

            if (transform.position.y < fallY)
            {
                StartCoroutine(RespawnRoutine());
                return;
            }

            Vector2 input = moveAction.ReadValue<Vector2>();
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 forward = gameCamera != null ? gameCamera.FlatForward : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 wish = forward * input.y + right * input.x;

            if (boostAction.WasPressedThisFrame() && wish.sqrMagnitude > 0.01f)
                TryStartBoost();

            float speed = (sprintAction.IsPressed() ? sprintSpeed : moveSpeed) * BoostMultiplier;
            float acceleration = IsGrounded ? groundAcceleration : airAcceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, wish * speed, acceleration * dt);

            if (wish.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * dt));
            }

            if (jumpAction.WasPressedThisFrame() && Time.frameCount != jumpSuppressFrame)
                jumpBufferTimer = jumpBufferTime;
            else
                jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);

            if (jumpAction.WasReleasedThisFrame() && verticalVelocity > 0f)
                verticalVelocity *= 0.45f;

            if (IsGrounded)
                coyoteTimer = coyoteTime;
            else
                coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);

            if (jumpBufferTimer > 0f && TryJump())
                jumpBufferTimer = 0f;

            verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -45f);

            Vector3 motion = (horizontalVelocity + Vector3.up * verticalVelocity) * dt;
            controller.Move(motion);

            if (verticalVelocity > 0.05f)
            {
                IsGrounded = false;
            }
            else
            {
                IsGrounded = controller.isGrounded || GroundProbe(out _);
            }

            if (IsGrounded)
            {
                float impact = Mathf.Clamp01(airTime * 2.6f);
                bool justLanded = !wasGrounded && airTime > 0.08f && graceTimer <= 0f;
                if (justLanded)
                    Land(impact);

                if (verticalVelocity < 0f) verticalVelocity = -2f;
                jumpsUsed = 0;
                airTime = 0f;
                coyoteTimer = coyoteTime;

                // Challenge mode is stricter than a normal landing: it only counts a
                // landing that follows a jump. Walking off a pad or dropping into a gap
                // must not fire a note, otherwise the campaign feels like walk-on.
                bool noteLanding = justLanded && (!landingOnlyNotes || jumpedSinceGrounded);
                jumpedSinceGrounded = false;
                UpdateGroundNote(noteLanding, justLanded ? Mathf.Max(impact, 0.35f) : 0.45f);
            }
            else
            {
                airTime += dt;
                // Leaving the ground clears the pad so landing or stepping back on replays it.
                lastPlayedTile = null;
                currentTile = null;
            }

            wasGrounded = IsGrounded;
        }

        /// <summary>
        /// Attempts the next jump in the ground -> double -> triple chain.
        /// Returns false when all jumps are spent.
        /// </summary>
        public bool TryJump()
        {
            if (IsGrounded || coyoteTimer > 0f)
            {
                Jump(jumpHeight);
                jumpsUsed = 1;
                return true;
            }

            if (jumpsUsed < 2)
            {
                Jump(doubleJumpHeight);
                jumpsUsed = 2;
                if (Visual != null) Visual.OnDoubleJump();
                return true;
            }

            if (jumpsUsed < 3)
            {
                Jump(tripleJumpHeight);
                jumpsUsed = 3;
                if (Visual != null) Visual.OnTripleJump();
                return true;
            }

            return false;
        }

        private void Jump(float height)
        {
            verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(gravity) * height);
            IsGrounded = false;
            wasGrounded = false;
            coyoteTimer = 0f;
            jumpedSinceGrounded = true;
            if (Visual != null) Visual.OnJump();
        }

        private bool GroundProbe(out RaycastHit hit)
        {
            Vector3 origin = transform.position + Vector3.up * 0.30f;
            return Physics.SphereCast(origin, 0.28f, Vector3.down, out hit, 0.25f,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
        }

        private void Land(float impact)
        {
            if (Visual != null) Visual.OnLand(impact);

            // In free play a bare-board landing becomes the new respawn point. During a
            // riff the checkpoint must stay on the last correct pad.
            bool freePlay = LevelManager.Instance == null || LevelManager.Instance.State != LevelState.Playing;
            if (freePlay && TileUnderfoot() == null)
                checkpoint = transform.position + Vector3.up * 0.06f;

            if (gameCamera != null)
                gameCamera.Shake(0.1f, 0.015f + impact * 0.03f, 0.15f);
        }

        /// <summary>
        /// Fires a note whenever the player stands on a pad they were not already standing on.
        /// In walk-on mode this covers landings and stepping between pads. In landing-only
        /// mode only a fresh landing counts, which is what the riff campaign uses.
        /// </summary>
        private void UpdateGroundNote(bool justLanded, float impact)
        {
            NoteTile tile = TileUnderfoot();

            // Adopt whatever pad we spawned/respawned/teleported on without replaying it.
            if (noteSuppressTimer > 0f)
            {
                currentTile = tile;
                lastPlayedTile = tile;
                return;
            }

            currentTile = tile;
            if (tile == null)
            {
                // Stepping onto bare board re-arms the pad we came from.
                lastPlayedTile = null;
                return;
            }

            if (landingOnlyNotes && !justLanded) return;
            if (tile == lastPlayedTile || noteDebounce > 0f) return;

            lastPlayedTile = tile;
            noteDebounce = 0.08f;
            checkpoint = new Vector3(tile.transform.position.x, FretboardLayout.PadHeight + 0.06f,
                tile.transform.position.z);
            tile.Play(impact);
            if (Visual != null) Visual.SetTrailColor(tile.noteColor);
            if (gameCamera != null)
                gameCamera.Shake(0.08f, 0.03f + impact * 0.05f, 0.55f);
        }

        private NoteTile TileUnderfoot()
        {
            if (!GroundProbe(out RaycastHit hit)) return null;
            NoteTile tile = hit.collider.GetComponent<NoteTile>();
            if (tile == null) tile = hit.collider.GetComponentInParent<NoteTile>();
            return tile;
        }

        /// <summary>
        /// Starts a short speed burst, or refreshes it when one is already running.
        /// There is no cooldown or stamina meter: pressing boost again restarts the burst.
        /// Input calls this while moving; tests may bypass that gate.
        /// </summary>
        public bool TryStartBoost(bool ignoreMovement = false)
        {
            if (!ignoreMovement && horizontalVelocity.sqrMagnitude < 0.25f) return false;

            boostRemaining = boostDuration;
            if (Visual != null) Visual.OnBoost();
            if (gameCamera != null) gameCamera.Shake(0.1f, 0.025f, 0.9f);
            return true;
        }

        public void SetLandingOnly(bool value)
        {
            landingOnlyNotes = value;
            if (Hud.Instance != null)
                Hud.Instance.SetNoteMode(value ? "LANDING ONLY" : "WALK-ON");
        }

        public void ToggleNoteMode()
        {
            SetLandingOnly(!landingOnlyNotes);
        }

        /// <summary>Freezes or restores movement, jumping and respawn input.</summary>
        public void SetInputLocked(bool value)
        {
            if (inputLocked == value) return;
            inputLocked = value;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            jumpBufferTimer = 0f;
            if (!value) jumpSuppressFrame = Time.frameCount;
        }

        /// <summary>Moves the player without replaying the destination pad or changing progress.</summary>
        public void TeleportTo(Vector3 position, bool setCheckpoint = true)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            transform.position = position;
            if (controller != null) controller.enabled = true;

            horizontalVelocity = Vector3.zero;
            verticalVelocity = -2f;
            jumpsUsed = 0;
            airTime = 0f;
            wasGrounded = true;
            IsGrounded = true;
            graceTimer = 0.2f;
            noteDebounce = 0f;
            noteSuppressTimer = 0.3f;
            jumpedSinceGrounded = false;
            currentTile = null;
            lastPlayedTile = null;
            if (setCheckpoint) checkpoint = position;
            if (gameCamera != null) gameCamera.Snap();
        }

        public void SetCheckpoint(Vector3 position)
        {
            checkpoint = position;
        }

        private IEnumerator RespawnRoutine()
        {
            if (respawning) yield break;
            respawning = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;

            if (Hud.Instance != null) yield return Hud.Instance.FadeOut(0.2f);
            else yield return new WaitForSeconds(0.2f);

            controller.enabled = false;
            transform.position = checkpoint + Vector3.up * 0.08f;
            controller.enabled = true;

            wasGrounded = true;
            IsGrounded = true;
            jumpsUsed = 0;
            airTime = 0f;
            verticalVelocity = -2f;
            horizontalVelocity = Vector3.zero;
            graceTimer = 0.45f;
            boostRemaining = 0f;
            noteDebounce = 0f;
            noteSuppressTimer = 0.4f;
            jumpedSinceGrounded = false;
            currentTile = null;
            lastPlayedTile = null;

            if (gameCamera != null) gameCamera.Snap();
            if (Hud.Instance != null) yield return Hud.Instance.FadeIn(0.28f);
            else yield return new WaitForSeconds(0.28f);

            respawning = false;
        }
    }
}
