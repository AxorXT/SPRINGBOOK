using UnityEngine;

public sealed class EventBookmark : MonoBehaviour
{
    public Transform ribbon;
    public Renderer foil;
    public Color glow = Color.yellow;
    public Texture2D illustration;
    public Transform[] motes;
    [Min(.05f)] public float revealTime = .75f;
    [Min(.1f)] public float interactionRadius = 1.8f;
    static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock tint;
    void Awake(){tint = new MaterialPropertyBlock();}
    float elapsed;

    public Vector3 InteractionPoint => transform.TransformPoint(Vector3.left * 1.25f + Vector3.up * .22f);
    public bool InReach(Vector3 position)
    {
        return (position - InteractionPoint).sqrMagnitude <= interactionRadius * interactionRadius;
    }
    void OnEnable()
    {
        elapsed = 0;
        Animate();
    }
    void Update()
    {
        elapsed += Time.deltaTime;
        Animate();
    }
    void Animate()
    {
        if (!ribbon) return;
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / revealTime));
        // Grow from the bound edge so the hidden end never clips through the page.
        ribbon.localScale = new Vector3(Mathf.Max(.001f,t), t, t);
        ribbon.localRotation = Quaternion.Euler(0,0,8 + 2 * Mathf.Sin(elapsed * 2));
        float pulse = .55f + .22f * Mathf.Sin(elapsed * 2.6f);
        foil.GetPropertyBlock(tint);
        tint.SetColor(Emission, glow * pulse);
        foil.SetPropertyBlock(tint);
        for (int i = 0; i < motes.Length; i++)
        {
            float phase = elapsed * 1.3f + i * 2.4f;
            motes[i].localPosition = new Vector3(1.6f + Mathf.Cos(phase)*.64f,
                .45f + Mathf.Repeat(elapsed*.38f+i*.23f,1f), Mathf.Sin(phase)*.55f);
        }
    }
}