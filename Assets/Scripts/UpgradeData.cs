using UnityEngine;

public enum UpgradeRarity { Common, Rare, Epic }
public enum PlayerStat { MaxHealth, DashDamage, DashCooldown, JumpVelocity, SlamDamage, SlamRadius, StompDamage, Rebound, HealOnKill, Echo }

[System.Serializable]
public struct StatModifier
{
    public PlayerStat stat;
    public float value;
    public StatModifier(PlayerStat stat, float value) { this.stat = stat; this.value = value; }
}

[CreateAssetMenu(menuName="SPRINGBOOK/Upgrade")]
public sealed class UpgradeData : ScriptableObject
{
    public string id, title;
    [TextArea] public string description;
    public UpgradeRarity rarity;
    public Texture2D icon;
    [Min(1)] public int limit = 1;
    public StatModifier[] modifiers;
}
