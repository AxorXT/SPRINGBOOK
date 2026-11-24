using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpringPlayerController), typeof(CombatFeedback))]
public sealed class SpringCombatController : MonoBehaviour
{
    public enum Attack { Idle, Windup, Dash, Dive, Recovery }
    public Attack State { get; private set; }
    [Header("Elastic dash")]
    [Min(1)] public int dashDamage = 25;
    [Min(0)] public float dashPush = 6f, dashCooldown = 1.3f;
    [Min(.01f)] public float windup = .15f, dashDuration = .20f, recovery = .20f;
    [Min(.01f)] public float dashDistance = 1.5f, hitRadius = .52f;
    [Header("Ground impact")]
    [Min(1)] public int slamDamage = 40;
    [Min(0)] public float slamPush = 8f, slamCooldown = 1.5f;
    [Min(.1f)] public float slamRadius = 2.5f, descentSpeed = 17f;
    [Min(0)] public float minimumSlamHeight = 1f;
    [Range(0, 1)] public float horizontalCarry = .16f;
    [Header("Stomp")]
    [Min(1)] public int stompDamage = 15;
    [Min(0)] public float stompSpeed = .35f, reboundSpeed = 5.5f, stompPush = 1f;
    [Range(.1f, 1)] public float topNormal = .65f;
    [Header("Spring pose")]
    [Range(0, .6f)] public float windupCompression = .45f, dashExtension = .38f;
    [Min(0)] public float attackLean = .48f;
    public Vector3 Lean => State == Attack.Dash || State == Attack.Windup ? direction * attackLean : Vector3.zero;
    public float HeightOffset => State == Attack.Dash ? dashExtension : State == Attack.Windup ? -windupCompression : 0;

    SpringPlayerController motor;
    struct Aftershock
    {
        public Vector3 point;
        public float radius;
        public int damage;
        public double due;
    }
    readonly Queue<Aftershock> aftershocks = new Queue<Aftershock>();
    PlayerStats stats;
    public void DiscardInput() { left = right = false; }
    Rigidbody body;
    CapsuleCollider capsule;
    CombatFeedback feedback;
    ThirdPersonCamera view;
    readonly HashSet<IDamageable> struck = new HashSet<IDamageable>();
    Vector3 direction;
    bool left, right;
    double end, nextDash;
    float nextDive, incomingY, lastImpact = -1;
    Collider lastStompSurface;
    readonly List<Collider> diveContacts = new List<Collider>();
    float descendingUntil = -1;
    Vector3 lastPosition;
    float travelled;
    public float CooldownRemaining => Mathf.Max(0, (float)(nextDash - Time.timeAsDouble));
    public bool DashAvailable => State == Attack.Idle && CooldownRemaining <= 0 && !motor.Charging;

    void Awake()
    {
        motor = GetComponent<SpringPlayerController>();
        stats = GetComponent<PlayerStats>();
        body = GetComponent<Rigidbody>();
        feedback = GetComponent<CombatFeedback>();
        capsule = GetComponentInChildren<CapsuleCollider>();
    }
    void Start() { view = Camera.main.GetComponent<ThirdPersonCamera>(); }
    void OnEnable() { motor.Respawned += ResetCombat; }
    void OnDisable() { motor.Respawned -= ResetCombat; ResetCombat(); }
    void ResetCombat()
    {
        RestoreDiveContacts(true);
        aftershocks.Clear();
        // A fall cancels the animation but still owes the unfinished phases and cooldown.
        double remainingEnd = end;
        if (State == Attack.Windup) remainingEnd += dashDuration + recovery;
        if (State == Attack.Dash) remainingEnd += recovery;
        if (State == Attack.Windup || State == Attack.Dash || State == Attack.Recovery)
        {
            end = remainingEnd;
            State = Attack.Recovery;
            lastPosition = body.position;
        }
        else State = Attack.Idle;
        struck.Clear();
        left = right = false;
        // A completed impact keeps its cooldown across respawns and page transitions.
        incomingY = 0;
        descendingUntil = -1;
        lastStompSurface = null;
        feedback.StopTrail();
    }

