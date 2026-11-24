using UnityEngine;

public sealed class CombatFeedback : MonoBehaviour
{
    public TrailRenderer trail;
    public Material effectMaterial;
    public Color waveColor = new Color(1f, .6f, .16f);
    public Color sparkColor = new Color(1f, .82f, .36f);
    [Min(.05f)] public float waveDuration = .32f;
    [Range(2, 30)] public int sparkCount = 10;
    ParticleSystem particles;
    LineRenderer ring;
    readonly Vector3[] points = new Vector3[64];
    Vector3 center;
    float radius, elapsed;

    void Awake()
    {
        var obj = new GameObject("Impact Sparks");
        obj.transform.SetParent(transform, false);
        particles = obj.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = .7f;
        var emission = particles.emission;
        emission.enabled = false;
        obj.GetComponent<ParticleSystemRenderer>().sharedMaterial = effectMaterial;

        obj = new GameObject("Impact Wave");
        obj.transform.SetParent(transform, false);
        ring = obj.AddComponent<LineRenderer>();
        ring.sharedMaterial = effectMaterial;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = points.Length;
        ring.enabled = false;
    }
    public void StartTrail() { trail.Clear(); trail.emitting = true; }
    public void StopTrail() { if (trail) trail.emitting = false; }

    public void ClearTransient()
    {
        StopTrail();
        trail.Clear();
        ring.enabled=false;
        particles.Clear(true);
    }

    public void Sparks(Vector3 point)
    {
        for (int i = 0; i < sparkCount; i++)
        {
            Vector3 velocity = Random.insideUnitSphere * 2.6f;
            velocity.y = Mathf.Abs(velocity.y) + .6f;
            particles.Emit(new ParticleSystem.EmitParams {
                position = point, velocity = velocity, startColor = sparkColor,
                startSize = Random.Range(.035f, .085f), startLifetime = Random.Range(.16f, .32f)
            }, 1);
        }
    }
    public void Shockwave(Vector3 point, float range)
    {
        center = point;
        radius = range;
        elapsed = 0;
        ring.enabled = true;
        DrawWave(0);
    }
    void LateUpdate()
    {
        if (!ring.enabled) return;
        elapsed += Time.deltaTime;
        float t = elapsed / waveDuration;
        if (t >= 1) { ring.enabled = false; return; }
        DrawWave(t);
    }
    void DrawWave(float t)
    {
        float size = Mathf.Lerp(.14f, radius, 1 - (1-t)*(1-t));
        for (int i = 0; i < points.Length; i++)
        {
            float a = i * 2 * Mathf.PI / points.Length;
            Vector3 p = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size;
            // Only the board mesh defines the elevation of the printed wave.
            if (board && board.Raycast(new Ray(p + Vector3.up * 4f, Vector3.down), out RaycastHit hit, 12f))
                p.y = hit.point.y;
            points[i] = p + Vector3.up * .055f;
        }
        ring.SetPositions(points);
        ring.startWidth = ring.endWidth = Mathf.Lerp(.13f, .025f, t);
        Color c = waveColor; c.a = 1f - t;
        ring.startColor = ring.endColor = c;
    }
    public MeshCollider board;
}
