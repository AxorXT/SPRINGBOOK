using UnityEngine;

public sealed class InkProjectile : MonoBehaviour
{
    [Min(.02f)] public float radius = .12f;
    [Min(.1f)] public float lifetime = 3f;
    public LayerMask obstructions = 1;
    Health target;
    Collider targetCollider;
    float speed;
    int damage;
    public void Launch(Health victim, int power, float velocity)
    {
        target = victim;
        targetCollider = victim.GetComponentInChildren<CapsuleCollider>();
        speed = velocity;
        damage = power;
        Destroy(gameObject, lifetime);
    }
    void FixedUpdate()
    {
        float distance = speed * Time.fixedDeltaTime;
        Vector3 origin = transform.position;
        int mask = obstructions | (1 << targetCollider.gameObject.layer);
        if ((targetCollider.ClosestPoint(origin)-origin).sqrMagnitude <= radius*radius)
        {
            target.TakeDamage(damage, Vector3.zero);
            Destroy(gameObject);
            return;
        }
        if (Physics.SphereCast(origin, radius, transform.forward, out var hit, distance, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Health>() == target)
                target.TakeDamage(damage, Vector3.zero);
            Destroy(gameObject);
            return;
        }
        transform.position += transform.forward * distance;
    }
}
