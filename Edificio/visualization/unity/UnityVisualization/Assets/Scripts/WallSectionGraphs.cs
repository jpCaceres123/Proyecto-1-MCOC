using System;
using System.Collections.Generic;
using UnityEngine;

// Envolvente nominal P-M calculada en Python para la dirección principal del muro.
public sealed class WallSectionGraphs : IDisposable
{
#pragma warning disable 0649
    [Serializable] private class Point { public string name; public float p, m; }
    [Serializable] private class Segment
    {
        public float z_min,z_max,length,thickness,dv,sv,dh,sh,boundary_diameter,As;
        public float boundary_left_diameter,boundary_right_diameter;
        public int boundary_count,boundary_left_count,boundary_right_count;
        public Point[] points,curve;
    }
    [Serializable] private class Wall { public int id; public string name,confidence; public Segment[] segments; }
    [Serializable] private class Demand { public string name; public float p,m; }
    [Serializable] private class SegmentDemand { public float z_min,z_max; public Demand[] demands; }
    [Serializable] private class WallDemand { public int id; public Demand[] demands; public SegmentDemand[] segments; }
    [Serializable] private class CapacityMaterial { public float fc_MPa,fy_MPa,Es_MPa; }
    [Serializable] private class Data { public Wall[] walls; public WallDemand[] wallDemands; public CapacityMaterial capacityMaterial; }
#pragma warning restore 0649
    private class Chart { public Texture2D texture; public float xmin,xmax,ymin,ymax; }
    private readonly Dictionary<int,Wall> walls=new Dictionary<int,Wall>();
    private readonly Dictionary<int,WallDemand> demands=new Dictionary<int,WallDemand>();
    private readonly Dictionary<string,Demand> storeyDemands=new Dictionary<string,Demand>();
    private readonly string error;
    private Chart chart;
    private string chartCase;
    private int selectedWall=-1,segmentIndex;
    private GUIStyle small;
    private const int W=330,H=230,Left=58,Right=314,Bottom=40,Top=207;
    private static readonly Color Blue=new Color(.12f,.38f,.75f);
    private static readonly Color Red=new Color(.85f,.18f,.12f);

    public WallSectionGraphs()
    {
        try
        {
            TextAsset asset=Resources.Load<TextAsset>("semana3_pm_muros");
            if(asset==null) throw new Exception("Falta exportar semana3_pm_muros.json");
            Data data=JsonUtility.FromJson<Data>(asset.text);
            if(data==null || data.walls==null || data.walls.Length!=24) throw new Exception("Datos P–M de muros incompletos");
            foreach(Wall wall in data.walls) walls.Add(wall.id,wall);
            TextAsset resultAsset=Resources.Load<TextAsset>("semana4_resultados");
            if(resultAsset==null) throw new Exception("Falta exportar semana4_resultados.json");
            Data resultData=JsonUtility.FromJson<Data>(resultAsset.text);
            if(resultData==null || resultData.wallDemands==null || resultData.wallDemands.Length!=24)
                throw new Exception("Demandas P–M de muros incompletas");
            dataMaterial=resultData.capacityMaterial;
            foreach(WallDemand demand in resultData.wallDemands) demands.Add(demand.id,demand);
            TextAsset storeyAsset=Resources.Load<TextAsset>("semana5_demanda_muros");
            if(storeyAsset!=null)
                foreach(string line in storeyAsset.text.Split('\n'))
                {
                    string[] p=line.Trim().Split(',');
                    if(p.Length<2 || p[0].TrimStart('\uFEFF')=="caso") continue;
                    if(p.Length<7) continue;
                    int source=int.Parse(p[2]); int floor=int.Parse(p[3]);
                    storeyDemands[p[0]+":"+source+":"+(floor-1)]=new Demand { name=p[0], p=float.Parse(p[5],System.Globalization.CultureInfo.InvariantCulture), m=float.Parse(p[6],System.Globalization.CultureInfo.InvariantCulture) };
                }
        }
        catch(Exception ex){error=ex.Message;Debug.LogError(error);}
    }

