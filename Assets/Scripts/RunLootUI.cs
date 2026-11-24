using UnityEngine;

[RequireComponent(typeof(RunLoot))]
public sealed class RunLootUI : MonoBehaviour
{
    RunLoot loot;
    static readonly Color paper=new Color(.97f,.91f,.76f), ink=new Color(.11f,.18f,.15f);
    static readonly Color[] tiers={new Color(.27f,.38f,.24f),new Color(.14f,.34f,.50f),new Color(.45f,.23f,.48f)};
    static readonly string[] names={"Común","Rara","Épica"};
    GUIStyle label;
    void Awake(){loot=GetComponent<RunLoot>();}
    static void Fill(Rect rect,Color color)
    {
        Color old=GUI.color; GUI.color=color; GUI.DrawTexture(rect,Texture2D.whiteTexture); GUI.color=old;
    }
    void Text(Rect r,string text,int size,Color color,TextAnchor align=TextAnchor.MiddleCenter,bool bold=false)
    {
        label.fontSize=size; label.normal.textColor=color; label.alignment=align;
        label.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;
        GUI.Label(r,text,label);
    }
    void OnGUI()
    {
        if(BookMenus.CoveringHUD)return;
        if(!loot.Stats || loot.run.State==RunState.Defeated)return;
        if(label==null)label=new GUIStyle(GUI.skin.label){wordWrap=true};
        GUI.depth=-10;
        if(loot.OpenPanel==RunLoot.Panel.Closed)
        {
            GUI.Box(new Rect(22,20,240,34),$"Tinta dorada  {loot.Stats.Gold}     ·     Mejoras  {loot.Stats.Total}  [Tab]");
            if(Time.unscaledTime<loot.NoticeUntil)GUI.Box(new Rect(Screen.width*.5f-170,72,340,34),loot.Notice);
            return;
        }
        Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.045f,.075f,.07f,.88f));
        var old=GUI.matrix;
        float scale=Mathf.Min(Screen.width/960f,Screen.height/590f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-940*scale)/2,(Screen.height-560*scale)/2,0),Quaternion.identity,Vector3.one*scale);
        Fill(new Rect(0,0,940,560),paper);
        Fill(new Rect(12,12,916,2),tiers[0]); Fill(new Rect(12,546,916,2),tiers[0]);
        if(loot.OpenPanel==RunLoot.Panel.Shop) DrawShop();
        else if(loot.OpenPanel==RunLoot.Panel.Choosing) DrawChoices(); else DrawLedger();
        GUI.matrix=old;
    }
    void DrawShop()
    {
        var shop=loot.GetComponent<BookmarkEvents>();
        GUI.DrawTexture(new Rect(28,21,103,91),shop.Merchant.illustration,ScaleMode.ScaleToFit);
        Text(new Rect(148,18,550,49),"Mercader de Historias",32,ink,TextAnchor.MiddleLeft,true);
        Text(new Rect(150,65,530,32),"Tinta dorada disponible   "+loot.Stats.Gold,19,tiers[0],TextAnchor.MiddleLeft);
        for(int slot=0;slot<4;slot++)
        {
            float y=122+slot*87;
            var item=shop.Item(slot);
            string name=slot==0?"Curación":item?item.title:"Sin existencias";
            string detail=slot==0?$"Recupera el {shop.healingFraction:P0} de tu vida máxima.":item?item.description:"Ya has obtenido todas las mejoras disponibles de esta rareza.";
            string tier=slot==0?"Servicio":names[slot-1];
            Color accent=slot==0?ink:tiers[slot-1];
            Fill(new Rect(30,y,880,80),new Color(1,.965f,.85f));
            Fill(new Rect(30,y,5,80),accent);
            Text(new Rect(48,y+4,485,31),name,21,ink,TextAnchor.MiddleLeft,true);
            Text(new Rect(48,y+31,545,45),detail,16,ink,TextAnchor.MiddleLeft);
            Text(new Rect(599,y+5,129,32),tier,17,accent,TextAnchor.MiddleCenter,true);
            Text(new Rect(599,y+37,129,30),shop.Price(slot)+" tintas",16,ink);
            bool enabled=GUI.enabled;
            GUI.enabled=shop.CanBuy(slot);
            bool full=slot==0 && loot.Stats.GetComponent<Health>().Current==loot.Stats.MaxHealth;
            string action=shop.Sold(slot)?"Agotado":slot>0&&!item?"Sin existencias":full?"Vida completa":loot.Stats.Gold<shop.Price(slot)?"Falta tinta":"Comprar";
            if(GUI.Button(new Rect(742,y+20,150,40),action))shop.Buy(slot);
            GUI.enabled=enabled;
        }
        if(GUI.Button(new Rect(345,486,250,43),"Cerrar tienda   [Esc]"))loot.Close();
    }
    void DrawChoices()
    {
        Text(new Rect(20,24,900,52),"Elige una mejora",31,ink,TextAnchor.MiddleCenter,true);
        float start=(940-loot.Offers.Count*286)/2f;
        for(int i=0;i<loot.Offers.Count;i++)
        {
            var d=loot.Offers[i];int tier=(int)d.rarity;float x=start+i*286;
            Rect frame=new Rect(x,90,272,336);
            Fill(frame,loot.Selected==i?ink:new Color(.68f,.61f,.45f));
            Fill(new Rect(x+4,94,264,328),new Color(1,.968f,.86f));
            Fill(new Rect(x+4,94,264,7),tiers[tier]);
            Text(new Rect(x+12,104,248,32),names[tier],16,tiers[tier],TextAnchor.MiddleCenter,true);
            GUI.DrawTexture(new Rect(x+96,144,80,80),d.icon,ScaleMode.ScaleToFit,true);
            Text(new Rect(x+10,226,252,58),d.title,22,ink,TextAnchor.MiddleCenter,true);
            Text(new Rect(x+18,284,236,78),d.description,16,ink);
            Text(new Rect(x+10,372,252,38),$"Acumulaciones   {loot.Stats.Count(d)} / {d.limit}",15,tiers[tier]);
            if(GUI.Button(frame,GUIContent.none,GUIStyle.none))loot.Select(i);
        }
        GUI.enabled=loot.Selected>=0;
        if(GUI.Button(new Rect(335,452,270,48),"Confirmar elección"))loot.Confirm();
        GUI.enabled=true;
        Text(new Rect(30,507,880,24),"Elige con el ratón o con 1–3. Confirma con el botón o con Enter.",14,ink);
    }
    void DrawLedger()
    {
        Text(new Rect(25,18,890,55),"Mejoras de esta partida",28,ink,TextAnchor.MiddleCenter,true);
        int n=0;
        foreach(var d in loot.catalogue)
        {
            int count=loot.Stats.Count(d);if(count==0)continue;
            float x=30+(n%2)*448, y=86+(n/2)*102; n++;
            GUI.DrawTexture(new Rect(x,y+12,50,50),d.icon);
            Text(new Rect(x+60,y,365,34),d.title+"   "+count+"/"+d.limit,17,tiers[(int)d.rarity],TextAnchor.MiddleLeft,true);
            Text(new Rect(x+60,y+34,360,56),d.description,14,ink,TextAnchor.UpperLeft);
        }
        if(n==0)Text(new Rect(60,200,820,60),"Aún no has obtenido mejoras.",20,ink);
        Text(new Rect(30,502,880,34),"Tab para volver al libro",16,ink);
    }
}
