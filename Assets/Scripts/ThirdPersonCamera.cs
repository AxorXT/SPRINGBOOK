using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public sealed class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] LayerMask obstacles = 1;
    [Range(2, 16)] public float distance = 6.6f;
    [Range(5, 80)] public float pitch = 28f;
    public Vector2 pitchLimits = new Vector2(12, 72);
    [Min(.01f)] public float sensitivity = .14f;
    [Min(.01f)] public float followTime = .10f;
    [Min(.05f)] public float collisionRadius = .22f;
    [Header("Strong impacts")]
    [Range(0,.25f)] public float shakeAmplitude = .075f;
    [Min(.01f)] public float shakeDuration = .16f;
    float shakeEnd;
    public void Shake() { if(!BookOptions.Shake)return; shakeEnd = Time.time + shakeDuration; }
    float yaw, arm;
    Vector3 pivot, smoothVelocity;
    SpringPlayerController player;

    void OnEnable()
    {
        player = target.GetComponentInParent<SpringPlayerController>();
        player.Respawned += Snap;
        yaw = transform.eulerAngles.y;
        Snap();
        Capture(true);
    }
    void OnDisable()
    {
        if (player) player.Respawned -= Snap;
        Capture(false);
    }
    void Snap() { shakeEnd = 0; pivot = target.position; smoothVelocity = Vector3.zero; arm = distance; }
    void Capture(bool capture)
    {
        Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !capture;
    }
    void LateUpdate()
    {
        if (player.Loot && player.Loot.BlockInput) return;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) Capture(true);
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue() * sensitivity * BookOptions.Sensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, pitchLimits.x, pitchLimits.y);
        }
        pivot = Vector3.SmoothDamp(pivot, target.position, ref smoothVelocity, followTime);
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 back = rotation * Vector3.back;
        float allowed = distance;
        if (Physics.SphereCast(pivot, collisionRadius, back, out RaycastHit hit, distance, obstacles, QueryTriggerInteraction.Ignore))
            allowed = Mathf.Max(.02f, hit.distance - .04f);
        arm = Mathf.Min(allowed, Mathf.MoveTowards(arm, allowed, 12f * Time.deltaTime));
        float remaining = !BookOptions.Shake ? 0 : Mathf.Clamp01((shakeEnd - Time.time) / shakeDuration);
        Vector3 shake = rotation * new Vector3(Mathf.Sin(Time.time * 89f), Mathf.Cos(Time.time * 71f), 0);
        transform.SetPositionAndRotation(pivot + back * arm + shake * (remaining * shakeAmplitude), rotation);
    }
}
