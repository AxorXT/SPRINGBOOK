using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public sealed class BookMenus : MonoBehaviour
{
    public enum Page { Hidden, Main, Pause, Guide, Options, Credits, Restart, Leave, Defeat, Victory }
    public Page Current {get;private set;}
    public BookRunManager run;
    public MenuDiorama diorama;
    public string developerName="";
    static BookMenus active;
    public static bool CoveringHUD => active && active.run && active.Current!=Page.Hidden;
    Canvas canvas;MenuSheet ui;RunLoot loot;
    bool leaving;
    void Awake(){active=this;}
    void Start()
    {
        BookOptions.Load();
        var root=new GameObject("Interfaz del libro",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
        var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1280,720);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        if(!FindFirstObjectByType<EventSystem>()){var events=new GameObject("Navegación UI",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform,false);}
        if(run){loot=run.GetComponent<RunLoot>();Show(Page.Hidden);}
        else {Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Show(Page.Main);}
    }
    void OnDestroy(){if(active==this)active=null;}
    public void Pause(){if(loot.OpenPanel==RunLoot.Panel.Pause)Show(Page.Pause);}
    public void Back()
    {
        if(leaving)return;
        if(Current==Page.Pause){loot.Close();Show(Page.Hidden);}
        else if(Current==Page.Guide||Current==Page.Options||Current==Page.Restart||Current==Page.Leave||Current==Page.Credits)
            Show(run?Page.Pause:Page.Main);
    }
    void Update()
    {
        if(leaving)return;
        if(!run){if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Back();return;}
        if(run.State==RunState.Defeated&&Current!=Page.Defeat)Show(Page.Defeat);
        if(run.ResultsReady&&Current!=Page.Victory)Show(Page.Victory);
    }
    public void Load(string scene)
    {
        if(leaving)return;
        leaving=true;if(loot)loot.Close();Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        SceneManager.LoadScene(scene);
    }
    void Play()
    {
        if(leaving)return;leaving=true;Show(Page.Hidden);
        diorama.Open(()=>{leaving=false;Load("Gameplay");});
    }
    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    void Show(Page page)
    {
        if(ui!=null){ui.sheet.gameObject.SetActive(false);Destroy(ui.sheet.gameObject);ui=null;}
        Current=page;if(page==Page.Hidden)return;
        bool main=page==Page.Main,terminal=page==Page.Victory||page==Page.Defeat;
        float w=main?450:page==Page.Guide?1060:page==Page.Options?730:terminal?515:510;
        float h=main?558:page==Page.Guide?620:page==Page.Options?640:terminal?500:page==Page.Pause?515:380;
        ui=new MenuSheet(canvas.transform,w,h,main,terminal);
        switch(page)
        {
            case Page.Main:
                ui.Label("SPRINGBOOK",35,48,385,72,48,MenuSheet.cream,true);
                ui.Label("Una aventura entre páginas",38,120,377,42,23,MenuSheet.gold,true);ui.Box(38,180,374,1,MenuSheet.gold);
                Button("Jugar",209,Play,w);Button("Cómo jugar",268,()=>Show(Page.Guide),w);Button("Opciones",327,()=>Show(Page.Options),w);
                Button("Créditos",386,()=>Show(Page.Credits),w);Button("Salir",445,Quit,w);break;
            case Page.Pause:
                ui.Heading("Partida en pausa",w);Button("Continuar",124,Back,w);Button("Cómo jugar",190,()=>Show(Page.Guide),w);Button("Opciones",256,()=>Show(Page.Options),w);Button("Reiniciar partida",322,()=>Show(Page.Restart),w);Button("Volver al menú principal",388,()=>Show(Page.Leave),w);break;
            case Page.Guide: Guide();break;
            case Page.Options: Options();break;
            case Page.Credits:
                ui.Heading("Créditos",w);ui.Label("SPRINGBOOK",36,110,440,40,26,MenuSheet.forest,true);
                ui.Label("Diseño y programación\n"+(string.IsNullOrWhiteSpace(developerName)?"Nombre por añadir":developerName)+"\n\nMotor: Unity     ·     Prototipo "+Application.version,36,155,440,125,19,MenuSheet.ink);
                Button("Volver",295,Back,w);break;
            case Page.Restart:case Page.Leave:
                ui.Heading(page==Page.Restart?"Reiniciar partida":"Volver al menú principal",w);
                ui.Label("Se perderá el progreso de la partida actual.",36,114,w-72,64,21,MenuSheet.ink);
                Button("Cancelar",196,Back,w);Button("Confirmar",261,()=>Load(page==Page.Restart?"Gameplay":"MainMenu"),w);break;
            default:
                ui.Heading(page==Page.Defeat?"La historia se interrumpe":"Historia completada",w);
                var stats=loot.Stats;
                string[] labels={page==Page.Defeat?"Página alcanzada":"Páginas completadas","Enemigos derrotados","Mejoras obtenidas","Tinta dorada recogida","Tiempo de partida"};
                string[] values={run.Page+" / "+run.TotalPages,run.TotalDefeated.ToString(),stats.Total.ToString(),stats.GoldEarned.ToString(),System.TimeSpan.FromSeconds(run.RunSeconds).ToString(@"hh\:mm\:ss")};
                for(int i=0;i<labels.Length;i++){ui.Label(labels[i],36,108+i*41,275,40,19,MenuSheet.ink);ui.Label(values[i],310,108+i*41,w-346,40,19,MenuSheet.forest,false,TextAnchor.MiddleRight);}
                Button(page==Page.Defeat?"Reiniciar partida":"Volver a jugar",330,()=>Load("Gameplay"),w);Button("Volver al menú principal",396,()=>Load("MainMenu"),w);break;
        }
        if(ui.first&&EventSystem.current)EventSystem.current.SetSelectedGameObject(ui.first.gameObject);
    }
    void Button(string label,float y,System.Action action,float width){ui.Button(label,36,y,width-72,48,action);}
    void Guide()
    {
        ui.Heading("Cómo jugar",1060);
        string[] keys={"WASD","Ratón","Espacio ↓","Espacio ↑","Clic izq.","Clic der.","Caída","E","Tab","Esc"};
        string[] moves={"Desplázate con pequeños rebotes.","Gira la cámara.","Mantén para cargar el salto.","Suelta para saltar.","Embestida elástica.","Impacto a partir de 1 unidad de altura tras salto cargado o aplastamiento.","Aterriza sobre un enemigo para aplastar y rebotar.","Interactúa con esquinas, cofres y mercader.","Consulta tus mejoras.","Pausa la partida."};
        float y=115;
        for(int i=0;i<keys.Length;i++){float row=i==5?53:36;ui.Box(36,y+3,97,29,MenuSheet.forest);ui.Label(keys[i],37,y+3,95,29,16,MenuSheet.cream,false,TextAnchor.MiddleCenter);ui.Label(moves[i],146,y,433,row,17,MenuSheet.ink);y+=row;}
        ui.Box(595,120,1,390,MenuSheet.gold);
        string[] rules={"Derrota a los enemigos para despejar la página.","Acércate a una esquina verde y brillante. El siguiente encuentro es un misterio.","Abre cofres y elige una mejora. Recoge la tinta dorada que dejan los enemigos.","El separador dorado te lleva al mercader. El oscuro anuncia el capítulo final.","Derrota al Devorador de Historias para completar la aventura."};
        for(int i=0;i<rules.Length;i++){GuideMark(i,126+i*78);ui.Label(rules[i],651,111+i*78,369,69,19,MenuSheet.ink);}
        ui.Button("Volver",36,542,230,47,Back);
    }
    void GuideMark(int index,float y)
    {
        var green=MenuSheet.forest;var gold=MenuSheet.gold;
        if(index==0)
        {
            ui.Box(618,y+16,27,12,green);
            ui.Box(621,y+19,5,3,gold);ui.Box(636,y+19,5,3,gold);
        }
        else if(index==1)
        {
            for(int x=0;x<2;x++)for(int z=0;z<2;z++)
                ui.Box(620+16*x,y+11+15*z,7,7,new Color(.19f,.55f,.28f)).localRotation=Quaternion.Euler(0,0,45);
        }
        else if(index==2)
        {
            ui.Box(617,y+13,28,17,green);ui.Box(617,y+18,28,3,gold);ui.Box(629,y+17,5,8,gold);
        }
        else if(index==3)
        {
            ui.Box(621,y+9,14,23,gold);ui.Box(622,y+25,9,9,gold).localRotation=Quaternion.Euler(0,0,45);
        }
        else
        {
            ui.Box(618,y+10,25,24,green);ui.Box(621,y+17,6,3,gold);ui.Box(633,y+17,6,3,gold);
        }
    }
    void Options()
    {
        ui.Heading("Opciones",730);
        string[] labels={"Volumen general","Música","Efectos sonoros","Sensibilidad del ratón","Vibración de cámara","Modo de pantalla","Resolución","Calidad gráfica","VSync"};
        for(int i=0;i<labels.Length;i++)
        {
            int row=i;float y=114+row*48;
            ui.Label(labels[row],40,y,315,39,20,MenuSheet.ink);
            var value=ui.Label(BookOptions.Value(row),398,y,190,39,18,MenuSheet.forest,false,TextAnchor.MiddleCenter);
            bool available=row!=6||BookOptions.Sizes.Length>1;
            available&=row!=7||QualitySettings.names.Length>1;
            var minus=ui.Button("−",351,y,44,38,()=>{BookOptions.Change(row,-1);value.text=BookOptions.Value(row);},available);
            var plus=ui.Button("+",593,y,44,38,()=>{BookOptions.Change(row,1);value.text=BookOptions.Value(row);},available);
            minus.name=labels[row]+" −";plus.name=labels[row]+" +";
        }
        ui.Button("Volver",36,562,180,48,Back);ui.Button("Restablecer valores predeterminados",234,562,459,48,()=>{BookOptions.Defaults();Show(Page.Options);});
    }
}