    // Sample before the camera handles recapture in LateUpdate.
    void Update()
    {
        if (motor.Loot && motor.Loot.BlockInput) { DiscardInput(); return; }
        var mouse = Mouse.current;
        if (Cursor.lockState != CursorLockMode.Locked || mouse == null ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        { left = right = false; return; }
        if (mouse.leftButton.wasPressedThisFrame && DashAvailable) left = true;
        if (mouse.rightButton.wasPressedThisFrame && CanDive(Time.time)) right = true;
    }

    bool CanDive(float now)
    {
        return State == Attack.Idle && motor.SlamLaunch && !motor.ChargePending &&
            !motor.Grounded && now >= nextDive &&
            motor.BookClearance(feedback.board, capsule, out float height) && height >= minimumSlamHeight;
    }

    public bool Step(bool jumpPending)
    {
        RestoreDiveContacts(false);
        while (aftershocks.Count > 0 && Time.fixedTimeAsDouble + .000001 >= aftershocks.Peek().due)
        {
            var echo = aftershocks.Dequeue();
            Wave(echo.point, echo.radius, echo.damage);
        }
        float now = Time.fixedTime;
        incomingY = body.linearVelocity.y;
        if (incomingY < -stompSpeed) descendingUntil = now + 2 * Time.fixedDeltaTime;
        if (lastStompSurface && (!lastStompSurface.gameObject.activeInHierarchy ||
            body.position.y > lastStompSurface.bounds.max.y + .04f))
            lastStompSurface = null;
        if (State == Attack.Idle && !motor.Charging && !jumpPending)
        {
            if (right && CanDive(now))
            {
                State = Attack.Dive;
                motor.ConsumeSlamLaunch();
                Vector3 drift = body.linearVelocity * horizontalCarry;
                body.linearVelocity = new Vector3(drift.x, -descentSpeed, drift.z);
            }
            else if (left && Time.fixedTimeAsDouble >= nextDash)
            {
                direction = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                State = Attack.Windup;
                struck.Clear();
                end = Time.fixedTimeAsDouble + windup;
                travelled = 0;
                lastPosition = body.position;
            }
        }
        left = right = false;
        if (State == Attack.Idle) return false;
        if (State == Attack.Dive)
        {
            if (!motor.Grounded)
            {
                Vector3 v = body.linearVelocity;
                v.y = Mathf.Min(-descentSpeed, v.y);
                body.linearVelocity = v;
            }
            return true;
        }
        Vector3 step = Vector3.ProjectOnPlane(body.position - lastPosition, Vector3.up);
        travelled += step.magnitude;
        lastPosition = body.position;
        double clock = Time.fixedTimeAsDouble;
        const double tickTolerance = .000001;
        if (State == Attack.Windup && clock + tickTolerance >= end)
        {
            State = Attack.Dash;
            end = clock + dashDuration;
            feedback.StartTrail();
        }
        if (State == Attack.Dash && clock + tickTolerance >= end)
        {
            State = Attack.Recovery;
            end = clock + recovery;
            feedback.StopTrail();
        }
        if (State == Attack.Recovery && clock + tickTolerance >= end)
        {
            State = Attack.Idle;
            nextDash = clock + stats.DashCooldown;
            body.linearVelocity = Vector3.up * body.linearVelocity.y;
            return false;
        }
        // Budget horizontal travel, preserving gravity and the vertical jump velocity.
        float remaining = Mathf.Max(0, dashDistance - travelled);
        float speed = Mathf.Min(dashDistance / dashDuration, remaining / Time.fixedDeltaTime);
        Vector3 horizontal = State == Attack.Dash ? direction * speed : Vector3.zero;
        body.linearVelocity = horizontal + Vector3.up * body.linearVelocity.y;
        if (State == Attack.Dash)
        {
            Vector3 from = body.position + Vector3.up * .70f;
            foreach (var col in Physics.OverlapCapsule(from, from + horizontal * Time.fixedDeltaTime, hitRadius,
                         Physics.AllLayers, QueryTriggerInteraction.Ignore))
                Hit(col, stats.DashDamage, direction * dashPush, from, struck);
        }
        return true;
    }

    void Hit(Collider col, int damage, Vector3 force, Vector3 origin, HashSet<IDamageable> hits)
    {
        if (col.attachedRigidbody == body) return;
        var target = col.GetComponentInParent<IDamageable>();
        if (target == null || !hits.Add(target)) return;
        Vector3 point = col.ClosestPoint(origin);
        if (target.TakeDamage(damage, force, gameObject)) feedback.Sparks(point);
    }

    // A hurtbox below the player must not substitute for the book or arrest the dive.
    void PassDiveTarget(Collider surface)
    {
        if (surface.GetComponentInParent<IDamageable>() == null || diveContacts.Contains(surface)) return;
        if (Physics.GetIgnoreCollision(capsule, surface)) return;
        Physics.IgnoreCollision(capsule, surface, true);
        diveContacts.Add(surface);
    }

    void RestoreDiveContacts(bool resetting)
    {
        for (int i = diveContacts.Count - 1; i >= 0; i--)
        {
            var surface = diveContacts[i];
            if (surface && !resetting && surface.gameObject.activeInHierarchy &&
                capsule.bounds.Intersects(surface.bounds)) continue;
            if (surface) Physics.IgnoreCollision(capsule, surface, false);
            diveContacts.RemoveAt(i);
        }
    }

    void Impact(Vector3 point)
    {
        State = Attack.Idle;
        lastImpact = Time.fixedTime;
        nextDive = lastImpact + slamCooldown;
        float radius = stats.SlamRadius;
        int damage = stats.SlamDamage;
        Wave(point, radius, damage);
        if (stats.EchoFraction > 0 && isActiveAndEnabled)
            aftershocks.Enqueue(new Aftershock
            {
                point = point, radius = radius, damage = Mathf.RoundToInt(damage * stats.EchoFraction),
                due = Time.fixedTimeAsDouble + .4
            });
        motor.AbsorbImpact();
        view.Shake();
    }

    void Wave(Vector3 point, float radius, int damage)
    {
        var hits = new HashSet<IDamageable>();
        foreach (var col in Physics.OverlapSphere(point, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            var entity = col.GetComponentInParent<IDamageable>() as Component;
            if (!entity) continue;
            Vector3 outward = Vector3.ProjectOnPlane(entity.transform.position - point, Vector3.up);
            if (outward.sqrMagnitude > radius * radius) continue;
            Hit(col, damage, (outward.normalized + Vector3.up * .22f) * slamPush, point, hits);
        }
        feedback.Shockwave(point, radius);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled) return;
        if (State == Attack.Dash)
        {
            Hit(collision.collider, stats.DashDamage, direction * dashPush, body.position, struck);
            return;
        }
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (contact.normal.y < topNormal || contact.point.y > body.position.y + .35f) continue;
            if (State == Attack.Dive)
            {
                if (collision.collider == feedback.board) Impact(contact.point);
                else PassDiveTarget(collision.collider);
                return;
            }
            TryStomp(collision, contact);
            return;
        }
    }

