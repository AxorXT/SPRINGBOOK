using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

public static class BookOptions
{
    const string Prefix="SPRINGBOOK.Options.";
    public static float Master=1, Music=1, SFX=1, Sensitivity=1;
    public static bool Shake=true, Fullscreen, VSync;
    public static int Quality, ResolutionIndex;
    public static Vector2Int[] Sizes {get;private set;}
    static AudioMixer mixer;
    static int defaultQuality,defaultSync,defaultSize;
    static bool initialized;
    static int Read(string key,int value)=>PlayerPrefs.GetInt(Prefix+key,value);
    static float ReadFloat(string key,float value)=>PlayerPrefs.GetFloat(Prefix+key,value);
    public static void Load()
    {
        if(!initialized)
        {
            mixer=Resources.Load<AudioMixer>("Springbook Audio");
            defaultQuality=QualitySettings.GetQualityLevel();defaultSync=QualitySettings.vSyncCount;
            Sizes=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Distinct().ToArray();
            var native=new Vector2Int(Screen.currentResolution.width,Screen.currentResolution.height);
            if(Sizes.Length==0)Sizes=new[]{native.x>0?native:new Vector2Int(Screen.width,Screen.height)};
            defaultSize=Mathf.Max(0,System.Array.IndexOf(Sizes,native));
            initialized=true;
        }
        Master=Mathf.Clamp01(ReadFloat("Master",1));Music=Mathf.Clamp01(ReadFloat("Music",1));SFX=Mathf.Clamp01(ReadFloat("SFX",1));
        Sensitivity=Mathf.Clamp(ReadFloat("Sensitivity",1),.25f,3);Shake=Read("Shake",1)!=0;
        Fullscreen=Read("Fullscreen",Application.isEditor||Screen.fullScreen?1:0)!=0;Quality=Mathf.Clamp(Read("Quality",defaultQuality),0,QualitySettings.names.Length-1);
        var size=new Vector2Int(Read("Width",Sizes[defaultSize].x),Read("Height",Sizes[defaultSize].y));
        ResolutionIndex=System.Array.IndexOf(Sizes,size);if(ResolutionIndex<0)ResolutionIndex=defaultSize;
        VSync=Read("VSync",defaultSync)!=0;
        Apply(PlayerPrefs.HasKey(Prefix+"Fullscreen"));
    }
    static void Apply(bool display)
    {
        mixer.SetFloat("Master",Decibels(Master));mixer.SetFloat("Music",Decibels(Music));mixer.SetFloat("SFX",Decibels(SFX));
        if(QualitySettings.GetQualityLevel()!=Quality)QualitySettings.SetQualityLevel(Quality,true);
        QualitySettings.vSyncCount=VSync?1:0;
        if(display)
        {
            var size=Sizes[ResolutionIndex];var mode=Fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
            if(Screen.width!=size.x||Screen.height!=size.y||Screen.fullScreenMode!=mode) Screen.SetResolution(size.x,size.y,mode);
        }
    }
    static float Decibels(float value)=>value<=0?-80:Mathf.Max(-80,20*Mathf.Log10(value));
    public static void Change(int row,int step)
    {
        switch(row)
        {
            case 0: Master=Mathf.Clamp01(Master+step*.1f);break;
            case 1: Music=Mathf.Clamp01(Music+step*.1f);break;
            case 2: SFX=Mathf.Clamp01(SFX+step*.1f);break;
            case 3: Sensitivity=Mathf.Clamp(Sensitivity+step*.25f,.25f,3);break;
            case 4: Shake=!Shake;break;
            case 5: Fullscreen=!Fullscreen;break;
            case 6: ResolutionIndex=(ResolutionIndex+step+Sizes.Length)%Sizes.Length;break;
            case 7: Quality=(Quality+step+QualitySettings.names.Length)%QualitySettings.names.Length;break;
            case 8: VSync=!VSync;break;
        }
        Save();Apply(row==5||row==6);
    }
    static void Save()
    {
        PlayerPrefs.SetFloat(Prefix+"Master",Master);PlayerPrefs.SetFloat(Prefix+"Music",Music);PlayerPrefs.SetFloat(Prefix+"SFX",SFX);
        PlayerPrefs.SetFloat(Prefix+"Sensitivity",Sensitivity);PlayerPrefs.SetInt(Prefix+"Shake",Shake?1:0);
        PlayerPrefs.SetInt(Prefix+"Fullscreen",Fullscreen?1:0);PlayerPrefs.SetInt(Prefix+"Width",Sizes[ResolutionIndex].x);PlayerPrefs.SetInt(Prefix+"Height",Sizes[ResolutionIndex].y);
        PlayerPrefs.SetInt(Prefix+"Quality",Quality);PlayerPrefs.SetInt(Prefix+"VSync",VSync?1:0);PlayerPrefs.Save();
    }
    public static void Defaults()
    {
        Master=Music=SFX=Sensitivity=1;Shake=Fullscreen=true;VSync=defaultSync!=0;Quality=defaultQuality;ResolutionIndex=defaultSize;Save();Apply(true);
    }
    public static string Value(int row)
    {
        switch(row){case 0:return Master.ToString("P0");case 1:return Music.ToString("P0");case 2:return SFX.ToString("P0");
        case 3:return Sensitivity.ToString("0.##")+" ×";case 4:return Shake?"Activada":"Desactivada";case 5:return Fullscreen?"Completa":"Ventana";
        case 6:var s=Sizes[ResolutionIndex];return s.x+" × "+s.y;case 7:return QualitySettings.names[Quality];default:return VSync?"Activado":"Desactivado";}
    }
}