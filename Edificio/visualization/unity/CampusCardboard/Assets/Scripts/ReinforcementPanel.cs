using System;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class ReinforcementPanel : MonoBehaviour
{
    [Serializable] public class Point { public float P_kN,M_kNm; }
    [Serializable] public class Curve { public string axis;public Point[] points; }
    [Serializable] public class Snapshot { public int schema,elementTag;public string modelHash,capacityRevision,scope,reinforcementSource;public bool globalStiffnessChanged;public Curve[] curves,previous; }
    public Snapshot Result { get; private set; }
    bool open;int axis;string bars="5",diameter="28";
    AnalysisNetworkClient client;
    void Awake(){client=GetComponent<AnalysisNetworkClient>();if(!client)client=gameObject.AddComponent<AnalysisNetworkClient>();}
    public void Calculate(int id,int count,double diameter_m){StartCoroutine(client.Submit(new AnalysisNetworkClient.Input{modelHash=AnalysisNetworkClient.BaseHash,
        operation="capacity",elementTag=id,barsPerFace=count,diameter_m=diameter_m},(folder,manifest)=>{
        var result=JsonUtility.FromJson<Snapshot>(File.ReadAllText(Path.Combine(folder,"capacity.json")));
        if(result.schema!=1||result.elementTag!=id||result.modelHash!=AnalysisNetworkClient.BaseHash||result.globalStiffnessChanged||result.curves==null)throw new InvalidDataException("Capacidad incompatible");Result=result;}));}
    public void Draw(int id,bool applicable,float demandP,float demandMz,float demandMy)
    {
        if(GUILayout.Button(open?"Cerrar refuerzo H5":"Refuerzo editable / capacidad H5"))open=!open;if(!open)return;
        GUILayout.Label("Capacidad nominal de sección · no biaxial / no normativa / EI global sin cambio");
        if(!applicable){GUILayout.Label("No hay sección/refuerzo compatible o hay una variante global activa");return;}
        GUILayout.Label("Hipótesis académica de armadura; confirmar planos");
        GUILayout.Label("PC por Wi-Fi");client.Server=GUILayout.TextField(client.Server);client.Token=GUILayout.PasswordField(client.Token,'*');
        GUILayout.Label("Barras por cara (2–12)");bars=GUILayout.TextField(bars);GUILayout.Label("Diámetro [mm] (8–40)");diameter=GUILayout.TextField(diameter);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.Busy;
        if(GUILayout.Button("Regenerar curvas My / Mz")){int count;double d;if(int.TryParse(bars,out count)&&double.TryParse(diameter.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out d))Calculate(id,count,d/1000);else client.Status="Valores numéricos inválidos";}
        GUI.enabled=enabled;GUILayout.Label(client.Status);if(client.Busy && GUILayout.Button("Cancelar"))client.Cancel();
        if(Result==null||Result.elementTag!=id)return;
        axis=GUILayout.Toolbar(axis,new[]{"P–Mz","P–My"});GUILayout.Label("Gris: anterior · cian: nueva · amarillo: demanda del mismo eje");
        var current=Result.curves[axis];var prior=Result.previous[axis];float pmin=Mathf.Min(current.points.Min(p=>p.P_kN),prior.points.Min(p=>p.P_kN)),pmax=Mathf.Max(current.points.Max(p=>p.P_kN),prior.points.Max(p=>p.P_kN));
        float mmax=Mathf.Max(1,Mathf.Max(current.points.Max(p=>Mathf.Abs(p.M_kNm)),prior.points.Max(p=>Mathf.Abs(p.M_kNm))));
        Rect r=GUILayoutUtility.GetRect(280,190);Color before=GUI.color;GUI.color=new Color(.035f,.06f,.09f);GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=before;
        Func<Point,Vector2> map=p=>new Vector2(r.x+12+Mathf.Abs(p.M_kNm)/mmax*(r.width-24),r.yMax-12-(p.P_kN-pmin)/Mathf.Max(1,pmax-pmin)*(r.height-24));
        Plot(prior,map,Color.gray);Plot(current,map,Color.cyan);
        var mark=map(new Point{P_kN=demandP,M_kNm=axis==0?demandMz:demandMy});GUI.color=Color.yellow;GUI.DrawTexture(new Rect(mark.x-3,mark.y-3,6,6),Texture2D.whiteTexture);GUI.color=before;
        GUILayout.Label("P [kN] "+pmin.ToString("F0")+"…"+pmax.ToString("F0")+" · |M| [kN·m] 0…"+mmax.ToString("F0"));
        GUILayout.Label(Result.reinforcementSource+" · revisión "+Result.capacityRevision.Substring(0,8));
    }
    static void Plot(Curve curve,Func<Point,Vector2> map,Color color){for(int k=1;k<curve.points.Length;k++){
        Vector2 a=map(curve.points[k-1]),b=map(curve.points[k]);var old=GUI.matrix;var c=GUI.color;GUI.color=color;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);GUI.DrawTexture(new Rect(a.x,a.y,(b-a).magnitude,1.5f),Texture2D.whiteTexture);GUI.matrix=old;GUI.color=c;}}
}
