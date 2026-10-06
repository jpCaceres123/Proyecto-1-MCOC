using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Architectural interpretation built on the unchanged analytical node/member contract.
public partial class CampusWorld : MonoBehaviour
{
    public const float Storey = 3.96f, Finish = .45f;
    public int nodeCount, memberCount, slabCount, wallCount;
    public Transform architecture, structure;
    public CampusPlayer player;
    public CampusEarthquake earthquake;
    public readonly List<CampusAction> actions = new List<CampusAction>();
    readonly Dictionary<int, Vector3> nodes = new Dictionary<int, Vector3>();
    readonly Dictionary<int, Slab> slabs = new Dictionary<int, Slab>();
    readonly List<string[]> records = new List<string[]>();
    readonly Dictionary<int, List<Rect>> footprints = new Dictionary<int, List<Rect>>();
    Material concrete, orange, glass, metal, floor, wood, blue, dark, green, soil, light, white, textMaterial;
    readonly System.Random random = new System.Random(441);
    public static readonly string[] FloorNames = { "Acceso", "Nivel 1", "Nivel 2", "Nivel 3", "Nivel 4", "Terraza" };
    class Slab { public int id; public float height; public Rect rect; public List<Rect> holes = new List<Rect>(); }
    struct Edge { public Vector2 a,b; public Edge(Vector2 a,Vector2 b){this.a=a;this.b=b;} }
    float F(string v) { return float.Parse(v, CultureInfo.InvariantCulture); }
    void Start()
    {
        Generate();
        var go = new GameObject("Visitante");
        player = go.AddComponent<CampusPlayer>();
        player.world = this;
        earthquake=gameObject.AddComponent<CampusEarthquake>();earthquake.player=player;
        go.AddComponent<CampusLaser>();
        go.AddComponent<CampusRifle>();
        player.Teleport(Entrance);
        if (Environment.GetCommandLineArgs().Contains("-campus-feature-check"))StartCoroutine(FeatureCheck());
        else if (Environment.GetCommandLineArgs().Contains("-campus-check"))
            StartCoroutine(FeatureCheck());
        else if (Environment.GetCommandLineArgs().Contains("-campus-ui-preview")) StartCoroutine(InspectorPreview());
        else if (Environment.GetCommandLineArgs().Contains("-campus-capture")) StartCoroutine(CaptureRevision());
    }
    public Vector3 Spawn(int level) { return ElevatorArrival(1,level); }
    public Vector3 Entrance { get { return new Vector3(-28,Finish+.08f,-16); } }
    public void Generate()
    {
        if (architecture != null) return;
        var asset=Resources.Load<TextAsset>("estructura_principal");
        if(!asset) throw new Exception("Falta Resources/estructura_principal.csv");
        foreach(string line in asset.text.Split('\n')) {
            var r=line.Trim().Split(','); if(r.Length<2 || r[0]=="kind")continue; records.Add(r);
            if(r[0]=="N") nodes[int.Parse(r[1])]=new Vector3(F(r[6]),F(r[8]),F(r[7]));
        }
        foreach(var r in records) if(r[0]=="S") {
            var pts=new[]{nodes[int.Parse(r[2])],nodes[int.Parse(r[3])],nodes[int.Parse(r[4])],nodes[int.Parse(r[5])]};
            slabs[int.Parse(r[1])]=new Slab {id=int.Parse(r[1]),height=pts[0].y,
                rect=Rect.MinMaxRect(pts.Min(p=>p.x),pts.Min(p=>p.z),pts.Max(p=>p.x),pts.Max(p=>p.z))};
        }
        foreach(var r in records) if(r[0]=="V" && slabs.ContainsKey(int.Parse(r[1])))
            slabs[int.Parse(r[1])].holes.Add(Rect.MinMaxRect(F(r[2]),F(r[4]),F(r[3]),F(r[5])));
        for(int level=0;level<=5;level++) {
            float z=(level==0?1:level)*Storey;
            var parts=new List<Rect>();
            foreach(var slab in slabs.Values.Where(s=>Mathf.Abs(s.height-z)<.02f)) {
                var split=new List<Rect>{slab.rect};
                foreach(var hole in slab.holes) split=split.SelectMany(p=>Cut(p,hole)).ToList();
                parts.AddRange(split);
            }
            footprints[level]=parts;
        }
        nodeCount=nodes.Count; slabCount=slabs.Count;
        structure=new GameObject("Estructura_original_IDS").transform; structure.SetParent(transform);
        architecture=new GameObject("Arquitectura_y_campus").transform; architecture.SetParent(transform);
        PrepareStructuralLayers();
        Materials(); EnvironmentSetup(); StructuralGeometry();
        for(int level=0;level<=5;level++) { Deck(level); if(level<5){ Facade(level); Interior(level); } }
        Circulation(); Landscape(); RearTerrace(); Cafeteria(); SideEntrance(); Roof();
        ClearLiftIntersections();
        Combine(structure); Combine(beamLayer); Combine(slabLayer); Combine(architecture);
        SlabInspectionTargets();
        RememberArchitectureState();
        Physics.SyncTransforms();
        Debug.Log($"CAMPUS_READY nodes={nodeCount} members={memberCount} slabs={slabCount} walls={wallCount} accessible_levels=6");
    }
    void SlabInspectionTargets() {
        foreach(var slab in slabs.Values) {
            var parts=new List<Rect>{slab.rect};
            foreach(var hole in slab.holes)parts=parts.SelectMany(r=>Cut(r,hole)).ToList();
            foreach(var r in parts) {
                var go=new GameObject("Losa_"+slab.id+" · objetivo laser");go.transform.SetParent(structure);
                go.transform.position=new Vector3(r.center.x,slab.height+Finish-.08f,r.center.y);
                var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(r.width,.16f,r.height);collider.isTrigger=true;
                var id=go.AddComponent<StructuralIdentity>();id.key="S:"+slab.id;id.description="Losa "+slab.id;
            }
        }
    }
    IEnumerable<Rect> Cut(Rect r,Rect h) {
        float x0=Mathf.Max(r.xMin,h.xMin),x1=Mathf.Min(r.xMax,h.xMax),z0=Mathf.Max(r.yMin,h.yMin),z1=Mathf.Min(r.yMax,h.yMax);
        if(x1<=x0 || z1<=z0){yield return r;yield break;}
        if(x0>r.xMin)yield return Rect.MinMaxRect(r.xMin,r.yMin,x0,r.yMax);
        if(x1<r.xMax)yield return Rect.MinMaxRect(x1,r.yMin,r.xMax,r.yMax);
        if(z0>r.yMin)yield return Rect.MinMaxRect(x0,r.yMin,x1,z0);
        if(z1<r.yMax)yield return Rect.MinMaxRect(x0,z1,x1,r.yMax);
    }
    public bool Inside(int level,float x,float z) { return footprints[level].Any(r=>r.Contains(new Vector2(x,z))); }
    void Materials() {
        concrete=Mat("Hormigon claro",new Color(.67f,.66f,.61f),.1f); concrete.mainTexture=Noise(new Color(.8f,.79f,.75f),.08f);
        concrete.mainTextureScale=new Vector2(4,4);
        orange=Mat("Terracota naranja",new Color(.94f,.36f,.10f),.22f);
        metal=Mat("Aluminio",new Color(.62f,.69f,.7f),.7f,.7f);
        floor=Mat("Pavimento piedra",new Color(.78f,.75f,.68f),.35f);
        wood=Mat("Roble",new Color(.53f,.32f,.14f),.25f);
        blue=Mat("Tapiceria azul",new Color(.09f,.22f,.29f),.12f);
        dark=Mat("Grafito",new Color(.045f,.06f,.07f),.25f);
        white=Mat("Tabiques blancos",new Color(.86f,.87f,.85f),.12f);
        green=Mat("Vegetacion",new Color(.23f,.34f,.1f),.08f);
        soil=Mat("Paisaje seco",new Color(.53f,.44f,.30f),.05f); soil.mainTexture=Noise(new Color(.76f,.72f,.61f),.2f);soil.mainTextureScale=new Vector2(70,70);
        light=Mat("Luminaria",new Color(1,.88f,.59f),.25f);
        light.EnableKeyword("_EMISSION");light.SetColor("_EmissionColor",new Color(1,.83f,.5f)*1.5f);
        glass=Mat("Vidrio azul",new Color(.28f,.52f,.67f,.30f),.94f,.12f);
        glass.SetFloat("_Mode",3);glass.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        glass.SetInt("_ZWrite",0);glass.DisableKeyword("_ALPHATEST_ON");glass.EnableKeyword("_ALPHABLEND_ON");glass.renderQueue=3000;
    }
    Material Mat(string name,Color color,float gloss,float metallic=0) {
        var m=new Material(Shader.Find("Standard")){name=name,color=color};m.SetFloat("_Glossiness",gloss);m.SetFloat("_Metallic",metallic);return m;
    }
    Texture2D Noise(Color color,float amount) {
        var t=new Texture2D(128,128);var pixels=new Color[128*128];
        for(int i=0;i<pixels.Length;i++){float v=(float)random.NextDouble()*amount-amount/2;pixels[i]=new Color(color.r+v,color.g+v,color.b+v);}
        t.SetPixels(pixels);t.Apply();return t;
    }
    void EnvironmentSetup() {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.65f,.76f,.9f);RenderSettings.ambientEquatorColor=new Color(.52f,.55f,.58f);RenderSettings.ambientGroundColor=new Color(.25f,.23f,.2f);
        var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.8f);RenderSettings.skybox=sky;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.002f;RenderSettings.fogColor=new Color(.65f,.75f,.84f);
        var sun=new GameObject("Sol de tarde").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.25f;sun.color=new Color(1,.94f,.84f);
        sun.transform.rotation=Quaternion.Euler(42,-38,0);sun.shadows=LightShadows.Soft;RenderSettings.sun=sun;
        QualitySettings.shadowDistance=95;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;
        QualitySettings.antiAliasing=4;QualitySettings.vSyncCount=1;
    }
    GameObject Box(string name,Vector3 p,Vector3 size,Material mat,bool collide=true,Transform parent=null) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent?parent:architecture);g.transform.position=p;g.transform.localScale=size;
        g.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collide)g.GetComponent<Collider>().enabled=false;
        if(mat==glass)g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        return g;
    }
    GameObject Cylinder(string name,Vector3 p,float radius,float height,Material mat,bool collide=false) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(architecture);g.transform.position=p;g.transform.localScale=new Vector3(radius*2,height/2,radius*2);
        g.GetComponent<Renderer>().sharedMaterial=mat;g.GetComponent<Collider>().enabled=collide;return g;
    }
    GameObject Line(string name,Vector3 a,Vector3 b,float width,Material mat,bool collide=false,Transform parent=null) {
        var g=Box(name,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat,collide,parent);
        g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);return g;
    }
    void StructuralGeometry() {
        foreach(var r in records) {
            if(r[0]=="E" && r[2]!="WALL") {
                Vector3 a=nodes[int.Parse(r[3])],b=nodes[int.Parse(r[4])];bool col=r[2].Contains("COLUMN");
                float w=col?.7f:.6f,h=col?.7f:.8f;if(r[2].Contains("SHS")){w=.3f;h=.3f;}
                if(r.Length>14 && r[13]!=""){w=F(r[13]);h=F(r[14]);}
                var g=Box((col?"Columna_":"Viga_")+r[1],(a+b)/2,new Vector3(h,(b-a).magnitude,w),concrete,true,col?structure:beamLayer);
                g.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);
                var id=g.AddComponent<StructuralIdentity>();id.key="E:"+r[1];id.description=(col?"Columna ":"Viga ")+r[1]+"\nNodos "+r[3]+" → "+r[4]+"\nGeometría del modelo principal";memberCount++;
            } else if(r[0]=="W") {
                var a=new Vector3(F(r[3]),0,F(r[4]));var b=new Vector3(F(r[6]),0,F(r[7]));float lo=F(r[5]),hi=F(r[8]);
                if(DrawConnectingPortal(r,a,b,lo,hi)){wallCount++;continue;}
                if(Mathf.Abs(a.z-16.4f)<.03f && Mathf.Abs(b.z-16.4f)<.03f && lo<4.5f && hi>6.7f && hi<8.1f && Mathf.Max(a.x,b.x)>33 && Mathf.Min(a.x,b.x)<36) {
                    Rect panel=Rect.MinMaxRect(Mathf.Min(a.x,b.x),lo,Mathf.Max(a.x,b.x),hi);
                    foreach(var part in Cut(panel,Rect.MinMaxRect(33,Storey+Finish-.01f,36,Storey+Finish+2.65f))) {
                        var piece=Box("Muro_"+r[1]+" · vano arquitectonico cafeteria",new Vector3(part.center.x,part.center.y,a.z),new Vector3(part.width,part.height,F(r[9])),concrete,true,structure);
                        var ident=piece.AddComponent<StructuralIdentity>();ident.key="W:"+r[1];ident.description="Muro "+r[1]+" · abertura visual de cafeteria; contrato analitico original conservado";
                    }
                    wallCount++;continue;
                }
                var g=Box("Muro_"+r[1],(a+b)/2+Vector3.up*((lo+hi)/2),new Vector3((b-a).magnitude,hi-lo,F(r[9])),concrete,true,structure);
                g.transform.rotation=Quaternion.FromToRotation(Vector3.right,(b-a).normalized);
                var wid=g.AddComponent<StructuralIdentity>();wid.key="W:"+r[1];wid.description="Muro "+r[1]+"\nPaño del modelo principal";wallCount++;
            }
        }
    }
    void Grid(int level,out float[] xs,out float[] zs,out bool[,] occupied,bool envelope=false) {
        var rects=envelope?Envelope(level):footprints[level];xs=rects.SelectMany(r=>new[]{r.xMin,r.xMax}).Distinct().OrderBy(v=>v).ToArray();
        zs=rects.SelectMany(r=>new[]{r.yMin,r.yMax}).Distinct().OrderBy(v=>v).ToArray();
        occupied=new bool[xs.Length-1,zs.Length-1];
        for(int x=0;x<xs.Length-1;x++)for(int z=0;z<zs.Length-1;z++){ var point=new Vector2((xs[x]+xs[x+1])/2,(zs[z]+zs[z+1])/2);occupied[x,z]=rects.Any(r=>r.Contains(point)); }
    }
    void Deck(int level) {
        float[] xs,zs;bool[,] o;Grid(level,out xs,out zs,out o);float top=level*Storey+Finish;
        for(int z=0;z<zs.Length-1;z++) { int start=-1;
            for(int x=0;x<xs.Length;x++) {
                bool yes=x<xs.Length-1 && o[x,z];
                if(yes && start<0)start=x;
                if(!yes && start>=0){Box("Piso_"+level,new Vector3((xs[start]+xs[x])/2,top-.08f,(zs[z]+zs[z+1])/2),new Vector3(xs[x]-xs[start],.16f,zs[z+1]-zs[z]),floor);start=-1;}
            }
        }
    }
    List<Edge> Boundary(int level) {
        float[] xs,zs;bool[,] o;Grid(level,out xs,out zs,out o,true);var edges=new List<Edge>();
        for(int z=0;z<zs.Length;z++) {int start=-1;
            for(int x=0;x<xs.Length;x++) {
                bool edge=x<xs.Length-1 && ((z>0 && o[x,z-1])!=(z<zs.Length-1 && o[x,z]));
                if(edge && start<0)start=x;
                if(!edge && start>=0){edges.Add(new Edge(new Vector2(xs[start],zs[z]),new Vector2(xs[x],zs[z])));start=-1;}
            }
        }
        for(int x=0;x<xs.Length;x++) {int start=-1;
            for(int z=0;z<zs.Length;z++) {
                bool edge=z<zs.Length-1 && ((x>0 && o[x-1,z])!=(x<xs.Length-1 && o[x,z]));
                if(edge && start<0)start=z;
                if(!edge && start>=0){edges.Add(new Edge(new Vector2(xs[x],zs[start]),new Vector2(xs[x],zs[z])));start=-1;}
            }
        }
        return edges;
    }
    // Only these two LT1 projections are occupied glazed volumes.
    List<Rect> Envelope(int level) {
        var parts=footprints[level].SelectMany(r=>Cut(r,Rect.MinMaxRect(9.69f,-20,60,-.25f))).ToList();
        if(level==2)parts.Add(Rect.MinMaxRect(9.7f,-4.42f,17.79f,-.25f));
        if(level==4)parts.Add(Rect.MinMaxRect(19.7f,-4.42f,30.3f,-.25f));
        return parts;
    }
    void Facade(int level) {
        float y=level*Storey+Finish;
        foreach(var e in Boundary(level).SelectMany(edge=>FacadeEdges(level,edge))) {
            Vector3 a=new Vector3(e.a.x,y,e.a.y),b=new Vector3(e.b.x,y,e.b.y),dir=(b-a).normalized;float len=(b-a).magnitude;
            var band=Box("Banda de hormigon",(a+b)/2+Vector3.up*.12f,new Vector3(len,.24f,.55f),concrete,!(portalEdge(e) || (level==1 && Mathf.Abs(e.a.y-e.b.y)<.01f && e.a.y>16.9f) || (level==2 && e.a.x>45 && e.a.x<46 && Mathf.Abs(e.a.x-e.b.x)<.01f)));
            band.transform.rotation=Quaternion.FromToRotation(Vector3.right,dir);
            var head=Box("Dintel de hormigon",(a+b)/2+Vector3.up*(Storey-.18f),new Vector3(len,.25f,.55f),concrete);
            head.transform.rotation=band.transform.rotation;
            int count=Mathf.Max(1,Mathf.CeilToInt(len/2.35f));float bay=len/count;
            for(int i=0;i<count;i++) {
                Vector3 mid=a+dir*(bay*(i+.5f));bool atrium=mid.x>=-.25f && mid.z<.26f;
                bool portal=(Mathf.Abs(dir.x)>.9f && mid.z>-.4f && mid.z<.3f && mid.x>0 && mid.x<45) || (Mathf.Abs(dir.x)>.9f && mid.z<0 && mid.x>-30 && mid.x<-17);
                portal |= level==1 && Mathf.Abs(dir.x)>.9f && mid.z>16.9f && ((mid.x>-26 && mid.x<-20) || (mid.x>33 && mid.x<39));
                bool sideEntry=level==2 && mid.x>45 && mid.x<46 && Mathf.Abs(dir.z)>.9f && mid.z>8 && mid.z<12;
                portal |= sideEntry;
                float sill=portal?2.65f:.42f,winH=Storey-sill-.3f;
                var win=Box(atrium?"Atrio acristalado":"Ventana",mid+Vector3.up*(sill+winH/2),new Vector3(Mathf.Max(.015f,bay-.09f),winH,.075f),glass,!portal);
                win.transform.rotation=band.transform.rotation;
                var fin=Box(atrium?"Montante atrio":"Aleta naranja",a+dir*(bay*i)+Vector3.up*(Storey*.5f),new Vector3(atrium?.065f:.66f,Storey-.25f,atrium?.1f:.75f),atrium?metal:orange,!atrium && !sideEntry);
                fin.transform.rotation=band.transform.rotation;
                if(!portal){var mull=Box("Travesano ventana",mid+Vector3.up*1.95f,new Vector3(bay,.045f,.1f),metal,false);mull.transform.rotation=band.transform.rotation;}
            }
        }
    }
    void Table(Vector3 p,float w,float d) {
        Box("Mesa",p+Vector3.up*.78f,new Vector3(w,.08f,d),wood);
        for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)Box("Pata",p+new Vector3(a*(w/2-.1f),.37f,b*(d/2-.1f)),new Vector3(.05f,.74f,.05f),metal,false);
        Box("Silla",p+new Vector3(0,.46f,-.85f),new Vector3(.5f,.09f,.5f),blue);
        Box("Respaldo silla",p+new Vector3(0,.75f,-1.05f),new Vector3(.5f,.5f,.07f),blue,false);
        for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)Box("Pata silla",p+new Vector3(a*.2f,.23f,-.85f+b*.2f),new Vector3(.035f,.46f,.035f),metal,false);
    }
    void Door(Vector3 p,float width) {
        var hinge=new GameObject("Puerta_interactiva").transform;hinge.SetParent(architecture);hinge.position=p;
        var door=hinge.gameObject.AddComponent<CampusDoor>();
        Box("Hoja",p+new Vector3(width/2,1.15f,0),new Vector3(width,2.3f,.06f),wood,true,hinge);
        Box("Visor",p+new Vector3(width/2,1.65f,-.035f),new Vector3(.3f,.75f,.01f),glass,false,hinge);
        var handle=Box("Manilla",p+new Vector3(width-.16f,1,-.08f),new Vector3(.15f,.035f,.08f),metal,false,hinge);
        var action=hinge.gameObject.AddComponent<CampusAction>();action.title="Abrir / cerrar puerta";action.kind=2;action.door=door;actions.Add(action);
    }
    void Station(Vector3 p,string title,int level) {
        var g=Box("Punto de exploracion "+level,p,new Vector3(2.4f,.75f,.14f),dark);
        Label(p+new Vector3(-1.05f,.12f,-.085f),title,.09f);
        Label(p+new Vector3(-1.05f,-.19f,-.085f),"E · DESCUBRIR EL ESPACIO",.055f);
        var a=g.AddComponent<CampusAction>();a.title=title;a.kind=0;a.index=level;actions.Add(a);
    }
    void Label(Vector3 p,string text,float size) {
        var g=new GameObject(text);g.transform.SetParent(architecture);g.transform.position=p;
        var t=g.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=text;t.characterSize=size*.3f;t.fontSize=64;t.color=Color.white;t.anchor=TextAnchor.MiddleLeft;
        if(!textMaterial) {
            textMaterial=new Material(Resources.Load<Shader>("CampusText"));
            textMaterial.mainTexture=t.font.material.mainTexture;
            Font.textureRebuilt += font => {if(textMaterial && font==t.font)textMaterial.mainTexture=font.material.mainTexture;};
        }
        g.GetComponent<Renderer>().sharedMaterial=textMaterial;
        g.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
    }
    bool portalEdge(Edge e) { return Mathf.Abs(e.a.y-e.b.y)<.01f && e.a.y>-.4f && e.a.y<.3f; }
    Vector3 StairPoint(float x,int level) {return new Vector3(x,level*Storey+Finish,-1.5f);}
    void StairFlight(Vector3 start,Vector3 finish,string name,Material sideMaterial,float width=2.04f) {
        Vector3 flat=finish-start;flat.y=0;float run=flat.magnitude;
        Vector3 dir=flat.normalized,side=Vector3.Cross(Vector3.up,dir)*(width*.5f);
        int steps=Mathf.CeilToInt((finish.y-start.y)/.18f);
        for(int i=0;i<steps;i++) {
            Vector3 p=Vector3.Lerp(start,finish,(i+.5f)/steps)-Vector3.up*.11f;
            var step=Box(name+" · peldaño",p,new Vector3(run/steps,.22f,width),concrete,false);
            step.transform.rotation=Quaternion.FromToRotation(Vector3.right,dir);
        }
        var g=new GameObject(name+" · colision");g.transform.SetParent(architecture);
        var mesh=new Mesh();mesh.vertices=new[]{start-side,start+side,finish-side,finish+side};
        mesh.triangles=new[]{0,2,1,2,3,1};mesh.RecalculateNormals();g.AddComponent<MeshCollider>().sharedMesh=mesh;
        var soffit=Box(name+" · losa inclinada",(start+finish)/2-Vector3.up*.22f,new Vector3((finish-start).magnitude,.18f,width),concrete,false);
        soffit.transform.rotation=Quaternion.FromToRotation(Vector3.right,finish-start);
        for(int edge=-1;edge<=1;edge+=2) {
            Vector3 inset=(finish-start)*.08f;
            Vector3 a=start+inset+side*edge+Vector3.up*.55f,b=finish-inset+side*edge+Vector3.up*.55f;
            var panel=Box(name+" · parapeto",(a+b)/2,new Vector3((b-a).magnitude,.85f,.10f),sideMaterial);
            // Align the panel's length with the stair run.
            panel.transform.rotation=Quaternion.FromToRotation(Vector3.right,b-a);
            Line(name+" · pasamanos",a+Vector3.up*.5f,b+Vector3.up*.5f,.05f,dark);
        }
    }
    void Circulation() {
        // Existing slab polygons, not the enclosed cantilevers, form the two landings.
        // Lower landing: X19.70..30.30 at Z11.88; upper: X9.70..12.50 at Z15.84.
        StairFlight(StairPoint(39.2f,2),StairPoint(29.2f,3),"LT1 tramo inferior",orange);
        StairFlight(StairPoint(20.8f,3),StairPoint(11.2f,4),"LT1 tramo intermedio",orange);
        StairFlight(StairPoint(11.2f,4),StairPoint(1.2f,5),"LT1 tramo superior",orange);
        Landing(19.7f,30.3f,3,-2.5f);
        Landing(9.7f,12.5f,4,-2.76f);
        Box("Llegada superior LT1",new Vector3(1.2f,5*Storey+Finish-.1f,-.6f),new Vector3(2.1f,.2f,3.1f),concrete);
        Box("Acceso inferior LT1",new Vector3(39.8f,2*Storey+Finish-.1f,-.65f),new Vector3(3.2f,.2f,3.1f),concrete);
        // Add 2.04 m toward the building (red annotation), keeping the outer edge at Y=-7.37 m.
        // One-storey rise and longitudinal direction stay unchanged.
        StairFlight(new Vector3(29.2f,Storey+Finish,-5.33f),new Vector3(39.2f,2*Storey+Finish,-5.33f),"LT1 acceso inferior · ancho ampliado",orange,4.08f);
        // Flat access slab before taking the first exterior flight.
        Box("LT1 losa de llegada a nivel de tierra",new Vector3(28.2f,Storey+Finish-.12f,-5.33f),new Vector3(4,.24f,5.44f),concrete);
        Box("LT1 acceso a losa inferior",new Vector3(28.2f,Storey+Finish-.04f,-9.1f),new Vector3(3,.08f,2.1f),floor);
        // Broad side apron visible in the oblique photograph, connected to the stair landing.
        float accessY=2*Storey+Finish;
        Box("Losa lateral de llegada LT1",new Vector3(43.1f,accessY-.15f,-3.75f),new Vector3(7.8f,.30f,7.5f),concrete);
        // The outer side is flush with the elevated campus terrain, allowing access onto the slab.
        Box("Antepecho frontal losa lateral LT1",new Vector3(43.1f,accessY+.46f,-7.45f),new Vector3(7.8f,.92f,.16f),concrete);
        InteriorElevators();
    }
    void Landing(float lo,float hi,int level,float front) {
        float y=level*Storey+Finish;
        Box("LT1 descanso abierto · losa existente",new Vector3((lo+hi)/2,y-.12f,(front-.25f)/2),new Vector3(hi-lo,.24f,-.25f-front),concrete);
        // Front rail, with no walls enclosing the landing.
        Box("LT1 descanso · antepecho naranja",new Vector3((lo+hi)/2,y+.48f,front+.05f),new Vector3(hi-lo,.96f,.12f),orange);
        Line("LT1 descanso · pasamanos",new Vector3(lo,y+1.03f,front),new Vector3(hi,y+1.03f,front),.05f,dark);
    }
    IEnumerator InspectorPreview() {
        yield return null;yield return null;
        player.enabled=false;player.eye.transform.position=new Vector3(24,12,-24);player.eye.transform.LookAt(new Vector3(34,8,-1.5f));
        var laser=player.GetComponent<CampusLaser>();laser.ShowPreview("E:207");
        var args=Environment.GetCommandLineArgs();string path=Application.persistentDataPath;
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-campus-output")path=args[i+1];
        System.IO.Directory.CreateDirectory(path);
        for(int page=0;page<=2;page+=2) {
            laser.PreviewPage(page);yield return null;yield return null;
            string file=System.IO.Path.Combine(path,page==0?"11_inspector_tarjetas.png":"12_inspector_diagramas.png");
            yield return new WaitForEndOfFrame();
            var preview=ScreenCapture.CaptureScreenshotAsTexture();
            if(preview){System.IO.File.WriteAllBytes(file,preview.EncodeToPNG());Destroy(preview);}
            for(int frame=0;frame<3;frame++)yield return null;
        }
        Application.Quit();
    }
    IEnumerator CaptureRevision() {
        yield return null;yield return null;
        var args=Environment.GetCommandLineArgs();string path=Application.persistentDataPath;
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-campus-output")path=args[i+1];
        System.IO.Directory.CreateDirectory(path);player.enabled=false;
        var camera=player.eye;camera.transform.position=new Vector3(9,23,-43);camera.transform.LookAt(new Vector3(18,12,0));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"04_LT1_corregido.png"));
        camera.transform.position=new Vector3(57,27,58);camera.transform.LookAt(new Vector3(5,8,19));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"05_terraza_posterior.png"));
        camera.transform.position=new Vector3(47,2*Storey+Finish+5.5f,-5.2f);camera.transform.LookAt(new Vector3(24,3,-3.75f));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"06_escalera_ancha_y_terreno.png"));
        camera.transform.position=new Vector3(60,15,30);camera.transform.LookAt(new Vector3(42,7,19));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"07_entrada_y_escalera_terraza.png"));
        camera.transform.position=new Vector3(52,17,28);camera.transform.LookAt(new Vector3(31,6,20));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"08_terraza_cafeteria.png"));
        camera.transform.position=new Vector3(35.8f,Storey+Finish+1.7f,15.3f);camera.transform.LookAt(new Vector3(26,Storey+Finish+1.3f,11.5f));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"09_cafeteria_interior.png"));
        camera.transform.position=new Vector3(23,12,-24);camera.transform.LookAt(new Vector3(34,6,-1.5f));
        yield return null;Capture(camera,System.IO.Path.Combine(path,"10_LT1_un_piso_y_losa.png"));Application.Quit();
    }
    void Landscape() {
        Box("Campus",new Vector3(8,.05f,30),new Vector3(240,.8f,240),soil);
        Box("Paseo principal",new Vector3(5,.49f,-17),new Vector3(110,.08f,7),floor);
        Box("Camino acceso",new Vector3(-28,.49f,-10),new Vector3(6,.08f,12),floor);
        Box("Cancha de futbol",new Vector3(0,.47f,65),new Vector3(64,.06f,80),green,false);
        for(int side=-1;side<=1;side+=2) {
            Box("Linea cancha",new Vector3(side*30,.51f,65),new Vector3(.12f,.02f,76),white,false);
            Box("Linea cancha",new Vector3(0,.51f,65+side*38),new Vector3(60,.02f,.12f),white,false);
            for(int s=-1;s<=1;s+=2)Line("Porteria",new Vector3(s*3,.53f,65+side*38),new Vector3(s*3,3,65+side*38),.1f,white);
            Line("Travesano porteria",new Vector3(-3,3,65+side*38),new Vector3(3,3,65+side*38),.1f,white);
        }
        Box("Medio cancha",new Vector3(0,.51f,65),new Vector3(60,.02f,.12f),white,false);
        for(int i=0;i<26;i++) {
            float x=-45+i*4.6f,z=i%2==0?-26:28;if(z<0 || x<-32 || x>48)Tree(new Vector3(x,.5f,z));
            if(i%3==0) {Cylinder("Farola",new Vector3(x,2.8f,-20),.06f,4.6f,metal);Box("Cabezal farola",new Vector3(x,5.15f,-19.7f),new Vector3(.3f,.07f,.8f),light,false);
                Box("Banco exterior",new Vector3(x,.9f,-22),new Vector3(2,.2f,.65f),wood); }
        }
        SlopingCampus(); BuriedLT1(); Mountains();
        Box("Marquesina entrada",new Vector3(-24,3.7f,-3),new Vector3(13,.25f,5),concrete);
        Label(new Vector3(-29,2.7f,-3.5f),"INGENIERÍA",.32f);
    }
    void BuriedLT1() {
        // The lowest LT1 level is below the surrounding soil; its analytical members remain in place.
        float grade=Storey+Finish-.03f;
        Box("LT1 relleno de tierra · nivel inferior enterrado",new Vector3(22.625f,(grade+Finish)/2,8.65f),new Vector3(45.75f,grade-Finish,17.3f),soil);
        const int nx=53,nz=31;
        var vertices=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[vertices.Length];var tris=new List<int>();
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++) {
            float px=-6+x,pz=-13+z;int i=x+z*(nx+1);
            float height=Finish+Storey*Mathf.Clamp01((px+6)/5.75f)*Mathf.Clamp01((pz+13)/7)-.03f;
            vertices[i]=new Vector3(px,height,pz);uv[i]=new Vector2(px*.12f,pz*.12f);
            if(x<nx && z<nz && (pz<0 || px<-.25f || px>45.5f)) {
                int a=i,b=i+1,c=i+nx+1,d=c+1;tris.AddRange(new[]{a,c,b,b,c,d});
            }
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();
        var g=new GameObject("LT1 terreno frontal a cota de losa inferior");g.transform.SetParent(architecture);
        g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=soil;g.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    float CampusHeight(float x,float z) {
        float across=Mathf.Min(Mathf.Clamp01((x-32)/13.5f),Mathf.Clamp01((85-x)/28));
        float along=z< -7.5f?Mathf.Clamp01((z+18)/10.5f):Mathf.Clamp01((110-z)/90);
        return Finish+2*Storey*across*along-.03f;
    }
    void SlopingCampus() {
        // Continuous dry hillside: slab elevation at the building side, field elevation below.
        const int nx=53,nz=128;
        var vertices=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[vertices.Length];var tris=new List<int>();
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++) {
            float px=32+x,pz=-18+z;int i=x+z*(nx+1);
            vertices[i]=new Vector3(px,CampusHeight(px,pz),pz);uv[i]=new Vector2(px*.16f,pz*.16f);
            if(x<nx && z<nz) {
                float cx=px+.5f,cz=pz+.5f;
                // Keep the building, terrace and broad stair free of terrain.
                bool hidden=(cx<39.2f && cz<0)||(cx<45.5f && cz>=0 && cz<18)||(cx<47 && cz>=18 && cz<26);
                if(!hidden){int a=i,b=i+1,c=i+nx+1,d=c+1;tris.AddRange(new[]{a,c,b,b,c,d});}
            }
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();
        var g=new GameObject("Terreno campus · talud hasta cancha");g.transform.SetParent(architecture);
        g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=soil;g.AddComponent<MeshCollider>().sharedMesh=mesh;
        // Pedestrian strip follows the slope rather than floating above flat ground.
        var pv=new List<Vector3>();var puv=new List<Vector2>();var pt=new List<int>();
        for(int i=0;i<=108;i++) {
            float z=-7.5f+i;
            for(int side=0;side<2;side++){float x=53+side*3;pv.Add(new Vector3(x,CampusHeight(x,z)+.035f,z));puv.Add(new Vector2(side,i*.2f));}
            if(i<108){int k=i*2;pt.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
        }
        var path=new Mesh();path.vertices=pv.ToArray();path.uv=puv.ToArray();path.triangles=pt.ToArray();path.RecalculateNormals();
        var walk=new GameObject("Sendero lateral al nivel del terreno");walk.transform.SetParent(architecture);
        walk.AddComponent<MeshFilter>().sharedMesh=path;walk.AddComponent<MeshRenderer>().sharedMaterial=floor;walk.AddComponent<MeshCollider>().sharedMesh=path;
        Box("Conexion losa con sendero elevado",new Vector3(50,2*Storey+Finish-.04f,-3.75f),new Vector3(6,.08f,3),floor);
        for(int i=0;i<9;i++){float z=30+i*8,x=43;Tree(new Vector3(x,CampusHeight(x,z),z));}
    }
    void SideEntrance() {
        float y=2*Storey+Finish;
        // Raised soil meets the occupied side facade, covering the previous void beneath it.
        Box("Entrada lateral · losa hasta terreno",new Vector3(49.25f,y-.12f,8.5f),new Vector3(7.5f,.24f,3.6f),floor);
        Door(new Vector3(45.8f,y,9.3f),1.6f);
        var door=actions.Last().door;door.transform.Find("Hoja").GetComponent<Renderer>().sharedMaterial=glass;door.baseYaw=90;door.transform.localRotation=Quaternion.Euler(0,90,0);
        Box("Entrada lateral · marquesina",new Vector3(47.3f,y+2.7f,8.5f),new Vector3(3.6f,.16f,3),concrete);
        var sign=Box("Entrada lateral · letrero",new Vector3(45.95f,y+2.6f,8.5f),new Vector3(.12f,.35f,1.7f),dark,false);
        // A clear approach continues behind the doorway into the corridor.
        Box("Entrada lateral · pavimento interior",new Vector3(43.5f,y-.08f,8.5f),new Vector3(4,.16f,1.8f),floor);
    }
    void RearTerrace() {
        // Architectural podium inferred from the second photograph; no analytical members are changed.
        float y=Storey+Finish;const float lo=-28,hi=45,front=17.05f,back=26.0f;
        Material brick=Mat("Zocalo ladrillo terraza",new Color(.43f,.24f,.16f),.10f);
        Box("Terraza posterior · losa saliente",new Vector3((lo+hi)/2,y-.18f,(front+back)/2),new Vector3(hi-lo,.36f,back-front),concrete);
        Box("Terraza posterior · zocalo",new Vector3((lo+hi)/2,(Finish+y-.36f)/2,back-.18f),new Vector3(hi-lo,y-Finish-.36f,.3f),brick);
        for(float x=lo;x<=hi;x+=hi-lo)Box("Terraza posterior · cierre lateral",new Vector3(x,(Finish+y-.36f)/2,(front+back)/2),new Vector3(.3f,y-Finish-.36f,back-front),brick);
        // Low pale parapet and coping, as in the roof of the service podium.
        Box("Terraza posterior · antepecho",new Vector3((lo+hi)/2,y+.45f,back),new Vector3(hi-lo,.9f,.18f),concrete);
        Box("Terraza posterior · coronacion",new Vector3((lo+hi)/2,y+.93f,back),new Vector3(hi-lo,.08f,.26f),metal,false);
        Box("Terraza posterior · baranda lateral derecha 1",new Vector3(hi,y+.45f,18),new Vector3(.18f,.9f,1.9f),concrete);
        Box("Terraza posterior · baranda lateral derecha 2",new Vector3(hi,y+.45f,25),new Vector3(.18f,.9f,2),concrete);
        Box("Terraza posterior · baranda lateral izquierda",new Vector3(lo,y+.45f,20.35f),new Vector3(.18f,.9f,6.6f),concrete);
        // Seating replaces the former service equipment; leave the facade corridor clear.
        for(float x=-20;x<33;x+=6)CafeTable(new Vector3(x,y,22),false);
        // Two stair flights with a flat intermediate slab.
        float middle=y+Storey*.5f;
        StairFlight(new Vector3(37,y,21.5f),new Vector3(41,middle,21.5f),"Terraza · tramo inferior",concrete,4);
        Box("Terraza · losa de descanso intermedia",new Vector3(42,middle-.12f,21.5f),new Vector3(2,.24f,4),concrete);
        for(int edge=-1;edge<=1;edge+=2) {
            Box("Terraza · antepecho descanso",new Vector3(42,middle+.45f,21.5f+edge*2),new Vector3(2,.9f,.12f),concrete);
            Line("Terraza · pasamanos descanso",new Vector3(41,middle+1,21.5f+edge*2),new Vector3(43,middle+1,21.5f+edge*2),.05f,dark);
        }
        StairFlight(new Vector3(43,middle,21.5f),new Vector3(47,2*Storey+Finish,21.5f),"Terraza · tramo superior",concrete,4);
        Box("Terraza posterior · descanso superior",new Vector3(49,2*Storey+Finish-.12f,21.5f),new Vector3(4,.24f,4),concrete);
        Box("Terraza posterior · acceso desde sendero",new Vector3(52,2*Storey+Finish-.08f,21.5f),new Vector3(2,.16f,4),floor);
        // Approach at the left end: opening in the parapet and an external stair.
        // The short wall sections replace the full railing near the access.
        StairFlight(new Vector3(-29,Finish,34),new Vector3(-29,y,25),"Terraza posterior · escalera de acceso",concrete);
        Box("Terraza posterior · descanso acceso",new Vector3(-28.7f,y-.12f,25),new Vector3(1.8f,.24f,2.1f),concrete);
        Box("Terraza · puerta servicio",new Vector3(35,1.55f,back+.18f),new Vector3(1.3f,2.2f,.06f),dark);
    }
    void CafeTable(Vector3 p,bool indoors) {
        Box("Cafeteria · mesa",p+Vector3.up*.77f,new Vector3(1.8f,.09f,1),wood);
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2) {
            Box("Cafeteria · pata mesa",p+new Vector3(x*.72f,.36f,z*.36f),new Vector3(.055f,.72f,.055f),metal,false);
            Vector3 seat=p+new Vector3(x*.48f,0,z*.98f);
            Box("Cafeteria · asiento",seat+Vector3.up*.45f,new Vector3(.46f,.09f,.46f),indoors?blue:wood);
            Box("Cafeteria · respaldo",seat+new Vector3(0,.72f,z*.21f),new Vector3(.46f,.55f,.06f),indoors?blue:wood);
            for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)Box("Cafeteria · pata silla",seat+new Vector3(a*.17f,.22f,b*.17f),new Vector3(.035f,.44f,.035f),metal,false);
        }
    }
    void Cafeteria() {
        float y=Storey+Finish;
        Box("Cafeteria · piso interior",new Vector3(30.5f,y-.08f,13.65f),new Vector3(17,.16f,7.3f),floor);
        Box("Cafeteria · pared lateral izquierda",new Vector3(22,y+1.5f,13.25f),new Vector3(.14f,3,6.5f),white);
        Box("Cafeteria · pared lateral derecha",new Vector3(39,y+1.5f,13.25f),new Vector3(.14f,3,6.5f),white);
        Box("Cafeteria · fondo",new Vector3(30.5f,y+1.5f,10),new Vector3(17,3,.14f),white);
        Box("Cafeteria · cielo",new Vector3(30.5f,y+3.05f,13.25f),new Vector3(17,.08f,6.5f),white,false);
        Box("Cafeteria · barra",new Vector3(27,y+.52f,11.5f),new Vector3(7,1.04f,1.05f),wood);
        Box("Cafeteria · cubierta barra",new Vector3(27,y+1.1f,11.5f),new Vector3(7.2f,.12f,1.15f),white);
        Box("Cafeteria · maquina de cafe",new Vector3(25,y+1.45f,11.5f),new Vector3(.8f,.6f,.5f),metal);
        for(int i=0;i<4;i++)Cylinder("Cafeteria · taza",new Vector3(28+i*.4f,y+1.23f,11.65f),.065f,.13f,white);
        CafeTable(new Vector3(25,y,14),true);CafeTable(new Vector3(30,y,14),true);
        Door(new Vector3(33.7f,y,16.6f),1.6f);
        var entry=actions.Last().door;entry.transform.Find("Hoja").GetComponent<Renderer>().sharedMaterial=glass;
        Box("Cafeteria · umbral hacia terraza",new Vector3(34.5f,y-.07f,16.9f),new Vector3(3,.14f,1),floor);
        Label(new Vector3(35.5f,y+2.62f,16.68f),"CAFETERIA",.22f);
        architecture.GetChild(architecture.childCount-1).rotation=Quaternion.Euler(0,180,0);
        Label(new Vector3(30,y+2.3f,10.12f),"CAFE · SANDWICHES · JUGOS",.14f);
        architecture.GetChild(architecture.childCount-1).rotation=Quaternion.Euler(0,180,0);
        var lamp=new GameObject("Cafeteria · luz interior").AddComponent<Light>();lamp.transform.SetParent(architecture);lamp.transform.position=new Vector3(31,y+2.6f,13);
        lamp.type=LightType.Point;lamp.range=16;lamp.intensity=1.3f;lamp.color=new Color(1,.9f,.72f);
    }
    void Mountains() {
        const int nx=90,nz=40;
        var vertices=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[vertices.Length];var tris=new List<int>();
        for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++) {
            float px=-230+x*5.5f,pz=115+z*6;
            float ridge=Mathf.Sin(z/(float)nz*Mathf.PI);
            float height=ridge*(25+Mathf.PerlinNoise(px*.008f,1.3f)*80+Mathf.PerlinNoise(px*.032f,pz*.025f)*15);
            int index=x+z*(nx+1);vertices[index]=new Vector3(px,height-1,pz);uv[index]=new Vector2(x/(float)nx,z/(float)nz);
            if(x<nx&&z<nz){int a=index,b=index+1,c=index+nx+1,d=c+1;tris.AddRange(new[]{a,c,b,b,c,d});}
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();
        var g=new GameObject("Cordillera del campus");g.transform.SetParent(architecture);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=soil;
    }
    void Tree(Vector3 p) {
        Cylinder("Tronco",p+Vector3.up*1.8f,.13f,3.6f,wood);
        for(int i=0;i<3;i++) {
            var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="Copa";g.transform.SetParent(architecture);g.transform.position=p+new Vector3((i-1)*.8f,3.5f+i*.55f,0);g.transform.localScale=new Vector3(2.4f,2.8f,2.2f);g.GetComponent<Renderer>().sharedMaterial=green;g.GetComponent<Collider>().enabled=false;
        }
    }
    void Planter(Vector3 p){Box("Macetero",p+Vector3.up*.35f,new Vector3(.7f,.7f,.7f),concrete);Cylinder("Planta",p+Vector3.up*.9f,.32f,.8f,green);}
    void Roof() {
        float y=5*Storey+Finish;
        for(float x=-20;x<40;x+=14)if(Inside(5,x,8)) {
            Planter(new Vector3(x,y,8));Table(new Vector3(x+2,y,8),2,1);
            Box("Equipo de cubierta",new Vector3(x,y+.55f,14),new Vector3(3,1.1f,1.6f),metal);
        }
        foreach(var e in Boundary(5)) {
            bool entrance=Mathf.Abs(e.a.y-e.b.y)<.01f && e.a.y<1 && e.a.x<-18 && e.b.x>-30;
            entrance |= Mathf.Abs(e.a.y-e.b.y)<.01f && e.a.y<.3f && e.a.x<2.5f && e.b.x>0;
            if(!entrance)Line("Baranda de terraza",new Vector3(e.a.x,y+1.1f,e.a.y),new Vector3(e.b.x,y+1.1f,e.b.y),.08f,metal,true);
        }
    }
    void Combine(Transform parent) {
        var groups=new Dictionary<Material,List<CombineInstance>>();
        foreach(var mf in parent.GetComponentsInChildren<MeshFilter>()) {
            var renderer=mf.GetComponent<MeshRenderer>();
            if(!renderer || !renderer.enabled || mf.GetComponent<TextMesh>() || mf.GetComponentInParent<CampusDoor>() ||
                (parent==structure && mf.transform.IsChildOf(beamLayer)))continue;
            var m=renderer.sharedMaterial;if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();
            groups[m].Add(new CombineInstance{mesh=mf.sharedMesh,transform=parent.worldToLocalMatrix*mf.transform.localToWorldMatrix});renderer.enabled=false;
        }
        foreach(var pair in groups) {
            var g=new GameObject("Superficie_"+pair.Key.name);g.transform.SetParent(parent,false);
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);
            g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=pair.Key;
            if(pair.Key==glass)r.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    bool WalkLine(Vector3 start,Vector3 end) {
        player.Teleport(start+Vector3.up*.06f);
        Vector3 horizontal=end-start;horizontal.y=0;int count=Mathf.CeilToInt(horizontal.magnitude/.035f);
        Vector3 step=horizontal/count+Vector3.down*.09f;
        for(int i=0;i<count;i++)player.controller.Move(step);
        bool pass=Vector3.Distance(new Vector3(player.transform.position.x,end.y,player.transform.position.z),end)<.4f && Mathf.Abs(player.transform.position.y-end.y)<.2f;
        Debug.Log("CAMPUS_WALK_CHECK "+start+" -> "+end+" actual="+player.transform.position+" passed="+pass);
        if(!pass)foreach(var c in Physics.OverlapCapsule(player.transform.position+Vector3.up*.3f,player.transform.position+Vector3.up*1.5f,.33f))Debug.Log("CAMPUS_BLOCKER "+c.gameObject.name+" "+c.bounds);
        return pass;
    }
    IEnumerator DeliveryCheck() {
        yield return null; yield return null;
        bool valid=nodeCount>1200 && memberCount>600 && slabCount>600 && wallCount>70;
        player.enabled=false;
        for(int level=0;level<=5;level++) {
            RaycastHit hit;bool floorHit=Physics.Raycast(Spawn(level)+Vector3.up*1,Vector3.down,out hit,3);
            valid &= floorHit;
            Debug.Log("CAMPUS_FLOOR_CHECK "+level+" "+floorHit);
            float x=-28;
            valid &= WalkLine(new Vector3(x,level*Storey+Finish,-5),new Vector3(x,level*Storey+Finish,2));
            if(level<5) {
                float top=level*Storey+Finish;
                valid &= WalkLine(new Vector3(x,top,2),new Vector3(-23.5f,top,8));
                var door=actions.FirstOrDefault(a=>a.kind==2 && Mathf.Abs(a.transform.position.x+24.3f)<.1f && Mathf.Abs(a.transform.position.y-top)<.1f);
                if(door!=null) {
                    door.door.Toggle();door.transform.localRotation=Quaternion.Euler(0,-100,0);Physics.SyncTransforms();
                    valid &= WalkLine(new Vector3(-23.5f,top,8),new Vector3(-23.5f,top,10));
                } else {valid=false;Debug.Log("CAMPUS_ROOM_DOOR_MISSING "+level);}
            }
            
        }
        string path=Application.persistentDataPath;
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-campus-output")path=args[i+1];
        System.IO.Directory.CreateDirectory(path);
        player.enabled=false;Cursor.lockState=CursorLockMode.None;
        var camera=player.eye;
        camera.transform.position=new Vector3(-46,22,-42);camera.transform.LookAt(new Vector3(2,10,6));
        yield return null; Capture(camera,System.IO.Path.Combine(path,"01_exterior.png"));
        camera.transform.position=new Vector3(-25,Storey*2+Finish+1.7f,10.3f);camera.transform.LookAt(new Vector3(-21,Storey*2+Finish+1.5f,14));
        yield return null; Capture(camera,System.IO.Path.Combine(path,"02_interior.png"));
        camera.transform.position=new Vector3(67,29,-34);camera.transform.LookAt(new Vector3(7,11,7));
        yield return null; Capture(camera,System.IO.Path.Combine(path,"03_fachada.png"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(path,"validation.json"),"{\"passed\":"+valid.ToString().ToLowerInvariant()+",\"nodes\":"+nodeCount+",\"members\":"+memberCount+",\"slabs\":"+slabCount+",\"walls\":"+wallCount+",\"walkable_levels\":6}");
        Debug.Log("CAMPUS_CHECK_COMPLETE "+valid);Application.Quit(valid?0:2);
    }
    void Capture(Camera camera,string path) {
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
        System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;Destroy(rt);Destroy(texture);
    }
}
public class StructuralIdentity : MonoBehaviour { public string description,key; }
public class CampusAction : MonoBehaviour { public string title; public int kind,index;public CampusDoor door; }
public class CampusDoor : MonoBehaviour
{
    public float baseYaw;bool open;public void Toggle(){open=!open;}
    void Update(){transform.localRotation=Quaternion.Slerp(transform.localRotation,Quaternion.Euler(0,baseYaw+(open?-100:0),0),Time.deltaTime*5);}
}
