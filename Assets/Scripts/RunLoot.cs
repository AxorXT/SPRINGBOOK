using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class RunLoot : MonoBehaviour
{
    public BookRunManager run;
    public UpgradeData[] catalogue;
    public Vector3 treasureWeights=new Vector3(60,30,10), eliteWeights=new Vector3(30,50,20);
    [Min(1)] public int alternativeGold=12;
    public GoldInkPickup pickup;
    public Vector2Int blobGold=new Vector2Int(1,3), spitterGold=new Vector2Int(2,4), golemGold=new Vector2Int(5,8);
    public PlayerStats Stats { get; private set; }
    public enum Panel { Closed, Choosing, Ledger, Shop, Pause }
    public Panel OpenPanel { get; private set; }
    public IReadOnlyList<UpgradeData> Offers => offers;
    public int Selected { get; private set; } = -1;
    public string Notice { get; private set; }
    public float NoticeUntil { get; private set; }
    readonly List<UpgradeData> offers=new List<UpgradeData>();
    float savedScale;
    CursorLockMode savedLock;
    bool savedVisible;
    public bool BlockInput => OpenPanel!=Panel.Closed || Time.frameCount==closedFrame;
    int closedFrame=-1;

    void Awake(){Stats=run.motor.GetComponent<PlayerStats>();}
    public void EnemyDied(InkEnemy enemy)
    {
        if(enemy.GetComponent<Health>().LastSource!=Stats.gameObject) return;
        Stats.GetComponent<Health>().Heal(Stats.HealOnKill);
        var range=enemy.species==InkEnemy.Species.Blob?blobGold:enemy.species==InkEnemy.Species.Spitter?spitterGold:golemGold;
        Vector3 pos=enemy.transform.position;
        if(run.generator.board.Raycast(new Ray(pos+Vector3.up*2,Vector3.down),out var hit,8))pos=hit.point;
        int value=Random.Range(range.x,range.y+1);
        Instantiate(pickup,pos+Vector3.up*.06f,Quaternion.identity,run.Content).Initialize(Stats,value);
    }
    public void OpenChest()
    {
        if(OpenPanel!=Panel.Closed || run.State!=RunState.Reward) return;
        offers.Clear();
        var bag=new List<UpgradeData>();
        foreach(var data in catalogue) if(Stats.CanTake(data)&&!bag.Contains(data))bag.Add(data);
        var weights=run.Kind==PageKind.EliteCombat?eliteWeights:treasureWeights;
        while(offers.Count<3 && bag.Count>0)
        {
            var counts=new int[3];
            foreach(var data in bag)counts[(int)data.rarity]++;
            float total=0;
            for(int i=0;i<3;i++)if(counts[i]>0)total+=Mathf.Max(0,weights[i]);
            int chosen=Random.Range(0,bag.Count);
            if(total>0)
            {
                float ticket=Random.value*total;
                for(int i=0;i<bag.Count;i++)
                {
                    int tier=(int)bag[i].rarity;
                    float share=Mathf.Max(0,weights[tier])/counts[tier];
                    if(share<=0)continue;
                    chosen=i;
                    ticket-=share;
                    if(ticket<=0)break;
                }
            }
            offers.Add(bag[chosen]);
            bag.RemoveAt(chosen);
        }
        if(offers.Count==0)
        {
            Stats.AddGold(alternativeGold);
            run.FinishLoot($"+{alternativeGold} de tinta dorada");
            return;
        }
        Selected=-1;
        Show(Panel.Choosing);
    }
    public void Select(int index)
    {
        if(OpenPanel==Panel.Choosing && index>=0 && index<offers.Count) Selected=index;
    }
    public void Confirm()
    {
        if(OpenPanel!=Panel.Choosing || Selected<0 || run.State!=RunState.Reward)return;
        var data=offers[Selected];
        if(!Stats.Apply(data))return;
        Notice="Mejora obtenida · "+data.title;
        NoticeUntil=Time.unscaledTime+2.8f;
        Close();
        offers.Clear();
        run.FinishLoot(Notice);
    }
    public bool HandleKeys(Keyboard keyboard)
    {
        if(OpenPanel==Panel.Pause)
        {
            if(keyboard.escapeKey.wasPressedThisFrame)GetComponent<BookMenus>().Back();
            return true;
        }
        if(OpenPanel==Panel.Ledger && keyboard.escapeKey.wasPressedThisFrame){Close();return true;}
        if(OpenPanel==Panel.Closed && keyboard.escapeKey.wasPressedThisFrame &&
            (run.State==RunState.Combat || run.State==RunState.Cleared || run.State==RunState.Reward))
        {
            Show(Panel.Pause);GetComponent<BookMenus>().Pause();return true;
        }
        if(OpenPanel==Panel.Shop)
        {
            if(keyboard.escapeKey.wasPressedThisFrame)Close();
            return true;
        }
        if(OpenPanel==Panel.Choosing)
        {
            if(keyboard.digit1Key.wasPressedThisFrame)Select(0);
            if(keyboard.digit2Key.wasPressedThisFrame)Select(1);
            if(keyboard.digit3Key.wasPressedThisFrame)Select(2);
            if(keyboard.enterKey.wasPressedThisFrame)Confirm();
            return true;
        }
        if(keyboard.tabKey.wasPressedThisFrame && (run.State==RunState.Combat || run.State==RunState.Reward || run.State==RunState.Cleared))
        {
            if(OpenPanel==Panel.Ledger)Close();else Show(Panel.Ledger);
            return true;
        }
        return BlockInput;
    }
    public void ShowShop(){if(OpenPanel==Panel.Closed)Show(Panel.Shop);}
    void Show(Panel panel)
    {
        savedScale=Time.timeScale;
        savedLock=Cursor.lockState;
        savedVisible=Cursor.visible;
        OpenPanel=panel;
        Stats.GetComponent<SpringCombatController>().DiscardInput();
        Time.timeScale=0;
        Cursor.lockState=CursorLockMode.None;
        Cursor.visible=true;
    }
    public void Close()
    {
        if(OpenPanel==Panel.Closed)return;
        OpenPanel=Panel.Closed;
        closedFrame=Time.frameCount;
        Time.timeScale=savedScale;
        Cursor.lockState=savedLock;
        Cursor.visible=savedVisible;
        Stats.GetComponent<SpringCombatController>().DiscardInput();
    }
    void OnDisable(){Close();}
}
