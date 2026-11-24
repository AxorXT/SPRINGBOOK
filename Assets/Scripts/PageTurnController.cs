using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class PageTurnController : MonoBehaviour
{
    public MeshCollider book;
    [Min(.1f)] public float duration = 1.5f;
    [Range(0,.6f)] public float curl = .34f;
    const int Across=32, Along=10;
    Mesh sheet;
    Vector3[] vertices;
    MeshRenderer paper;
    void Awake()
    {
        paper=GetComponent<MeshRenderer>();
        paper.enabled=false;
        sheet=new Mesh {name="Turning leaf"};
        sheet.MarkDynamic();
        int face=(Across+1)*(Along+1);
        vertices=new Vector3[face*2];
        int[] triangles=new int[Across*Along*12];
        int k=0;
        for(int side=0;side<2;side++)
        for(int row=0;row<Along;row++)
        for(int col=0;col<Across;col++)
        {
            int a=side*face+row*(Across+1)+col,b=a+1,c=a+Across+1,d=c+1;
            if(side==0){triangles[k++]=a;triangles[k++]=c;triangles[k++]=b;triangles[k++]=b;triangles[k++]=c;triangles[k++]=d;}
            else {triangles[k++]=a;triangles[k++]=b;triangles[k++]=c;triangles[k++]=b;triangles[k++]=d;triangles[k++]=c;}
        }
        sheet.vertices=vertices;sheet.triangles=triangles;
        GetComponent<MeshFilter>().sharedMesh=sheet;
    }
    public IEnumerator Turn(Vector3 corner)
    {
        Bounds b=book.bounds;
        float side=Mathf.Sign(corner.x-b.center.x), end=Mathf.Sign(corner.z-b.center.z);
        paper.enabled=true;
        for(float t=0;t<1;t+=Time.deltaTime/Mathf.Max(.1f,duration))
        {
            for(int z=0;z<=Along;z++)
            {
                float v=(float)z/Along;
                float delay=(end>0?1-v:v)*.12f;
                float u=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-delay)/(1-.12f)));
                float x=b.center.x,y=b.max.y+.025f;
                for(int i=0;i<=Across;i++)
                {
                    int index=z*(Across+1)+i;
                    var p=new Vector3(x,y,Mathf.Lerp(b.min.z,b.max.z,v));
                    vertices[index]=vertices[index+vertices.Length/2]=transform.InverseTransformPoint(p);
                    float angle=Mathf.PI*(u+curl*Mathf.Sin(Mathf.PI*u)*((i+.5f)/Across-.5f));
                    x+=side*Mathf.Cos(angle)*b.extents.x/Across;
                    y+=Mathf.Sin(angle)*b.extents.x/Across;
                }
            }
            sheet.vertices=vertices;sheet.RecalculateNormals();sheet.RecalculateBounds();
            yield return null;
        }
        paper.enabled=false;
    }
    void OnDestroy(){if(sheet) Destroy(sheet);}
}
