using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Displays the saved station results. No load, stiffness or analytical geometry is changed.
public sealed class CampusBeamDiagram : MonoBehaviour
{
    public string Description { get; private set; }
    public Vector3[] Curve { get; private set; }=new Vector3[0];
    public float[] Values { get; private set; }=new float[0];
    public float[] Stations { get; private set; }=new float[0];
    public float VisualScale { get; private set; }
    public string Label { get; private set; }
    public string Unit { get; private set; }
    Transform root;
    Material material;
    LineRenderer marker;
    public static readonly string[] Modes={"Axial N","Cortante Vy","Cortante Vz","Torsión T","Momento My","Momento Mz","Deformada","Ocultar"};
    public void Clear()
    {
        if(root){root.gameObject.SetActive(false);Destroy(root.gameObject);root=null;}
        marker=null;Curve=new Vector3[0];Values=new float[0];Stations=new float[0];Description="Sin diagrama sobre el elemento.";
    }
    public static Vector3 ToUnity(Vector3 v){return new Vector3(v.x,v.z,v.y);}
    public static Vector3 Vector(float[] v,int offset=0){return new Vector3(v[offset],v[offset+1],v[offset+2]);}
    // Evaluate in right-handed OpenSees coordinates: do not map rotations as ordinary Unity vectors.
    // Axial motion is linear; transverse motion is cubic Hermite from saved nodal translations/rotations.
    public static Vector3 Displacement(float s,float length,Vector3 ex,float[] i,float[] j)
    {
        Vector3 ui=Vector(i),uj=Vector(j),ri=Vector(i,3),rj=Vector(j,3);
        Vector3 ai=ex*Vector3.Dot(ui,ex),aj=ex*Vector3.Dot(uj,ex);
        float s2=s*s,s3=s2*s;
        return Vector3.Lerp(ai,aj,s)+(1-3*s2+2*s3)*(ui-ai)+(3*s2-2*s3)*(uj-aj)+
            length*((s-2*s2+s3)*Vector3.Cross(ri,ex)+(-s2+s3)*Vector3.Cross(rj,ex));
    }
    static bool Valid(float[] values,int min){return values!=null && values.Length>=min && values.All(v=>!float.IsNaN(v) && !float.IsInfinity(v));}
    public void Show(StructuralIdentity bar,CampusLaser.Case result,int mode,Vector3 viewer)
    {
        Clear();VisualScale=0;Label="";Unit="";
        if(mode==7)return;
        if(!bar || !bar.isBar){Description="Selecciona una barra; muros/losas no tienen deformada de viga.";return;}
        float length=Vector3.Distance(bar.start,bar.end);
        if(length<.001f || result==null){Description="Geometría o caso no disponible.";return;}
        Vector3 ex=(bar.end-bar.start)/length;
        Color color=mode==6?new Color(1,.28f,.72f):new Color(.12f,.95f,.8f);
        if(mode==6){
            if(!Valid(result.moveI,6)||!Valid(result.moveJ,6)){Description="Deformada no disponible: faltan movimientos nodales.";return;}
            Stations=Enumerable.Range(0,41).Select(k=>k/40f).ToArray();
            Vector3 openEx=ToUnity(ex); // The XYZ -> XZY mapping is its own inverse.
            var motions=Stations.Select(s=>ToUnity(Displacement(s,length,openEx,result.moveI,result.moveJ))).ToArray();
            Values=motions.Select(u=>u.magnitude*1000).ToArray();Label="||u|| · Hermite nodal";Unit="mm";
            float maximum=motions.Max(u=>u.magnitude);
            VisualScale=maximum>1e-9f?Mathf.Clamp(Mathf.Min(.8f,length*.18f)/maximum,1,5000):1;
            Curve=Stations.Select((s,k)=>Vector3.Lerp(bar.start,bar.end,s)+motions[k]*VisualScale).ToArray();
            Description="Deformada ×"+Number(VisualScale)+" · interpolación nodal, no estaciones OpenSees";
        }else{
            if(result.graphs==null || mode<0 || mode>=result.graphs.Length){Description="Componente no exportada.";return;}
            var graph=result.graphs[mode];
            if(!Valid(graph.values,2)||!Valid(graph.stations,graph.values.Length) || graph.stations.Length!=graph.values.Length){Description="Estaciones no disponibles o inválidas.";return;}
            if(graph.stations.Any(s=>s<0 || s>1)||graph.stations.Where((s,k)=>k>0 && s<=graph.stations[k-1]).Any()){
                Description="Estaciones fuera del contrato x/L; no se dibujan.";return;
            }
            Stations=(float[])graph.stations.Clone();Values=(float[])graph.values.Clone();Label=graph.label;Unit=graph.unit;
            // The diagram is a scalar plot, not a force vector. The plane is selected for readability.
            Vector3 up=Vector3.ProjectOnPlane(Vector3.up,ex).normalized;
            if(up.sqrMagnitude<.1f)up=Vector3.ProjectOnPlane(Vector3.right,ex).normalized;
            float maximum=Values.Max(v=>Mathf.Abs(v));
            // A minimum display range of 1 kN / 1 kNm avoids magnifying solver round-off.
            // Values and signs remain untouched; only the labelled visual scale changes.
            VisualScale=Mathf.Min(.8f,length*.18f)/Mathf.Max(1f,maximum);
            Curve=Stations.Select((s,k)=>Vector3.Lerp(bar.start,bar.end,s)+up*(Values[k]*VisualScale)).ToArray();
            Description=Label+" ["+Unit+"] · escala "+Number(VisualScale)+" m/"+Unit+" · estaciones exportadas";
        }
        root=new GameObject("Diagrama sobre "+bar.key+" · "+result.name+" · "+Label).transform;
        // Keep labels and geometry attached to the world, not to the moving visitor.
        if(!material){material=new Material(Resources.Load<Shader>("CampusDiagram"));}
        Draw("Referencia i-j",new[]{bar.start,bar.end},new Color(.7f,.77f,.82f,.75f),.012f);
        Draw(Label,Curve,color,.025f);
        for(int k=0;k<Curve.Length;k+=Mathf.Max(1,(Curve.Length-1)/10))
            Draw("Ordenada "+k,new[]{Vector3.Lerp(bar.start,bar.end,Stations[k]),Curve[k]},new Color(color.r,color.g,color.b,.45f),.009f);
        int peak=Enumerable.Range(0,Values.Length).OrderByDescending(k=>Mathf.Abs(Values[k])).First();
        string scale=mode==6?"Hermite nodal · ×"+Number(VisualScale):"Escala "+Number(VisualScale)+" m/"+Unit;
        Text(bar.key+" · "+result.name+" · "+Label+"\nPico muestreado="+Number(Values[peak])+" "+Unit+"\n"+scale,
            Curve[peak]+Vector3.up*.16f,viewer,.020f);
        Text("i",bar.start,viewer,.035f);Text("j",bar.end,viewer,.035f);
        marker=Draw("Estación consultada",new Vector3[5],new Color(1,.78f,.18f),.016f);SetStation(0);
    }
    public void SetStation(int index)
    {
        if(!marker || Curve.Length==0)return;
        var p=Curve[index%Curve.Length];
        marker.SetPositions(new[]{p-Vector3.right*.04f,p+Vector3.right*.04f,p,p+Vector3.up*.04f,p-Vector3.up*.04f});
    }
    LineRenderer Draw(string name,Vector3[] points,Color color,float width)
    {
        var go=new GameObject(name);go.transform.SetParent(root,false);
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
        line.positionCount=points.Length;line.SetPositions(points);line.startColor=line.endColor=color;
        line.startWidth=line.endWidth=width;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;
        return line;
    }
    void Text(string content,Vector3 position,Vector3 viewer,float size)
    {
        var go=new GameObject("Etiqueta de resultados");go.transform.SetParent(root,false);go.transform.position=position;
        go.transform.rotation=Quaternion.LookRotation(position-viewer);
        var text=go.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize=48;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.text=content;text.color=Color.white;
        go.GetComponent<Renderer>().sharedMaterial=text.font.material;
    }
    static string Number(float value){float n=Mathf.Abs(value);return value.ToString(n>0 && (n<.01f || n>1e6f)?"0.##E+0":"0.###",CultureInfo.InvariantCulture);}
    void LateUpdate()
    {
        if(!root || !Camera.main)return;
        Vector3 viewer=Camera.main.transform.position;
        foreach(var text in root.GetComponentsInChildren<TextMesh>())
            if((text.transform.position-viewer).sqrMagnitude>.001f)text.transform.rotation=Quaternion.LookRotation(text.transform.position-viewer);
    }
    void OnDestroy(){Clear();if(material)Destroy(material);}
}
