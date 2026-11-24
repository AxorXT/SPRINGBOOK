using System;
using System.Collections;
using UnityEngine;

public sealed class MenuDiorama : MonoBehaviour
{
    public Transform lid,view,hero;
    Vector3 startView,heroStart;
    Quaternion baseAngle;
    bool opening;
    void Awake(){startView=view.position;baseAngle=view.rotation;heroStart=hero.position;}
    void Update()
    {
        if(opening)return;
        float t=Time.unscaledTime;
        view.position=startView+view.right*(.07f*Mathf.Sin(t*.43f));
        view.rotation=baseAngle*Quaternion.Euler(.22f*Mathf.Sin(t*.35f),.35f*Mathf.Sin(t*.29f),0);
        hero.position=heroStart+Vector3.up*.025f*Mathf.Sin(t*2);
    }
    public void Open(Action ready){if(!opening)StartCoroutine(Turn(ready));}
    IEnumerator Turn(Action ready)
    {
        opening=true;
        for(float t=0;t<1;t+=Time.unscaledDeltaTime/1.15f)
        {
            float a=Mathf.SmoothStep(0,1,t);lid.localRotation=Quaternion.AngleAxis(a*160,Vector3.forward);
            view.position=startView+baseAngle*Vector3.forward*(a*.8f);yield return null;
        }
        ready();
    }
}