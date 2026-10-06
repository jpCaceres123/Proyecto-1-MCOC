using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CampusLaser : MonoBehaviour {
    [Serializable] public class Graph { public string label,unit; public float[] values,stations; }
    [Serializable] public class Load { public string label,distribution;public float g,q,qg,qq,wg,wq; }
    [Serializable] public class Case { public string name,text,detail; public float[] endI,endJ,moveI,moveJ,reactionI,reactionJ,wall;public Graph[] graphs; }
    [Serializable] public class Entry { public string key,summary,title,category;public string[] geometry;public bool hasLoads;public float loadG,loadQ;public Load[] loads; public Case[] cases; }
    [Serializable] public class Database { public string source; public Entry[] entries; }
    CampusPlayer player; Database database;
    readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
    LineRenderer beam; Material beamMaterial; GameObject dot;
    bool active=true,pinned,details; int selectedCase; string key="",note=""; Vector2 scroll;
    Vector3 point; GUIStyle body,heading,big,caption,tabStyle;int page;bool endJ;
    readonly Color bg=new Color(.035f,.055f,.078f,.98f),surface=new Color(.075f,.10f,.135f,1),muted=new Color(.58f,.66f,.74f),accent=new Color(.20f,.85f,.76f);
    public void PreviewPage(int index){page=index;scroll=Vector2.zero;}
    public void ShowPreview(string element){key=element;pinned=true;active=true;point=transform.position+Vector3.forward*10;}
    void Start() {
        player=GetComponent<CampusPlayer>();
        var asset=Resources.Load<TextAsset>("inspeccion_estructural");
        if(asset){database=JsonUtility.FromJson<Database>(asset.text);foreach(var e in database.entries)entries[e.key]=e;}
        var go=new GameObject("Laser de inspeccion estructural");beam=go.AddComponent<LineRenderer>();
        beamMaterial=new Material(Shader.Find("Standard"));beamMaterial.color=Color.red;beamMaterial.EnableKeyword("_EMISSION");beamMaterial.SetColor("_EmissionColor",Color.red*3);
        beam.sharedMaterial=beamMaterial;beam.positionCount=2;beam.startWidth=.009f;beam.endWidth=.016f;beam.useWorldSpace=true;
        beam.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        dot=GameObject.CreatePrimitive(PrimitiveType.Sphere);dot.name="Punto laser";dot.transform.localScale=Vector3.one*.07f;
        Destroy(dot.GetComponent<Collider>());dot.GetComponent<Renderer>().sharedMaterial=beamMaterial;
    }
    void LateUpdate() {
        if(!player || !beam)return;
        if(player.InspectionAllowed && !player.ThirdPerson && (Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1))){active=true;pinned=false;}
        if(player.InspectionAllowed && !player.ThirdPerson && Input.GetKeyDown(KeyCode.L)){active=!active;pinned=false;player.equippedTool=1;}
        bool visible=active && player.equippedTool==1 && player.InspectionAllowed && !player.ThirdPerson;beam.enabled=visible;dot.SetActive(visible);if(!visible)return;
        if(Input.GetKeyDown(KeyCode.Tab)){page=(page+1)%4;scroll=Vector2.zero;}
        if(Input.GetKeyDown(KeyCode.I))endJ=!endJ;
        if(Input.GetKeyDown(KeyCode.Q)){selectedCase=(selectedCase+1)%9;scroll=Vector2.zero;}
        if(Input.GetMouseButtonDown(1))pinned=!pinned;
        if(!pinned) {
            var hits=Physics.RaycastAll(player.eye.transform.position,player.eye.transform.forward,45,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).ToArray();
            string next="";note="Apunta a una viga, columna, muro o losa.";
            point=player.eye.transform.position+player.eye.transform.forward*30;
            foreach(var hit in hits) {
                var id=hit.collider.GetComponentInParent<StructuralIdentity>();
                if(id && !string.IsNullOrEmpty(id.key)){next=id.key;point=hit.point;note=id.description;break;}
            }
            if(next=="" && hits.Length>0){point=hits[0].point;note=hits[0].collider.gameObject.name+"\nElemento arquitectonico: sin resultados analiticos asociados.";}
            if(next!=key){key=next;scroll=Vector2.zero;}
        }
        beam.SetPosition(0,player.eye.transform.position+player.eye.transform.right*.16f-player.eye.transform.up*.14f);beam.SetPosition(1,point);
        dot.transform.position=point;
        scroll.y=Mathf.Max(0,scroll.y-Input.mouseScrollDelta.y*55);
    }
    void Styles() {
        if(body!=null)return;
        body=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,normal={textColor=Color.white}};
        heading=new GUIStyle(body){fontSize=25,fontStyle=FontStyle.Bold};
        big=new GUIStyle(body){fontSize=24,fontStyle=FontStyle.Bold};
        caption=new GUIStyle(body){fontSize=11,normal={textColor=muted}};
        tabStyle=new GUIStyle(body){fontSize=12,alignment=TextAnchor.MiddleCenter};
    }
    void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void Txt(Rect r,string s,GUIStyle style,Color? color=null){var old=GUI.color;GUI.color=color??Color.white;GUI.Label(r,s,style);GUI.color=old;}
    string Number(float value){float v=Mathf.Abs(value);return v>0 && (v<.01f || v>=1000000)?value.ToString("0.##E+0"):value.ToString("0.##");}
    void Card(float x,float y,float w,string label,float value,string unit,string description="") {
        Fill(new Rect(x,y,w,94),surface);Fill(new Rect(x,y,3,94),accent);
        Txt(new Rect(x+14,y+10,w-24,18),label,caption);
        Txt(new Rect(x+14,y+31,w-24,33),Number(value),big);
        Txt(new Rect(x+14,y+66,w-24,20),unit+(description==""?"":" · "+description),caption);
    }
    void Section(float w,ref float y,string name,string sub="") {
        Txt(new Rect(0,y,w,24),name,body);y+=26;
        if(sub!=""){Txt(new Rect(0,y,w,35),sub,caption);y+=38;}
    }
    void Metrics(float w,ref float y,float[] values,string[] labels,string[] units) {
        if(values==null || values.Length==0){Txt(new Rect(0,y,w,40),"No hay resultados exportados para este caso.",body);y+=48;return;}
        float cw=(w-10)/2;
        for(int i=0;i<Mathf.Min(values.Length,labels.Length);i++)Card((i%2)*(cw+10),y+(i/2)*104,cw,labels[i],values[i],units[i]);
        y+=Mathf.Ceil(values.Length/2f)*104+8;
    }
    void Segment(Vector2 a,Vector2 b,Color color,float thickness=2) {
        var matrix=GUI.matrix;Vector2 d=b-a;GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);
        Fill(new Rect(a.x,a.y-thickness/2,d.magnitude,thickness),color);GUI.matrix=matrix;
    }
    void Plot(float w,ref float y,Graph graph) {
        if(graph.values==null || graph.values.Length<2)return;
        Fill(new Rect(0,y,w,166),surface);
        Txt(new Rect(14,y+9,w-28,20),graph.label+"  /  "+graph.unit,body);
        float lo=graph.values.Min(),hi=graph.values.Max(),range=Mathf.Max(Mathf.Max(Mathf.Abs(lo),Mathf.Abs(hi)),.000001f);
        Rect area=new Rect(16,y+40,w-32,78);float zero=area.center.y;
        Segment(new Vector2(area.xMin,zero),new Vector2(area.xMax,zero),new Color(.24f,.30f,.36f),1);
        for(int i=1;i<graph.values.Length;i++) {
            float s0=graph.stations!=null?graph.stations[i-1]:(i-1)/(float)(graph.values.Length-1);
            float s1=graph.stations!=null?graph.stations[i]:i/(float)(graph.values.Length-1);
            Segment(new Vector2(area.xMin+s0*area.width,zero-graph.values[i-1]/range*area.height*.45f),new Vector2(area.xMin+s1*area.width,zero-graph.values[i]/range*area.height*.45f),accent,2);
        }
        Txt(new Rect(16,y+126,w-32,20),"mín  "+Number(lo)+"     máx  "+Number(hi),caption);
        Txt(new Rect(w-112,y+145,100,17),"i  →  x/L  →  j",caption);y+=178;
    }
    float Layout(float w,Entry entry,Case current,bool draw) {
        // Called inside an off-screen group once to determine scroll height.
        float y=0;
        if(page==0) {
            if(entry.key.StartsWith("E:")) {
                Section(w,ref y,"Esfuerzos internos", "Extremo "+(endJ?"j":"i")+" · ejes locales · I para cambiar extremo");
                Metrics(w,ref y,endJ?current.endJ:current.endI,new[]{"N · AXIAL","Vy · CORTANTE","Vz · CORTANTE","T · TORSIÓN","My · MOMENTO","Mz · MOMENTO"},new[]{"kN","kN","kN","kN·m","kN·m","kN·m"});
            } else if(entry.key.StartsWith("W:")) {
                Section(w,ref y,"Demandas del paño","Corte inferior del piso · signos del archivo de resultados");
                Metrics(w,ref y,current.wall,new[]{"P · COMPRESIÓN","V · EN PLANO","V · FUERA DEL PLANO","M · PRINCIPAL","M · VERTICAL"},new[]{"kN","kN","kN","kN·m","kN·m"});
            } else {Section(w,ref y,"Losa de reparto","Las demandas se consultan en sus vigas y muros de borde. No hay esfuerzos de placa exportados.");}
            Section(w,ref y,"Geometría del elemento");
            foreach(string line in entry.geometry??new string[0]){Fill(new Rect(0,y,w,30),surface);Txt(new Rect(12,y+5,w-24,24),line,body);y+=35;}
            if(current.graphs!=null && current.graphs.Length>5){y+=10;Section(w,ref y,"Diagrama de momento Mz");Plot(w,ref y,current.graphs[5]);}
        } else if(page==1) {
            Section(w,ref y,"Cargas tributarias","G · carga permanente     Q · sobrecarga de uso");
            if(entry.hasLoads) {
                Card(0,y,(w-10)/2,"G · TOTAL RECIBIDO",entry.loadG,"kN");Card((w+10)/2,y,(w-10)/2,"Q · TOTAL RECIBIDO",entry.loadQ,"kN");y+=110;
                foreach(var load in entry.loads) {
                    Fill(new Rect(0,y,w,144),surface);Txt(new Rect(14,y+10,w-28,24),load.label,body);
                    Txt(new Rect(14,y+38,w-28,27),load.distribution,caption);
                    Txt(new Rect(14,y+66,w-28,24),"G  "+Number(load.g)+" kN      Q  "+Number(load.q)+" kN",body);
                    Txt(new Rect(14,y+94,w-28,22),"qG / qQ   "+Number(load.qg)+" / "+Number(load.qq)+" kN/m²",caption);
                    Txt(new Rect(14,y+116,w-28,22),"w máx G / Q   "+Number(load.wg)+" / "+Number(load.wq)+" kN/m",caption);y+=155;
                }
            } else {Txt(new Rect(0,y,w,70),"Sin cargas tributarias directas en el archivo. Esto no significa que el elemento tenga demanda nula.",body);y+=80;}
        } else if(page==2) {
            Section(w,ref y,"Diagramas de esfuerzos","Variación a lo largo de la barra · todas las estaciones exportadas");
            if(current.graphs!=null && current.graphs.Length>0)foreach(var graph in current.graphs)Plot(w,ref y,graph);
            else {Txt(new Rect(0,y,w,70),"No hay diagramas de barra para este elemento y caso.",body);y+=80;}
        } else {
            Section(w,ref y,"Desplazamientos nodales","Extremo "+(endJ?"j":"i")+" · ejes globales · I para cambiar");
            var moves=endJ?current.moveJ:current.moveI;
            if(moves!=null){moves=(float[])moves.Clone();for(int i=0;i<Mathf.Min(3,moves.Length);i++)moves[i]*=1000;}
            Metrics(w,ref y,moves,new[]{"Ux · TRASLACIÓN","Uy · TRASLACIÓN","Uz · TRASLACIÓN","Rx · GIRO","Ry · GIRO","Rz · GIRO"},new[]{"mm","mm","mm","rad","rad","rad"});
            Section(w,ref y,"Reacciones en el apoyo");
            Metrics(w,ref y,endJ?current.reactionJ:current.reactionI,new[]{"Fx","Fy","Fz","Mx","My","Mz"},new[]{"kN","kN","kN","kN·m","kN·m","kN·m"});
        }
        return y+12;
    }
    void OnGUI() {
        if(!player || !active || player.equippedTool!=1 || !player.InspectionAllowed || player.ThirdPerson)return;
        Styles();float w=Mathf.Min(490,Screen.width*.40f),h=Screen.height-48,x=Screen.width-w-24;
        Fill(new Rect(x+5,29,w,h),new Color(0,0,0,.25f));Fill(new Rect(x,24,w,h),bg);Fill(new Rect(x,24,w,3),accent);
        entries.TryGetValue(key,out var entry);
        Txt(new Rect(x+20,42,w-40,18),"INSPECTOR ESTRUCTURAL",caption,accent);
        Txt(new Rect(x+20,67,w-40,34),entry!=null?entry.title:"Selecciona un elemento",heading);
        Txt(new Rect(x+20,107,w-40,20),entry!=null?entry.category:"VIGAS · COLUMNAS · MUROS · LOSAS",caption);
        if(pinned){Fill(new Rect(x+w-94,44,73,22),surface);Txt(new Rect(x+w-91,47,67,18),"FIJADO",tabStyle,accent);}
        float top=142;
        if(entry!=null) {
            var current=entry.cases[selectedCase%entry.cases.Length];
            float chip=(w-40)/9;
            for(int i=0;i<entry.cases.Length;i++) {
                Rect r=new Rect(x+20+i*chip,top,chip-3,28);Fill(r,i==selectedCase%entry.cases.Length?accent:surface);
                var old=GUI.color;GUI.color=i==selectedCase%entry.cases.Length?new Color(.02f,.09f,.09f):Color.white;
                if(GUI.Button(r,entry.cases[i].name,tabStyle)){selectedCase=i;scroll=Vector2.zero;}GUI.color=old;
            }
            top+=40;string[] tabs={"Resumen","Cargas","Diagramas","Movimiento"};
            for(int i=0;i<4;i++){Rect r=new Rect(x+20+i*(w-40)/4,top,(w-40)/4-3,31);Fill(r,page==i?surface:bg);if(page==i)Fill(new Rect(r.x,r.yMax-2,r.width,2),accent);if(GUI.Button(r,tabs[i],tabStyle)){page=i;scroll=Vector2.zero;}}
            top+=46;float inner=w-58,visible=h-top-35;
            GUI.BeginGroup(new Rect(-10000,-10000,inner,20000));float total=Layout(inner,entry,current,false);GUI.EndGroup();
            scroll=GUI.BeginScrollView(new Rect(x+20,24+top,w-34,visible),scroll,new Rect(0,0,inner,total));Layout(inner,entry,current,true);GUI.EndScrollView();
        } else {
            Txt(new Rect(x+20,170,w-40,110),note==""?"Apunta con el láser a un elemento para consultar sus resultados.":note,body);
            Txt(new Rect(x+20,290,w-40,75),"Lectura hasta 45 m. El escáner busca la estructura detrás de los acabados.",caption);
        }
        Fill(new Rect(x+20,24+h-56,w-40,1),surface);
        Txt(new Rect(x+20,24+h-47,w-40,18),"Q caso · Tab sección · I extremo · clic derecho fijar",caption);
        Txt(new Rect(x+20,24+h-26,w-40,18),"Resultados guardados · rueda para desplazar",caption);
    }
    void OnDestroy(){if(beam)Destroy(beam.gameObject);if(dot)Destroy(dot);if(beamMaterial)Destroy(beamMaterial);}
}
