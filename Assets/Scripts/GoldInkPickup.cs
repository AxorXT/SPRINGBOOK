using UnityEngine;

public sealed class GoldInkPickup : MonoBehaviour
{
    [Min(.1f)] public float attractionRadius = 2.5f;
    [Min(.1f)] public float speed = 7f;
    public Transform droplet;
    PlayerStats owner;
    int amount;
    bool drawn, collected;
    float phase;
    public void Initialize(PlayerStats player,int value)
    {
        owner=player;
        amount=value;
        phase=Random.value*Mathf.PI*2f;
    }
    void Update()
    {
        if(!owner || collected) return;
        droplet.localPosition=Vector3.up*(.32f+.09f*Mathf.Sin(Time.time*3.8f+phase));
        droplet.Rotate(0,80f*Time.deltaTime,0);
        Vector3 target=owner.transform.position+Vector3.up*.45f;
        drawn |= Vector3.Distance(transform.position,target)<=attractionRadius;
        if(!drawn) return;
        transform.position=Vector3.MoveTowards(transform.position,target,speed*Time.deltaTime);
        if(Vector3.Distance(transform.position,target)>.20f) return;
        collected=true;
        owner.AddGold(amount);
        Destroy(gameObject);
    }
}
