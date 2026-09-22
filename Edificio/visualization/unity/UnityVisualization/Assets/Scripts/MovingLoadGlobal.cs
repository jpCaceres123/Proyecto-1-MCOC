using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

public sealed partial class MovingLoadViewer
{
    private sealed class Transfer { public int id; public double s,weight; public Vector3 q,m; }
    private readonly List<Transfer> transfers=new List<Transfer>();
    private readonly Dictionary<int,double[][]> nodalBases=new Dictionary<int,double[][]>();
    private int[] ActiveIds=new int[0];
    private string slabSearch="",selectionNote="";
    private bool showFloorMap;
    private Vector2 pickStart;
    private bool picking;
    private bool Valid(Panel p,float x,float y)
    {
        if(x<p.xmin || x>p.xmax || y<p.ymin || y>p.ymax)return false;
        foreach(var h in p.voids)if(x>=h.xmin && x<=h.xmax && y>=h.ymin && y<=h.ymax)return false;
        return true;
    }
    private void SafeCenter()
    {
        var p=Current;
        if(Valid(p,(p.xmin+p.xmax)/2,(p.ymin+p.ymax)/2)){xi=eta=.5f;return;}
        // Cells partitioned by exact void boundaries, not a grid that can miss narrow strips.
        var xs=new List<float>{p.xmin,p.xmax};var ys=new List<float>{p.ymin,p.ymax};
        foreach(var h in p.voids){xs.Add(Mathf.Clamp(h.xmin,p.xmin,p.xmax));xs.Add(Mathf.Clamp(h.xmax,p.xmin,p.xmax));ys.Add(Mathf.Clamp(h.ymin,p.ymin,p.ymax));ys.Add(Mathf.Clamp(h.ymax,p.ymin,p.ymax));}
        xs.Sort();ys.Sort();
        for(int i=1;i<xs.Count;i++)for(int j=1;j<ys.Count;j++) {
            float x=(xs[i-1]+xs[i])/2,y=(ys[j-1]+ys[j])/2;
            if(Valid(p,x,y)){xi=Mathf.InverseLerp(p.xmin,p.xmax,x);eta=Mathf.InverseLerp(p.ymin,p.ymax,y);return;}
        }
        throw new Exception("Losa sin superficie transitable: "+p.id);
    }
    private void SelectPanel(int index)
    {
        panelIndex=index;receiver=0;playing=false;SafeCenter();dirty=true;selectionNote="";Focus();
    }
    private bool PlaceAt(float x,float y,bool focus)
    {
        int found=-1;
        if(Valid(Current,x,y))found=panelIndex;
        else for(int k=0;k<data.panels.Length;k++) {
            var p=data.panels[k];if(Mathf.Abs(p.z-Current.z)<.001f && Valid(p,x,y)){found=k;break;}
        }
        if(found<0){selectionNote="Fuera de losa / vacío: posición bloqueada";return false;}
        bool changed=found!=panelIndex;panelIndex=found;var chosen=Current;
        xi=Mathf.InverseLerp(chosen.xmin,chosen.xmax,x);eta=Mathf.InverseLerp(chosen.ymin,chosen.ymax,y);
        if(changed){receiver=0;GetComponent<BuildingVisualizer>().FocusMovingPanel(chosen.id,chosen.z);}
        if(focus)Focus();selectionNote="";dirty=true;return true;
    }
    private void PickInWorld()
    {
        bool inside=viewCamera && viewCamera.pixelRect.Contains(Input.mousePosition) && !Blocks(Input.mousePosition);
        if(Input.GetMouseButtonDown(0)){pickStart=Input.mousePosition;picking=inside;}
        if(picking && Vector2.Distance(pickStart,Input.mousePosition)>5)picking=false;
        if(!Input.GetMouseButtonUp(0))return;
        bool select=picking&&inside;picking=false;if(!select)return;
        var plane=new Plane(Vector3.up,new Vector3(0,Current.z+.46f,0));
        Ray ray=viewCamera.ScreenPointToRay(Input.mousePosition);float distance;
        if(plane.Raycast(ray,out distance)){var point=ray.GetPoint(distance);PlaceAt(point.x,point.z,false);playing=false;}
    }
    private bool CanWalk(float x0,float y0,float x1,float y1)
    {
        var cuts=new List<float>{0,1};float dx=x1-x0,dy=y1-y0;
        void Bound(float x,float y) {
            if(Mathf.Abs(dx)>1e-9f){float t=(x-x0)/dx;if(t>0 && t<1)cuts.Add(t);}
            if(Mathf.Abs(dy)>1e-9f){float t=(y-y0)/dy;if(t>0 && t<1)cuts.Add(t);}
        }
        foreach(var p in data.panels)if(Mathf.Abs(p.z-Current.z)<.001f) {
            Bound(p.xmin,p.ymin);Bound(p.xmax,p.ymax);
            foreach(var h in p.voids){Bound(h.xmin,h.ymin);Bound(h.xmax,h.ymax);}
        }
        cuts.Sort();
        for(int k=1;k<cuts.Count;k++) {
            float t=(cuts[k-1]+cuts[k])/2;bool surface=false;
            foreach(var p in data.panels)if(Mathf.Abs(p.z-Current.z)<.001f && Valid(p,x0+t*dx,y0+t*dy)){surface=true;break;}
            if(!surface)return false;
        }
        return true;
    }
    private double[][] NodeBasis(int n)
    {
        double[][] samples;if(nodalBases.TryGetValue(n,out samples))return samples;
        // Bound decompressed memory while visiting hundreds of panels.
        if(nodalBases.Count>=16)nodalBases.Clear();
        var asset=Resources.Load<TextAsset>("SQ4Nodes/n"+data.nodes[n].id);
        if(!asset)throw new Exception("Faltan bases del nodo "+data.nodes[n].id+"; regenerar SQ4");
        int count=u.Length+f.Length+6;samples=new double[3][];
        using(var stream=new GZipStream(new MemoryStream(asset.bytes),CompressionMode.Decompress))
        using(var reader=new BinaryReader(stream)) {
            for(int k=0;k<3;k++){samples[k]=new double[count];for(int j=0;j<count;j++)samples[k][j]=reader.ReadDouble();}
            if(reader.BaseStream.ReadByte()!=-1)throw new Exception("Dimensiones de base SQ4 incompatibles");
        }
        Resources.UnloadAsset(asset);nodalBases[n]=samples;return samples;
    }
    private double[] Equivalent(Bar b,Transfer t)
    {
        double s=t.s,L=Vector3.Distance(xyz[b.i],xyz[b.j]),P=magnitude*t.weight;
        Vector3 ex=V(b.x),ey=V(b.y),ez=V(b.z);
        double px=-ex.z*P,py=-ey.z*P,pz=-ez.z*P,mx=Vector3.Dot(ex,t.m),my=Vector3.Dot(ey,t.m),mz=Vector3.Dot(ez,t.m);
        double[] h={1-3*s*s+2*s*s*s,L*(s-2*s*s+s*s*s),3*s*s-2*s*s*s,L*(-s*s+s*s*s)};
        double[] dh={(-6*s+6*s*s)/L,1-4*s+3*s*s,(6*s-6*s*s)/L,-2*s+3*s*s};
        double[] v=new double[4],w=new double[4];for(int k=0;k<4;k++){v[k]=py*h[k]+mz*dh[k];w[k]=pz*h[k]-my*dh[k];}
        return new[]{px*(1-s),v[0],w[0],mx*(1-s),-w[1],v[1],px*s,v[2],w[2],mx*s,-w[3],v[3]};
    }
    private void ApplyTransfers()
    {
        transfers.Clear();var p=Current;Vector3 r=new Vector3(Mathf.Lerp(p.xmin,p.xmax,xi),Mathf.Lerp(p.ymin,p.ymax,eta),p.z);
        for(int k=0;k<p.groups.Length;k++) {
            Transfer best=null;double distance=double.PositiveInfinity;
            foreach(int tag in p.groups[k].ids) {
                var b=data.bars[barIndex[tag]];Vector3 a=xyz[b.i],d=xyz[b.j]-a;
                double s=Math.Max(0,Math.Min(1,Vector3.Dot(r-a,d)/d.sqrMagnitude));Vector3 q=a+(float)s*d;
                double next=(r-q).sqrMagnitude;
                if(next<distance-1e-6 || (Math.Abs(next-distance)<1e-6 && (best==null || tag<best.id))) {distance=next;best=new Transfer{id=tag,s=s,q=q};}
            }
            best.weight=p.rule=="opposite"?(k==0?1-eta:eta):1;
            Vector3 applied=r;if(p.rule=="opposite")applied.y=k==0?p.ymin:p.ymax;
            best.m=Vector3.Cross(applied-best.q,new Vector3(0,0,(float)(-magnitude*best.weight)));
            transfers.Add(best);
        }
        var ids=new List<int>();foreach(var t in transfers)if(!ids.Contains(t.id))ids.Add(t.id);ActiveIds=ids.ToArray();
        var loads=new Dictionary<int,double[]>();var correction=new Dictionary<int,double[]>();
        foreach(var t in transfers) {
            var b=data.bars[barIndex[t.id]];double[] local=Equivalent(b,t);
            if(!correction.ContainsKey(t.id))correction[t.id]=new double[12];
            for(int k=0;k<12;k++)correction[t.id][k]+=local[k];
            for(int end=0;end<2;end++) {
                int n=end==0?b.i:b.j;if(!loads.ContainsKey(n))loads[n]=new double[6];
                for(int group=0;group<2;group++)for(int axis=0;axis<3;axis++)
                    loads[n][group*3+axis]+=b.x[axis]*local[end*6+group*3]+b.y[axis]*local[end*6+group*3+1]+b.z[axis]*local[end*6+group*3+2];
            }
        }
        double[] force=new double[3],moment=new double[3];
        foreach(var entry in loads) {
            int n=entry.Key;var v=entry.Value;var samples=NodeBasis(n);
            for(int k=0;k<3;k++) {
                double factor=v[k+2];var sample=samples[k];int offset=0;
                for(int j=0;j<u.Length;j++)u[j]+=factor*sample[offset++];
                for(int j=0;j<f.Length;j++)f[j]+=factor*sample[offset++];
                for(int j=0;j<6;j++)reaction[j]+=factor*sample[offset++];
            }
            for(int k=0;k<3;k++)force[k]+=v[k];
            moment[0]+=xyz[n].y*v[2]-xyz[n].z*v[1]+v[3];moment[1]+=xyz[n].z*v[0]-xyz[n].x*v[2]+v[4];moment[2]+=xyz[n].x*v[1]-xyz[n].y*v[0]+v[5];
        }
        foreach(var entry in correction)for(int k=0;k<12;k++)f[12*barIndex[entry.Key]+k]-=entry.Value[k];
        transferError=Math.Max(Math.Abs(force[0]),Math.Max(Math.Abs(force[1]),Math.Abs(force[2]+magnitude)));
        momentError=Math.Max(Math.Abs(moment[0]+r.y*magnitude),Math.Max(Math.Abs(moment[1]-r.x*magnitude),Math.Abs(moment[2])));
    }
    private double CutGlobal(Bar b,int c,double s)
    {
        int k=12*barIndex[b.id];double x=s*Vector3.Distance(xyz[b.i],xyz[b.j]);
        double value=c==2?-f[k+2]:c==1?-f[k+1]:c==4?-f[k+4]-f[k+2]*x:-f[k+5]+f[k+1]*x;
        foreach(var t in transfers)if(t.id==b.id) {
            double a=t.s*Vector3.Distance(xyz[b.i],xyz[b.j]),arm=Math.Max(0,x-a),step=s>=t.s?1:0,P=magnitude*t.weight;
            double py=-b.y[2]*P,pz=-b.z[2]*P,my=Vector3.Dot(V(b.y),t.m),mz=Vector3.Dot(V(b.z),t.m);
            value+=c==2?-pz*step:c==1?-py*step:c==4?-pz*arm-my*step:py*arm-mz*step;
        }
        return value;
    }
    private Vector3 DisplacementGlobal(Bar b,float s)
    {
        double L=Vector3.Distance(xyz[b.i],xyz[b.j]),x=s*L;int k=12*barIndex[b.id];
        Vector3 ex=V(b.x),ey=V(b.y),ez=V(b.z),ui=Motion(b.i),ri=Motion(b.i,true);
        if(Portion(b.id)==0)return StructuralPostprocessor.Displacement(s,(float)L,ex,ui,Motion(b.j),ri,Motion(b.j,true));
        double axial=-f[k]*x,vy=-f[k+5]*x*x/2+f[k+1]*x*x*x/6,vz=f[k+4]*x*x/2+f[k+2]*x*x*x/6;
        foreach(var t in transfers)if(t.id==b.id) {
            double arm=Math.Max(0,x-t.s*L),P=magnitude*t.weight;
            axial+=ex.z*P*arm;vy+=-ey.z*P*arm*arm*arm/6-Vector3.Dot(ez,t.m)*arm*arm/2;
            vz+=-ez.z*P*arm*arm*arm/6+Vector3.Dot(ey,t.m)*arm*arm/2;
        }
        return ex*(float)(Vector3.Dot(ui,ex)+axial/(b.E*b.A))+ey*(float)(Vector3.Dot(ui,ey)+Vector3.Dot(ri,ez)*x+vy/(b.E*b.Iz))+ez*(float)(Vector3.Dot(ui,ez)-Vector3.Dot(ri,ey)*x+vz/(b.E*b.Iy));
    }
    private void DrawPanelSelector()
    {
        GUILayout.Label(data.panels.Length+" losas · selecciona cualquier nivel",muted);
        GUILayout.BeginHorizontal();
        foreach(float z in new[]{3.96f,7.92f,11.88f,15.84f,19.8f})
            if(GUILayout.Button((z/3.96f).ToString("F0"),button)) {for(int k=0;k<data.panels.Length;k++)if(Mathf.Abs(data.panels[k].z-z)<.01f){SelectPanel(k);break;}}
        GUILayout.EndHorizontal();GUILayout.BeginHorizontal();
        GUI.SetNextControlName("SQ4LosaID");slabSearch=GUILayout.TextField(slabSearch,12,GUILayout.Width(140));
        if(GUILayout.Button("Ir a ID",button)) {
            int id;int found=-1;if(int.TryParse(slabSearch,out id))found=Array.FindIndex(data.panels,p=>p.id==id);
            if(found>=0){SelectPanel(found);GUI.FocusControl(null);}else selectionNote="ID de losa no encontrado";
        }
        GUILayout.EndHorizontal();
        if(GUILayout.Button(showFloorMap?"Cerrar planta del nivel":"Elegir en planta del nivel",button))showFloorMap=!showFloorMap;
        if(showFloorMap)DrawFloorMap(GUILayoutUtility.GetRect(260,150,GUILayout.ExpandWidth(true)));
        if(selectionNote!="")GUILayout.Label(selectionNote,muted);
    }
    private void DrawFloorMap(Rect r)
    {
        float xmin=float.PositiveInfinity,xmax=float.NegativeInfinity,ymin=xmin,ymax=xmax;
        foreach(var p in data.panels)if(Mathf.Abs(p.z-Current.z)<.001f){xmin=Mathf.Min(xmin,p.xmin);xmax=Mathf.Max(xmax,p.xmax);ymin=Mathf.Min(ymin,p.ymin);ymax=Mathf.Max(ymax,p.ymax);}
        Fill(r,new Color(.06f,.09f,.13f));
        float scaleMap=Mathf.Min(r.width/(xmax-xmin),r.height/(ymax-ymin));
        Rect RectOf(float x0,float x1,float y0,float y1)=>new Rect(r.x+(x0-xmin)*scaleMap,r.yMax-(y1-ymin)*scaleMap,(x1-x0)*scaleMap,(y1-y0)*scaleMap);
        foreach(var p in data.panels)if(Mathf.Abs(p.z-Current.z)<.001f) {
            Rect cell=RectOf(p.xmin,p.xmax,p.ymin,p.ymax);Fill(cell,p==Current?teal:new Color(.17f,.32f,.39f));
            Stroke(new Vector2(cell.x,cell.y),new Vector2(cell.xMax,cell.y),new Color(.07f,.12f,.16f),1);
            Stroke(new Vector2(cell.x,cell.y),new Vector2(cell.x,cell.yMax),new Color(.07f,.12f,.16f),1);
            foreach(var h in p.voids)Fill(RectOf(h.xmin,h.xmax,h.ymin,h.ymax),pink);
        }
        var e=Event.current;if(e.type==EventType.MouseDown && e.button==0 && r.Contains(e.mousePosition)) {
            PlaceAt(xmin+(e.mousePosition.x-r.x)/scaleMap,ymin+(r.yMax-e.mousePosition.y)/scaleMap,true);playing=false;e.Use();
        }
    }
}
