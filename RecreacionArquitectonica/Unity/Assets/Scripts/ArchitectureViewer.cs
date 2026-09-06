using System;
using System.Collections;
using System.IO;
using UnityEngine;

public class ArchitectureViewer : MonoBehaviour {
    public Camera view;
    public Transform architecture;
    public Transform landscape;
    public Vector3 focus=new Vector3(9,9,0);
    float yaw=153,pitch=24,distance=100;
    bool panel=true,walk=false;
    GUIStyle title,label,small,button;
    void Start() {
        Application.targetFrameRate=60;
        SetView(0);
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke")>=0)StartCoroutine(Capture());
    }
    public void SetView(int v) {
        walk=false;
        if(v==0){focus=new Vector3(8,9,1);yaw=-22;pitch=20;distance=107;}
        if(v==1){focus=new Vector3(7,8,4);yaw=158;pitch=25;distance=102;}
        if(v==2){focus=new Vector3(8,8,0);yaw=-29;pitch=54;distance=109;}
        if(v==3){focus=new Vector3(33,10,-1);yaw=-38;pitch=10;distance=47;}
        PositionCamera();
    }
    void PositionCamera(){view.transform.rotation=Quaternion.Euler(pitch,yaw,0);view.transform.position=focus-view.transform.forward*distance;}
    void Update() {
        if(Input.GetKeyDown(KeyCode.Tab))panel=!panel;
        for(int i=0;i<4;i++)if(Input.GetKeyDown(KeyCode.Alpha1+i))SetView(i);
        if(Input.GetKeyDown(KeyCode.F)){walk=!walk;}
        if(Input.GetMouseButton(1)){yaw+=Input.GetAxis("Mouse X")*3;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-75,85);}
        float speed=(Input.GetKey(KeyCode.LeftShift)?25:9)*Time.deltaTime;
        Vector3 forward=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up).normalized;
        Vector3 translation=(forward*Input.GetAxis("Vertical")+view.transform.right*Input.GetAxis("Horizontal"))*speed;
        if(Input.GetKey(KeyCode.E))translation+=Vector3.up*speed;
        if(Input.GetKey(KeyCode.Q))translation-=Vector3.up*speed;
        if(walk){view.transform.rotation=Quaternion.Euler(pitch,yaw,0);view.transform.position+=translation;focus=view.transform.position+view.transform.forward*distance;}
        else {
            focus+=translation;
            distance=Mathf.Clamp(distance*(1-Input.mouseScrollDelta.y*.08f),6,200);
            if(Input.GetMouseButton(2))focus+=(-view.transform.right*Input.GetAxis("Mouse X")-view.transform.up*Input.GetAxis("Mouse Y"))*distance*.015f;
            PositionCamera();
        }
    }
    void Styles(){if(title!=null)return;
        title=new GUIStyle(GUI.skin.label){fontSize=23,fontStyle=FontStyle.Bold,wordWrap=true};title.normal.textColor=Color.white;
        label=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};label.normal.textColor=new Color(.82f,.86f,.89f);
        small=new GUIStyle(label){fontSize=12};
        button=new GUIStyle(GUI.skin.button){fontSize=14,fixedHeight=36,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(12,6,4,4)};
    }
    void OnGUI(){if(!panel)return;Styles();
        GUI.color=new Color(.055f,.085f,.10f,.94f);GUI.DrawTexture(new Rect(22,22,302,440),Texture2D.whiteTexture);GUI.color=Color.white;
        GUILayout.BeginArea(new Rect(40,37,268,410));
        GUILayout.Label("ESTUDIO DE FACHADAS",small);GUILayout.Space(8);
        GUILayout.Label("Edificio de ingeniería",title);GUILayout.Space(7);
        GUILayout.Label("Recreación arquitectónica a partir de fotografías y geometría de referencia.",label);GUILayout.Space(14);
        if(GUILayout.Button("01   Fachada de vidrio",button))SetView(0);
        if(GUILayout.Button("02   Fachada ocre y terraza",button))SetView(1);
        if(GUILayout.Button("03   Cubierta y conjunto",button))SetView(2);
        if(GUILayout.Button("04   Acceso y voladizo",button))SetView(3);
        GUILayout.Space(10);
        GUILayout.Label("Arrastrar botón derecho: girar · Rueda: zoom\nBotón central: desplazar · WASD: mover\nQ/E: bajar/subir · F: vuelo libre · Tab: ocultar",small);
        GUILayout.Space(8);GUILayout.Label("Versión aproximada · Dimensiones en metros",small);
        GUILayout.EndArea();
    }
    IEnumerator Capture(){
        panel=false;
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Capturas"));Directory.CreateDirectory(dir);
        for(int i=0;i<4;i++){
            SetView(i);yield return new WaitForSeconds(1.5f);yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(dir,"vista_"+(i+1)+".png"),texture.EncodeToPNG());Destroy(texture);
        }
        File.WriteAllText(Path.Combine(dir,"verificacion_unity.txt"),"Escena cargada y cuatro vistas renderizadas.\nObjetos arquitectónicos: "+architecture.GetComponentsInChildren<Renderer>().Length+"\n");
        Debug.Log("ARCHITECTURE_SMOKE_OK");Application.Quit();
    }
}
