using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class ProceduralSpringMesh : MonoBehaviour
{
    [Range(2, 10)] public int turns = 5;
    [Range(8, 40)] public int segmentsPerTurn = 24;
    [Range(5, 16)] public int tubeSides = 10;
    [Min(.05f)] public float coilRadius = .29f;
    [Min(.01f)] public float wireRadius = .045f;
    public Vector3 Top { get; private set; }

    Mesh mesh;
    Vector3[] vertices, normals;
    int rings, sides;

    // The prefab holds a rest mesh. Each live instance owns its deformable copy.
    public void Build(float height, Vector2 lean)
    {
        int length = turns * segmentsPerTurn + 1;
        if (!mesh || rings != length || sides != tubeSides)
        {
            if (mesh) Destroy(mesh);
            rings = length;
            sides = tubeSides;
            mesh = new Mesh { name = "Spring coil" };
            mesh.MarkDynamic();
            vertices = new Vector3[rings * sides + 2];
            normals = new Vector3[vertices.Length];
            int[] indices = new int[((rings - 1) * sides * 2 + sides * 2) * 3];
            int k = 0;
            for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j, b = i * sides + (j + 1) % sides;
                indices[k++] = a; indices[k++] = b; indices[k++] = a + sides;
                indices[k++] = b; indices[k++] = b + sides; indices[k++] = a + sides;
            }
            for (int j = 0; j < sides; j++)
            {
                int next = (j + 1) % sides, end = (rings - 1) * sides;
                indices[k++] = rings * sides; indices[k++] = next; indices[k++] = j;
                indices[k++] = rings * sides + 1; indices[k++] = end + j; indices[k++] = end + next;
            }
            mesh.vertices = vertices;
            mesh.triangles = indices;
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        height = Mathf.Max(2 * wireRadius + .01f, height);
        Top = new Vector3(lean.x, height, lean.y);
        float phase = turns * Mathf.PI * 2f;
        for (int i = 0; i < rings; i++)
        {
            float t = i / (float)(rings - 1), angle = t * phase;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 center = radial * coilRadius + new Vector3(lean.x * t * t,
                wireRadius + (height - 2 * wireRadius) * t, lean.y * t * t);
            Vector3 tangent = new Vector3(-radial.z * coilRadius * phase + 2 * lean.x * t,
                height - 2 * wireRadius, radial.x * coilRadius * phase + 2 * lean.y * t).normalized;
            Vector3 across = Vector3.Cross(tangent, radial).normalized;
            radial = Vector3.Cross(across, tangent);
            for (int j = 0; j < sides; j++)
            {
                float theta = j * 2f * Mathf.PI / sides;
                Vector3 normal = radial * Mathf.Cos(theta) + across * Mathf.Sin(theta);
                normals[i * sides + j] = normal;
                vertices[i * sides + j] = center + normal * wireRadius;
            }
            if (i == 0 || i == rings - 1)
            {
                int cap = rings * sides + (i == 0 ? 0 : 1);
                vertices[cap] = center;
                normals[cap] = i == 0 ? -tangent : tangent;
            }
        }
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.RecalculateBounds();
    }

    void OnDestroy() { if (mesh && Application.isPlaying) Destroy(mesh); }
}