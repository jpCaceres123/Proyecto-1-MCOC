using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using UnityEngine;

// SQ4: incremental response only. The slab transfers load; it has no plate DOFs.
public sealed partial class MovingLoadViewer : MonoBehaviour
{
    [Serializable] public class Node { public int id; public float[] xyz; }
    [Serializable] public class Bar { public int id,i,j; public float[] x,y,z; public float E,A,Iy,Iz; }
    [Serializable] public class Group { public int[] ids; }
    [Serializable] public class Hole { public float xmin,xmax,ymin,ymax; }
    [Serializable] public class Panel { public int id; public float xmin,xmax,ymin,ymax,z; public int[] receivers; public Group[] groups; public Hole[] voids; public string rule,status; }
    [Serializable] public class Data { public int schema; public Node[] nodes; public Bar[] bars; public Panel[] panels; }
    public static MovingLoadViewer Instance { get; private set; }
    public static bool Active => Instance != null && Instance.active;
    private Data data;
    private bool active, playing, dirty=true, savedDeformed, savedForces;
    private int panelIndex, receiver;
    private float xi=.5f, eta=.5f, magnitude=1f, speed=.5f, scale=1000f;
    private double[] u,f,reaction;
    private Vector3[] xyz;
    private readonly Dictionary<int,int> barIndex=new Dictionary<int,int>();
    private readonly List<LineRenderer> lines=new List<LineRenderer>();
    private int usedLines;
    private Transform root, avatar;
    private Transform panelFill;
    private Rect savedViewport;
    private Camera viewCamera;
    private Material lineMaterial, avatarMaterial;
    private GUIStyle box,title,muted,metric,button;
    private Texture2D background;
    private Vector2 scroll;
    private string error;
    private float peakMoment, peakPosition, maxDisplacement;
    private double transferError, momentError;
    private Color teal=new Color(.16f,.88f,.76f), amber=new Color(1f,.69f,.22f), pink=new Color(1f,.37f,.62f);
    private Panel Current => data.panels[panelIndex];
    private Rect Dock => new Rect(Screen.width-350,74,338,Mathf.Max(150,Screen.height-86));
    private Rect ChartArea => new Rect(264,Screen.height-245,Mathf.Max(200,Screen.width-626),233);
    public static bool Blocks(Vector2 screen)
    {
        if(!Active) return false;
        var p=new Vector2(screen.x,Screen.height-screen.y);
        return Instance.Dock.Contains(p)||Instance.ChartArea.Contains(p);
    }

