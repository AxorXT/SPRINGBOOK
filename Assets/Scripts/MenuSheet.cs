using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class MenuSheet
{
    public static readonly Color forest=new Color(.065f,.18f,.145f),cream=new Color(.98f,.94f,.83f),gold=new Color(.73f,.57f,.29f), ink=new Color(.15f,.12f,.085f);
    static readonly Font body=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    static readonly Font title=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","DejaVu Serif","Arial"},52);
    public RectTransform sheet;
    public Button first;
    public MenuSheet(Transform canvas,float w,float h,bool cover,bool terminal)
    {
        sheet=Rect(canvas,"Hoja",0,0,w,h);
        sheet.anchorMin=sheet.anchorMax=new Vector2(cover?.23f:terminal?.72f:.5f,.5f);
        sheet.pivot=new Vector2(.5f,.5f);sheet.anchoredPosition=Vector2.zero;
        Paint(sheet,cover?forest:cream);
        Box(12,12,w-24,2,gold);Box(12,h-14,w-24,2,gold);Box(12,14,2,h-28,gold);Box(w-14,14,2,h-28,gold);
    }
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,-y);return r;
    }
    static Image Paint(RectTransform r,Color color)
    {
        var image=r.gameObject.AddComponent<Image>();image.color=color;return image;
    }
    public RectTransform Box(float x,float y,float w,float h,Color color)
    {
        var r=Rect(sheet,"Filete",x,y,w,h);Paint(r,color).raycastTarget=false;return r;
    }
    public Text Label(string s,float x,float y,float w,float h,int size,Color color,bool serif=false,TextAnchor align=TextAnchor.MiddleLeft)
    {
        var r=Rect(sheet,s,x,y,w,h);var t=r.gameObject.AddComponent<Text>();
        t.font=serif&&title?title:body;t.fontSize=size;t.text=s;t.color=color;t.alignment=align;t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }
    public Button Button(string s,float x,float y,float w,float h,Action clicked,bool enabled=true)
    {
        var r=Rect(sheet,s,x,y,w,h);var img=Paint(r,Color.white);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=img;
        var c=b.colors;c.normalColor=new Color(.11f,.27f,.215f);c.highlightedColor=new Color(.23f,.39f,.28f);c.selectedColor=new Color(.26f,.37f,.23f);c.pressedColor=new Color(.40f,.29f,.14f);c.disabledColor=new Color(.40f,.42f,.38f);b.colors=c;b.interactable=enabled;b.onClick.AddListener(()=>clicked());
        var text=Label(s,x+3,y,w-6,h,20,cream,false,TextAnchor.MiddleCenter);text.transform.SetParent(r,true);
        if(!first&&enabled)first=b;return b;
    }
    public void Heading(string s,float width){Label(s,36,28,width-72,60,36,forest,true);Box(36,96,width-72,1,gold);}
}