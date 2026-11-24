using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RunState { Preparing, Combat, Reward, Cleared, Transitioning, Defeated, Completed }

public sealed class BookRunManager : MonoBehaviour
{
    public InkBossController bossPrefab;
    public InkBossController Boss {get;private set;}
    float runStarted,finished;
    public float RunSeconds => (State==RunState.Completed||State==RunState.Defeated?finished:Time.time)-runStarted;
    public void CountSummon(InkEnemy enemy){TotalDefeated++;}
    public void BossDefeated()
    {
        if(State!=RunState.Combat || Kind!=PageKind.FinalBoss || Boss.Life.Current!=0)return;
        TotalDefeated++;
        life.DamageFactor=0;
        EndRun(RunState.Completed);
        orbit.enabled=false;
        StartCoroutine(VictoryBook.Close(this));
        Cursor.lockState=CursorLockMode.None;
        Cursor.visible=true;
    }
    public void Replay()
    {
        if(restarting || (State!=RunState.Completed&&State!=RunState.Defeated))return;
        restarting=true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public PageEncounter encounter;
    public PageEncounterGenerator generator;
    public PageTurnController sheet;
    public PageCorner[] corners;
    public SpringPlayerController motor;
    public ThirdPersonCamera orbit;
    [Min(0)] public float preparation = .8f;
    [Min(0)] public float combatPreparation = 2f;
    float combatStartsAt;
    public int Page { get; private set; }
    public int TotalPages => generator.difficulty.totalPages;
    public string PageLabel => $"Página {Mathf.Max(1,Page)} / {TotalPages}";
    public int CombatStreak { get; private set; }
    public int UnrewardedCombats { get; private set; }
    int upgradesAtPageStart;
    public int TotalDefeated { get; private set; }
    public PageKind Kind { get; private set; }
    public RunState State { get; private set; }
    public int Remaining => Kind==PageKind.FinalBoss ? (Boss && Boss.Life && Boss.Life.Current>0 ? 1 : 0) : encounter.Remaining;
    public Transform Content { get; private set; }
    BookmarkEvents bookmarks;
    RunLoot loot;
    Health life;
    SpringCombatController combat;
    Rigidbody body;
    CombatFeedback feedback;
    PageReward reward;
    PageCorner nearby;
    string receipt;
    bool restarting;
    Quaternion previousView;
    Vector3 previousPosition;
    void Start()
    {
        runStarted=Time.time;
        bookmarks=GetComponent<BookmarkEvents>();
        loot=GetComponent<RunLoot>();
        motor.Loot=loot;
        life=motor.GetComponent<Health>();
        combat=motor.GetComponent<SpringCombatController>();
        feedback=motor.GetComponent<CombatFeedback>();
        body=motor.GetComponent<Rigidbody>();
        life.died.AddListener(Defeat);
        encounter.pageCleared.AddListener(Clear);
        encounter.EnemyDefeated+=Count;
        LockCorners();
        Freeze();
        StartCoroutine(Prepare(PageEncounterGenerator.FirstPage));
    }
    void Count(InkEnemy enemy)
    {
        TotalDefeated++;
        loot.EnemyDied(enemy);
        if (Remaining==0 && Kind==PageKind.EliteCombat && State==RunState.Combat) EliteChest();
    }
    void EliteChest()
    {
        reward=generator.SpawnReward(PageKind.Treasure,Content);
        State=RunState.Reward;
    }
    public void FinishLoot(string message)
    {
        if(State!=RunState.Reward || !reward || !reward.Used) return;
        receipt=message;
        encounter.AwaitingReward=false;
        encounter.Complete();
    }
    void OnDestroy()
    {
        if(life) life.died.RemoveListener(Defeat);
        if(encounter){encounter.EnemyDefeated-=Count;encounter.pageCleared.RemoveListener(Clear);}
    }
    void LockCorners(){nearby=null;foreach(var c in corners)c.Lock();}
    void Freeze()
    {
        motor.Respawn();
        motor.enabled=false;
        combat.enabled=false;
        feedback.ClearTransient();
        body.isKinematic=true;
    }
    void Resume()
    {
        body.isKinematic=false;
        combat.enabled=true;
        motor.enabled=true;
    }
    IEnumerator Prepare(PageKind kind)
    {
        State=RunState.Preparing;
        Kind=kind;
        upgradesAtPageStart = loot.Stats.Total;
        receipt=null;
        encounter.ResetEncounter();
        var root=new GameObject("Page "+(Page+1)+" · "+kind);
        Content=root.transform;
        Content.SetParent(encounter.transform,false);
        root.SetActive(false);
        encounter.AwaitingReward=kind==PageKind.EliteCombat;
        reward=null;
        if(kind==PageKind.FinalBoss)
        {
            var at=generator.board.bounds.center+Vector3.right*5;
            if(generator.board.Raycast(new Ray(at+Vector3.up*4,Vector3.down),out var hit,8))at=hit.point;
            Boss=Instantiate(bossPrefab,at+Vector3.up*.06f,Quaternion.identity,Content);
            Boss.Bind(this);
        }
        else generator.Populate(Page + 1,kind,Content,body.position,encounter,out reward);
        if (reward) yield return new WaitForSeconds(preparation);
        else if(kind!=PageKind.FinalBoss)
        {
            combatStartsAt = Time.time + combatPreparation;
            foreach (var enemy in root.GetComponentsInChildren<InkEnemy>(true))
                enemy.DelayAIUntil(combatStartsAt);
        }
        Page++;
        CombatStreak = PageEncounterGenerator.NextCombatStreak(CombatStreak, kind);
        State=reward?RunState.Reward:RunState.Combat;
        root.SetActive(true);
        Resume();
        if(kind!=PageKind.FinalBoss && State==RunState.Combat && Remaining==0)
        {
            if(kind==PageKind.EliteCombat) EliteChest(); else encounter.Complete();
        }
    }
    void Clear()
    {
        if(State!=RunState.Combat && State!=RunState.Reward)return;
        if (Kind==PageKind.FinalBoss) return;
        State=RunState.Cleared;
        bool gainedUpgrade = loot.Stats.Total > upgradesAtPageStart;
        UnrewardedCombats = gainedUpgrade ? 0 : PageEncounterGenerator.NextCombatStreak(UnrewardedCombats, Kind);
        var choices = generator.DrawChoices(Page, Kind, CombatStreak, UnrewardedCombats);
        for (int i = 0; i < corners.Length; i++) corners[i].Offer(choices[i], encounter.unlocked);
        bookmarks.PageCleared();
    }
    void Update()
    {
        var keyboard=Keyboard.current;
        if(keyboard==null)return;
        if(State==RunState.Defeated || State==RunState.Completed)
        {
            if(!restarting && keyboard.rKey.wasPressedThisFrame)
            {
                Replay();
            }
            return;
        }
        if(loot.HandleKeys(keyboard)) return;
        if(State==RunState.Reward && reward.InReach && !reward.Used && keyboard.eKey.wasPressedThisFrame)
        {
            reward.Consume();
            if(Kind==PageKind.Rest)
            {
                int amount=life.Heal(Mathf.RoundToInt(life.maximum*generator.healingFraction));
                receipt=$"Curación utilizada · +{amount} de vida";
            }
            else { loot.OpenChest(); return; }
            encounter.Complete();
            return;
        }
        if(State!=RunState.Cleared)return;
        if(keyboard.eKey.wasPressedThisFrame && bookmarks.Visit())return;
        nearby=null;
        foreach(var c in corners) if(c.Available && c.PlayerInside){nearby=c;break;}
        if(nearby && keyboard.eKey.wasPressedThisFrame) StartCoroutine(Advance(nearby));
    }
    IEnumerator Advance(PageCorner selected)
    {
        State=RunState.Transitioning;
        var chosen=selected.Choice;
        LockCorners();
        previousView=orbit.transform.rotation;
        Freeze();
        previousPosition=motor.transform.Find("CameraTarget").position+previousView*Vector3.back*orbit.distance;
        orbit.enabled=false;
        Bounds b=generator.board.bounds;
        var camera=orbit.GetComponent<Camera>();
        float distance=Mathf.Max(27f,b.extents.x/(Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*camera.aspect)+9f);
        var angle=Quaternion.Euler(35,0,0);
        Vector3 focus=b.center+Vector3.up*3.6f;
        orbit.transform.SetPositionAndRotation(focus+angle*Vector3.back*distance,angle);
        Content.gameObject.SetActive(false);
        Destroy(Content.gameObject);
        reward=null;
        yield return sheet.Turn(selected.marker.bounds.center);
        orbit.transform.SetPositionAndRotation(previousPosition,previousView);
        orbit.enabled=true;
        yield return Prepare(chosen);
    }
    void Defeat()
    {
        EndRun(RunState.Defeated);
    }
    void EndRun(RunState endState)
    {
        if (State==RunState.Defeated || State==RunState.Completed) return;
        loot.Close();
        StopAllCoroutines();
        finished=Time.time;
        State=endState;
        LockCorners();
        if(endState==RunState.Defeated)orbit.enabled=false;
        if(Content && endState!=RunState.Completed)Content.gameObject.SetActive(false);
        motor.enabled=false;combat.enabled=false;
        body.linearVelocity=Vector3.zero;
        body.isKinematic=true;
        feedback.ClearTransient();
    }
    public bool ResultsReady => State==RunState.Completed && Time.time-finished>=2.7f;
    void DrawVictory()
    {
        float elapsed=Time.time-finished;
        if(elapsed<2.7f)
        {
            GUI.Box(new Rect(Screen.width*.5f-120,65,240,40),"Victoria · Fin del último capítulo");
            return;
        }

    }
    void OnGUI()
    {
        if(BookMenus.CoveringHUD)return;
        if(State==RunState.Completed){DrawVictory();return;}
        if(State==RunState.Defeated)return;
        if(loot && loot.OpenPanel!=RunLoot.Panel.Closed)return;
        string status=State==RunState.Combat && Kind!=PageKind.FinalBoss ? $"   ·   Enemigos {Remaining}" : "";
        GUI.Box(new Rect(Screen.width-220,20,200,32),$"{PageLabel}{status}");
        string message=null;
        if(State==RunState.Combat && Time.time<combatStartsAt)
            message=$"Prepárate · El combate comienza en {Mathf.CeilToInt(combatStartsAt-Time.time)}";
        if(State==RunState.Transitioning)message="Pasando la página…";
        if(State==RunState.Preparing)message="Preparando encuentro…";
        if(State==RunState.Cleared)
            message=nearby ? nearby.Prompt :string.IsNullOrEmpty(receipt)?encounter.clearMessage:receipt+"\n"+encounter.clearMessage;
        if(State==RunState.Cleared && bookmarks.WithinReach)message="Presiona E para visitar al mercader";
        if(State==RunState.Cleared && bookmarks.Omen)GUI.Box(new Rect(Screen.width*.5f-155,80,310,32),"El último capítulo te espera");
        if(State==RunState.Reward)
            message=Kind==PageKind.Rest?(reward.InReach?"Presiona E para curarte":"Descanso · Busca la poción"):
                (reward.InReach?"Presiona E para abrir el cofre":"Tesoro · Busca el cofre");
        if(message!=null)GUI.Box(new Rect(Screen.width*.5f-230,20,460,48),message);
    }
}
