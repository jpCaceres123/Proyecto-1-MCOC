using UnityEngine;

// Shared logical coordinates for the GUI and all pointer hit tests.
public static class MobileViewerUI
{
    public static bool Preview;
    public static bool Enabled => Preview || Application.isMobilePlatform || Screen.width < 850 || TouchBrowser;
    private static bool TouchBrowser {
        get {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Input.touchSupported;
#else
            return false;
#endif
        }
    }
    public static int Tab = -1;
    public static bool AdvancedMass;
    public static float Scale => Mathf.Max(.5f, Mathf.Min(Screen.width, Screen.height) / 410f);
    public static float Width => Screen.width / Scale;
    public static float Height => Screen.height / Scale;
    public static Rect Safe {
        get { Rect s = Screen.safeArea; return new Rect(s.x/Scale, (Screen.height-s.yMax)/Scale, s.width/Scale, s.height/Scale); }
    }
    public static Rect Panel {
        get { Rect s=Safe; float w=Mathf.Min(410,s.width-16);
            float h=Mathf.Min(s.height*.58f, 620);
            if(Width>Height) return new Rect(s.xMax-w-8,s.y+66,w,s.height-138);
            return new Rect(s.x+8,s.yMax-70-h,s.width-16,h); }
    }
    public static bool Blocks(Vector2 screenPosition)
    {
        if(!Enabled) return screenPosition.x<285 || screenPosition.x>Screen.width-410;
        Vector2 p=new Vector2(screenPosition.x/Scale,(Screen.height-screenPosition.y)/Scale);
        Rect s=Safe;
        return !s.Contains(p) || p.y<s.y+64 || p.y>s.yMax-68 || (Tab>=0 && Panel.Contains(p));
    }
    private static GUISkin skin, previousSkin;
    private static Matrix4x4 previousMatrix;
    private static Texture2D background, button, active;
    private static Texture2D Solid(Color c) { var t=new Texture2D(1,1); t.SetPixel(0,0,c);t.Apply();return t; }
    public static void Begin()
    {
        previousMatrix=GUI.matrix;previousSkin=GUI.skin;
        if(skin==null) {
            skin=Object.Instantiate(GUI.skin);
            background=Solid(new Color(.055f,.08f,.12f,.98f));
            button=Solid(new Color(.13f,.19f,.26f)); active=Solid(new Color(.08f,.43f,.48f));
            skin.box.normal.background=background;skin.box.padding=new RectOffset(16,16,14,14);
            foreach(GUIStyle style in new[]{skin.label,skin.button,skin.toggle,skin.textField,skin.textArea}) {
                style.fontSize=16;style.normal.textColor=new Color(.94f,.96f,1f);style.wordWrap=true;
            }
            skin.label.padding=new RectOffset(0,0,6,6);
            skin.button.normal.background=button;skin.button.active.background=active;
            skin.button.onNormal.background=active;skin.button.padding=new RectOffset(8,8,10,10);
            skin.button.fixedHeight=46;
            skin.textField.fixedHeight=46;skin.textField.padding=new RectOffset(10,10,12,8);
            skin.toggle.fixedHeight=46;skin.toggle.padding=new RectOffset(26,6,12,8);
            skin.verticalScrollbar.fixedWidth=24;
        }
        GUI.skin=skin;GUI.matrix=Matrix4x4.Scale(new Vector3(Scale,Scale,1));
    }
    public static void End() { GUI.matrix=previousMatrix;GUI.skin=previousSkin; }
    public static Vector2 Scroll(Vector2 value)
    {
        if(Event.current.type==EventType.Repaint && Input.touchCount==1) {
            Touch t=Input.GetTouch(0);
            Vector2 p=new Vector2(t.position.x/Scale,(Screen.height-t.position.y)/Scale);
            if(t.phase==TouchPhase.Moved && Panel.Contains(p) && Mathf.Abs(t.deltaPosition.y)>Mathf.Abs(t.deltaPosition.x))
                value.y=Mathf.Max(0,value.y+t.deltaPosition.y/Scale);
        }
        return GUILayout.BeginScrollView(value);
    }
    public static void BeginPanel(string title)
    {
        Begin();GUILayout.BeginArea(Panel,GUI.skin.box);
        GUILayout.BeginHorizontal();GUILayout.Label(title,new GUIStyle(GUI.skin.label){fontSize=20,fontStyle=FontStyle.Bold});
        if(GUILayout.Button("Cerrar",GUILayout.Width(76))) Tab=-1;
        GUILayout.EndHorizontal();
    }
    public static void EndPanel() { GUILayout.EndArea();End(); }
    public static float PlotWidth => Enabled ? Mathf.Max(220,Panel.width-56) : 350;
}

public class MobileViewerNavigation : MonoBehaviour
{
    private Vector2 helpScroll;
    private void OnGUI()
    {
        if(!MobileViewerUI.Enabled) return;
        MobileViewerUI.Begin();Rect s=MobileViewerUI.Safe;
        GUI.Box(new Rect(s.x+8,s.y+8,s.width-16,52),GUIContent.none);
        GUI.Label(new Rect(s.x+22,s.y+14,s.width-140,38),"EDIFICIO · explorar");
        if(GUI.Button(new Rect(s.xMax-116,s.y+12,100,44),"Centrar")) {
            OrbitCamera camera=FindAnyObjectByType<OrbitCamera>();if(camera) camera.ResetView();
        }
        string[] tabs={"Modelo","Cargas","Elemento","Ayuda"};
        float width=(s.width-16)/4;
        for(int i=0;i<4;i++) {
            GUI.backgroundColor=MobileViewerUI.Tab==i?new Color(.25f,1f,.85f):Color.white;
            if(GUI.Button(new Rect(s.x+8+i*width,s.yMax-60,width-3,50),tabs[i])) MobileViewerUI.Tab=MobileViewerUI.Tab==i?-1:i;
        }
        GUI.backgroundColor=Color.white;MobileViewerUI.End();
        if(MobileViewerUI.Tab==3) {
            MobileViewerUI.BeginPanel("Cómo explorar");
            helpScroll=MobileViewerUI.Scroll(helpScroll);
            GUILayout.Label("Toca una barra, columna o muro para ver sus datos. Arrastra con un dedo para girar el edificio.");
            GUILayout.Label("Usa dos dedos: sepáralos para acercar; muévelos juntos para desplazar la vista.");
            GUILayout.Label("Modelo: muestra u oculta partes.\nCargas: cambia el caso y la deformada.\nElemento: fuerzas y capacidad de lo seleccionado.");
            GUILayout.Label("Cierra el panel para recuperar toda la vista. Centrar vuelve a la posición inicial.");
            GUILayout.EndScrollView();MobileViewerUI.EndPanel();
        }
    }
}