    // An initial flank contact can roll onto the crown without a second Enter event.
    void OnCollisionStay(Collision collision)
    {
        if (!isActiveAndEnabled) return;
        if (State == Attack.Dive && collision.collider != feedback.board)
        {
            PassDiveTarget(collision.collider);
            return;
        }
        if (State == Attack.Dive && collision.collider == feedback.board)
        {
            for (int i = 0; i < collision.contactCount; i++)
                if (collision.GetContact(i).normal.y >= topNormal) { Impact(collision.GetContact(i).point); return; }
        }
        if (State != Attack.Idle || Time.fixedTime > descendingUntil) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            var contact = collision.GetContact(i);
            if (contact.normal.y >= topNormal && contact.point.y <= body.position.y + .35f)
            {
                TryStomp(collision, contact);
                return;
            }
        }
    }

    void TryStomp(Collision collision, ContactPoint contact)
    {
        if (State != Attack.Idle || lastImpact == Time.fixedTime || lastStompSurface ||
            Time.fixedTime > descendingUntil || incomingY > .01f) return;
        var surface = collision.collider;
        if (contact.point.y < surface.bounds.center.y) return;
        var target = surface.GetComponentInParent<IDamageable>();
        if (target == null || !target.TakeDamage(stats.StompDamage, Vector3.down * stompPush, gameObject)) return;
        lastImpact = Time.fixedTime;
        descendingUntil = -1;
        lastStompSurface = surface;
        motor.Rebound(stats.Rebound);
        feedback.Sparks(contact.point);
    }
}
