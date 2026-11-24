using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public static class PageRouteChecks
{
    const int Runs = 1000;

    [MenuItem("SPRINGBOOK/Pruebas/Simular rutas de páginas")]
    public static void RunFromMenu() => Debug.Log(Run());

    public static string Run()
    {
        var generator = UnityEngine.Object.FindFirstObjectByType<PageEncounterGenerator>();
        var loot = UnityEngine.Object.FindFirstObjectByType<RunLoot>();
        if (!generator || !loot) throw new InvalidOperationException("Abre Gameplay antes de ejecutar la simulación.");
        int capacity = loot.catalogue.Sum(upgrade => upgrade.limit);
        int length = generator.difficulty.totalPages;
        var saved = Random.state;
        try
        {
            int[] visits = new int[5];
            int upgrades = 0, maxStreak = 0, forced = 0, postUpgradeBreaks = 0;
            for (int seed = 0; seed < Runs; seed++)
            {
                Random.InitState(57000 + seed);
                var chooser = new System.Random(87000 + seed);
                var kind = PageEncounterGenerator.FirstPage;
                int streak = 0, dry = 0, acquired = 0;
                Require(kind == PageKind.Combat, "Apertura");
                for (int page = 1; page <= length; page++)
                {
                    visits[(int)kind]++;
                    streak = PageEncounterGenerator.NextCombatStreak(streak, kind);
                    bool chest = kind == PageKind.Treasure || kind == PageKind.EliteCombat;
                    bool reward = chest && acquired < capacity;
                    if (reward) { acquired++; upgrades++; }
                    dry = reward ? 0 : PageEncounterGenerator.NextCombatStreak(dry, kind);
                    maxStreak = Math.Max(maxStreak, streak);
                    Require(streak <= 3, "Cuarto combate");
                    if (page == length) break;
                    var options = generator.DrawChoices(page, kind, streak, dry);
                    Require(options.Length == 4, "Cuatro esquinas");
                    foreach (var next in options)
                    {
                        if(page+1==length){Require(next==PageKind.FinalBoss,"Jefe final");continue;}
                        bool fight = next == PageKind.Combat || next == PageKind.EliteCombat;
                        if (page == 1 || dry >= 3) Require(next == PageKind.Treasure, "Tesoro obligatorio");
                        if (streak == 3) Require(!fight, "Límite de combates");
                        if (kind == PageKind.Treasure) Require(fight, "Combate después de tesoro");
                        if (kind == PageKind.EliteCombat || kind == PageKind.Rest) Require(next != kind, "Repetición");
                    }
                    if (dry == 3) forced++;
                    if (streak == 3 && dry < 3) postUpgradeBreaks++;
                    kind = options[chooser.Next(options.Length)];
                }
            }
            return $"runs={Runs} pages={length} visits={string.Join(",", visits)} upgrades={upgrades} max={maxStreak} guaranteed={forced} breaksAfterUpgrades={postUpgradeBreaks}";
        }
        finally { Random.state = saved; }
    }

    static void Require(bool valid, string rule)
    {
        if (!valid) throw new InvalidOperationException("Regla incumplida: " + rule);
    }
}
