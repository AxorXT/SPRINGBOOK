using UnityEngine;

[RequireComponent(typeof(Health))]
public sealed class TrainingDummy : MonoBehaviour
{
    public Transform bar, fill;
    public Renderer[] paint;
    public Color flashColor = new Color(1f, .87f, .5f);
    [Min(.01f)] public float flashDuration = .13f;
    Health health;
    Camera viewer;
    MaterialPropertyBlock flash;
    float flashEnd;

    void Awake()
    {
        health = GetComponent<Health>();
        flash = new MaterialPropertyBlock();
        flash.SetColor("_BaseColor", flashColor);
        viewer = Camera.main;
    }
    void OnEnable() { health.damaged.AddListener(ShowHit); health.died.AddListener(Die); }
    void OnDisable() { health.damaged.RemoveListener(ShowHit); health.died.RemoveListener(Die); }
    void ShowHit(int amount)
    {
        foreach (var part in paint) part.SetPropertyBlock(flash);
        flashEnd = Time.time + flashDuration;
        Vector3 scale = fill.localScale;
        scale.x = (float)health.Current / health.maximum;
        fill.localScale = scale;
    }
    void LateUpdate()
    {
        if (viewer) bar.rotation = viewer.transform.rotation;
        if (flashEnd > 0 && Time.time >= flashEnd)
        {
            foreach (var part in paint) part.SetPropertyBlock(null);
            flashEnd = 0;
        }
    }
    void Die() { gameObject.SetActive(false); }
}
