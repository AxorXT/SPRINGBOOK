using System;
using UnityEngine;

[RequireComponent(typeof(RunLoot))]
public sealed class BookmarkEvents : MonoBehaviour
{
    public EventBookmark merchantPrefab, omenPrefab;
    [Range(0,1)] public float merchantChance = .25f;
    public int[] guaranteedPages = {5,10,14};
    [Min(0)] public int healingPrice=12, commonPrice=20, rarePrice=35, epicPrice=55;
    [Range(0,1)] public float healingFraction=.25f;
    public EventBookmark Merchant {get; private set;}
    public EventBookmark Omen {get; private set;}
    public bool WithinReach => Merchant && Merchant.isActiveAndEnabled && Merchant.InReach(loot.run.motor.transform.position);
    readonly UpgradeData[] stock=new UpgradeData[3];
    readonly bool[] sold=new bool[4];
    RunLoot loot;
    int decidedPage, lastVisit;
    void Awake(){loot=GetComponent<RunLoot>();}
    public void PageCleared()
    {
        var run=loot.run;
        if(run.State!=RunState.Cleared || decidedPage==run.Page)return;
        decidedPage=run.Page;
        var bounds=run.generator.board.bounds;
        if(run.Page==run.TotalPages-1)
            Omen=Spawn(omenPrefab,bounds,-1);
        if(run.Page<3 && run.Page!=run.TotalPages-1)return;
        if(run.Page!=run.TotalPages-1 && Array.IndexOf(guaranteedPages,run.Page)<0 &&
           (lastVisit==run.Page-1 || UnityEngine.Random.value>=merchantChance))return;
        lastVisit=run.Page;
        Merchant=Spawn(merchantPrefab,bounds,1);
        Array.Clear(sold,0,sold.Length);
        Array.Clear(stock,0,stock.Length);
        var eligibleCounts=new int[3];
        foreach(var item in loot.catalogue)
        {
            if(!loot.Stats.CanTake(item))continue;
            int tier=(int)item.rarity;
            if(UnityEngine.Random.Range(0,++eligibleCounts[tier])==0)stock[tier]=item;
        }
    }
    EventBookmark Spawn(EventBookmark prefab,Bounds book,int side)
    {
        var anchor=new Vector3(book.center.x+side*(book.extents.x-.15f),book.max.y-.18f,book.center.z);
        return Instantiate(prefab,anchor,Quaternion.Euler(0,side>0?0:180,0),loot.run.Content);
    }
    public bool Visit()
    {
        if(loot.run.State!=RunState.Cleared || !WithinReach || loot.BlockInput)return false;
        loot.ShowShop();
        return true;
    }
    public UpgradeData Item(int slot)=>slot>0 && slot<=3 ? stock[slot-1]:null;
    public int Price(int slot)=>slot==0?healingPrice:slot==1?commonPrice:slot==2?rarePrice:epicPrice;
    public bool Sold(int slot)=>sold[slot];
    public bool CanBuy(int slot)
    {
        if(loot.OpenPanel!=RunLoot.Panel.Shop || loot.run.State!=RunState.Cleared || slot<0 || slot>=4 || sold[slot] || loot.Stats.Gold<Price(slot))return false;
        var health=loot.Stats.GetComponent<Health>();
        return slot==0 ? health.Current>0 && health.Current<health.maximum && HealingAmount(health)>0 : loot.Stats.CanTake(Item(slot));
    }
    int HealingAmount(Health health)=>Mathf.RoundToInt(health.maximum*healingFraction);
    public bool Buy(int slot)
    {
        if(!CanBuy(slot))return false;
        var health=loot.Stats.GetComponent<Health>();
        bool delivered=slot==0 ? health.Heal(HealingAmount(health))>0 : loot.Stats.Apply(Item(slot));
        if(!delivered)return false;
        loot.Stats.SpendGold(Price(slot));
        sold[slot]=true;
        return true;
    }
}