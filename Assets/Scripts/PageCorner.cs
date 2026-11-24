using UnityEngine;

[DefaultExecutionOrder(150)]
public sealed class PageCorner : MonoBehaviour
{
    public Renderer marker;
    public Color glow = new Color(.12f,.95f,.48f);
    public bool Available { get; private set; }
    public bool PlayerInside { get; private set; }
    public PageKind Choice { get; private set; }
    public string Prompt => "Presiona E para pasar la página";
    Material locked;
    MaterialPropertyBlock pulse;
    void Awake() { locked = marker.sharedMaterial; pulse = new MaterialPropertyBlock(); }
    public void Lock()
    {
        Available = false;
        marker.sharedMaterial = locked;
        marker.SetPropertyBlock(null);
    }
    public void Offer(PageKind choice, Material material)
    {
        Choice = choice;
        Available = true;
        marker.sharedMaterial = material;
    }
    void LateUpdate()
    {
        if (!Available) return;
        float wave = .5f - .5f * Mathf.Cos(Time.time * (2f * Mathf.PI / 2.2f));
        pulse.SetColor("_EmissionColor", glow * Mathf.Lerp(.12f, 1.55f, wave));
        marker.SetPropertyBlock(pulse);
    }
    void OnTriggerStay(Collider other) { if (other.attachedRigidbody && other.attachedRigidbody.CompareTag("Player")) PlayerInside = true; }
    void OnTriggerExit(Collider other) { if (other.attachedRigidbody && other.attachedRigidbody.CompareTag("Player")) PlayerInside = false; }
}
