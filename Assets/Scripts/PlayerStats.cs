using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerStats : MonoBehaviour
{
    public const float CooldownFloor = .6f;
    readonly Dictionary<string,int> stacks = new Dictionary<string,int>();
    readonly Dictionary<PlayerStat,float> bonuses = new Dictionary<PlayerStat,float>();
    SpringCombatController combat;
    Health health;
    int baseHealth;
    float cooldownFactor = 1;
    public int Total { get; private set; }
    public int GoldEarned {get; private set;}
    public int Gold { get; private set; }
    public int DashDamage => Rounded(combat.dashDamage * (1 + Bonus(PlayerStat.DashDamage)));
    public float DashCooldown => Mathf.Max(CooldownFloor, combat.dashCooldown * cooldownFactor);
    public float JumpMultiplier => 1 + Bonus(PlayerStat.JumpVelocity);
    public int SlamDamage => Rounded(combat.slamDamage * (1 + Bonus(PlayerStat.SlamDamage)));
    public float SlamRadius => combat.slamRadius * (1 + Bonus(PlayerStat.SlamRadius));
    public int StompDamage => Rounded(combat.stompDamage * (1 + Bonus(PlayerStat.StompDamage)));
    public float Rebound => combat.reboundSpeed * (1 + Bonus(PlayerStat.Rebound));
    public int HealOnKill => Rounded(Bonus(PlayerStat.HealOnKill));
    public float EchoFraction => Bonus(PlayerStat.Echo);
    public int MaxHealth => baseHealth + Rounded(Bonus(PlayerStat.MaxHealth));

    void Awake()
    {
        health = GetComponent<Health>();
        combat = GetComponent<SpringCombatController>();
        baseHealth = health.maximum;
    }
    static int Rounded(float value) => Mathf.FloorToInt(value + .5f);
    float Bonus(PlayerStat stat) => bonuses.TryGetValue(stat,out float n) ? n : 0;
    public int Count(UpgradeData data) => stacks.TryGetValue(data.id,out int n) ? n : 0;
    public void AddGold(int value) { int gain=Mathf.Max(0,value); Gold+=gain; GoldEarned+=gain; }

    internal void SpendGold(int price)
    {
        if(price<0 || price>Gold)throw new System.ArgumentOutOfRangeException(nameof(price));
        Gold-=price;
    }

    public bool CanTake(UpgradeData data)
    {
        if (!data || Count(data) >= data.limit) return false;
        foreach (var mod in data.modifiers)
        {
            if (mod.value <= 0) continue;
            if (mod.stat != PlayerStat.DashCooldown || DashCooldown > CooldownFloor + .00001f) return true;
        }
        return false;
    }
    public bool Apply(UpgradeData data)
    {
        if (!CanTake(data)) return false;
        int prior = MaxHealth;
        foreach (var mod in data.modifiers)
        {
            if (mod.stat == PlayerStat.DashCooldown) cooldownFactor *= 1-Mathf.Clamp01(mod.value);
            else bonuses[mod.stat] = Bonus(mod.stat) + mod.value;
        }
        stacks[data.id] = Count(data)+1;
        Total++;
        health.maximum = MaxHealth;
        health.Heal(MaxHealth-prior);
        return true;
    }
}
