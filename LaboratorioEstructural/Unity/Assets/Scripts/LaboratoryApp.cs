using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace StructuralLab {
public class LaboratoryApp : MonoBehaviour {
    ProjectData data; Camera cam; GameObject modelRoot,gridRoot,annotationRoot,tributaryRoot;
    readonly Dictionary<int,NodeData> nodes=new(); readonly Dictionary<int,ElementData> elements=new();
    readonly Dictionary<int,GameObject> elementObjects=new(),nodeObjects=new();
    readonly Dictionary<int,LineRenderer> deformed=new();
    readonly Dictionary<string,Dictionary<int,NodeResult>> caseNodes=new();
    readonly Dictionary<string,Dictionary<int,ElementResult>> caseElements=new();
    readonly Dictionary<int,double[]> combinedNodes=new();
    readonly List<GameObject> slabObjects=new(),wallObjects=new(),supportObjects=new();
    readonly List<Tuple<float,GameObject>> floorObjects=new();
    readonly string[] caseNames={"G","Q","EX","EY"}; float[] factors={1,0,0,0};
    Color bg=new(.035f,.061f,.094f),panel=new(.063f,.094f,.133f),cyan=new(.22f,.85f,.81f),gold=new(1f,.66f,.29f);
    Material lineMat; GUIStyle title,heading,text,small,muted,button,box; Texture2D white;
    Vector3 pivot=new(10,8,7);float yaw=-28,pitch=27,distance=105;bool orbit=true;
    bool showNodes,showWalls=true,showSlabs,showArms,showDeformed=true,showIds,showLoads,showDiaphragms,showAxes;
    int selected=1,selectedNode=-1,tab=0,capacityIndex=0,plotMode=0,filterBuilding=0,levelIndex=-1;
    float deformationScale=30;string status="Cargando cálculo…",idSearch="1";Vector2 notesScroll;
    bool dirty=true;Overrides edits=new();Process python;string pythonLog="";float reanalysisStarted;
    WebCamTexture webcam;GameObject cameraBackdrop;bool arMode;float arYaw,arX,arZ;Vector3 normalPivot;float normalDistance;
    const float VW=1600,VH=1000;

    void Start() {
        Application.runInBackground=true;Application.targetFrameRate=60;
        var cameraObject=new GameObject("Cámara de inspección");cam=cameraObject.AddComponent<Camera>();
        cam.tag="MainCamera";cam.backgroundColor=bg;cam.clearFlags=CameraClearFlags.SolidColor;
        cam.nearClipPlane=.03f;cam.farClipPlane=3000;cam.fieldOfView=46;
        cam.rect=new Rect(296f/VW,189f/VH,944f/VW,737f/VH);
        RenderSettings.ambientLight=new Color(.65f,.71f,.8f);
        var sun=new GameObject("Luz principal").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.transform.rotation=Quaternion.Euler(45,-35,0);
        lineMat=new Material(Shader.Find("Sprites/Default"));
        white=Texture2D.whiteTexture;
        Reload();
        if(Environment.GetCommandLineArgs().Contains("--smoke")||Environment.GetCommandLineArgs().Contains("--smoke-reanalysis"))StartCoroutine(Smoke());
    }
    void Reload() {
        try {
            string path=Path.Combine(Application.streamingAssetsPath,"proyecto.json");
            var incoming=JsonUtility.FromJson<ProjectData>(File.ReadAllText(path));
            if(incoming.schemaVersion!=1||!incoming.validation.checksPassed)throw new Exception("Resultado sin validaciones aprobadas");
            data=incoming;
            edits=incoming.config??new Overrides();
            string localRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            if(File.Exists(Path.Combine(localRoot,"python/run_project.py")))data.projectRoot=localRoot;
            if(!File.Exists(data.pythonExecutable))data.pythonExecutable="python";
            nodes.Clear();elements.Clear();caseNodes.Clear();caseElements.Clear();
            foreach(var n in data.model.nodes)nodes[n.id]=n;
            foreach(var e in data.model.elements)elements[e.id]=e;
            foreach(var c in data.cases){caseNodes[c.name]=c.nodes.ToDictionary(x=>x.id);caseElements[c.name]=c.elements.ToDictionary(x=>x.id);}
            if(!elements.ContainsKey(selected))selected=data.model.elements.First(x=>x.kind!="RIGID_ARM").id;
            BuildView();dirty=true;status="Cálculo cargado · "+DateTime.Parse(data.generated).ToLocalTime().ToString("dd/MM HH:mm");
        }catch(Exception e){status="No se pudo cargar: "+e.Message;Debug.LogException(e);}
    }
    Material Material(Color color,bool transparent=false) {
        var m=new Material(Shader.Find("Standard"));m.color=color;m.SetFloat("_Glossiness",.25f);
        if(transparent){m.SetFloat("_Mode",3);m.SetInt("_SrcBlend",5);m.SetInt("_DstBlend",10);m.SetInt("_ZWrite",0);m.EnableKeyword("_ALPHABLEND_ON");m.renderQueue=3000;}
        return m;
    }
    static Vector3 V(float[] a){return new Vector3(a[0],a[2],a[1]);}
    static Vector3 V(double[] a){return new Vector3((float)a[0],(float)a[2],(float)a[1]);}
    GameObject Cube(string name,Vector3 a,Vector3 b,float width,float depth,Color color,Transform parent,bool transparent=false) {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);
        o.transform.localPosition=(a+b)/2;o.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        o.transform.localScale=new Vector3(width,(b-a).magnitude,depth);o.GetComponent<Renderer>().material=Material(color,transparent);return o;
    }
    LineRenderer Line(string name,IEnumerable<Vector3> pts,Color color,float width,Transform parent) {
        var o=new GameObject(name);o.transform.SetParent(parent,false);var lr=o.AddComponent<LineRenderer>();lr.useWorldSpace=false;
        lr.material=lineMat;lr.startColor=lr.endColor=color;lr.startWidth=lr.endWidth=width;
        var a=pts.ToArray();lr.positionCount=a.Length;lr.SetPositions(a);return lr;
    }
    void BuildView() {
        if(modelRoot)Destroy(modelRoot);if(gridRoot)Destroy(gridRoot);
        modelRoot=new GameObject("Modelo LT1 LT2");gridRoot=new GameObject("Retícula de referencia");
        elementObjects.Clear();nodeObjects.Clear();deformed.Clear();slabObjects.Clear();wallObjects.Clear();supportObjects.Clear();floorObjects.Clear();
        foreach(var e in data.model.elements) {
            Vector3 a=nodes[e.i].World,b=nodes[e.j].World;bool arm=e.kind=="RIGID_ARM";
            Color c=arm?new Color(.3f,.35f,.42f):e.building=="LT1"?new Color(.24f,.55f,.66f):new Color(.81f,.52f,.29f);
            if(e.kind=="WALL")c=new Color(.56f,.46f,.77f);
            var o=Cube("Elemento "+e.id,a,b,arm?.06f:e.kind=="WALL"?.13f:e.b,arm?.06f:e.kind=="WALL"?.13f:e.h,c,modelRoot.transform);
            if(!arm&&e.kind!="WALL")o.transform.localRotation=Quaternion.LookRotation(Vector3.Cross(V(e.ez),V(e.ex)),V(e.ex));
            o.AddComponent<PickElement>().id=e.id;elementObjects[e.id]=o;
            if(!arm)deformed[e.id]=Line("Deformada "+e.id,new[]{a,b},cyan,.04f,modelRoot.transform);
        }
        foreach(var n in data.model.nodes) {
            var o=GameObject.CreatePrimitive(PrimitiveType.Sphere);o.name="Nodo "+n.id;o.transform.SetParent(modelRoot.transform,false);o.transform.localPosition=n.World;o.transform.localScale=Vector3.one*.22f;
            o.GetComponent<Renderer>().material=Material(new Color(.83f,.89f,.94f));o.AddComponent<PickNode>().id=n.id;nodeObjects[n.id]=o;
            if(n.fix){var s=Cube("Empotramiento "+n.id,n.World-Vector3.up*.3f,n.World,.8f,.8f,new Color(.45f,.52f,.6f),modelRoot.transform);Destroy(s.GetComponent<Collider>());s.AddComponent<PickNode>().id=n.id;supportObjects.Add(s);}
        }
        foreach(var w in data.model.walls) {
            var a=new Vector3(w.ax,w.z0,w.ay);var b=new Vector3(w.bx,w.z0,w.by);var center=(a+b)/2;
            var o=Cube("Muro físico "+w.id,center,center+Vector3.up*(w.z1-w.z0),(b-a).magnitude,w.thickness,new Color(.55f,.51f,.75f,.18f),modelRoot.transform,true);
            o.transform.localRotation=Quaternion.LookRotation(Vector3.Cross((b-a).normalized,Vector3.up),Vector3.up);
            o.AddComponent<PickElement>().id=data.model.elements.First(e=>e.wall==w.id&&e.kind=="WALL").id;wallObjects.Add(o);
            var outline=Line("Contorno muro "+w.id,new[]{a,b,b+Vector3.up*(w.z1-w.z0),a+Vector3.up*(w.z1-w.z0),a},new Color(.64f,.57f,.84f,.45f),.025f,o.transform);
            // Outline coordinates are in model space, hence parent it under the model root.
            outline.transform.SetParent(modelRoot.transform,false);wallObjects.Add(outline.gameObject);
        }
        foreach(var s in data.model.slabs.Where(x=>x.state=="AUTO_CLOSED_BY_BEAMS_AND_WALLS")) {
            var pts=s.points.Select(p=>p.World).ToList();if(pts.Count<3)continue;pts.Add(pts[0]);
            var lr=Line("Panel "+s.id,pts,new Color(.37f,.55f,.67f,.4f),.025f,modelRoot.transform);slabObjects.Add(lr.gameObject);floorObjects.Add(Tuple.Create(s.z,lr.gameObject));
        }
        for(int x=-40;x<=60;x+=5)Line("X "+x,new[]{new Vector3(x,-.35f,-10),new Vector3(x,-.35f,25)},new Color(.2f,.27f,.34f,.4f),.02f,gridRoot.transform);
        for(int y=-10;y<=25;y+=5)Line("Y "+y,new[]{new Vector3(-40,-.35f,y),new Vector3(60,-.35f,y)},new Color(.2f,.27f,.34f,.4f),.02f,gridRoot.transform);
        Arrow(new Vector3(-35,0,-5),new Vector3(-29,0,-5),new Color(1,.35f,.35f),gridRoot.transform);
        Arrow(new Vector3(-35,0,-5),new Vector3(-35,0,1),new Color(.4f,1,.5f),gridRoot.transform);
        Arrow(new Vector3(-35,0,-5),new Vector3(-35,6,-5),new Color(.4f,.6f,1),gridRoot.transform);
        RebuildSelection();ApplyVisibility();
    }
    void Arrow(Vector3 a,Vector3 b,Color c,Transform parent) {
        if((a-b).magnitude<.01f)return;
        Line("Vector",new[]{a,b},c,.065f,parent);Vector3 v=(a-b).normalized;Vector3 side=Vector3.Cross(v,Vector3.up);
        if(side.sqrMagnitude<.01f)side=Vector3.right;side.Normalize();
        Line("Flecha",new[]{b+v*.45f+side*.19f,b,b+v*.45f-side*.19f},c,.065f,parent);
    }
    bool Visible(ElementData e) {
        if(filterBuilding==1&&e.building!="LT1"||filterBuilding==2&&e.building!="LT2")return false;
        if(levelIndex>=0) {
            float z=data.model.levels[levelIndex];float lo=(float)Math.Min(nodes[e.i].z,nodes[e.j].z),hi=(float)Math.Max(nodes[e.i].z,nodes[e.j].z);
            if(hi>z+.01f||hi<z-.01f&&lo<z-.01f)return false;
        }
        return true;
    }
    void ApplyVisibility() {
        if(data==null)return;
        foreach(var e in data.model.elements) {
            bool visible=Visible(e);elementObjects[e.id].SetActive(visible&&(e.kind!="RIGID_ARM"||showArms));
            if(deformed.ContainsKey(e.id))deformed[e.id].gameObject.SetActive(visible&&showDeformed);
        }
        foreach(var n in data.model.nodes)nodeObjects[n.id].SetActive(showNodes&&(levelIndex<0||Math.Abs(n.z-data.model.levels[levelIndex])<.01)&&(filterBuilding==0||n.building=="LT"+filterBuilding));
        foreach(var o in wallObjects)o.SetActive(showWalls&&levelIndex<0&&filterBuilding==0);
        foreach(var o in supportObjects){var n=nodes[o.GetComponent<PickNode>().id];o.SetActive(levelIndex<0&&(filterBuilding==0||n.building=="LT"+filterBuilding));}
        foreach(var o in slabObjects)o.SetActive(showSlabs);
        foreach(var f in floorObjects)f.Item2.SetActive(showSlabs&&(levelIndex<0||Math.Abs(f.Item1-data.model.levels[levelIndex])<.01));
        RebuildAnnotations();
    }
    void Combine() {
        if(data==null)return;
        combinedNodes.Clear();
        foreach(var n in data.model.nodes) {
            var u=new double[6];for(int c=0;c<4;c++){var r=caseNodes[caseNames[c]][n.id].u;for(int k=0;k<6;k++)u[k]+=r[k]*factors[c];}combinedNodes[n.id]=u;
        }
        foreach(var e in data.model.elements) {
            if(!deformed.ContainsKey(e.id))continue;
            Vector3 a=nodes[e.i].World,b=nodes[e.j].World;var ui=combinedNodes[e.i];var uj=combinedNodes[e.j];
            var localI=ToLocal(ui,e);var localJ=ToLocal(uj,e);var pts=new Vector3[17];
            for(int k=0;k<pts.Length;k++) {
                float t=k/(float)(pts.Length-1);double h1=1-3*t*t+2*t*t*t,h2=t-2*t*t+t*t*t,h3=3*t*t-2*t*t*t,h4=-t*t+t*t*t;
                double ux=localI[0]*(1-t)+localJ[0]*t;
                double uy=h1*localI[1]+h2*e.L*localI[5]+h3*localJ[1]+h4*e.L*localJ[5];
                double uz=h1*localI[2]-h2*e.L*localI[4]+h3*localJ[2]-h4*e.L*localJ[4];
                pts[k]=Vector3.Lerp(a,b,t)+deformationScale*(V(e.ex)*(float)ux+V(e.ey)*(float)uy+V(e.ez)*(float)uz);
            }
            deformed[e.id].positionCount=pts.Length;deformed[e.id].SetPositions(pts);
        }
        dirty=false;RebuildAnnotations();
    }
    static double[] ToLocal(double[] u,ElementData e) {
        var r=new double[6];var axes=new[]{e.ex,e.ey,e.ez};for(int i=0;i<3;i++)for(int j=0;j<3;j++){r[i]+=axes[i][j]*u[j];r[i+3]+=axes[i][j]*u[j+3];}return r;
    }
    double[] Forces(int id) {
        var f=new double[12];for(int c=0;c<4;c++){var r=caseElements[caseNames[c]][id].forces;for(int i=0;i<12;i++)f[i]+=factors[c]*r[i];}return f;
    }
    void RebuildAnnotations() {
        if(annotationRoot)Destroy(annotationRoot);annotationRoot=new GameObject("Anotaciones");annotationRoot.transform.SetParent(modelRoot.transform,false);
        foreach(var d in data.model.diaphragms) {
            if(levelIndex>=0&&Math.Abs(d.z-data.model.levels[levelIndex])>.01)continue;
            if(filterBuilding!=0&&d.building!="LT"+filterBuilding)continue;
            var pts=d.nodes.Select(id=>nodes[id].World).ToArray();float minx=pts.Min(p=>p.x),maxx=pts.Max(p=>p.x),minz=pts.Min(p=>p.z),maxz=pts.Max(p=>p.z);
            if(showDiaphragms) {
                Line("Diafragma "+d.id,new[]{new Vector3(minx,d.z,minz),new Vector3(maxx,d.z,minz),new Vector3(maxx,d.z,maxz),new Vector3(minx,d.z,maxz),new Vector3(minx,d.z,minz)},new Color(.52f,.45f,1,.6f),.055f,annotationRoot.transform);
                Arrow(new Vector3(d.x,d.z,d.y),new Vector3(d.x,d.z+1,d.y),new Color(.7f,.6f,1),annotationRoot.transform);
            }
            if(showLoads) {
                var p=new Vector3(d.x,d.z,d.y);
                float maxForce=Mathf.Max(1,data.model.diaphragms.Where(x=>x.building==d.building).Max(x=>x.force));
                Arrow(p,p+new Vector3(factors[2]*4,0,factors[3]*4)*(d.force/maxForce),gold,annotationRoot.transform);
            }
        }
        if(showLoads)foreach(var e in data.model.elements.Where(e=>e.kind.StartsWith("BEAM")&&Visible(e))) {
            var rows=data.loadRows.Where(r=>r.beam_id==e.source);double q=rows.Sum(r=>r.dead_load_kN*factors[0]+r.live_load_kN*factors[1]);
            if(Math.Abs(q)<.01)continue;var p=(nodes[e.i].World+nodes[e.j].World)/2;
            Arrow(p+Vector3.up*(q>0?1.8f:-1.8f),p,gold,annotationRoot.transform);
        }
        if(showAxes&&elements.TryGetValue(selected,out var sel)) {
            var p=nodes[sel.i].World;
            Arrow(p,p+V(sel.ex)*2,Color.red,annotationRoot.transform);Arrow(p,p+V(sel.ey)*2,Color.green,annotationRoot.transform);Arrow(p,p+V(sel.ez)*2,Color.blue,annotationRoot.transform);
        }
    }
    void SelectElement(int id) {if(!elements.ContainsKey(id))return;selected=id;selectedNode=-1;idSearch=id.ToString();RebuildSelection();RebuildAnnotations();}
    void RebuildSelection() {
        foreach(var e in data.model.elements) {
            var r=elementObjects[e.id].GetComponent<Renderer>();
            r.material.SetColor("_EmissionColor",e.id==selected?new Color(.55f,.5f,.15f):Color.black);
            r.material.EnableKeyword("_EMISSION");
        }
        if(tributaryRoot)Destroy(tributaryRoot);tributaryRoot=new GameObject("Área tributaria seleccionada");tributaryRoot.transform.SetParent(modelRoot.transform,false);
        if(!elements.TryGetValue(selected,out var element))return;
        var rows=data.loadRows.Where(r=>r.beam_id==element.source).ToArray();
        var keys=new HashSet<string>(rows.Select(r=>r.slab_id+":"+r.edge));
        foreach(var t in data.model.tributaries.Where(t=>keys.Contains(t.slab+":"+t.edge))) {
            var slab=data.model.slabs.First(s=>s.id==t.slab);var pts=t.points.Select(p=>new Vector3(p.x,slab.z+.07f,p.y)).ToList();
            if(pts.Count<3)continue;var mesh=new Mesh();mesh.vertices=pts.ToArray();var tris=new List<int>();
            for(int k=1;k<pts.Count-1;k++){tris.AddRange(new[]{0,k,k+1});}mesh.triangles=tris.ToArray();mesh.RecalculateNormals();mesh.colors=Enumerable.Repeat(Color.white,pts.Count).ToArray();
            var o=new GameObject("Tributaria "+t.slab);o.transform.SetParent(tributaryRoot.transform,false);o.AddComponent<MeshFilter>().mesh=mesh;
            var areaMaterial=new Material(Shader.Find("Sprites/Default"));areaMaterial.color=new Color(.2f,.85f,.75f,.27f);o.AddComponent<MeshRenderer>().material=areaMaterial;
            pts.Add(pts[0]);Line("Borde tributario",pts,cyan,.05f,tributaryRoot.transform);
        }
        tributaryRoot.SetActive(tab==1);
    }
    bool OverUI() {Vector2 p=new(Input.mousePosition.x/Screen.width*VW,(1-Input.mousePosition.y/Screen.height)*VH);return p.x<300||p.x>1240||p.y<84||p.y>815;}
    void Update() {
        if(data==null)return;
        if(dirty)Combine();
        if(Input.GetKeyDown(KeyCode.Escape)){arMode=false;if(webcam)webcam.Stop();if(cameraBackdrop)cameraBackdrop.SetActive(false);modelRoot.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);gridRoot.SetActive(true);}
        if(!OverUI()) {
            if(Input.GetMouseButton(1)){yaw+=Input.GetAxis("Mouse X")*3;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,2,89);}
            if(Input.GetMouseButton(2))pivot-=cam.transform.right*Input.GetAxis("Mouse X")*.35f+cam.transform.up*Input.GetAxis("Mouse Y")*.35f;
            distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*5,4,220);
            if(Input.GetMouseButtonDown(0)&&Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition),out RaycastHit hit)) {
                var pick=hit.collider.GetComponent<PickElement>();if(pick)SelectElement(pick.id);
                var np=hit.collider.GetComponent<PickNode>();if(np){selectedNode=np.id;tab=0;}
            }
        }
        if(GUIUtility.keyboardControl==0) {
            float speed=(Input.GetKey(KeyCode.LeftShift)?20:7)*Time.deltaTime;
            Vector3 forward=cam.transform.forward;forward.y=0;forward.Normalize();
            if(Input.GetKey(KeyCode.W))pivot+=forward*speed;if(Input.GetKey(KeyCode.S))pivot-=forward*speed;
            if(Input.GetKey(KeyCode.A))pivot-=cam.transform.right*speed;if(Input.GetKey(KeyCode.D))pivot+=cam.transform.right*speed;
            if(Input.GetKey(KeyCode.R))pivot+=Vector3.up*speed;if(Input.GetKey(KeyCode.F))pivot-=Vector3.up*speed;
        }
        cam.transform.position=pivot+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);cam.transform.LookAt(pivot);
        if(arMode){modelRoot.transform.SetPositionAndRotation(new Vector3(arX,0,arZ),Quaternion.Euler(0,arYaw,0));}
        if(python!=null&&python.HasExited) {
            int rc=python.ExitCode;python.Dispose();python=null;
            if(rc==0){Reload();status="Reanálisis completado y verificado.";}else status="Python terminó con error. Revisar resultados/reanalisis.log";
            File.WriteAllText(Path.Combine(data.projectRoot,"resultados/reanalisis.log"),pythonLog);
        }
    }
    void Styles() {
        if(text!=null)return;
        Font font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text=new GUIStyle(GUI.skin.label){font=font,fontSize=15,wordWrap=true,normal={textColor=new Color(.85f,.9f,.95f)}};
        small=new GUIStyle(text){fontSize=12};muted=new GUIStyle(small);muted.normal.textColor=new Color(.55f,.65f,.74f);
        heading=new GUIStyle(text){fontSize=19,fontStyle=FontStyle.Bold};title=new GUIStyle(heading){fontSize=27};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=13,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.9f,.94f,1f)}};
        box=new GUIStyle(GUI.skin.box){normal={background=white,textColor=Color.white}};
        GUI.skin.font=font;
    }
    void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,white);GUI.color=old;}
    void Label(float x,float y,float w,float h,string s,GUIStyle style=null){GUI.Label(new Rect(x,y,w,h),s,style??text);}
    bool Button(float x,float y,float w,float h,string s,bool active=false){Color old=GUI.backgroundColor;GUI.backgroundColor=active?cyan:new Color(.2f,.3f,.39f);bool b=GUI.Button(new Rect(x,y,w,h),s,button);GUI.backgroundColor=old;return b;}
    bool Toggle(float x,float y,float w,string label,bool value){return Button(x,y,w,29,(value?"● ":"○ ")+label,value)?!value:value;}
    float Slider(float x,float y,float w,string label,float value,float min,float max,string format="0.00") {
        Label(x,y,w-70,23,label,small);Label(x+w-70,y,70,23,value.ToString(format,CultureInfo.InvariantCulture),small);
        return GUI.HorizontalSlider(new Rect(x,y+25,w,18),value,min,max);
    }
    void OnGUI() {
        Styles();GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/VW,Screen.height/VH,1));
        Fill(new Rect(0,0,VW,74),new Color(.04f,.07f,.1f));Fill(new Rect(0,74,296,926),panel);Fill(new Rect(1240,74,360,926),panel);
        Label(24,16,920,36,"LABORATORIO ESTRUCTURAL",title);
        Label(540,25,480,26,"LT1 + LT2   /   OpenSeesPy + Unity",small);
        Fill(new Rect(1058,22,9,9),cyan);Label(1079,18,460,33,data==null?"Cargando datos":"kN · m · s    |    Junta 0,10 m    |    Modelo académico",small);
        if(data==null){Label(25,105,1500,120,status,heading);return;}
        LeftPanel();RightPanel();BottomPanel();
        Label(318,91,650,30,"GEOMETRÍA, RESPUESTA Y CAPACIDAD",small);
        Label(318,118,800,26,levelIndex<0?"Vista general · cinco niveles elevados":"Nivel del modelo +"+data.model.levels[levelIndex].ToString("0.00")+" m",muted);
        Label(318,765,895,30,"Arrastre derecho: orbitar   ·   Rueda: acercar   ·   Botón central: desplazar   ·   WASD: navegar",muted);
        if(showIds)DrawNodeIds();
        if(python!=null)Label(320,155,870,30,"Calculando en Python… "+(Time.realtimeSinceStartup-reanalysisStarted).ToString("0")+" s",heading);
        GUI.matrix=Matrix4x4.identity;
    }
    void LeftPanel() {
        Label(20,94,255,26,"01  EXPLORAR",heading);
        if(Button(20,133,80,30,"Ambos",filterBuilding==0)){filterBuilding=0;ApplyVisibility();}
        if(Button(105,133,76,30,"LT1",filterBuilding==1)){filterBuilding=1;ApplyVisibility();}
        if(Button(186,133,90,30,"LT2",filterBuilding==2)){filterBuilding=2;ApplyVisibility();}
        Label(20,180,255,23,"Planta / nivel Z (m)",small);
        if(Button(20,209,75,30,"Todos",levelIndex<0)){levelIndex=-1;ApplyVisibility();}
        for(int i=1;i<data.model.levels.Length;i++) {
            int col=(i-1)%3,row=(i-1)/3;
            if(Button(20+col*86,249+row*36,80,29,data.model.levels[i].ToString("0.00"),levelIndex==i)){levelIndex=i;ApplyVisibility();}
        }
        if(Button(105,209,75,30,"Planta")){pitch=89;yaw=0;}
        if(Button(186,209,90,30,"3D")){pitch=27;yaw=-28;pivot=new Vector3(10,8,7);distance=105;}
        bool changed=false;
        void T(float x,float y,string name,ref bool value){bool v=Toggle(x,y,123,name,value);changed|=v!=value;value=v;}
        T(20,337,"Nodos",ref showNodes);T(153,337,"IDs",ref showIds);
        T(20,373,"Muros",ref showWalls);T(153,373,"Losas",ref showSlabs);
        T(20,409,"Diafragmas",ref showDiaphragms);T(153,409,"Cargas",ref showLoads);
        T(20,445,"Ejes locales",ref showAxes);T(153,445,"Brazos",ref showArms);
        if(changed)ApplyVisibility();
        Fill(new Rect(20,493,256,1),new Color(.23f,.31f,.38f));Label(20,514,256,27,"02  COMBINAR",heading);
        Label(20,549,256,38,"R = λG·G + λQ·Q + λEX·EX + λEY·EY",small);
        for(int i=0;i<4;i++){float v=Slider(20,590+i*54,256,caseNames[i],factors[i],-2,2);if(v!=factors[i]){factors[i]=v;dirty=true;}}
        if(Button(20,813,80,29,"Gravedad")){factors=new[]{1f,0,0,0};dirty=true;}
        if(Button(105,813,80,29,"EX")){factors=new[]{0f,0,1,0};dirty=true;}
        if(Button(190,813,86,29,"Prueba")){factors=new[]{1.2f,.5f,.8f,-.3f};dirty=true;}
        bool sd=Toggle(20,857,256,"Deformada",showDeformed);if(sd!=showDeformed){showDeformed=sd;ApplyVisibility();}
        float ds=Slider(20,903,256,"Amplificación visual",deformationScale,0,100,"0.0×");if(ds!=deformationScale){deformationScale=ds;dirty=true;}
        Label(20,956,256,32,"La escala visual no cambia el cálculo.",muted);
    }
    void RightPanel() {
        string[] tabs={"Inspección","Cargas","Sección","Cambios","AR","Notas"};
        for(int i=0;i<6;i++)if(Button(1256+(i%3)*111,91+(i/3)*35,105,29,tabs[i],tab==i)){tab=i;if(tab==2)plotMode=0;RebuildSelection();}
        if(tab==0)InspectPanel();else if(tab==1)LoadsPanel();else if(tab==2)CapacityPanel();else if(tab==3)ChangesPanel();else if(tab==4)ARPanel();else NotesPanel();
    }
    void InspectPanel() {
        Label(1260,179,315,30,"INSPECCIÓN DE ELEMENTO",heading);
        idSearch=GUI.TextField(new Rect(1260,219,200,30),idSearch,20);
        if(Button(1472,219,108,30,"Buscar ID")&&int.TryParse(idSearch,out int id))SelectElement(id);
        if(selectedNode>=0&&nodes.TryGetValue(selectedNode,out var n)) {
            Label(1260,272,310,34,"Nodo "+n.id,heading);
            Label(1260,318,310,65,$"{n.building}\nX {n.x:0.000} · Y {n.y:0.000} · Z {n.z:0.000} m",text);
            var u=combinedNodes[n.id];Label(1260,400,310,125,$"Ux {u[0]*1000:0.000} mm\nUy {u[1]*1000:0.000} mm\nUz {u[2]*1000:0.000} mm\nRz {u[5]:0.000000} rad\nApoyo: {(n.fix?"empotrado":"libre / diafragma")}",text);
            if(n.fix) {
                var r=new double[6];for(int c=0;c<4;c++)for(int k=0;k<6;k++)r[k]+=factors[c]*caseNodes[caseNames[c]][n.id].reaction[k];
                Label(1260,581,310,30,"Reacciones en ejes globales",heading);
                Label(1260,625,310,151,$"Fx  {r[0]:0.00} kN\nFy  {r[1]:0.00} kN\nFz  {r[2]:0.00} kN\nMx  {r[3]:0.00} kN m\nMy  {r[4]:0.00} kN m\nMz  {r[5]:0.00} kN m",text);
            }
            return;
        }
        if(!elements.TryGetValue(selected,out var e))return;
        Label(1260,273,310,36,"#"+e.id+"   "+e.kind,heading);
        Label(1260,319,310,90,$"{e.building} · Fuente Excel {e.source}\nNodo i: {e.i}   →   nodo j: {e.j}\nLongitud {e.L:0.000} m\nSección {e.b:0.000} × {e.h:0.000} m",text);
        Label(1260,435,310,28,"Propiedades de sección",heading);
        Label(1260,477,310,96,$"A = {e.A:0.0000} m²\nIy = {e.Iy:0.000000} m⁴\nIz = {e.Iz:0.000000} m⁴\nJ = {e.J:0.000000} m⁴",text);
        var f=Forces(e.id);Label(1260,608,310,28,"Fuerzas de extremo i",heading);
        Label(1260,649,310,136,$"Fx  {f[0]:0.00} kN\nFy  {f[1]:0.00} kN\nFz  {f[2]:0.00} kN\nMx  {f[3]:0.00} kN m\nMy  {f[4]:0.00} kN m\nMz  {f[5]:0.00} kN m",text);
        Label(1260,820,310,114,"Ejes locales: x longitudinal; y y z de sección. Signos de fuerzas nodales locales de OpenSees. Selecciona nodos o barras en la vista 3D.",muted);
        if(Button(1260,942,315,31,"Centrar elemento")){pivot=(nodes[e.i].World+nodes[e.j].World)/2;distance=Mathf.Max(8,e.L*3);}
    }
    void LoadsPanel() {
        Label(1260,178,315,31,"ÁREAS TRIBUTARIAS",heading);
        var e=elements[selected];var rows=data.loadRows.Where(r=>r.beam_id==e.source).ToArray();
        double area=rows.Sum(r=>r.tributary_area_m2),g=rows.Sum(r=>r.dead_load_kN),q=rows.Sum(r=>r.live_load_kN);
        Label(1260,224,315,144,$"Elemento {e.id} · Fuente {e.source}\n{rows.Length} aportes de panel / zona\nÁrea tributaria = {area:0.000} m²\nG de losas = {g:0.00} kN\nQ = {q:0.00} kN\nCombinación = {g*factors[0]+q*factors[1]:0.00} kN",text);
        Label(1260,392,315,31,"Conservación de carga",heading);
        Label(1260,437,315,79,"Cada aporte conserva q × A. Los polígonos turquesa muestran las áreas tributarias disponibles para la viga de origen.",text);
        double residual=rows.Length==0?0:rows.Max(r=>Math.Abs(r.dead_load_kN-r.q_G_kN_m2*r.tributary_area_m2));
        Label(1260,540,315,48,"Máx. diferencia tabulada qG·A:\n"+residual.ToString("0.00E+0")+" kN",small);
        Label(1260,614,315,190,"Las cargas son aportes a la viga de origen. Si fue subdividida, el cálculo reparte el aporte entre sus tramos.\n\nLos pesos propios de vigas, columnas y muros se agregan en Python; no están incluidos en los totales de piso mostrados aquí.",muted);
        if(rows.Any(r=>r.distribution=="missing_area_to_nearest_beam"))Label(1260,829,315,90,"Esta selección incluye paneles asignados a la viga más cercana en el Excel. Revisar esa asignación geométrica.",small);
    }
    void CapacityPanel() {
        Label(1260,178,315,31,"SECCIÓN DE FIBRAS",heading);
        if(Button(1260,223,150,31,"Columna",capacityIndex==0))capacityIndex=0;
        if(Button(1420,223,155,31,"Muro",capacityIndex==1))capacityIndex=1;
        var c=data.capacities[capacityIndex];Label(1260,276,315,54,c.name,heading);
        if(Button(1260,343,315,31,"Seleccionar elemento "+c.elementId))SelectElement(c.elementId);
        var f=Forces(c.elementId);double P=f[0],M=Math.Abs(f[5]);
        Label(1260,402,315,79,$"Demanda del elemento de referencia\nP = {P:0.00} kN (compresión +)\n|Mz| = {M:0.00} kN m",text);
        double cap=MomentAtP(c.pm,P);
        Label(1260,507,315,82,cap>0?$"Capacidad nominal interpolada\nMn(P) = {cap:0.0} kN m\n|Mz| / Mn = {M/cap:0.000}":"P está fuera del intervalo de la curva nominal.",text);
        Label(1260,620,315,180,"ARMADURA DIDÁCTICA\nLos diámetros / cuantías están en config.json. Deben reemplazarse por el armado real.\n\nComparación uniaxial: no verifica corte, inestabilidad ni interacción biaxial. No se aplica factor de reducción.",small);
        Label(1260,839,315,105,"P-M: OpenSees Concrete01 + Steel01.\nLínea discontinua: bloque Whitney.\nM-phi: axial constante del 10% de la compresión máxima de la sección.",muted);
    }
    static double MomentAtP(PM[] pts,double P) {
        if(P<pts[0].P||P>pts[pts.Length-1].P)return -1;
        for(int i=1;i<pts.Length;i++)if(P<=pts[i].P){double t=(P-pts[i-1].P)/Math.Max(1e-9,pts[i].P-pts[i-1].P);return pts[i-1].M*(1-t)+pts[i].M*t;}return -1;
    }
    void ChangesPanel() {
        Label(1260,178,315,31,"MODIFICAR Y RECALCULAR",heading);
        Label(1260,223,315,61,"Estos cambios regeneran el modelo y los cuatro casos mediante Python.",text);
        edits.stiffness_multiplier=Slider(1260,314,315,"Multiplicador de E",(float)edits.stiffness_multiplier,.2f,2f);
        edits.beam_section_multiplier=Slider(1260,383,315,"Dimensiones b y h de vigas",(float)edits.beam_section_multiplier,.7f,1.4f);
        edits.column_section_multiplier=Slider(1260,452,315,"Dimensiones b y h de columnas",(float)edits.column_section_multiplier,.7f,1.4f);
        edits.wall_stiffness_multiplier=Slider(1260,521,315,"Rigidez de muros",(float)edits.wall_stiffness_multiplier,.2f,1.5f);
        edits.seismic_coefficient=Slider(1260,590,315,"Coeficiente sísmico idealizado C",(float)edits.seismic_coefficient,0,.3f);
        GUI.enabled=python==null;
        if(Button(1260,684,315,39,"Guardar cambios y ejecutar Python"))RunPython();
        if(Button(1260,738,315,34,"Recargar resultados verificados"))Reload();
        GUI.enabled=true;
        Label(1260,804,315,150,"E y las dimensiones cambian la matriz de rigidez. Las dimensiones también cambian el peso propio.\n\nLos factores de combinación de la izquierda usan resultados ya calculados y no requieren reanálisis.",muted);
    }
    void RunPython() {
        try {
            string path=Path.Combine(data.projectRoot,"overrides_unity.json");File.WriteAllText(path,JsonUtility.ToJson(edits,true));pythonLog="";
            var start=new ProcessStartInfo {FileName=data.pythonExecutable,Arguments="-u \""+Path.Combine(data.projectRoot,"python/run_project.py")+"\" --overrides \""+path+"\"",WorkingDirectory=data.projectRoot,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            python=new Process {StartInfo=start};python.OutputDataReceived+=(s,e)=>{if(e.Data!=null)pythonLog+=e.Data+"\n";};python.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)pythonLog+=e.Data+"\n";};
            python.Start();python.BeginOutputReadLine();python.BeginErrorReadLine();reanalysisStarted=Time.realtimeSinceStartup;status="Reanálisis en curso";
        }catch(Exception e){python=null;status="No se pudo iniciar Python: "+e.Message;}
    }
    void ARPanel() {
        Label(1260,178,315,31,"AR CON ALINEACIÓN MANUAL",heading);
        Label(1260,228,315,114,"Superpone el modelo sobre una cámara conectada. Requiere alinear manualmente la vista con referencias físicas. No incluye seguimiento SLAM ni anclajes persistentes.",text);
        if(Button(1260,367,315,35,arMode?"Detener cámara":"Activar cámara y superposición"))ToggleAR();
        arYaw=Slider(1260,450,315,"Giro horizontal del modelo",arYaw,-180,180,"0°");
        arX=Slider(1260,521,315,"Ajuste X (m)",arX,-50,50);
        arZ=Slider(1260,592,315,"Ajuste profundidad (m)",arZ,-50,50);
        Label(1260,691,315,230,"1. Coloca la cámara en un punto fijo.\n2. Usa dos referencias conocidas del edificio.\n3. Ajusta giro y posición; usa rueda / órbita para encuadrar.\n4. Comprueba una tercera referencia antes de interpretar la superposición.\n\nEsc detiene la cámara. En el edificio, funciona con una cámara conectada al computador.",small);
    }
    void ToggleAR() {
        if(arMode){arMode=false;if(webcam)webcam.Stop();if(cameraBackdrop)cameraBackdrop.SetActive(false);modelRoot.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);gridRoot.SetActive(true);return;}
        if(WebCamTexture.devices.Length==0){status="No se encontró una cámara. La escena 3D sigue disponible.";return;}
        webcam=new WebCamTexture(WebCamTexture.devices[0].name,1280,720,30);webcam.Play();
        if(!cameraBackdrop){cameraBackdrop=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(cameraBackdrop.GetComponent<Collider>());cameraBackdrop.transform.SetParent(cam.transform,false);}
        cameraBackdrop.SetActive(true);cameraBackdrop.transform.localPosition=new Vector3(0,0,1500);cameraBackdrop.transform.localRotation=Quaternion.identity;
        float height=2*1500*Mathf.Tan(cam.fieldOfView*Mathf.Deg2Rad/2);cameraBackdrop.transform.localScale=new Vector3(height*cam.aspect,height,1);
        var mat=new Material(Shader.Find("Unlit/Texture"));mat.mainTexture=webcam;cameraBackdrop.GetComponent<Renderer>().material=mat;
        gridRoot.SetActive(false);arMode=true;status="Cámara activa · alineación manual";
    }
    void NotesPanel() {
        Label(1260,178,315,31,"VERIFICACIÓN Y ALCANCE",heading);
        Label(1260,226,315,112,$"{data.model.nodes.Length} nodos analíticos\n{data.model.elements.Length} elementos\n{data.model.diaphragms.Length} diafragmas\nSuperposición: {data.validation.superposition.displacements.relative:0.0E+0}\nEquilibrio G: {data.cases[0].forceResidual:0.0E+0}",text);
        notesScroll=GUI.BeginScrollView(new Rect(1255,367,330,574),notesScroll,new Rect(0,0,305,1350));
        float y=0;foreach(string note in data.notes){Label(6,y,292,125,note,small);y+=134;}GUI.EndScrollView();
    }
    void BottomPanel() {
        Fill(new Rect(296,811,944,189),new Color(.055f,.085f,.12f));
        if(tab==2) {
            if(Button(322,824,104,25,"P-M",plotMode==0))plotMode=0;
            if(Button(433,824,104,25,"M-phi",plotMode==1))plotMode=1;
            var c=data.capacities[capacityIndex];
            if(plotMode==0) {
                var f=Forces(c.elementId);var sets=new[]{c.pm.Select(p=>new Vector2((float)p.M,(float)p.P)).ToArray(),c.whitney.Select(p=>new Vector2((float)p.M,(float)p.P)).ToArray()};
                Plot(new Rect(590,846,595,117),sets,new[]{cyan,gold},new Vector2((float)Math.Abs(f[5]),(float)f[0]),"M (kN m)","P (kN)");
                Label(322,864,245,82,"Curva nominal y referencia Whitney. Punto blanco: demanda del elemento "+c.elementId+".",small);
            } else {
                Plot(new Rect(590,846,595,117),new[]{c.mphi.Select(p=>new Vector2((float)p.curvature,(float)p.moment)).ToArray()},new[]{cyan},null,"φ (1/m)","M (kN m)");
                Label(322,866,245,80,"Axial fijo. Curva no lineal de sección, independiente del modelo global elástico.",small);
            }
        } else {
            string[] names={"N","Vy","Vz","T","My","Mz"};plotMode=Mathf.Clamp(plotMode,0,5);
            for(int i=0;i<6;i++)if(Button(322+i*48,824,43,25,names[i],plotMode==i))plotMode=i;
            var e=elements[selected];var pts=new Vector2[21];
            for(int i=0;i<21;i++) {
                double v=0;for(int c=0;c<4;c++){var d=caseElements[caseNames[c]][e.id].diagram[i];v+=factors[c]*(plotMode==0?d.N:plotMode==1?d.Vy:plotMode==2?d.Vz:plotMode==3?d.T:plotMode==4?d.My:d.Mz);}
                pts[i]=new Vector2(i/20f*e.L,(float)v);
            }
            Plot(new Rect(680,846,505,117),new[]{pts},new[]{cyan},null,"x local (m)",names[plotMode]+(plotMode<3?" (kN)":" (kN m)"));
            Label(322,866,322,63,"Diagrama del elemento "+selected+"\nFuerzas internas en ejes locales.",small);
        }
        Label(322,970,891,25,status,muted);
    }
    void Plot(Rect rect,Vector2[][] curves,Color[] colors,Vector2? point,string xlabel,string ylabel) {
        var all=curves.SelectMany(x=>x).ToList();if(point.HasValue)all.Add(point.Value);if(all.Count==0)return;
        float xmin=Mathf.Min(0,all.Min(p=>p.x)),xmax=all.Max(p=>p.x),ymin=Mathf.Min(0,all.Min(p=>p.y)),ymax=all.Max(p=>p.y);
        if(xmax-xmin<1e-7f)xmax=xmin+1;if(ymax-ymin<1e-7f)ymax=ymin+1;
        Vector2 Map(Vector2 p)=>new(rect.x+(p.x-xmin)/(xmax-xmin)*rect.width,rect.yMax-(p.y-ymin)/(ymax-ymin)*rect.height);
        DrawLine(Map(new Vector2(xmin,0)),Map(new Vector2(xmax,0)),new Color(.32f,.4f,.48f),1);
        DrawLine(Map(new Vector2(0,ymin)),Map(new Vector2(0,ymax)),new Color(.32f,.4f,.48f),1);
        for(int c=0;c<curves.Length;c++)for(int i=1;i<curves[c].Length;i++)if(c==0||i%2==1)DrawLine(Map(curves[c][i-1]),Map(curves[c][i]),colors[c],2);
        if(point.HasValue){Vector2 p=Map(point.Value);Fill(new Rect(p.x-4,p.y-4,8,8),Color.white);}
        Label(rect.x,rect.y-24,rect.width,22,ylabel+"    ["+ymin.ToString("0.##")+" … "+ymax.ToString("0.##")+"]",muted);
        Label(rect.x+rect.width-200,rect.yMax+2,200,20,xlabel+"   "+xmax.ToString("0.####"),muted);
    }
    void DrawLine(Vector2 a,Vector2 b,Color c,float width) {
        Matrix4x4 old=GUI.matrix;Vector2 d=b-a;float angle=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;
        GUIUtility.RotateAroundPivot(angle,a);Fill(new Rect(a.x,a.y-width/2,d.magnitude,width),c);GUI.matrix=old;
    }
    void DrawNodeIds() {
        int count=0;foreach(var n in data.model.nodes) {
            if(levelIndex>=0&&Math.Abs(n.z-data.model.levels[levelIndex])>.01)continue;
            var p=cam.WorldToScreenPoint(modelRoot.transform.TransformPoint(n.World));
            float x=p.x/Screen.width*VW,y=(1-p.y/Screen.height)*VH;
            if(p.z>0&&x>300&&x<1230&&y>165&&y<735){Label(x+3,y,90,22,n.id.ToString(),small);if(++count>130)break;}
        }
    }
    IEnumerator Smoke() {
        yield return null;yield return new WaitForEndOfFrame();
        if(data==null||data.model.nodes.Length<100)throw new Exception("Smoke: data missing");
        string reanalysisEvidence="Reanálisis: no solicitado en esta prueba\n";
        if(Environment.GetCommandLineArgs().Contains("--smoke-reanalysis")) {
            double baseline=data.cases.First(c=>c.name=="G").maxDisplacement_m;
            edits.stiffness_multiplier=.8;RunPython();float deadline=Time.realtimeSinceStartup+180;
            while(python!=null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(python!=null)throw new Exception("Smoke reanalysis timeout");
            double ratio=data.cases.First(c=>c.name=="G").maxDisplacement_m/baseline;
            if(Math.Abs(ratio-1.25)>1e-4)throw new Exception("E scaling mismatch: "+ratio);
            edits.stiffness_multiplier=1;RunPython();deadline=Time.realtimeSinceStartup+180;
            while(python!=null&&Time.realtimeSinceStartup<deadline)yield return null;
            if(python!=null||Math.Abs(data.cases.First(c=>c.name=="G").maxDisplacement_m/baseline-1)>1e-4)throw new Exception("Baseline restore failed");
            reanalysisEvidence="Reanálisis Unity -> Python -> Unity verificado: E x 0.8 produce desplazamiento x "+ratio.ToString("0.000000")+"; base restaurada\n";
        }
        factors=new[]{1.2f,.5f,.8f,-.3f};Combine();
        if(combinedNodes.Values.Any(u=>u.Any(double.IsNaN)))throw new Exception("Smoke: NaN displacement");
        SelectElement(data.model.elements.First(e=>e.kind.StartsWith("BEAM")&&e.L>8).id);
        tab=0;plotMode=5;showWalls=true;showDeformed=true;ApplyVisibility();
        yield return new WaitForEndOfFrame();
        string dir=Path.Combine(data.projectRoot,"resultados");
        ScreenCapture.CaptureScreenshot(Path.Combine(dir,"unity_vista_general.png"));
        yield return new WaitForSeconds(1);
        tab=1;levelIndex=Array.FindIndex(data.model.levels,z=>Math.Abs(z-nodes[elements[selected].j].z)<.01);filterBuilding=2;
        pitch=89;yaw=0;pivot=new Vector3(-16,(float)nodes[elements[selected].j].z,8);distance=50;
        RebuildSelection();ApplyVisibility();yield return null;yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"unity_tributarias.png"));
        yield return new WaitForSeconds(1);
        levelIndex=-1;filterBuilding=0;pitch=27;yaw=-28;pivot=new Vector3(10,8,7);distance=105;ApplyVisibility();
        tab=2;capacityIndex=1;plotMode=0;SelectElement(data.capacities[1].elementId);
        yield return null;yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(dir,"unity_capacidad.png"));
        yield return new WaitForSeconds(1);
        if(Environment.GetCommandLineArgs().Contains("--smoke-reanalysis"))File.WriteAllText(Path.Combine(dir,"unity_reanalysis.txt"),reanalysisEvidence);
        File.WriteAllText(Path.Combine(dir,"unity_smoke.txt"),"SMOKE_OK\nJSON cargado\nCombinación de 4 casos sin NaN\nSelección de barra y muro\nP-M y M-phi disponibles\n"+reanalysisEvidence+data.model.nodes.Length+" nodos; "+data.model.elements.Length+" elementos\n");
        Debug.Log("LABORATORY_SMOKE_OK");Application.Quit(0);
    }
    void OnDestroy(){if(webcam)webcam.Stop();}
}
}
