using System;
using System.Linq;
using UnityEngine;

// A labelled graph NEXT TO the column, not a moment vector or biaxial capacity.
public sealed class CampusCapacityDiagram : MonoBehaviour
{
    GameObject root;Material material;string key;
    public void Clear(){if(root){root.SetActive(false);Destroy(root);}root=null;key=null;}
    public void Show(StructuralIdentity bar,ReinforcementPanel.Snapshot result,CampusLaser.Case demand){
        if(!bar || result==null || demand==null || bar.key!="E:"+result.elementTag){Clear();return;}
        var mz=demand.graphs[5];var n=demand.graphs[0];
        string next=bar.key+result.capacityRevision+demand.name;if(next==key)return;Clear();key=next;
        root=new GameObject("P–Mz nominal · "+bar.key);material=material?material:new Material(Resources.Load<Shader>("CampusDiagram"));
        var a=(bar.start+bar.end)*.5f;Vector3 up=(bar.end-bar.start).normalized,right=Vector3.ProjectOnPlane(Vector3.right,up).normalized;if(right.sqrMagnitude<.1f)right=Vector3.forward;
        a+=right*.7f;var curve=result.curves.First(c=>c.axis=="Mz");var prior=result.previous.First(c=>c.axis=="Mz");
        float maxM=Mathf.Max(1,Mathf.Max(curve.points.Max(p=>Mathf.Abs(p.M_kNm)),prior.points.Max(p=>Mathf.Abs(p.M_kNm))));float minP=Mathf.Min(curve.points.Min(p=>p.P_kN),prior.points.Min(p=>p.P_kN)),maxP=Mathf.Max(curve.points.Max(p=>p.P_kN),prior.points.Max(p=>p.P_kN));
        Func<ReinforcementPanel.Point,Vector3> point=p=>a+right*(Mathf.Abs(p.M_kNm)/maxM*.7f)+up*((p.P_kN-minP)/Mathf.Max(1,maxP-minP)*1.1f);
        Draw(prior.points.Select(point).ToArray(),Color.gray);Draw(curve.points.Select(point).ToArray(),Color.cyan);
        var mark=point(new ReinforcementPanel.Point{P_kN=n.values[0],M_kNm=mz.values[0]});Draw(new[]{mark-right*.04f,mark+right*.04f,mark,mark-up*.04f,mark+up*.04f},Color.yellow);
        var go=new GameObject("Etiqueta nominal");go.transform.SetParent(root.transform);go.transform.position=a+up*1.35f;var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=48;t.characterSize=.02f;t.text=bar.key+" · "+demand.name+" · P–Mz nominal\nP_i="+n.values[0].ToString("F1")+" kN · Mz_i="+mz.values[0].ToString("F1")+" kN·m\nGris anterior / cian nueva / amarillo demanda i\nNo biaxial ni capacidad de miembro";go.GetComponent<Renderer>().sharedMaterial=t.font.material;
    }
    void Draw(Vector3[] p,Color c){var go=new GameObject("Curva");go.transform.SetParent(root.transform);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=p.Length;line.SetPositions(p);line.startColor=line.endColor=c;line.startWidth=line.endWidth=.015f;}
    void LateUpdate(){if(root && Camera.main)foreach(var t in root.GetComponentsInChildren<TextMesh>())t.transform.rotation=Quaternion.LookRotation(t.transform.position-Camera.main.transform.position);}
    void OnDestroy(){Clear();if(material)Destroy(material);}
}
