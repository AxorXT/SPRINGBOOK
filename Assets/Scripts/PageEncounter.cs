using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class PageEncounter : MonoBehaviour
{
    public Renderer[] corners;
    public Material unlocked;
    public string clearMessage = "Página despejada. Elige una esquina.";
    public UnityEvent pageCleared = new UnityEvent();
    readonly HashSet<InkEnemy> alive = new HashSet<InkEnemy>();
    public bool IsClear { get; private set; }
    public bool AwaitingReward { get; set; }
    public int Remaining => alive.Count;
    public event System.Action<InkEnemy> EnemyDefeated;

    public void ResetEncounter()
    {
        foreach (var enemy in alive) if (enemy) enemy.Defeated -= OnDeath;
        alive.Clear();
        AwaitingReward = false;
        IsClear = false;
    }
    public void Register(InkEnemy enemy)
    {
        if (alive.Add(enemy)) enemy.Defeated += OnDeath;
    }
    void OnDeath(InkEnemy enemy)
    {
        if (!alive.Remove(enemy)) return;
        enemy.Defeated -= OnDeath;
        EnemyDefeated?.Invoke(enemy);
        if (alive.Count == 0 && !AwaitingReward) Complete();
    }
    public void Complete()
    {
        if (IsClear || alive.Count != 0) return;
        IsClear = true;
        pageCleared.Invoke();
    }
    void OnDestroy() { ResetEncounter(); }
}
