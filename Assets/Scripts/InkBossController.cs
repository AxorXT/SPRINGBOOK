using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(180)]
[RequireComponent(typeof(Health),typeof(BookNavigation),typeof(BossArena))]
public sealed class InkBossController : MonoBehaviour
{
    public enum Mode { Introduction, Chase, Telegraph, Attack, Recovery, Vulnerable, Dead }
    public enum Move { Swipe, Wave, Rain, Summon }
    public const string Title="El Devorador de Historias";
    public Mode State {get;private set;}
    public Move Action {get;private set;}
    public int Phase {get;private set;}=1;
    public Health Life {get;private set;}
    [Min(.1f)] public float speed=1.6f, intro=3, swipeRange=3.25f, swipeWindup=.7f, waveWindup=1.2f, rainWindup=1.2f, summonWindup=1.2f;
    [Min(1)] public int swipeDamage=12,waveDamage=15,rainDamage=8;
    [Min(.1f)] public float waveSpeed=5,waveRadius=8,jumpClearance=.78f,poolDuration=3,poolInterval=1;
    [Range(.3f,2.3f)] public float poolRadius=1.45f;
    public Vector2Int rainCount=new Vector2Int(3,5);
    [Range(.01f,1)] public float secondPhase=.6f,thirdPhase=.3f;
    public Vector3 recoveries=new Vector3(2,1.7f,1.3f);
    [Min(1)] public int fatigueAfter=3;
    [Min(.1f)] public float vulnerableTime=3, vulnerableMultiplier=1.5f;
    public Transform shape,leftArm,rightArm,papers;
    public Renderer[] eyes;
    public Material wearyEyes;
    BookRunManager run;BossArena arena; BookNavigation navigation; Rigidbody body;
    Health victim; CapsuleCollider foot; Material awakeEyes;
    float end,began,range,priorWave,nextPoolTick,poolsUntil;
    int cycle,spent;bool crossed;
    Vector3 center,aim;
    LineRenderer signal;
    readonly List<Vector3> pools=new List<Vector3>();
    readonly List<LineRenderer> warnings=new List<LineRenderer>();
    readonly List<InkEnemy> minions=new List<InkEnemy>();
    public void Bind(BookRunManager owner){run=owner;}
    void Awake(){Life=GetComponent<Health>();body=GetComponent<Rigidbody>();arena=GetComponent<BossArena>();navigation=GetComponent<BookNavigation>();}
    void Start()
    {
        victim=run.motor.GetComponent<Health>();foot=run.motor.GetComponentInChildren<CapsuleCollider>();
        var facing=Vector3.ProjectOnPlane(run.motor.transform.position-body.position,Vector3.up);
        if(facing.sqrMagnitude>.01f)body.rotation=Quaternion.LookRotation(facing);
        arena.Bind(run.generator.board);awakeEyes=eyes[0].sharedMaterial;
        Life.died.AddListener(Die);Life.damaged.AddListener(PhaseCheck);
        Life.DamageFactor=0;Enter(Mode.Introduction,intro);
    }
    void PhaseCheck(int amount)
    {
        float fraction=(float)Life.Current/Life.maximum;
        Phase=Mathf.Max(Phase,fraction<thirdPhase?3:fraction<secondPhase?2:1);
    }
    void Enter(Mode mode,float duration){State=mode;began=Time.time;end=began+duration;}
    static float Distance(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    void FixedUpdate()
    {
        if(State==Mode.Dead||run.State!=RunState.Combat)return;
        TickPools();
        Vector3 player=run.motor.transform.position;
        float dist=Distance(player,body.position);
        if(State==Mode.Chase)
        {
            Vector3 dir=Vector3.ProjectOnPlane(player-body.position,Vector3.up);
            if(dir.sqrMagnitude>.01f)body.MoveRotation(Quaternion.RotateTowards(body.rotation,Quaternion.LookRotation(dir),160*Time.fixedDeltaTime));
            var goal=arena.Interior(player,1.55f);
            navigation.Drive(goal,dist>2.7f?speed:0);
            Move next=Next();
            if(dist<=(next==Move.Swipe?swipeRange-.25f:waveRadius))Begin(next);
            return;
        }
        navigation.Drive(body.position,0);
        if(State==Mode.Attack && Action==Move.Wave){TickWave();return;}
        if(Time.time<end)return;
        switch(State)
        {
            case Mode.Introduction: Life.DamageFactor=1;Enter(Mode.Chase,0);break;
            case Mode.Telegraph: Strike();break;
            case Mode.Attack: FinishAttack();break;
            case Mode.Recovery: Enter(Mode.Chase,0);break;
            case Mode.Vulnerable:
                Life.DamageFactor=1;foreach(var eye in eyes)eye.sharedMaterial=awakeEyes;
                Enter(Mode.Chase,0);break;
        }
    }
    Move Next()
    {
        if(Phase==1)return cycle%2==0?Move.Swipe:Move.Wave;
        Move move=(Move)(cycle%4);
        minions.RemoveAll(e=>!e||!e.gameObject.activeInHierarchy);
        return move==Move.Summon&&minions.Count==2?Move.Swipe:move;
    }
    void Begin(Move move)
    {
        Action=move;cycle++;center=body.position;
        aim=Vector3.ProjectOnPlane(run.motor.transform.position-center,Vector3.up).normalized;
        float duration=swipeWindup;
        if(move==Move.Rain){duration=rainWindup;PlanPools();}
        else if(move==Move.Summon)duration=summonWindup;
        else
        {
            bool wave=move==Move.Wave;if(wave)duration=waveWindup;
            signal=arena.Ring(wave?"Onda anunciada":"Golpe anunciado");
            arena.Draw(signal,wave?center:center+aim*swipeRange*.55f,wave?1.5f:swipeRange*.57f,.075f,new Color(1,.54f,.14f));
        }
        Enter(Mode.Telegraph,duration);
    }
    void Strike()
    {
        Enter(Mode.Attack,.22f);
        var player=run.motor.transform.position;
        switch(Action)
        {
            case Move.Swipe:
                if(Distance(player,center+aim*swipeRange*.55f)<=swipeRange*.57f&&Mathf.Abs(player.y-center.y)<2.8f)
                    victim.TakeDamage(swipeDamage,Vector3.zero,gameObject);
                break;
            case Move.Wave: priorWave=0;crossed=false;TickWave();break;
            case Move.Rain: foreach(var p in pools)arena.Pool(p,poolRadius);poolsUntil=Time.time+poolDuration;nextPoolTick=Time.time;break;
            case Move.Summon: SpawnMinions();break;
        }
    }
    void TickWave()
    {
        range=Mathf.Min(waveRadius,(Time.time-began)*waveSpeed);
        arena.Draw(signal,center,range,.20f,new Color(1,.23f,.60f));
        float d=Distance(run.motor.transform.position,center);
        if(!crossed&&d>=priorWave-.48f&&d<=range+.48f)
        {
            crossed=true;
            if(run.motor.BookClearance(run.generator.board,foot,out float h)&&h<jumpClearance)
                victim.TakeDamage(waveDamage,Vector3.zero,gameObject);
        }
        priorWave=range;
        if(range>=waveRadius)FinishAttack();
    }
    void FinishAttack()
    {
        if(signal)Destroy(signal.gameObject);
        if(++spent>=fatigueAfter)
        {
            spent=0;Life.DamageFactor=vulnerableMultiplier;
            foreach(var eye in eyes)eye.sharedMaterial=wearyEyes;
            Enter(Mode.Vulnerable,vulnerableTime);
        }
        else Enter(Mode.Recovery,recoveries[Phase-1]);
    }
    void PlanPools()
    {
        ClearPools();
        int count=Random.Range(Mathf.Clamp(rainCount.x,3,5),Mathf.Clamp(rainCount.y,3,5)+1);
        float radius=Mathf.Min(poolRadius,2.3f);
        for(int attempt=0;attempt<100&&pools.Count<count;attempt++)
        {
            var b=run.generator.board.bounds;
            var desired=attempt==0?run.motor.transform.position:new Vector3(Random.Range(b.min.x,b.max.x),b.center.y,Random.Range(b.min.z,b.max.z));
            if(!arena.Spot(desired,radius+.35f,out var p)||!arena.PoolFits(p,radius)||pools.Exists(q=>Distance(q,p)<radius*2+.4f))continue;
            pools.Add(p);var line=arena.Ring("Lluvia anunciada");warnings.Add(line);
            arena.Draw(line,p,radius,.09f,new Color(1,.40f,.12f));
        }
    }
    void TickPools()
    {
        if(poolsUntil<=0)return;
        if(Time.time>=poolsUntil){ClearPools();return;}
        if(Time.time<nextPoolTick)return;
        var player=run.motor.transform.position;
        if(pools.Exists(p=>Distance(player,p)<=poolRadius)&&run.motor.BookClearance(run.generator.board,foot,out var height)&&height<jumpClearance)
        {
            victim.TakeDamage(rainDamage,Vector3.zero,gameObject);nextPoolTick=Time.time+poolInterval;
        }
    }
    void ClearPools()
    {
        foreach(var line in warnings)if(line)Destroy(line.gameObject);
        warnings.Clear();pools.Clear();poolsUntil=0;arena.ClearPools();
    }
    void SpawnMinions()
    {
        minions.RemoveAll(e=>!e||!e.gameObject.activeInHierarchy);
        var g=run.generator;
        for(int i=0;i<32&&minions.Count<2;i++)
        {
            float angle=i*2.4f;
            var pos=body.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(4+i%7);
            if(!arena.Spot(pos,1,out var at)||Distance(at,run.motor.transform.position)<g.safeDistance||
                Distance(at,body.position)<3||minions.Exists(e=>Distance(at,e.transform.position)<g.spacing))continue;
            var e=Instantiate(g.species[0],at+Vector3.up*.06f,Quaternion.identity,arena.Effects);
            g.difficulty.ConfigureEnemy(e,g.species[0],5);minions.Add(e);
            e.Defeated+=run.CountSummon;
        }
    }
    void Die()
    {
        Enter(Mode.Dead,1.3f);Life.DamageFactor=0;
        arena.Effects.gameObject.SetActive(false);Destroy(arena.Effects.gameObject);
        foreach(var hitbox in GetComponentsInChildren<Collider>())hitbox.enabled=false;
        body.isKinematic=true;run.BossDefeated();
    }
    void Update()
    {
        float t=Mathf.Clamp01((Time.time-began)/Mathf.Max(.01f,end-began));
        float size=State==Mode.Introduction?Mathf.SmoothStep(.015f,1,t):State==Mode.Dead?1-Mathf.SmoothStep(0,1,t):1;
        float squat=State==Mode.Vulnerable?.22f:State==Mode.Telegraph?.14f*t:0;
        float spread=State==Mode.Dead ? (1-t*t*t)*(1+.8f*t) : size;
        shape.localScale=new Vector3(spread*(1+squat*.2f),size*size*(1-squat),spread*(1+squat*.2f));
        float angle=State==Mode.Telegraph?-80*t:State==Mode.Attack?25:8*Mathf.Sin(Time.time*2);
        leftArm.localRotation=Quaternion.Euler(angle,0,-16);rightArm.localRotation=Quaternion.Euler(angle,0,16);
        papers.Rotate(0,30*Time.deltaTime,0);
    }
    // Frame the forming silhouette, then hand the view back to the normal follow camera.
    void LateUpdate()
    {
        if(State!=Mode.Introduction || !run || !run.orbit.enabled || run.State!=RunState.Combat)return;
        var camera=run.orbit.transform;
        float blend=Mathf.SmoothStep(0,1,Mathf.Clamp01((end-Time.time)/.65f));
        Vector3 subject=body.position+Vector3.up*2.05f;
        Vector3 direction=Quaternion.Euler(0,14,0)*transform.forward;
        Vector3 shot=subject+direction*8.4f+Vector3.up*1.45f;
        camera.SetPositionAndRotation(Vector3.Lerp(camera.position,shot,blend),
            Quaternion.Slerp(camera.rotation,Quaternion.LookRotation(subject-shot),blend));
    }
    void OnGUI()
    {
        if(BookMenus.CoveringHUD)return;
        if(!Life||!run||run.State!=RunState.Combat||run.motor.Loot.OpenPanel!=RunLoot.Panel.Closed)return;
        var previous=GUI.matrix;
        float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float w=455,x=(Screen.width/scale-w)/2;
        GUI.Box(new Rect(x,12,w,59),Title+"    "+Life.Current+" / "+Life.maximum);
        var track=new Rect(x+12,46,w-24,12);var old=GUI.color;
        GUI.color=Color.black;GUI.DrawTexture(track,Texture2D.whiteTexture);
        GUI.color=State==Mode.Vulnerable?Color.yellow:new Color(.82f,.19f,.48f);GUI.DrawTexture(new Rect(track.x,track.y,track.width*Life.Current/Life.maximum,12),Texture2D.whiteTexture);
        GUI.color=Color.white;foreach(float threshold in new[]{secondPhase,thirdPhase})GUI.DrawTexture(new Rect(track.x+track.width*threshold,track.y,2,12),Texture2D.whiteTexture);
        GUI.color=old;
        if(State==Mode.Introduction||State==Mode.Vulnerable)GUI.Box(new Rect(x,75,w,32),State==Mode.Introduction?"El último capítulo · "+Title:"Agotado · Vulnerable");
        GUI.matrix=previous;
    }
}