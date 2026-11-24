using UnityEngine;

public sealed class SpringVisualController : MonoBehaviour
{
    [SerializeField] SpringPlayerController motor;
    [SerializeField] ProceduralSpringMesh spring;
    [SerializeField] Transform head;
    [Min(.1f)] public float restHeight = 1f;
    [Range(.45f, .95f)] public float compressedHeight = .50f;
    [Min(0)] public float extension = .5f;
    [Min(0)] public float impactCompression = .32f;
    [Range(2, 30)] public float elasticity = 15f;
    [Range(.1f, 1)] public float damping = .58f;
    [Min(0)] public float leanAmount = .13f;

    float height, rate, impulse;
    Vector2 lean;
    SpringCombatController combat;
    float reboundAt;
    void StompPose() { impulse = -impactCompression; rate = -elasticity * impactCompression; reboundAt = Time.time + .065f; }
    void Awake() { height = restHeight; combat = motor.GetComponent<SpringCombatController>(); }
    void OnEnable()
    {
        motor.Rebounded += StompPose;
        motor.Launched += OnLaunch;
        motor.Landed += OnLand;
        motor.Respawned += ResetPose;
    }
    void OnDisable()
    {
        motor.Rebounded -= StompPose;
        motor.Launched -= OnLaunch;
        motor.Landed -= OnLand;
        motor.Respawned -= ResetPose;
    }
    void OnLaunch(float power) { impulse = extension * Mathf.Lerp(.38f, 1f, power) * (motor.Charging ? motor.chargeHopHeightMultiplier : 1f); rate += impulse * elasticity; }
    void OnLand(float impact) { impulse = -impactCompression * Mathf.Lerp(.35f, 1f, impact); rate += impulse * elasticity; }
    void ResetPose() { height = restHeight; reboundAt = impulse = rate = 0; lean = Vector2.zero; }
    void LateUpdate()
    {
        float elapsed = Mathf.Min(Time.deltaTime, .08f);
        Vector3 local = transform.InverseTransformDirection(motor.Velocity);
        Vector2 targetLean = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / Mathf.Max(.1f, motor.speed), 1) * leanAmount;
        if (combat)
        {
            Vector3 localLean = transform.InverseTransformDirection(combat.Lean);
            targetLean += new Vector2(localLean.x, localLean.z);
        }
        if (reboundAt > 0 && Time.time >= reboundAt) { reboundAt = 0; OnLaunch(.55f); }
        lean = Vector2.Lerp(lean, targetLean, 1f - Mathf.Exp(-10f * elapsed));
        float chargeHeight = restHeight * Mathf.Lerp(1f, compressedHeight, motor.Charge);
        if (combat) chargeHeight += combat.HeightOffset;
        int steps = Mathf.Max(1, Mathf.CeilToInt(elapsed * 120f));
        float dt = elapsed / steps;
        for (int i = 0; i < steps; i++)
        {
            rate += ((chargeHeight + impulse - height) * elasticity * elasticity - 2 * damping * elasticity * rate) * dt;
            height += rate * dt;
            impulse *= Mathf.Exp(-7f * dt);
        }
        float minHeight = spring.wireRadius * 2f * (spring.turns + .6f);
        spring.Build(Mathf.Max(minHeight, height), lean);
        head.localPosition = spring.transform.localPosition + spring.Top;
    }
}
