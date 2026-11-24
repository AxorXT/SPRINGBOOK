using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public sealed class SpringPlayerController : MonoBehaviour
{
    [SerializeField] InputActionAsset actions;
    [SerializeField] Transform groundCheck;
    [SerializeField] Camera movementCamera;
    [SerializeField] LayerMask groundLayers = 1;
    [Header("Locomotion")]
    [Min(0)] public float speed = 4.4f;
    [Min(0)] public float acceleration = 32f;
    [Min(0)] public float braking = 40f;
    [Range(0, 1)] public float airControl = .23f;
    [Min(.01f)] public float hopHeight = .16f;
    [Min(0)] public float groundPause = .07f;
    [Header("Charged jump")]
    [Min(.01f)] public float chargeDuration = .85f;
    [Min(0)] public float minimumJumpHeight = .42f;
    [Min(.01f)] public float maximumJumpHeight = 2.75f;
    [Range(0, 1)] public float chargeSpeedMultiplier = .50f;
    [Range(0, 1)] public float chargeHopHeightMultiplier = .30f;
    [Header("Ground and recovery")]
    [Range(.02f, .3f)] public float probeRadius = .15f;
    [Range(.01f, .15f)] public float groundTolerance = .045f;
    public float fallThreshold = -4f;

    public bool SlamLaunch { get; private set; }
    public void ConsumeSlamLaunch() { SlamLaunch = false; }
    public bool ChargePending => phase != ChargePhase.Idle;
    public bool Grounded { get; private set; }
    public float Charge { get; private set; }
    public bool Charging => phase == ChargePhase.Building || phase == ChargePhase.ReleaseOnLanding;
    public Vector3 Velocity => body.linearVelocity;
    public event System.Action<float> Launched, Landed;
    public event System.Action Respawned;

    public RunLoot Loot { get; set; }
    PlayerStats stats;
    SpringCombatController combat;
    public event System.Action Rebounded;
    public bool BookClearance(MeshCollider board, CapsuleCollider capsule, out float clearance)
    {
        clearance = 0;
        if (!board || (groundLayers.value & (1 << board.gameObject.layer)) == 0) return false;
        var bounds = capsule.bounds;
        Vector3 foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        const float inset = .1f;
        if (!board.Raycast(new Ray(foot + Vector3.up * inset, Vector3.down), out var hit, Mathf.Infinity)
            || hit.normal.y <= .65f) return false;
        clearance = foot.y - hit.point.y;
        return true;
    }
    public void AbsorbImpact() { Landed?.Invoke(1f); }
    public void Rebound(float upwardSpeed)
    {
        SetLaunchVelocity(upwardSpeed);
        SlamLaunch = true;
        Rebounded?.Invoke();
    }

    Rigidbody body;
    InputActionMap controls;
    InputAction move, jump;
    Vector3 spawn;
    Quaternion spawnRotation;
    enum ChargePhase { Idle, StartOnLanding, Building, ReleaseOnLanding }
    ChargePhase phase;
    bool automaticHop;
    float lastFloorContact = float.NegativeInfinity;
    float nextHop, ignoreGroundUntil, lastFall;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        combat = GetComponent<SpringCombatController>();
        body = GetComponent<Rigidbody>();
        spawn = body.position;
        spawnRotation = body.rotation;
        if (!movementCamera) movementCamera = Camera.main;
        controls = actions.FindActionMap("Player", true).Clone();
        move = controls.FindAction("Move", true);
        jump = controls.FindAction("Jump", true);
        jump.performed += OnPress;
        jump.canceled += OnRelease;
    }

    void OnEnable() { move.Enable(); jump.Enable(); }
    void OnDisable()
    {
        move.Disable();
        jump.Disable();
        ResetCharge();
    }
    void OnDestroy() { controls.Dispose(); }
    void OnPress(InputAction.CallbackContext context)
    {
        if (Loot && Loot.BlockInput) return;
        if (phase == ChargePhase.Idle && (!combat || combat.State == SpringCombatController.Attack.Idle))
            phase = ChargePhase.StartOnLanding;
    }

    void OnRelease(InputAction.CallbackContext context)
    {
        if (phase != ChargePhase.Idle) phase = ChargePhase.ReleaseOnLanding;
    }

    void ResetCharge()
    {
        phase = ChargePhase.Idle;
        Charge = 0;
    }

    void OnCollisionEnter(Collision collision) { RecordFloorContact(collision); }
    void OnCollisionStay(Collision collision) { RecordFloorContact(collision); }

    void RecordFloorContact(Collision collision)
    {
        if ((groundLayers.value & (1 << collision.gameObject.layer)) == 0) return;
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y > .65f)
            {
                if (Time.fixedTime >= ignoreGroundUntil && body.linearVelocity.y <= .01f) SlamLaunch = false;
                lastFloorContact = Time.fixedTime;
                return;
            }
    }

    public void Respawn()
    {
        body.position = spawn;
        body.rotation = spawnRotation;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        ResetCharge();
        SlamLaunch = false;
        Grounded = automaticHop = false;
        lastFloorContact = float.NegativeInfinity;
        lastFall = ignoreGroundUntil = 0;
        nextHop = Time.fixedTime + groundPause;
        Respawned?.Invoke();
    }

    void FixedUpdate()
    {
        if (Loot && Loot.BlockInput) return;
        if (body.position.y < fallThreshold)
        {
            Respawn();
            return;
        }

        float dt = Time.fixedDeltaTime;
        float now = Time.fixedTime;
        Vector3 velocity = body.linearVelocity;
        Vector3 origin = groundCheck.position + Vector3.up * (probeRadius + .12f);
        bool previous = Grounded;
        Grounded = now >= ignoreGroundUntil && velocity.y < .8f &&
            Physics.SphereCast(origin, probeRadius, Vector3.down, out RaycastHit hit,
                .12f + groundTolerance, groundLayers, QueryTriggerInteraction.Ignore)
            && hit.normal.y > .65f;
        // The ordinary ground probe reaches almost as high as a charging hop.
        // Wait for a supporting contact before consuming a buffered landing.
        if (phase != ChargePhase.Idle && !previous)
            Grounded &= lastFloorContact >= now - dt * 1.5f;
        if (Grounded) { automaticHop = false; SlamLaunch = false; }
        if (Grounded && !previous)
        {
            Landed?.Invoke(Mathf.Clamp01(lastFall / 6f));
            nextHop = now + groundPause;
            lastFall = 0;
        }
        if (!Grounded) lastFall = Mathf.Max(lastFall, -velocity.y);
        if (combat && combat.isActiveAndEnabled && combat.Step(phase != ChargePhase.Idle && Grounded))
        {
            ResetCharge();
            return;
        }
        if (phase == ChargePhase.StartOnLanding && Grounded) phase = ChargePhase.Building;
        if (phase == ChargePhase.Building && (Grounded || automaticHop))
            Charge = Mathf.Clamp01(Charge + dt / Mathf.Max(.01f, chargeDuration));

        Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
        Vector3 forward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up).normalized;
        Vector3 desired = (forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x) * speed;
        if (Charging) desired *= chargeSpeedMultiplier;
        Vector3 planar = new Vector3(velocity.x, 0, velocity.z);
        float rate = desired.sqrMagnitude > .001f ? acceleration : braking;
        planar = Vector3.MoveTowards(planar, desired, rate * (Grounded ? 1 : airControl) * dt);
        body.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        if (desired.sqrMagnitude > .01f)
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(desired), 600f * dt));

        if (phase == ChargePhase.ReleaseOnLanding && Grounded)
        {
            float height = Mathf.Lerp(minimumJumpHeight, maximumJumpHeight, Charge);
            ResetCharge();
            Launch(height, stats.JumpMultiplier);
            SlamLaunch = true;
        }
        else if (Grounded && input.sqrMagnitude > .01f && now >= nextHop)
        {
            float height = Charging ? hopHeight * chargeHopHeightMultiplier : hopHeight;
            if (height > 0)
            {
                Launch(height);
                automaticHop = true;
            }
        }
    }

    void Launch(float height, float velocityMultiplier = 1f)
    {
        SetLaunchVelocity(Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * height) * velocityMultiplier);
        Launched?.Invoke(Mathf.Clamp01(height / Mathf.Max(.01f, maximumJumpHeight)));
    }

    void SetLaunchVelocity(float upwardSpeed)
    {
        Vector3 velocity = body.linearVelocity;
        velocity.y = upwardSpeed;
        body.linearVelocity = velocity;
        SlamLaunch = false;
        Grounded = automaticHop = false;
        lastFloorContact = float.NegativeInfinity;
        ignoreGroundUntil = Time.fixedTime + .07f;
        lastFall = 0;
    }
}
