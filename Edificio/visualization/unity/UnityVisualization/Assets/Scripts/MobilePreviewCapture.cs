using System;
using System.Collections;
using System.IO;
using UnityEngine;

// Opt-in desktop capture of the real mobile GUI, never enabled in a normal run.
public class MobilePreviewCapture : MonoBehaviour
{
    private static string directory;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--mobile-preview");
        if(i<0 || i+1>=args.Length) return;
        directory=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(directory);
        MobileViewerUI.Preview=true;
        var owner=new GameObject("MobilePreviewCapture");DontDestroyOnLoad(owner);owner.AddComponent<MobilePreviewCapture>();
    }
    private IEnumerator Start()
    {
        Application.runInBackground=true;
        yield return new WaitForSeconds(3);
        MobileViewerUI.Tab=-1;
        Debug.Assert(!MobileViewerUI.Blocks(new Vector2(Screen.width*.5f,Screen.height*.5f)),"El centro libre debe permitir explorar");
        Debug.Assert(MobileViewerUI.Blocks(new Vector2(Screen.width*.5f,10)),"La barra inferior debe bloquear cámara");
        for(int tab=-1;tab<4;tab++) {
            MobileViewerUI.Tab=tab;
            if(tab==2) {
                ElementInspector inspector=FindAnyObjectByType<ElementInspector>();
                if(inspector) inspector.SelectElementById("Columna",15);
            }
            if(tab>=0) {
                Rect panel=MobileViewerUI.Panel;
                Debug.Assert(MobileViewerUI.Blocks(new Vector2(panel.center.x*MobileViewerUI.Scale,Screen.height-panel.center.y*MobileViewerUI.Scale)),"El panel debe bloquear cámara");
                Debug.Assert(panel.width>=330 && panel.height>100,"Panel utilizable");
            }
            yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"pantalla_"+(tab+1)+".png"));
            yield return new WaitForSeconds(.5f);
        }
        Debug.Log("MOBILE_UI_CHECKS_DONE: "+Screen.width+"x"+Screen.height);
        Application.Quit(0);
    }
}
