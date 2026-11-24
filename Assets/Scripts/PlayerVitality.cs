using UnityEngine;

[RequireComponent(typeof(Health), typeof(SpringPlayerController))]
public sealed class PlayerVitality : MonoBehaviour
{
    [Min(.05f)] public float flashTime = .18f;
    public Color hitColor = new Color(1f,.58f,.46f);
    Health health;
    Renderer[] parts;
    MaterialPropertyBlock flash;
    float until;
    Color[] baseColors;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    void Awake()
    {
        health = GetComponent<Health>();
        parts = transform.Find("VisualRoot").GetComponentsInChildren<MeshRenderer>();
        flash = new MaterialPropertyBlock();
        baseColors = new Color[parts.Length];
        for (int i=0; i<parts.Length; i++)
        {
            var material = parts[i].sharedMaterial;
            baseColors[i] = material && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
        }
    }
    void OnEnable() { health.damaged.AddListener(Hit); }
    void OnDisable()
    {
        health.damaged.RemoveListener(Hit);
        if(until>0) ClearFlash();
    }
    void Hit(int amount)
    {
        until = Time.time + Mathf.Max(flashTime,health.invulnerability);
    }
    void Update()
    {
        if (until <= 0) return;
        float remaining = until-Time.time;
        if (remaining <= 0) { ClearFlash(); return; }
        float elapsed = Mathf.Max(flashTime,health.invulnerability)-remaining;
        float wave = .5f + .5f*Mathf.Cos(elapsed*Mathf.PI*2/Mathf.Max(.1f,2*flashTime));
        float strength = (.18f+.58f*wave)*Mathf.Clamp01(remaining/flashTime);
        for(int i=0;i<parts.Length;i++)
        {
            flash.SetColor(BaseColor,Color.Lerp(baseColors[i],hitColor,strength));
            parts[i].SetPropertyBlock(flash);
        }
    }
    void ClearFlash()
    {
        foreach (var part in parts) part.SetPropertyBlock(null);
        until = 0;
    }
    void OnGUI()
    {
        if(BookMenus.CoveringHUD)return;
        GUI.Box(new Rect(22, Screen.height-100,230,34), $"Vida   {health.Current} / {health.maximum}");
    }
}
