using System;
using System.IO;
using UnityEngine;

// Records measured frame rate, never asserts physical-device acceptance.
public sealed class CampusPerformance : MonoBehaviour
{
    [Serializable] class Sample { public float seconds,fps,batteryTemperature_C; public bool nativeStereo,temperatureAvailable; public string device,unity; }
    StreamWriter writer;float elapsed,window;int frames;
    void Start(){Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
        string folder=Path.Combine(Application.persistentDataPath,"Evidence");Directory.CreateDirectory(folder);
        writer=new StreamWriter(Path.Combine(folder,"vr-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff")+".jsonl"));
        Debug.Log("VR_EVIDENCE "+folder);}
    void Update(){elapsed+=Time.unscaledDeltaTime;window+=Time.unscaledDeltaTime;frames++;
        if(window<1 || writer==null)return;
        var vr=GetComponent<CampusCardboard>();float temperature=Temperature();writer.WriteLine(JsonUtility.ToJson(new Sample{seconds=elapsed,fps=frames/window,
            nativeStereo=vr && vr.NativeStereo,temperatureAvailable=temperature>=0,batteryTemperature_C=temperature,device=SystemInfo.deviceModel,unity=Application.unityVersion}));writer.Flush();window=0;frames=0;
        if(elapsed>=300){writer.Dispose();writer=null;}}
    void OnDestroy(){if(writer!=null)writer.Dispose();}
    static float Temperature(){
#if UNITY_ANDROID && !UNITY_EDITOR
        try{using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var filter=new AndroidJavaObject("android.content.IntentFilter","android.intent.action.BATTERY_CHANGED"))using(var intent=activity.Call<AndroidJavaObject>("registerReceiver",null,filter)){int value=intent.Call<int>("getIntExtra","temperature",-1);return value<0?-1:value/10f;}}catch{return -1;}
#else
        return -1;
#endif
    }
}
