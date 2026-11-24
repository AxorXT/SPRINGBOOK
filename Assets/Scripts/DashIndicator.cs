using UnityEngine;

[RequireComponent(typeof(SpringCombatController))]
public sealed class DashIndicator : MonoBehaviour
{
    [SerializeField] Vector2 margin = new Vector2(22, 20);
    [SerializeField, Min(160)] float width = 230;
    [SerializeField, Min(12)] int fontSize = 16;
    [SerializeField] Color readyColor = new Color(.55f, .93f, .72f);
    [SerializeField] Color waitingColor = new Color(1f, .82f, .53f);
    SpringCombatController combat;
    GUIStyle label;

    void Awake() { combat = GetComponent<SpringCombatController>(); }

    void OnGUI()
    {
        if(BookMenus.CoveringHUD)return;
        if (Event.current.type != EventType.Repaint) return;
        if (label == null)
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        label.fontSize = fontSize;
        float time = combat.CooldownRemaining;
        string status;
        switch (combat.State)
        {
            case SpringCombatController.Attack.Windup: status = "Comprimiendo"; break;
            case SpringCombatController.Attack.Dash: status = "Impulso"; break;
            case SpringCombatController.Attack.Recovery: status = "Recuperación"; break;
            default:
                status = time > 0 ? (Mathf.Ceil(time * 10f) / 10f).ToString("0.0") + " s"
                    : combat.DashAvailable ? "Lista" : "Ocupada";
                break;
        }
        float height = fontSize + 22;
        Rect rect = new Rect(margin.x, Screen.height - margin.y - height, width, height);
        Color original = GUI.color;
        GUI.color = new Color(.08f, .13f, .12f, .9f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        label.normal.textColor = combat.DashAvailable ? readyColor : waitingColor;
        GUI.Label(rect, "Embestida  ·  " + status, label);
        GUI.color = original;
    }
}
