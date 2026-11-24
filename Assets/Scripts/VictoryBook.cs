using System.Collections;
using UnityEngine;

public static class VictoryBook
{
    public static IEnumerator Close(BookRunManager run)
    {
        yield return new WaitForSeconds(1.3f);
        var board=run.generator.board;
        var bounds=board.bounds;
        var hinge=new Vector3(bounds.center.x,bounds.max.y+.03f,bounds.center.z);
        var stage=new GameObject("Libro de resultados").transform;
        stage.SetParent(run.transform,false);
        stage.position=hinge+Vector3.right*500;
        var leaf=new GameObject("Tapa y hojas derechas").transform;
        leaf.SetParent(stage,false);
        foreach(var original in board.transform.parent.GetComponentsInChildren<MeshRenderer>())
        {
            if(!original.enabled || original.transform.IsChildOf(run.sheet.transform))continue;
            bool corner=false;
            foreach(var marker in run.corners)if(original==marker.marker)corner=true;
            if(corner)continue;
            var mesh=original.GetComponent<MeshFilter>();
            if(!mesh||!mesh.sharedMesh)continue;
            var copy=new GameObject(original.name);
            copy.transform.SetPositionAndRotation(stage.position+original.transform.position-hinge,original.transform.rotation);
            copy.transform.localScale=original.transform.lossyScale;
            copy.transform.SetParent(original.bounds.center.x>hinge.x+.1f?leaf:stage,true);
            copy.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
            copy.AddComponent<MeshRenderer>().sharedMaterials=original.sharedMaterials;
        }
        var camera=run.orbit.GetComponent<Camera>();
        var target=stage.position+Vector3.left*(bounds.extents.x*.22f)+Vector3.up*3;
        var rot=Quaternion.Euler(32,14,0);
        float fov=Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
        float distance=Mathf.Max(26,bounds.extents.x/(fov*Mathf.Min(1,camera.aspect))+8);
        camera.transform.SetPositionAndRotation(target+rot*Vector3.back*distance,rot);
        const float duration=1.35f;
        for(float t=0;t<duration;t+=Time.deltaTime)
        {
            leaf.localRotation=Quaternion.AngleAxis(179*Mathf.SmoothStep(0,1,t/duration),Vector3.forward);
            yield return null;
        }
        leaf.localRotation=Quaternion.AngleAxis(179,Vector3.forward);
    }
}