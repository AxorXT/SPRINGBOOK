using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public sealed class BossArena : MonoBehaviour
{
    public Material signal, ink;
    MeshCollider board;
    public Transform Effects {get; private set;}
    public void Bind(MeshCollider surface)
    {
        board=surface;
        Effects=new GameObject("Tinta del capítulo final").transform;
        Effects.SetParent(transform.parent,false);
    }
    public bool Floor(Vector3 location,out Vector3 point)
    {
        location.y=board.bounds.max.y+2;
        if(board.Raycast(new Ray(location,Vector3.down),out var hit,8)){point=hit.point;return true;}
        point=default;return false;
    }
    public Vector3 Interior(Vector3 p,float margin)
    {
        var b=board.bounds;
        p.x=Mathf.Clamp(p.x,b.min.x+margin,b.max.x-margin);
        p.z=Mathf.Clamp(p.z,b.min.z+margin,b.max.z-margin);
        return p;
    }
    public bool Spot(Vector3 desired,float margin,out Vector3 p)
    {
        p=default;
        return Floor(Interior(desired,margin),out var floor) &&
            NavMesh.SamplePosition(floor,out var nav,.7f,NavMesh.AllAreas) && Floor(nav.position,out p);
    }
    public LineRenderer Ring(string label)
    {
        var go=new GameObject(label);go.transform.SetParent(Effects,false);
        var ring=go.AddComponent<LineRenderer>();ring.sharedMaterial=signal;ring.loop=true;
        ring.positionCount=72;ring.useWorldSpace=true;return ring;
    }
    public void Draw(LineRenderer line,Vector3 center,float radius,float width,Color color)
    {
        line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;
        for(int i=0;i<line.positionCount;i++)
        {
            float a=i*Mathf.PI*2/line.positionCount;
            var pos=Interior(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,.04f);
            if(Floor(pos,out var floor))pos=floor;
            line.SetPosition(i,pos+Vector3.up*.055f);
        }
    }
    public bool PoolFits(Vector3 center,float radius)
    {
        for(int i=0;i<32;i++)
        {
            float a=i*Mathf.PI/16;
            if(!Floor(center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius,out _))return false;
        }
        return true;
    }
    public void Pool(Vector3 center,float radius)
    {
        // A fan projected onto the actual spread also follows its central crease.
        var go=new GameObject("Charco de tinta");go.transform.SetParent(Effects,false);
        var points=new Vector3[33];var triangles=new int[96];points[0]=go.transform.InverseTransformPoint(center+Vector3.up*.03f);
        for(int i=0;i<32;i++)
        {
            float a=i*2*Mathf.PI/32;
            var pos=center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;
            Floor(pos,out var floor);points[i+1]=go.transform.InverseTransformPoint(floor+Vector3.up*.035f);
            triangles[i*3+1]=1+(i+1)%32;triangles[i*3+2]=1+i;
        }
        var mesh=new Mesh{name="Ink pool"};mesh.vertices=points;mesh.triangles=triangles;mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=ink;
        pools.Add(go);meshes.Add(mesh);
    }
    readonly List<GameObject> pools=new List<GameObject>();
    readonly List<Mesh> meshes=new List<Mesh>();
    public void ClearPools(){foreach(var p in pools)if(p)Destroy(p);pools.Clear();foreach(var mesh in meshes)if(mesh)Destroy(mesh);meshes.Clear();}
    void OnDestroy(){ClearPools();}
}