    private void Awake() { Instance=this; }
    private IEnumerator Start()
    {
        string[] args=Environment.GetCommandLineArgs();int capture=Array.IndexOf(args,"--moving-preview");
        if(capture<0 || capture+1>=args.Length) yield break;
        Application.runInBackground=true;
        yield return new WaitForSeconds(2);
        Toggle();if(!active){Application.Quit(1);yield break;}
        string directory=Path.GetFullPath(args[capture+1]);Directory.CreateDirectory(directory);
        bool passed=RuntimeChecks();
        magnitude=50;panelIndex=1;xi=.5f;eta=.5f;scale=3000;Focus();dirty=true;
        yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"carga_movil_centro.png"));
        yield return new WaitForSeconds(.5f);
        xi=.27f;eta=.82f;dirty=true;
        yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"carga_movil_reparto.png"));
        yield return new WaitForSeconds(.5f);
        magnitude=0;dirty=true;
        yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"carga_movil_cero.png"));
        yield return new WaitForSeconds(.5f);
        int cantilever=Array.FindIndex(data.panels,p=>p.status=="EXPLICIT_CANTILEVER");
        magnitude=20;SelectPanel(cantilever);showFloorMap=true;
        yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"carga_movil_voladizo.png"));
        yield return new WaitForSeconds(.5f);
        int holePanel=Array.FindIndex(data.panels,p=>p.voids.Length>0);
        SelectPanel(holePanel);
        yield return new WaitForSeconds(1);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,"carga_movil_vacios.png"));
        yield return new WaitForSeconds(.5f);
        Debug.Log(passed?"SQ4_RUNTIME_OK":"SQ4_RUNTIME_FAILED");Application.Quit(passed?0:1);
    }

    private bool RuntimeChecks()
    {
        double largest=0,largestMoment=0;bool positions=true;var categories=new HashSet<string>();
        for(int index=0;index<data.panels.Length;index++) {
            panelIndex=index;SafeCenter();
            positions&=Valid(Current,Mathf.Lerp(Current.xmin,Current.xmax,xi),Mathf.Lerp(Current.ymin,Current.ymax,eta));
            if(!categories.Add(Current.status+Current.z))continue;
            magnitude=37;Recalculate();largestMoment=Math.Max(largestMoment,momentError);
            foreach(int tag in ActiveIds) {
                var b=data.bars[barIndex[tag]];
                largest=Math.Max(largest,Vector3.Distance(Displacement(b,1),Motion(b.j)));
            }
        }
        magnitude=0;Recalculate();foreach(double value in u) largest=Math.Max(largest,Math.Abs(value));
        Panel[] savedPanels=data.panels;int savedPanel=panelIndex;
        data.panels=new[]{new Panel{xmin=0,xmax=1,ymin=0,ymax=1,z=0,voids=new Hole[0]},new Panel{xmin=1.001f,xmax=2,ymin=0,ymax=1,z=0,voids=new Hole[0]}};panelIndex=0;
        bool gapBlocked=!CanWalk(.9f,.5f,1.1f,.5f);
        data.panels=new[]{new Panel{xmin=0,xmax=2,ymin=0,ymax=1,z=0,voids=new[]{new Hole{xmin=.9999f,xmax=1.0001f,ymin=0,ymax=1}}}};
        bool holeBlocked=!CanWalk(.9f,.5f,1.1f,.5f);
        data.panels=savedPanels;panelIndex=savedPanel;positions&=gapBlocked && holeBlocked;
        bool ok=positions && largest<1e-6 && largestMoment<.002;
        Debug.Log("SQ4_RUNTIME panels="+data.panels.Length+" categories="+categories.Count+" validCenters="+positions+" gapBlocked="+gapBlocked+" holeBlocked="+holeBlocked+" momentError="+largestMoment.ToString("G10"));
        Debug.Log("SQ4_RUNTIME endpoint and zero-load error [m]: "+largest.ToString("G10"));
        return ok;
    }
    private bool Load()
    {
        if(data!=null) return true;
        if(error!=null) return false;
        try {
            var asset=Resources.Load<TextAsset>("carga_movil");
            if(!asset) throw new Exception("Ejecute carga_movil.py para generar las bases OpenSees.");
            data=JsonUtility.FromJson<Data>(asset.text);
            if(data.schema!=2 || data.panels.Length==0) throw new Exception("Regenerar SQ4: se requiere contrato versión 2.");
            xyz=new Vector3[data.nodes.Length];
            for(int k=0;k<xyz.Length;k++) xyz[k]=V(data.nodes[k].xyz);
            for(int k=0;k<data.bars.Length;k++) barIndex.Add(data.bars[k].id,k);
            u=new double[xyz.Length*6]; f=new double[data.bars.Length*12]; reaction=new double[6];
            root=new GameObject("SQ4_CargaMovil").transform; root.SetParent(transform,false);
            var shader=Resources.Load<Shader>("SQ4Overlay");
            if(!shader)throw new Exception("Falta el shader SQ4Overlay en Resources.");
            lineMaterial=new Material(shader);
            avatarMaterial=new Material(shader); avatarMaterial.color=amber;
            avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;
            avatar.name="Usuario_carga_localizada"; avatar.SetParent(root,false);
            avatar.localScale=new Vector3(.30f,.45f,.30f);
            avatar.GetComponent<Renderer>().sharedMaterial=avatarMaterial;
            Destroy(avatar.GetComponent<Collider>());
            panelFill=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            panelFill.name="SQ4_PanelActivo";panelFill.SetParent(root,false);
            panelFill.rotation=Quaternion.Euler(90,0,0);
            panelFill.GetComponent<Renderer>().sharedMaterial=lineMaterial;
            Destroy(panelFill.GetComponent<Collider>());
            root.gameObject.SetActive(false);
            return true;
        } catch(Exception ex) { error=ex.Message; data=null; Debug.LogError("SQ4: "+ex); return false; }
    }

    public void Toggle()
    {
        if(!Load()) return;
        active=!active; playing=false; root.gameObject.SetActive(active);
        var viewer=GetComponent<Semana3Visualizer>();
        if(active) {
            viewCamera=Camera.main?Camera.main:FindAnyObjectByType<Camera>();
            if(viewCamera) savedViewport=viewCamera.rect;
            GetComponent<BuildingVisualizer>().BeginMovingView();
            savedDeformed=viewer.ShowDeformed; savedForces=viewer.ShowForces;
            viewer.SetDisplay(false,false,viewer.DeformationScale);
            StructuralPostprocessor.Get(gameObject).ClearSelection();
            SafeCenter();Recalculate();Render();Focus(); dirty=true;
        } else {
            if(viewCamera) viewCamera.rect=savedViewport;
            GetComponent<BuildingVisualizer>().EndMovingView();
            viewer.SetDisplay(savedDeformed,savedForces,viewer.DeformationScale);
        }
    }

    public void DrawLauncher()
    {
        GUILayout.Space(12);
        if(GUILayout.Button(active?"Cerrar carga móvil":"Explorar carga móvil",GUILayout.Height(34))) Toggle();
        if(error!=null) GUILayout.Label(error);
    }

    private void Focus()
    {
        var p=Current;
        GetComponent<BuildingVisualizer>().FocusMovingPanel(p.id,p.z);
        var camera=FindAnyObjectByType<OrbitCamera>();
        if(camera) camera.FocusPanel(new Vector3((p.xmin+p.xmax)/2,p.z+.4f,(p.ymin+p.ymax)/2),Mathf.Max(p.xmax-p.xmin,p.ymax-p.ymin));
    }

    private static Vector3 V(float[] a) => new Vector3(a[0],a[1],a[2]);
    private static Vector3 UnityPoint(Vector3 a) => new Vector3(a.x,a.z,a.y);
    private Vector3 Motion(int n,bool rotation=false) {
        int k=6*n+(rotation?3:0); return new Vector3((float)u[k],(float)u[k+1],(float)u[k+2]);
    }
    public static double[] InfluenceWeights(double s)
    {
        double[] nodes={0,1.0/3,2.0/3,1}, result=new double[4];
        for(int k=0;k<4;k++) { result[k]=1; for(int j=0;j<4;j++) if(k!=j) result[k]*=(s-nodes[j])/(nodes[k]-nodes[j]); }
        return result;
    }
    private double LocalPosition(Bar b) { foreach(var t in transfers) if(t.id==b.id)return t.s;return .5; }
    private double Portion(int tag) { double value=0;foreach(var t in transfers)if(t.id==tag)value+=t.weight;return value; }

    private void Update()
    {
        if(!active || data==null) return;
        if(viewCamera && Screen.width>850 && Screen.height>500)
            viewCamera.rect=new Rect(264f/Screen.width,245f/Screen.height,(Screen.width-614f)/Screen.width,(Screen.height-319f)/Screen.height);
        if(playing) {
            float before=xi;MoveTo(Mathf.Lerp(Current.xmin,Current.xmax,xi)+Time.deltaTime*speed,Mathf.Lerp(Current.ymin,Current.ymax,eta));
            if(before==xi)playing=false;
        }
        if(!Input.GetMouseButton(0) && GUIUtility.keyboardControl==0) {
            float dx=(Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0);
            float dy=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
            if(dx!=0 || dy!=0) {
                MoveTo(Current.xmin+xi*(Current.xmax-Current.xmin)+dx*speed*Time.deltaTime,
                    Current.ymin+eta*(Current.ymax-Current.ymin)+dy*speed*Time.deltaTime);
            }
        }
        PickInWorld();
        if(dirty) { Recalculate(); Render();dirty=false; }
    }

    private void MoveTo(float x,float y)
    {
        // Walk in short segments: never jump across a hole or a gap at low frame rate.
        float startX=Mathf.Lerp(Current.xmin,Current.xmax,xi),startY=Mathf.Lerp(Current.ymin,Current.ymax,eta);
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(x,y),new Vector2(startX,startY))/.025f));
        for(int k=1;k<=steps;k++) {
            float tx=Mathf.Lerp(startX,x,(float)k/steps),ty=Mathf.Lerp(startY,y,(float)k/steps);
            if(!CanWalk(Mathf.Lerp(Current.xmin,Current.xmax,xi),Mathf.Lerp(Current.ymin,Current.ymax,eta),tx,ty) || !PlaceAt(tx,ty,false))break;
        }
    }

    private void Recalculate()
    {
        Array.Clear(u,0,u.Length);Array.Clear(f,0,f.Length);Array.Clear(reaction,0,6);
        ApplyTransfers();
        maxDisplacement=0; foreach(int tag in ActiveIds) {
            var b=data.bars[barIndex[tag]];
            for(int k=0;k<=100;k++) maxDisplacement=Mathf.Max(maxDisplacement,Displacement(b,k/100f).magnitude*1000);
        }
        receiver=Mathf.Clamp(receiver,0,ActiveIds.Length-1);
        var selected=data.bars[barIndex[ActiveIds[receiver]]];
        peakMoment=0;peakPosition=0;
        foreach(double s in new[]{0.0,Math.Max(0,LocalPosition(selected)-1e-8),LocalPosition(selected),1.0}) {
            float moment=(float)Cut(selected,4,s);
            if(Mathf.Abs(moment)>Mathf.Abs(peakMoment)) {peakMoment=moment;peakPosition=(float)s*Vector3.Distance(xyz[selected.i],xyz[selected.j]);}
        }
    }

    // Positive-x cut convention, with the exact shear jump at the point load.
    private double Cut(Bar b,int component,double s)
    {
        return CutGlobal(b,component,s);
    }

    private Vector3 Displacement(Bar b,float s)
    {
        return DisplacementGlobal(b,s);
    }

    private void Line(Vector3[] points,Color color,float width)
    {
        LineRenderer line;
        if(usedLines==lines.Count) {
            var go=new GameObject("SQ4_line");go.transform.SetParent(root,false);line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=lineMaterial;line.useWorldSpace=true;line.numCornerVertices=3;line.numCapVertices=3;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;lines.Add(line);
        } else line=lines[usedLines];
        usedLines++;line.gameObject.SetActive(true);line.positionCount=points.Length;line.SetPositions(points);
        line.startColor=line.endColor=color;line.startWidth=line.endWidth=width;
    }

    private void Render()
    {
        usedLines=0;var p=Current;float z=p.z+.46f;
        panelFill.position=new Vector3((p.xmin+p.xmax)/2,z-.02f,(p.ymin+p.ymax)/2);
        panelFill.localScale=new Vector3(p.xmax-p.xmin,p.ymax-p.ymin,1);
        var block=new MaterialPropertyBlock();block.SetColor("_Color",new Color(.08f,.8f,.7f,.18f));panelFill.GetComponent<Renderer>().SetPropertyBlock(block);
        Vector3 position=new Vector3(Mathf.Lerp(p.xmin,p.xmax,xi),z,Mathf.Lerp(p.ymin,p.ymax,eta));
        avatar.position=position+Vector3.up*.47f;
        Line(new[]{new Vector3(p.xmin,z,p.ymin),new Vector3(p.xmax,z,p.ymin),new Vector3(p.xmax,z,p.ymax),new Vector3(p.xmin,z,p.ymax),new Vector3(p.xmin,z,p.ymin)},teal,.07f);
        for(int edge=0;edge<transfers.Count;edge++) {
            var t=transfers[edge];int tag=t.id;var b=data.bars[barIndex[tag]];
            Vector3 target=UnityPoint(t.q)+Vector3.up*.46f;
            Color c=edge==0?teal:amber;
            Line(new[]{position,target},new Color(c.r,c.g,c.b,.6f),.025f);
            Line(new[]{UnityPoint(xyz[b.i])+Vector3.up*.47f,UnityPoint(xyz[b.j])+Vector3.up*.47f},c,.10f);
            if(magnitude*Portion(tag)>1e-8) {
                float h=.3f+(float)Portion(tag)*1.5f;
                Line(new[]{target+Vector3.up*h,target},c,.06f);
                Line(new[]{target+Vector3.up*.22f+Vector3.right*.13f,target,target+Vector3.up*.22f-Vector3.right*.13f},c,.06f);
            }
        }
        foreach(var hole in p.voids) {
            Line(new[]{new Vector3(hole.xmin,z,hole.ymin),new Vector3(hole.xmax,z,hole.ymin),new Vector3(hole.xmax,z,hole.ymax),new Vector3(hole.xmin,z,hole.ymax),new Vector3(hole.xmin,z,hole.ymin)},pink,.07f);
        }
        // All bars on the active floor: complete structural response, not isolated simply-supported beams.
        foreach(var b in data.bars) {
            if(Mathf.Abs(xyz[b.i].z-p.z)>.01f || Mathf.Abs(xyz[b.j].z-p.z)>.01f) continue;
            Vector3[] points=new Vector3[41];
            for(int k=0;k<points.Length;k++) {float s=k/40f;points[k]=UnityPoint(Vector3.Lerp(xyz[b.i],xyz[b.j],s)+Displacement(b,s)*scale);}
            Line(points,Portion(b.id)>0?pink:new Color(.58f,.43f,.71f,.45f),Portion(b.id)>0?.06f:.025f);
        }
        for(int k=usedLines;k<lines.Count;k++) lines[k].gameObject.SetActive(false);
    }

    private void Styles()
    {
        if(box!=null) return;
        background=new Texture2D(1,1);background.SetPixel(0,0,new Color(.035f,.061f,.087f,.98f));background.Apply();
        box=new GUIStyle(GUI.skin.box){padding=new RectOffset(16,16,14,14)};box.normal.background=background;
        title=new GUIStyle(GUI.skin.label){fontSize=19,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
        muted=new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true,normal={textColor=new Color(.65f,.74f,.8f)}};
        metric=new GUIStyle(title){fontSize=25,normal={textColor=teal}};
        button=new GUIStyle(GUI.skin.button){fontSize=12,padding=new RectOffset(8,8,8,8)};
    }
    private void OnGUI()
    {
        if(!active || data==null) return;
        Styles();
        GUILayout.BeginArea(Dock,box);scroll=GUILayout.BeginScrollView(scroll);
        GUILayout.Label("CARGA MÓVIL",title);
        GUILayout.Label("SQ4  /  CAMINO DE CARGA",muted);GUILayout.Space(10);
        DrawPanelSelector();
        GUILayout.Label(magnitude.ToString("F2")+" kN",metric);
        GUILayout.Label("Carga vertical localizada · cuasiestática",muted);
        float next=GUILayout.HorizontalSlider(magnitude,0,100);if(next!=magnitude){magnitude=next;dirty=true;}
        GUILayout.BeginHorizontal();
        foreach(float preset in new[]{0f,1f,10f,50f}) if(GUILayout.Button(preset+" kN",button)){magnitude=preset;dirty=true;}
        GUILayout.EndHorizontal();GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("‹",button,GUILayout.Width(34))) ChangePanel(-1);
        GUILayout.Label("Panel "+Current.id+"\nNivel +"+Current.z.ToString("F2")+" m",muted);
        if(GUILayout.Button("›",button,GUILayout.Width(34))) ChangePanel(1);
        GUILayout.EndHorizontal();
        GUILayout.Label("POSICIÓN DEL USUARIO",muted);
        Rect map=GUILayoutUtility.GetRect(260,140,GUILayout.ExpandWidth(true));DrawMap(map);
        GUILayout.Label("Arrastra en la planta · W/A/S/D para caminar",muted);
        GUILayout.Label("X "+Mathf.Lerp(Current.xmin,Current.xmax,xi).ToString("F2")+" m   Y "+Mathf.Lerp(Current.ymin,Current.ymax,eta).ToString("F2")+" m",muted);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button(playing?"Pausar":"Recorrer →",button)){if(xi>=1)xi=0;playing=!playing;dirty=true;}
        if(GUILayout.Button("Centrar",button)){SafeCenter();playing=false;dirty=true;}
        if(GUILayout.Button("Enfocar",button)) Focus();
        GUILayout.EndHorizontal();
        GUILayout.Label("Velocidad visual: "+speed.ToString("F2")+" m/s",muted);speed=GUILayout.HorizontalSlider(speed,.1f,2f);
        GUILayout.Space(12);GUILayout.Label("REPARTO A VIGAS",title);
        for(int k=0;k<ActiveIds.Length;k++)ReceiverRow(k,k==0?teal:amber);
        GUILayout.Space(8);
        bool ok=transferError<1e-4 && momentError<.002 && Math.Abs(reaction[2]-magnitude)<.002;
        GUI.color=ok?teal:pink;GUILayout.Label(ok?"✓ CARGA CONSERVADA":"REVISAR EQUILIBRIO",muted);GUI.color=Color.white;
        GUILayout.Label("ΣP = "+magnitude.ToString("F4")+" kN   ·   error "+transferError.ToString("G2")+" kN\nError de momento: "+momentError.ToString("G2")+" kN·m\nΣRz apoyos = "+reaction[2].ToString("F4")+" kN",muted);
        GUILayout.Space(10);GUILayout.Label("RESPUESTA ADICIONAL Δ",title);
        GUILayout.Label("|Δu| máx. receptoras: "+maxDisplacement.ToString("G4")+" mm",muted);
        GUILayout.Label("Deformada rosa ×"+scale.ToString("F0"),muted);next=GUILayout.HorizontalSlider(scale,1,10000);if(next!=scale){scale=next;dirty=true;}
        GUILayout.Label(Current.rule=="opposite"?"P inferior = P(1−η) · P superior = Pη":"Apoyo asignado: fuerza y par por excentricidad.",muted);
        GUILayout.Label("Transferencia idealizada; la losa no es FE. Sólo ΔSQ4, sin G/Q/sismo. Vacíos en rosa: no transitables.",muted);
        if(GUILayout.Button("Volver a casos del edificio",button)) Toggle();
        GUILayout.EndScrollView();GUILayout.EndArea();
        DrawCharts();
    }

    private void ChangePanel(int direction) { SelectPanel((panelIndex+direction+data.panels.Length)%data.panels.Length); }
    private void ReceiverRow(int k,Color c)
    {
        int tag=ActiveIds[k];float w=(float)Portion(tag);GUI.color=c;
        if(GUILayout.Button("Viga "+tag+"   "+(magnitude*w).ToString("F3")+" kN  · "+(100*w).ToString("F1")+" %"+(receiver==k?"  ●":""),button)){receiver=k;dirty=true;}
        double couple=0;foreach(var t in transfers)if(t.id==tag)couple+=t.m.magnitude;
        GUILayout.Label("Par transferido: "+couple.ToString("G4")+" kN·m",muted);
        Rect r=GUILayoutUtility.GetRect(10,4,GUILayout.ExpandWidth(true));GUI.DrawTexture(new Rect(r.x,r.y,r.width*w,4),Texture2D.whiteTexture);GUI.color=Color.white;
    }
    private void DrawMap(Rect r)
    {
        Fill(r,new Color(.07f,.12f,.16f));
        for(int k=1;k<5;k++){Stroke(new Vector2(r.x+r.width*k/5,r.y),new Vector2(r.x+r.width*k/5,r.yMax),new Color(.14f,.22f,.27f),1);Stroke(new Vector2(r.x,r.y+r.height*k/5),new Vector2(r.xMax,r.y+r.height*k/5),new Color(.14f,.22f,.27f),1);}
        Fill(new Rect(r.x,r.yMax-3,r.width,3),teal);Fill(new Rect(r.x,r.y,r.width,3),amber);
        foreach(var h in Current.voids)Fill(new Rect(r.x+(h.xmin-Current.xmin)/(Current.xmax-Current.xmin)*r.width,r.yMax-(h.ymax-Current.ymin)/(Current.ymax-Current.ymin)*r.height,(h.xmax-h.xmin)/(Current.xmax-Current.xmin)*r.width,(h.ymax-h.ymin)/(Current.ymax-Current.ymin)*r.height),new Color(.65f,.15f,.25f));
        Vector2 point=new Vector2(r.x+xi*r.width,r.yMax-eta*r.height);
        Stroke(new Vector2(point.x,r.y),new Vector2(point.x,r.yMax),new Color(.7f,.8f,.8f,.6f),1);
        Fill(new Rect(point.x-5,point.y-5,10,10),Color.white);
        var e=Event.current;int control=GUIUtility.GetControlID(FocusType.Passive);
        if(e.type==EventType.MouseDown && e.button==0 && r.Contains(e.mousePosition)) GUIUtility.hotControl=control;
        if((e.type==EventType.MouseDown || e.type==EventType.MouseDrag) && e.button==0 && GUIUtility.hotControl==control) {
            PlaceAt(Mathf.Lerp(Current.xmin,Current.xmax,Mathf.Clamp01((e.mousePosition.x-r.x)/r.width)),Mathf.Lerp(Current.ymin,Current.ymax,Mathf.Clamp01(1-(e.mousePosition.y-r.y)/r.height)),false);playing=false;e.Use();
        }
        if(e.type==EventType.MouseUp && GUIUtility.hotControl==control){GUIUtility.hotControl=0;e.Use();}
    }
    private static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    private static void Stroke(Vector2 a,Vector2 b,Color c,float width=2)
    {
        Matrix4x4 matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);
        Fill(new Rect(a.x,a.y-width/2,Vector2.Distance(a,b),width),c);GUI.matrix=matrix;
    }
    private void DrawCharts()
    {
        Rect panel=ChartArea;GUI.Box(panel,"",box);
        GUI.Label(new Rect(panel.x+16,panel.y+8,panel.width*.44f,26),"VIGA "+ActiveIds[receiver]+" · ΔSQ4",title);
        GUI.Label(new Rect(panel.x+panel.width*.45f,panel.y+12,panel.width*.53f,20),"ΣP = "+magnitude.ToString("F2")+" kN · ΣRz = "+reaction[2].ToString("F2")+" kN",muted);
        var b=data.bars[barIndex[ActiveIds[receiver]]];
        float width=(panel.width-48)/2;
        Plot(new Rect(panel.x+16,panel.y+55,width,125),b,4,"ΔMy [kN·m]",teal);
        Plot(new Rect(panel.x+32+width,panel.y+55,width,125),b,2,"ΔVz [kN]",amber);
        GUI.Label(new Rect(panel.x+16,panel.y+192,panel.width-32,34),"Máx. |ΔMy| = "+Mathf.Abs(peakMoment).ToString("G5")+" kN·m  en x = "+peakPosition.ToString("F3")+" m desde i\nDeformada rosa ×"+scale.ToString("F0")+" · |Δu| máx. receptoras = "+maxDisplacement.ToString("G4")+" mm",muted);
    }
    private void Plot(Rect r,Bar b,int component,string label,Color color)
    {
        double a=LocalPosition(b);double bound=1e-10;
        foreach(double s in new[]{0.0,Math.Max(0,a-1e-8),a,1.0})bound=Math.Max(bound,Math.Abs(Cut(b,component,s)));
        GUI.Label(new Rect(r.x,r.y-19,r.width,20),label,muted);
        Vector2 Map(double s,double value)=>new Vector2(r.x+(float)s*r.width,r.center.y-(float)(value/bound)*r.height*.36f);
        Stroke(Map(0,0),Map(1,0),new Color(.3f,.4f,.45f),1);
        Stroke(new Vector2(Map(a,0).x,r.y),new Vector2(Map(a,0).x,r.yMax),new Color(.5f,.5f,.5f,.5f),1);
        var ss=new List<double>{0,1,a};if(a>0)ss.Add(Math.Max(0,a-1e-8));ss.Sort();
        for(int k=1;k<ss.Count;k++) Stroke(Map(ss[k-1],Cut(b,component,ss[k-1])),Map(ss[k],Cut(b,component,ss[k])),color,2);
        GUI.Label(new Rect(r.x,r.yMax-15,r.width/2,20),"i "+Cut(b,component,0).ToString("G4"),muted);
        GUI.Label(new Rect(r.center.x,r.yMax-15,r.width/2,20),"j "+Cut(b,component,1).ToString("G4"),muted);
        GUI.Label(new Rect(r.x,r.y,r.width,20),"±"+bound.ToString("G4"),muted);
    }

    private void OnDestroy()
    {
        if(Instance==this)Instance=null;
        if(lineMaterial)Destroy(lineMaterial);if(avatarMaterial)Destroy(avatarMaterial);if(background)Destroy(background);
    }
}