    public void Draw(int id,string caseName,int requestedSegment,Semana3Visualizer viewer)
    {
        GUILayout.Space(7);
        if(error!=null){GUILayout.Label(error);return;}
        Wall wall;
        if(!walls.TryGetValue(id,out wall)){GUILayout.Label("Muro sin asignación de armadura.");return;}
        if(selectedWall!=id){selectedWall=id;segmentIndex=0;Destroy(chart);chart=null;chartCase=null;}
        if(requestedSegment>=0 && requestedSegment<wall.segments.Length && requestedSegment!=segmentIndex)
        { segmentIndex=requestedSegment; Destroy(chart); chart=null; chartCase=null; }
        if(wall.segments.Length>1)
        {
            string[] labels=new string[wall.segments.Length];
            for(int i=0;i<labels.Length;i++) labels[i]=wall.segments[i].z_min.ToString("G3")+"–"+wall.segments[i].z_max.ToString("G3")+" m";
            int next=GUILayout.Toolbar(segmentIndex,labels);
            if(next!=segmentIndex){segmentIndex=next;Destroy(chart);chart=null;}
        }
        Segment s=wall.segments[Mathf.Clamp(segmentIndex,0,wall.segments.Length-1)];
        WallDemand wallDemand;
        Demand demand=null;
        if(storeyDemands.Count>0)
            demand=DetailedDemand(id,segmentIndex,caseName,viewer);
        if(demand==null && demands.TryGetValue(id,out wallDemand))
        {
            Demand[] values=wallDemand.demands;
            if(wallDemand.segments!=null && wallDemand.segments.Length>0)
                values=wallDemand.segments[Mathf.Clamp(segmentIndex,0,wallDemand.segments.Length-1)].demands;
            demand=Array.Find(values,value=>value.name==caseName);
        }
        if(chart==null || chartCase!=caseName)
        {
            Destroy(chart); chart=CreateChart(s.curve,s.points,demand); chartCase=caseName;
        }
        GUILayout.Label("CURVA P–M DEL " + wall.name.ToUpperInvariant());
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(360));
        DrawChart(chart,"M principal [kN·m]","P [kN], compresión +");
        GUILayout.Label("Azul: envolvente ±M · rojo: demanda (caso " + caseName + ")");
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(190));
        GUILayout.Label("Puntos característicos (kN, kN·m)");
        foreach(Point p in s.points)
            GUILayout.Label(p.name+"    P = "+p.p.ToString("G6")+"    |M| = "+p.m.ToString("G6"));
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(190));
        GUILayout.Label("Estado y verificación");
        if(demand!=null)
        {
            bool inside = Inside(s.curve,demand);
            GUILayout.Label("P (demanda)   "+demand.p.ToString("G6")+" kN");
            GUILayout.Label("M (demanda)   "+demand.m.ToString("G6")+" kN·m");
            GUI.color = inside ? new Color(.25f,1f,.4f) : new Color(1f,.3f,.25f);
            GUILayout.Label(inside ? "[ OK ] DENTRO DE CAPACIDAD" : "[ ! ] FUERA DE CAPACIDAD");
            GUI.color = Color.white;
            GUILayout.Label(inside ? "La demanda se encuentra dentro de la envolvente de capacidad nominal." : "La demanda se encuentra fuera de la envolvente de capacidad nominal.");
        }
        else GUILayout.Label("No hay demanda para el caso " + caseName + ".");
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(220));
        GUILayout.Label("Datos del tramo");
        GUILayout.Label("L = "+s.length.ToString("F3")+" m · e = "+s.thickness.ToString("F2")+" m");
        GUILayout.Label("Z = "+s.z_min.ToString("G4")+"–"+s.z_max.ToString("G4")+" m");
        GUILayout.Label("Vertical D.M. Ø"+s.dv.ToString("G3")+"@"+(s.sv/10).ToString("G3")+" cm · As = "+(s.As*1e6f).ToString("F0")+" mm²");
        GUILayout.Label("Horizontal D.M. Ø"+s.dh.ToString("G3")+"@"+(s.sh/10).ToString("G3")+" cm");
        if(s.boundary_left_count>0 || s.boundary_right_count>0)
            GUILayout.Label("Borde E': "+s.boundary_left_count+"Ø"+s.boundary_left_diameter.ToString("G3")+" · Ec: "+s.boundary_right_count+"Ø"+s.boundary_right_diameter.ToString("G3"));
        if(dataMaterial!=null)
            GUILayout.Label("f'c = "+dataMaterial.fc_MPa.ToString("G5")+" MPa · fy = "+dataMaterial.fy_MPa.ToString("G5")+" MPa");
        GUILayout.Label("Capacidad nominal: compatibilidad de deformaciones y bloque de Whitney.");
        if(wall.confidence!="alta") GUILayout.Label("Revisar relación nombre–ID contra planos.");
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private CapacityMaterial dataMaterial;

    private Demand DetailedDemand(int id,int segment,string caseName,Semana3Visualizer viewer)
    {
        Demand direct;
        if(caseName!="EX" && caseName!="EY" && caseName!="R")
            return storeyDemands.TryGetValue(caseName+":"+id+":"+segment,out direct) ? direct : null;
        Demand result=new Demand { name=caseName };
        if(caseName=="EX" || caseName=="EY")
        {
            Demand g,q;
            if(!storeyDemands.TryGetValue(caseName+"G:"+id+":"+segment,out g) || !storeyDemands.TryGetValue(caseName+"Q:"+id+":"+segment,out q)) return null;
            result.p=(float)(viewer.MassG*g.p+viewer.MassQ*q.p); result.m=(float)(viewer.MassG*g.m+viewer.MassQ*q.m); return result;
        }
        for(int k=0;k<4;k++)
        {
            Demand source;
            if(!storeyDemands.TryGetValue(Semana3Visualizer.BaseCases[k]+":"+id+":"+segment,out source)) return null;
            result.p+=(float)(viewer.Combination[k]*source.p); result.m+=(float)(viewer.Combination[k]*source.m);
        }
        return result;
    }

    private static Chart CreateChart(Point[] curve,Point[] keys,Demand demand)
    {
        List<Vector2> values=new List<Vector2>();
        foreach(Point p in curve){values.Add(new Vector2(p.m,p.p));values.Add(new Vector2(-p.m,p.p));}
        if(demand!=null) values.Add(new Vector2(demand.m,demand.p));
        Chart c=Create(values);
        for(int branch=-1;branch<=1;branch+=2)
            for(int i=1;i<curve.Length;i++) Line(c,new Vector2(branch*curve[i-1].m,curve[i-1].p),new Vector2(branch*curve[i].m,curve[i].p),Blue);
        foreach(Point p in keys){Dot(c.texture,Pixel(c,new Vector2(p.m,p.p)),3,Blue);Dot(c.texture,Pixel(c,new Vector2(-p.m,p.p)),3,Blue);}
        if(demand!=null) Dot(c.texture,Pixel(c,new Vector2(demand.m,demand.p)),5,
            Inside(curve,demand) ? new Color(.1f,.65f,.15f) : new Color(.9f,.1f,.05f));
        c.texture.Apply(); return c;
    }

    private static bool Inside(Point[] curve,Demand demand)
    {
        List<Vector2> polygon=new List<Vector2>();
        foreach(Point point in curve) polygon.Add(new Vector2(point.m,point.p));
        for(int i=curve.Length-1;i>=0;i--) polygon.Add(new Vector2(-curve[i].m,curve[i].p));
        Vector2 test=new Vector2(demand.m,demand.p); bool inside=false;
        for(int i=0,j=polygon.Count-1;i<polygon.Count;j=i++)
        {
            Vector2 a=polygon[i],b=polygon[j];
            if(((a.y>test.y)!=(b.y>test.y)) &&
                test.x < (b.x-a.x)*(test.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
        }
        return inside;
    }
    private static Chart Create(List<Vector2> points)
    {
        Chart c=new Chart();
        foreach(Vector2 p in points){c.xmin=Mathf.Min(c.xmin,p.x);c.xmax=Mathf.Max(c.xmax,p.x);c.ymin=Mathf.Min(c.ymin,p.y);c.ymax=Mathf.Max(c.ymax,p.y);}
        float dx=Mathf.Max(c.xmax-c.xmin,1e-8f),dy=Mathf.Max(c.ymax-c.ymin,1e-8f);
        c.xmin-=dx*.07f;c.xmax+=dx*.07f;c.ymin-=dy*.07f;c.ymax+=dy*.07f;
        c.texture=new Texture2D(W,H,TextureFormat.RGBA32,false);
        Color[] pixels=new Color[W*H];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(.055f,.09f,.13f,1f);c.texture.SetPixels(pixels);
        for(int i=0;i<=4;i++)
        {
            float x=Mathf.Lerp(c.xmin,c.xmax,i/4f),y=Mathf.Lerp(c.ymin,c.ymax,i/4f);
            Line(c,new Vector2(x,c.ymin),new Vector2(x,c.ymax),new Color(.16f,.23f,.30f,1f));
            Line(c,new Vector2(c.xmin,y),new Vector2(c.xmax,y),new Color(.16f,.23f,.30f,1f));
        }
        Line(c,new Vector2(c.xmin,0),new Vector2(c.xmax,0),new Color(.65f,.72f,.78f,1f));Line(c,new Vector2(0,c.ymin),new Vector2(0,c.ymax),new Color(.65f,.72f,.78f,1f));
        return c;
    }
    private static Vector2 Pixel(Chart c,Vector2 p){return new Vector2(Mathf.Lerp(Left,Right,Mathf.InverseLerp(c.xmin,c.xmax,p.x)),Mathf.Lerp(Bottom,Top,Mathf.InverseLerp(c.ymin,c.ymax,p.y)));}
    private static void Line(Chart c,Vector2 a,Vector2 b,Color color){Vector2 x=Pixel(c,a),y=Pixel(c,b);int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(x,y)));for(int i=0;i<=n;i++)Dot(c.texture,Vector2.Lerp(x,y,(float)i/n),0,color);}
    private static void Dot(Texture2D tex,Vector2 p,int radius,Color color){int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)if(dx*dx+dy*dy<=radius*radius&&x+dx>=0&&x+dx<W&&y+dy>=0&&y+dy<H)tex.SetPixel(x+dx,y+dy,color);}
    private void DrawChart(Chart c,string xlabel,string ylabel)
    {
        if(small==null)small=new GUIStyle(GUI.skin.label){fontSize=10,normal={textColor=new Color(.82f,.87f,.91f)}};
        Rect r=GUILayoutUtility.GetRect(W,H,GUILayout.Width(W),GUILayout.Height(H));GUI.DrawTexture(r,c.texture);
        GUI.Label(new Rect(r.x+Left,r.y+1,250,18),ylabel,small);GUI.Label(new Rect(r.x+105,r.y+H-18,220,17),xlabel,small);
        for(int i=0;i<=4;i++)
        {
            float x=Mathf.Lerp(c.xmin,c.xmax,i/4f),y=Mathf.Lerp(c.ymin,c.ymax,i/4f);
            GUI.Label(new Rect(r.x+Mathf.Lerp(Left,Right,i/4f)-20,r.y+H-Bottom+3,62,17),x.ToString("G3"),small);
            GUI.Label(new Rect(r.x+2,r.y+H-Mathf.Lerp(Bottom,Top,i/4f)-8,55,17),y.ToString("G3"),small);
        }
    }
    private static void Destroy(Chart c){if(c!=null&&c.texture!=null)UnityEngine.Object.Destroy(c.texture);}
    public void Dispose(){Destroy(chart);}
}
