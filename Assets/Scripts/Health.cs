using UnityEngine;
using UnityEngine.Events;

public interface IDamageable
{
    bool TakeDamage(int amount, Vector3 impulse, GameObject source = null);
}

[DisallowMultipleComponent]
public sealed class Health : MonoBehaviour, IDamageable
{
    [Min(1)] public int maximum = 100;
    public int Current { get; private set; }
    public UnityEvent<int> damaged = new UnityEvent<int>();
    public UnityEvent died = new UnityEvent();
    [Min(0)] public float invulnerability;
    public float DamageFactor {get;set;} = 1;
    float protectedUntil;
    public void Restore(float protection)
    {
        Current = maximum;
        protectedUntil = Time.time + protection;
    }
    public int Heal(int amount)
    {
        if (Current <= 0) return 0;
        int previous = Current;
        Current = Mathf.Min(maximum, Current + Mathf.Max(0,amount));
        return Current - previous;
    }
    public GameObject LastSource { get; private set; }
    Rigidbody body;

    void Awake() { Current = maximum; body = GetComponent<Rigidbody>(); }

    public bool TakeDamage(int amount, Vector3 impulse, GameObject source = null)
    {
        if (Current <= 0 || amount <= 0 || Time.time < protectedUntil) return false;
        if(DamageFactor!=1) amount=Mathf.FloorToInt(amount*DamageFactor+.5f);
        if(amount<=0)return false;
        protectedUntil = Time.time + invulnerability;
        LastSource = source;
        Current = Mathf.Max(0, Current - amount);
        if (body && !body.isKinematic) body.AddForce(impulse, ForceMode.Impulse);
        damaged.Invoke(amount);
        if (Current == 0) died.Invoke();
        return true;
    }
}
