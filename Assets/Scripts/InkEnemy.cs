using UnityEngine;

[RequireComponent(typeof(Health), typeof(BookNavigation))]
public sealed class InkEnemy : MonoBehaviour
{
    public enum Species { Blob, Spitter, Golem }
    public Species species;
    [Header("Perception and movement")]
    [Min(0)] public float detection = 12f, speed = 3.2f;
    [Header("Contact attack")]
    [Min(1)] public int damage = 10;
    [Min(.1f)] public float reach = .96f, cooldown = 1f, verticalReach = .85f;
    [Header("Ranged attack")]
    public InkProjectile projectile;
    public Transform muzzle;
    public Vector2 preferredRange = new Vector2(6,8);
    [Min(.1f)] public float projectileSpeed = 6f;
    [Header("Golem impact")]
    [Min(1)] public int slamDamage = 25;
    [Min(.1f)] public float slamRadius = 2.2f, preparation = .72f, slamCooldown = 3f;
    public LineRenderer warning;
    [Header("Appearance")]
    public Transform shape, bar, fill;
    public Renderer[] ink;
    public GameObject stain;
    public Color hitColor = new Color(.88f,.79f,.55f);
    [Min(.02f)] public float flashDuration = .13f;
    public event System.Action<InkEnemy> Defeated;
    public bool Preparing { get; private set; }
    Health health, player;
    Rigidbody body;
    BookNavigation navigation;
    Camera viewer;
    MaterialPropertyBlock flash;
    float nextAttack, nextSlam, strikeAt, flashEnd, pulse;
    Vector3 slamCenter;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        navigation = GetComponent<BookNavigation>();
        health = GetComponent<Health>();
        flash = new MaterialPropertyBlock();
        flash.SetColor("_BaseColor", hitColor);
    }
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Health>();
        viewer = Camera.main;
    }
    void OnEnable() { health.damaged.AddListener(OnHit); health.died.AddListener(Die); }
    void OnDisable() { health.damaged.RemoveListener(OnHit); health.died.RemoveListener(Die); }

    public float ActiveFrom { get; private set; }
    public void DelayAIUntil(float time) { ActiveFrom = time; }

    void FixedUpdate()
    {
        if (Time.time < ActiveFrom) return;
        Vector3 goal = player.transform.position;
        Vector3 offset = Vector3.ProjectOnPlane(goal - body.position, Vector3.up);
        float range = offset.magnitude;
        if (Preparing)
        {
            navigation.Drive(body.position, 0);
            if (Time.time >= strikeAt)
            {
                Preparing = false;
                warning.enabled = false;
                Vector3 delta = goal - slamCenter;
                if (Mathf.Abs(delta.y) <= verticalReach && Vector3.ProjectOnPlane(delta,Vector3.up).magnitude <= slamRadius)
                    player.TakeDamage(slamDamage, Vector3.zero);
                pulse = 1;
                nextAttack = Time.time + cooldown;
            }
            return;
        }
        if (range > detection || player.Current == 0) { navigation.Drive(body.position, 0); return; }
        if (offset.sqrMagnitude > .005f)
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(offset), 360f * Time.fixedDeltaTime));
        if (species == Species.Spitter)
        {
            Vector3 destination = goal;
            float pace = speed;
            if (range < preferredRange.x)
                destination = body.position - offset.normalized * 2.6f;
            else if (range <= preferredRange.y) pace = 0;
            navigation.Drive(destination, pace);
            if (Time.time >= nextAttack)
            {
                Vector3 aim = goal + Vector3.up * .72f - muzzle.position;
                Instantiate(projectile, muzzle.position, Quaternion.LookRotation(aim), transform.parent).Launch(player, damage, projectileSpeed);
                nextAttack = Time.time + cooldown;
                pulse = .65f;
            }
            return;
        }
        if (species == Species.Golem && range <= slamRadius && Time.time >= nextSlam && Time.time >= nextAttack)
        {
            slamCenter = body.position;
            Preparing = true;
            strikeAt = Time.time + preparation;
            nextSlam = strikeAt + slamCooldown;
            warning.enabled = true;
            for (int i=0; i<warning.positionCount; i++)
            {
                float a = Mathf.PI*2*i/warning.positionCount;
                Vector3 p = slamCenter + new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*slamRadius;
                if (Physics.Raycast(p+Vector3.up*2f,Vector3.down,out var hit,5,1,QueryTriggerInteraction.Ignore))
                    p.y = hit.point.y;
                warning.SetPosition(i,p+Vector3.up*.045f);
            }
            navigation.Drive(body.position, 0);
            return;
        }
        navigation.Drive(goal, range > reach * .94f ? speed : 0);
        if (range <= reach && Mathf.Abs(goal.y-body.position.y) <= verticalReach && Time.time >= nextAttack)
        {
            player.TakeDamage(damage, Vector3.zero);
            nextAttack = Time.time + cooldown;
            pulse = .8f;
        }
    }

    void OnHit(int amount)
    {
        navigation.Stagger();
        foreach (var part in ink) part.SetPropertyBlock(flash);
        flashEnd = Time.time + flashDuration;
        fill.localScale = new Vector3((float)health.Current / health.maximum, 1, 1);
    }
    void LateUpdate()
    {
        if (viewer) bar.rotation = viewer.transform.rotation;
        if (flashEnd > 0 && Time.time >= flashEnd)
        {
            foreach (var part in ink) part.SetPropertyBlock(null);
            flashEnd = 0;
        }
        pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime*4f);
        float squash = Preparing ? .22f : pulse*.15f;
        shape.localScale = new Vector3(1+squash*.5f,1-squash,1+squash*.5f);
    }
    void Die()
    {
        if (stain && UnityEngine.AI.NavMesh.SamplePosition(body.position,out var floor,3f,UnityEngine.AI.NavMesh.AllAreas))
        {
            Vector3 p = floor.position;
            if (Physics.Raycast(p + Vector3.up*.5f,Vector3.down,out var hit,2,1,QueryTriggerInteraction.Ignore))
                p = hit.point;
            Instantiate(stain,p+Vector3.up*.018f,Quaternion.Euler(0,body.rotation.eulerAngles.y,0), transform.parent);
        }
        Defeated?.Invoke(this);
        gameObject.SetActive(false);
    }
}
