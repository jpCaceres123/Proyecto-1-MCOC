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
        public string note;
        public int boundary_count;
        public Point[] points,curve;
    }
    [Serializable] private class Wall { public int id,source_id,floor; public string name,confidence; public Segment[] segments; }
    [Serializable] private class Data { public Wall[] walls; }
#pragma warning restore 0649
    private class Demand
    {
        public double p,m,vPlane,vNormal,mVertical;
    }
    private class Chart { public Texture2D texture; public float xmin,xmax,ymin,ymax; }
    private readonly Dictionary<int,Wall> walls=new Dictionary<int,Wall>();
    private readonly Dictionary<string,Demand> demands=new Dictionary<string,Demand>();
    private readonly string error;
    private string demandError;
    private Chart chart;
    private int selectedWall=-1,segmentIndex;
    private string chartCase="";
    private double chartP=double.NaN,chartM=double.NaN;
    private GUIStyle small;
    private const int W=330,H=230,Left=58,Right=314,Bottom=40,Top=207;
    private static readonly Color Blue=new Color(.12f,.38f,.75f);
    private static readonly Color Red=new Color(.85f,.18f,.12f);
    private static readonly Color Orange=new Color(.95f,.55f,.08f);

    public WallSectionGraphs()
    {
        try
        {
            TextAsset asset=Resources.Load<TextAsset>("semana3_pm_muros");
            if(asset==null) throw new Exception("Falta exportar semana3_pm_muros.json");
            Data data=JsonUtility.FromJson<Data>(asset.text);
            if(data==null || data.walls==null || data.walls.Length==0) throw new Exception("Datos P–M de muros incompletos");
            foreach(Wall wall in data.walls) walls.Add(wall.id,wall);
        }
        catch(Exception ex){error=ex.Message;Debug.LogError(error);}
        try
        {
            TextAsset asset=Resources.Load<TextAsset>("semana5_demanda_muros");
            if(asset==null) throw new Exception("Falta exportar semana5_demanda_muros.csv");
            foreach(string line in asset.text.Split('\n'))
            {
                string[] p=line.Trim().Split(',');
                if(p.Length<2 || p[0].TrimStart('\uFEFF')=="caso") continue;
                if(p.Length!=12) throw new Exception("Fila de demanda P–M de muro incompleta");
                demands.Add(p[0]+":"+p[1],new Demand {
                    p=Parse(p[5]),m=Parse(p[6]),vPlane=Parse(p[7]),
                    vNormal=Parse(p[8]),mVertical=Parse(p[9])});
            }
        }
        catch(Exception ex){demandError=ex.Message;Debug.LogError(demandError);}
    }

    private static double Parse(string value)
    {
        return double.Parse(value,System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Draw(int id,string loadCase,Semana3Visualizer viewer)
    {
        GUILayout.Space(7); GUILayout.Label("CURVA P–M DEL MURO · DIRECCIÓN PRINCIPAL");
        if(error!=null){GUILayout.Label(error);return;}
        Wall wall;
        if(!walls.TryGetValue(id,out wall)){GUILayout.Label("Muro sin asignación de armadura.");return;}
        if(selectedWall!=id){selectedWall=id;segmentIndex=0;Destroy(chart);chart=null;}
        GUILayout.Label("Paño "+id+" · muro origen "+wall.source_id+" · piso "+wall.floor);
        GUILayout.Label(wall.name+" · confianza de asignación: "+wall.confidence);
        if(wall.segments.Length>1)
        {
            string[] labels=new string[wall.segments.Length];
            for(int i=0;i<labels.Length;i++) labels[i]=wall.segments[i].z_min.ToString("G3")+"–"+wall.segments[i].z_max.ToString("G3")+" m";
            int next=GUILayout.Toolbar(segmentIndex,labels);
            if(next!=segmentIndex){segmentIndex=next;Destroy(chart);chart=null;}
        }
        Segment s=wall.segments[Mathf.Clamp(segmentIndex,0,wall.segments.Length-1)];
        GUILayout.Label("L = "+s.length.ToString("F3")+" m · e = "+s.thickness.ToString("F2")+" m · Z = "+s.z_min.ToString("G4")+"–"+s.z_max.ToString("G4")+" m");
        GUILayout.Label("Vertical D.M. Ø"+s.dv.ToString("G3")+"@"+(s.sv/10).ToString("G3")+" cm · As = "+(s.As*1e6f).ToString("F0")+" mm²");
        GUILayout.Label("Horizontal D.M. Ø"+s.dh.ToString("G3")+"@"+(s.sh/10).ToString("G3")+" cm (no participa en P–M principal)");
        if(s.boundary_count>0) GUILayout.Label("Bordes: "+s.boundary_count+"Ø"+s.boundary_diameter.ToString("G3")+" en cada extremo");
        if(!string.IsNullOrEmpty(s.note)) GUILayout.Label(s.note);
        Demand demand;
        bool hasDemand=TryDemand(loadCase,id,viewer,out demand);
        if(hasDemand && (chart==null || chartCase!=loadCase || Math.Abs(chartP-demand.p)>1e-7 || Math.Abs(chartM-demand.m)>1e-7))
        {
            Destroy(chart); chart=CreateChart(s.curve,s.points,demand);
            chartCase=loadCase; chartP=demand.p; chartM=demand.m;
        }
        else if(!hasDemand && (chart==null || chartCase!=loadCase))
        {
            Destroy(chart); chart=CreateChart(s.curve,s.points,null);
            chartCase=loadCase; chartP=double.NaN; chartM=double.NaN;
        }
        DrawChart(chart,"M principal [kN·m]","P [kN], compresión +");
        GUILayout.Label("Azul: capacidad ±M · naranja: puntos A–G · rojo: demanda OpenSees");
        if(hasDemand)
        {
            float capacity;
            bool axialRange=CapacityAt(s.curve,(float)demand.p,out capacity);
            double utilization=axialRange && capacity>1e-9 ? Math.Abs(demand.m)/capacity : double.PositiveInfinity;
            bool inside=axialRange && utilization<=1.0+1e-6;
            Color previous=GUI.contentColor; GUI.contentColor=inside?new Color(.15f,.55f,.2f):Red;
            string verdict=inside?"DENTRO DE LA ENVOLVENTE":"FUERA DE LA ENVOLVENTE MODELADA";
            if(!inside && wall.confidence!="alta") verdict+=" · ARMADURA PROVISIONAL";
            GUILayout.Label("DEMANDA "+loadCase+": "+verdict);
            GUI.contentColor=previous;
            GUILayout.Label("P = "+demand.p.ToString("F2")+" kN · M principal = "+demand.m.ToString("+0.00;-0.00;0.00")+" kN·m");
            if(demand.p<0)
                GUILayout.Label("P < 0 significa tracción del paño por volcamiento; no se cambió artificialmente el signo.");
            if(loadCase=="EX" || loadCase=="EY")
            {
                GUILayout.Label(loadCase+" es el efecto sísmico incremental en el sentido positivo, sin G ni Q.");
                GUILayout.Label("Para −"+loadCase+": P = "+(-demand.p).ToString("F2")+" kN · M = "+(-demand.m).ToString("+0.00;-0.00;0.00")+" kN·m. Use R para combinarlo con gravedad.");
            }
            if(axialRange && capacity>1e-9)
                GUILayout.Label("|M| / Mcap(P) = "+utilization.ToString("F3")+" · Mcap = "+capacity.ToString("F2")+" kN·m");
            else GUILayout.Label("La carga axial está fuera del rango nominal calculado.");
            GUILayout.Label("V en plano = "+demand.vPlane.ToString("+0.00;-0.00;0.00")+" kN · V fuera del plano = "+demand.vNormal.ToString("+0.00;-0.00;0.00")+" kN");
            GUILayout.Label("Mz vertical = "+demand.mVertical.ToString("+0.00;-0.00;0.00")+" kN·m (se informa, no entra en este P–M uniaxial)");
            GUILayout.Label("Demanda reducida en el centro del corte inferior Z = "+s.z_min.ToString("G4")+" m a partir de fuerzas nodales ShellMITC4.");
        }
        else GUILayout.Label(demandError ?? "No hay demanda OpenSees exportada para este paño y caso.");
        foreach(Point p in s.points)
            GUILayout.Label(p.name+": P = "+p.p.ToString("G6")+" kN; |M| = "+p.m.ToString("G6")+" kN·m");
        GUILayout.Label("Compatibilidad de deformaciones y bloque de Whitney. Capacidad nominal sin factores φ, esbeltez, interacción biaxial ni verificación de corte.");
        if(wall.confidence!="alta") GUILayout.Label("Revisar la relación nombre–ID y los refuerzos de borde contra planos antes de interpretar un punto exterior como falla de diseño.");
    }

    private bool TryDemand(string loadCase,int id,Semana3Visualizer viewer,out Demand value)
    {
        if(loadCase!="R" && loadCase!="EX" && loadCase!="EY")
            return demands.TryGetValue(loadCase+":"+id,out value);
        value=new Demand();
        if(viewer==null) return false;
        if(loadCase=="EX" || loadCase=="EY")
        {
            Demand g,q;
            if(!demands.TryGetValue(loadCase+"G:"+id,out g) || !demands.TryGetValue(loadCase+"Q:"+id,out q)) return false;
            Add(value,g,viewer.MassG); Add(value,q,viewer.MassQ); return true;
        }
        for(int k=0;k<4;k++)
        {
            Demand source;
            if(!TryDemand(Semana3Visualizer.BaseCases[k],id,viewer,out source)) return false;
            Add(value,source,viewer.Combination[k]);
        }
        return true;
    }

    private static void Add(Demand target,Demand source,double factor)
    {
        target.p+=factor*source.p; target.m+=factor*source.m;
        target.vPlane+=factor*source.vPlane; target.vNormal+=factor*source.vNormal;
        target.mVertical+=factor*source.mVertical;
    }

    private static bool CapacityAt(Point[] curve,float p,out float capacity)
    {
        capacity=0; if(curve==null || curve.Length<2) return false;
        float pmin=float.PositiveInfinity,pmax=float.NegativeInfinity;
        foreach(Point point in curve){pmin=Mathf.Min(pmin,point.p);pmax=Mathf.Max(pmax,point.p);}
        if(p<pmin-1e-4f || p>pmax+1e-4f) return false;
        for(int i=1;i<curve.Length;i++)
        {
            Point a=curve[i-1],b=curve[i];
            if(p<Mathf.Min(a.p,b.p)-1e-4f || p>Mathf.Max(a.p,b.p)+1e-4f) continue;
            float t=Mathf.Abs(b.p-a.p)<1e-8f?0:Mathf.InverseLerp(a.p,b.p,p);
            capacity=Mathf.Max(capacity,Mathf.Lerp(a.m,b.m,t));
        }
        return true;
    }

    private static Chart CreateChart(Point[] curve,Point[] keys,Demand demand)
    {
        List<Vector2> values=new List<Vector2>();
        foreach(Point p in curve){values.Add(new Vector2(p.m,p.p));values.Add(new Vector2(-p.m,p.p));}
        if(demand!=null) values.Add(new Vector2((float)demand.m,(float)demand.p));
        Chart c=Create(values);
        for(int branch=-1;branch<=1;branch+=2)
            for(int i=1;i<curve.Length;i++) Line(c,new Vector2(branch*curve[i-1].m,curve[i-1].p),new Vector2(branch*curve[i].m,curve[i].p),Blue);
        foreach(Point p in keys){Dot(c.texture,Pixel(c,new Vector2(p.m,p.p)),3,Orange);Dot(c.texture,Pixel(c,new Vector2(-p.m,p.p)),3,Orange);}
        if(demand!=null)
        {
            Vector2 point=Pixel(c,new Vector2((float)demand.m,(float)demand.p));
            Dot(c.texture,point,5,Color.white); Dot(c.texture,point,4,Red);
            LinePixels(c.texture,point+new Vector2(-7,0),point+new Vector2(7,0),Red);
            LinePixels(c.texture,point+new Vector2(0,-7),point+new Vector2(0,7),Red);
        }
        c.texture.Apply(); return c;
    }
    private static Chart Create(List<Vector2> points)
    {
        Chart c=new Chart();
        foreach(Vector2 p in points){c.xmin=Mathf.Min(c.xmin,p.x);c.xmax=Mathf.Max(c.xmax,p.x);c.ymin=Mathf.Min(c.ymin,p.y);c.ymax=Mathf.Max(c.ymax,p.y);}
        float dx=Mathf.Max(c.xmax-c.xmin,1e-8f),dy=Mathf.Max(c.ymax-c.ymin,1e-8f);
        c.xmin-=dx*.07f;c.xmax+=dx*.07f;c.ymin-=dy*.07f;c.ymax+=dy*.07f;
        c.texture=new Texture2D(W,H,TextureFormat.RGBA32,false);
        Color[] pixels=new Color[W*H];for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white;c.texture.SetPixels(pixels);
        for(int i=0;i<=4;i++)
        {
            float x=Mathf.Lerp(c.xmin,c.xmax,i/4f),y=Mathf.Lerp(c.ymin,c.ymax,i/4f);
            Line(c,new Vector2(x,c.ymin),new Vector2(x,c.ymax),new Color(.88f,.9f,.92f));
            Line(c,new Vector2(c.xmin,y),new Vector2(c.xmax,y),new Color(.88f,.9f,.92f));
        }
        Line(c,new Vector2(c.xmin,0),new Vector2(c.xmax,0),Color.black);Line(c,new Vector2(0,c.ymin),new Vector2(0,c.ymax),Color.black);
        return c;
    }
    private static Vector2 Pixel(Chart c,Vector2 p){return new Vector2(Mathf.Lerp(Left,Right,Mathf.InverseLerp(c.xmin,c.xmax,p.x)),Mathf.Lerp(Bottom,Top,Mathf.InverseLerp(c.ymin,c.ymax,p.y)));}
    private static void Line(Chart c,Vector2 a,Vector2 b,Color color){Vector2 x=Pixel(c,a),y=Pixel(c,b);int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(x,y)));for(int i=0;i<=n;i++)Dot(c.texture,Vector2.Lerp(x,y,(float)i/n),0,color);}
    private static void LinePixels(Texture2D texture,Vector2 a,Vector2 b,Color color){int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));for(int i=0;i<=n;i++)Dot(texture,Vector2.Lerp(a,b,(float)i/n),1,color);}
    private static void Dot(Texture2D tex,Vector2 p,int radius,Color color){int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)if(dx*dx+dy*dy<=radius*radius&&x+dx>=0&&x+dx<W&&y+dy>=0&&y+dy<H)tex.SetPixel(x+dx,y+dy,color);}
    private void DrawChart(Chart c,string xlabel,string ylabel)
    {
        if(small==null)small=new GUIStyle(GUI.skin.label){fontSize=10,normal={textColor=Color.black}};
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
