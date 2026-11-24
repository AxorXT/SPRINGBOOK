using System;
using UnityEngine;

public enum ReservedEncounter { None, Shop, FinalBoss }

[Serializable]
public struct ReservedPage
{
    [Tooltip("0 = última página de la partida. Las páginas 1 y 2 conservan su apertura fija.")]
    [Min(0)] public int page;
    public ReservedEncounter role;
    public PageKind substitute;
    public ReservedPage(int page, ReservedEncounter role, PageKind substitute)
    { this.page = page; this.role = role; this.substitute = substitute; }
}

[Serializable]
public struct EnemyCountStage
{
    [Min(1)] public int firstPage;
    public Vector2Int combat, elite;
    public EnemyCountStage(int first, int min, int max, int eliteMin, int eliteMax)
    {
        firstPage = first;
        combat = new Vector2Int(min, max);
        elite = new Vector2Int(eliteMin, eliteMax);
    }
}

[CreateAssetMenu(menuName="SPRINGBOOK/Run difficulty")]
public sealed class DifficultySettings : ScriptableObject
{
    [Min(2)] public int totalPages = 15;
    [Min(0)] public float healthGrowth = .06f, damageGrowth = .04f;
    [Min(1)] public float maxHealthMultiplier = 2f, maxDamageMultiplier = 2f;
    [Min(1)] public int spitterFirstPage = 3, golemFirstPage = 5, secondGolemFirstPage = 11;
    public EnemyCountStage[] stages = {
        new EnemyCountStage(1,2,3,2,3), new EnemyCountStage(3,3,4,4,5),
        new EnemyCountStage(6,4,6,5,7), new EnemyCountStage(11,5,8,6,9)
    };
    public ReservedPage[] reservations = {
        new ReservedPage(0, ReservedEncounter.FinalBoss, PageKind.FinalBoss)
    };

    public ReservedPage Reservation(int page)
    {
        if (page <= 2 || page > totalPages) return default;
        // The final-page reservation wins if a shorter run overlaps a shop.
        foreach (var entry in reservations)
            if (entry.page == 0 && page == totalPages) return entry;
        foreach (var entry in reservations)
            if (entry.page == page) return entry;
        return default;
    }

    public Vector2Int CountRange(int page, bool elite)
    {
        var best = new EnemyCountStage(0,3,6,5,8);
        foreach (var stage in stages)
            if (stage.firstPage <= page && stage.firstPage > best.firstPage) best = stage;
        return elite ? best.elite : best.combat;
    }
    public void ConfigureEnemy(InkEnemy instance, InkEnemy prefab, int page)
    {
        int advanced = Mathf.Max(0,page-1);
        float healthFactor = Mathf.Min(maxHealthMultiplier,1 + advanced * healthGrowth);
        float damageFactor = Mathf.Min(maxDamageMultiplier,1 + advanced * damageGrowth);
        var life = instance.GetComponent<Health>();
        life.maximum = Scale(prefab.GetComponent<Health>().maximum,healthFactor);
        life.Restore(0);
        instance.damage = Scale(prefab.damage,damageFactor);
        instance.slamDamage = Scale(prefab.slamDamage,damageFactor);
    }
    static int Scale(int value,float multiplier) => Mathf.FloorToInt(value * multiplier + .5f);
}
