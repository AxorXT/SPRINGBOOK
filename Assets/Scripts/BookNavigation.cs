using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Rigidbody))]
public sealed class BookNavigation : MonoBehaviour
{
    [Min(.05f)] public float pathInterval = .18f;
    [Min(1)] public float acceleration = 18f;
    [Min(0)] public float hitRecovery = .22f;
    public float fallHeight = -3f;
    Rigidbody body;
    NavMeshPath path;
    Vector3[] corners;
    Vector3 spawn;
    int corner;
    float nextPath, staggerEnd;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        spawn = body.position;
        path = new NavMeshPath();
    }
    public void Stagger() { staggerEnd = Time.time + hitRecovery; }

    public void Drive(Vector3 goal, float speed)
    {
        if (body.position.y < fallHeight)
        {
            body.position = spawn;
            body.linearVelocity = Vector3.zero;
            nextPath = 0;
            corners = null;
            return;
        }
        if (Time.time < staggerEnd) return;
        Vector3 planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        Vector3 desired = Vector3.zero;
        if (speed > 0 && NavMesh.SamplePosition(body.position, out var here, 1.8f, NavMesh.AllAreas))
        {
            if (Time.time >= nextPath || corners == null)
            {
                nextPath = Time.time + pathInterval;
                corners = null;
                if (NavMesh.SamplePosition(goal, out var there, 7f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(here.position, there.position, NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete)
                { corners = path.corners; corner = 1; }
            }
            if (corners != null && corner < corners.Length)
            {
                Vector3 offset = Vector3.ProjectOnPlane(corners[corner] - body.position, Vector3.up);
                if (offset.magnitude < .18f) corner++;
                else desired = offset.normalized * Mathf.Min(speed, offset.magnitude / Time.fixedDeltaTime);
            }
        }
        Vector3 next = Vector3.MoveTowards(planar, desired, acceleration * Time.fixedDeltaTime);
        body.linearVelocity = next + Vector3.up * body.linearVelocity.y;
    }
}
