using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum PageKind { Combat, EliteCombat, Treasure, Rest, FinalBoss }

public sealed class PageEncounterGenerator : MonoBehaviour
{
    [Header("Page weights: combat, elite, treasure, rest")]
    [Tooltip("Pesos relativos desde la página 3. Cada esquina sortea entre los tipos permitidos para esa página.")]
    public Vector4 pageWeights = new Vector4(50,15,25,10);
    public const PageKind FirstPage = PageKind.Combat;
    public DifficultySettings difficulty;
    [Tooltip("Species weights: blob, spitter, golem")]
    public Vector3 combatMix = new Vector3(5,3,1), eliteMix = new Vector3(2,2,5);
    [UnityEngine.Serialization.FormerlySerializedAs("eliteGolemLimit")]
    [Range(0,2)] public int golemLimit = 2;
    public InkEnemy[] species;
    public PageReward chest, potion;
    public MeshCollider board;
    [Min(0)] public float safeDistance = 6f;
    [Min(1.5f)] public float spacing = 2.1f;
    [Range(0,1)] public float healingFraction = .25f;

    static int Roll(Vector4 weights)
    {
        float sum = 0;
        for (int i=0;i<4;i++) sum += Mathf.Max(0,weights[i]);
        float ticket = Random.value * sum;
        int last = 0;
        for (int i=0;i<4;i++)
        {
            if (weights[i] <= 0) continue;
            last = i;
            ticket -= weights[i];
            if (ticket < 0) return i;
        }
        return last;
    }
    public static int NextCombatStreak(int previous, PageKind entered)
    {
        return entered == PageKind.Combat || entered == PageKind.EliteCombat ? previous + 1 : 0;
    }

    // Called once after the encounter and any mandatory chest have been resolved.
    public PageKind[] DrawChoices(int currentPage, PageKind currentKind, int combatStreak, int unrewardedCombats)
    {
        if(currentPage+1==difficulty.totalPages)
            return new[]{PageKind.FinalBoss,PageKind.FinalBoss,PageKind.FinalBoss,PageKind.FinalBoss};
        Vector4 allowed = Vector4.one;
        if (currentPage == 1 || unrewardedCombats >= 3)
            allowed = new Vector4(0, 0, 1, 0);
        else if (currentKind == PageKind.Treasure)
            allowed = new Vector4(1, 1, 0, 0);
        else
        {
            if (combatStreak >= 3) { allowed.x = 0; allowed.y = 0; }
            if (currentKind == PageKind.Rest) allowed.w = 0;
            if (currentKind == PageKind.EliteCombat) allowed.y = 0;
        }

        Vector4 weights = Vector4.Scale(Vector4.Max(pageWeights, Vector4.zero), allowed);
        // Even a custom Inspector configuration with no positive legal weight must produce a legal route.
        if (weights.x + weights.y + weights.z + weights.w <= 0) weights = allowed;
        var choices = new PageKind[4];
        var reservation = difficulty.Reservation(currentPage + 1);
        if (reservation.role != ReservedEncounter.None)
        {
            // Prefer the configured substitute. When illegal, draw a single legal substitute for all corners.
            PageKind playable = reservation.substitute!=PageKind.FinalBoss && allowed[(int)reservation.substitute] > 0 ? reservation.substitute : (PageKind)Roll(weights);
            for (int i = 0; i < choices.Length; i++) choices[i] = playable;
        }
        else
        {
            for (int i = 0; i < choices.Length; i++) choices[i] = (PageKind)Roll(weights);
        }
        return choices;
    }

    public void Populate(int page, PageKind kind, Transform root, Vector3 player, PageEncounter encounter, out PageReward reward)
    {
        reward = null;
        Bounds b = board.bounds;
        if (kind == PageKind.Treasure || kind == PageKind.Rest)
        {
            reward = SpawnReward(kind,root);
            return;
        }
        bool elite = kind == PageKind.EliteCombat;
        Vector2Int limits = difficulty.CountRange(page, elite);
        int low = Mathf.Clamp(limits.x,1,64);
        int desired = Random.Range(low,Mathf.Clamp(limits.y,low,64)+1);
        var used = new List<Vector3>();
        var candidates = new List<Vector3>();
        // A shuffled finite grid gives a safe fallback when random samples miss.
        float pitch = Mathf.Max(1.5f,spacing);
        for (float x=b.min.x+1; x<=b.max.x-1; x+=pitch)
            for (float z=b.min.z+1; z<=b.max.z-1; z+=pitch)
                candidates.Add(new Vector3(x,b.center.y,z));
        for (int i=candidates.Count-1;i>0;i--)
        {
            int j=Random.Range(0,i+1);
            var tmp=candidates[i]; candidates[i]=candidates[j]; candidates[j]=tmp;
        }
        int golems = 0;
        for (int slot=0;slot<desired;slot++)
        {
            bool left = slot % 2 == 0;
            int index = FindSpot(candidates,used,player,left,true,out var at);
            if (index < 0) index = FindSpot(candidates,used,player,left,false,out at);
            if (index < 0) break;
            candidates.RemoveAt(index);
            used.Add(at);
            Vector3 mix=elite?eliteMix:combatMix;
            if (page < difficulty.spitterFirstPage) mix.y = 0;
            int limit = page < difficulty.secondGolemFirstPage ? Mathf.Min(1, golemLimit) : golemLimit;
            if (page < difficulty.golemFirstPage || golems >= limit) mix.z = 0;
            int id=Roll(new Vector4(mix.x,mix.y,mix.z,0));
            if (id==2) golems++;
            var enemy=Instantiate(species[id],at+Vector3.up*.06f,Quaternion.identity,root);
            difficulty.ConfigureEnemy(enemy, species[id], page);
            encounter.Register(enemy);
        }
        if (used.Count < desired)
            Debug.LogWarning($"Only {used.Count} of {desired} enemies fit the page's spawn settings.");
    }
    public PageReward SpawnReward(PageKind kind, Transform root)
    {
        Vector3 at = board.bounds.center + Vector3.forward * 1.8f;
        if (board.Raycast(new Ray(at + Vector3.up*4,Vector3.down),out var floor,8)) at = floor.point;
        return Instantiate(kind == PageKind.Rest ? potion : chest,at,Quaternion.identity,root);
    }

    int FindSpot(List<Vector3> candidates,List<Vector3> used,Vector3 player,bool left,bool restrictSide,out Vector3 point)
    {
        point=default;
        for (int i=0;i<candidates.Count;i++)
        {
            Vector3 p=candidates[i];
            if (restrictSide && (p.x<board.bounds.center.x)!=left) continue;
            if (!NavMesh.SamplePosition(p,out var nav,.65f,NavMesh.AllAreas) ||
                !board.Raycast(new Ray(nav.position+Vector3.up,Vector3.down),out var floor,2f)) continue;
            p=floor.point;
            if (Vector3.ProjectOnPlane(p-player,Vector3.up).magnitude<safeDistance) continue;
            bool free=true;
            foreach(var taken in used)
                if (Vector3.ProjectOnPlane(taken-p,Vector3.up).magnitude<spacing) { free=false; break; }
            if (!free) continue;
            point=p;
            return i;
        }
        return -1;
    }
}
