using System;
using System.Collections.Generic;
using UnityEngine;

// Nominal section reference and monotonic material laws exported by P1L3.
// The global building analysis has no nonlinear fiber history.
public sealed class SectionGraphs : IDisposable
{
    // These fields are populated by Unity's JSON deserializer.
#pragma warning disable 0649
    [Serializable] private class Member { public int id; public string type; public bool has_capacity; }
    [Serializable] private class PM { public float p, m; }
    [Serializable] private class Stress { public float strain, stress_MPa; }
    [Serializable] private class Data
    {
        public float b_m, h_m, fc_MPa, fy_MPa;
        public Member[] members;
        public PM[] pm;
        public Stress[] concrete, steel;
    }
#pragma warning restore 0649
    private class Chart
    {
        public Texture2D texture;
        public float xmin, xmax, ymin, ymax;
    }
    private readonly Data data;
    private readonly string error;
    private readonly Dictionary<int, Member> members = new Dictionary<int, Member>();
    private Chart concrete, steel, interaction;
    private string cacheKey;
    private int end, axis, material;
    private GUIStyle small;
    private const int W = 330, H = 220, Left = 55, Right = 314, Bottom = 38, Top = 198;
    private static readonly Color Blue = new Color(.12f,.38f,.75f);
    private static readonly Color Red = new Color(.85f,.18f,.12f);

    public SectionGraphs()
    {
        try
        {
            TextAsset asset = Resources.Load<TextAsset>("semana3_graficos_seccion");
            if (asset == null) throw new Exception("Falta exportar semana3_graficos_seccion.json");
            data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.members == null || data.pm == null || data.pm.Length < 2)
                throw new Exception("Datos de gráficos incompletos");
            foreach (Member member in data.members) members.Add(member.id, member);
            concrete = MaterialChart(data.concrete, Blue);
            steel = MaterialChart(data.steel, Red);
        }
        catch (Exception ex) { error = ex.Message; Debug.LogError(error); }
    }

    public void Draw(int id, string loadCase, double[] forces)
    {
        GUILayout.Space(8);
        GUILayout.Label("DIAGRAMAS DE LA SECCIÓN");
        if (error != null) { GUILayout.Label(error); return; }
        Member member;
        if (!members.TryGetValue(id, out member)) { GUILayout.Label("Sección sin datos de capacidad."); return; }
        if (!member.has_capacity)
        {
            GUILayout.Label(member.type.StartsWith("STEEL")
                ? "P–M y σ–ε pendientes: falta el límite de fluencia del acero estructural."
                : "P–M y σ–ε pendientes: falta definir la armadura y los materiales de esta sección.");
            return;
        }
        GUILayout.Label("HA " + data.b_m.ToString("F2") + " × " + data.h_m.ToString("F2") + " m · f’c = " + data.fc_MPa.ToString("G4") + " MPa");
        GUILayout.Label("P–M nominal · sección con armadura supuesta");
        end = GUILayout.Toolbar(end, new[] { "Extremo i", "Extremo j" });
        axis = GUILayout.Toolbar(axis, new[] { "|My|", "|Mz|" });
        // localForce axial action is positive in compression at i, negative at j.
        float p = (float)(end == 0 ? forces[0] : -forces[6]);
        float m = Mathf.Abs((float)forces[end * 6 + 4 + axis]);
        string key = id + ":" + loadCase + ":" + end + ":" + axis + ":" + p.ToString("R") + ":" + m.ToString("R");
        if (key != cacheKey)
        {
            Destroy(interaction); interaction = InteractionChart(p, m); cacheKey = key;
        }
        DrawChart(interaction, "|M| [kN·m]", "P [kN], compresión +");
        GUILayout.Label("Azul: puntos nominales · rojo: demanda " + loadCase);
        GUILayout.Label("VALORES P–M · sección seleccionada");
        for (int i = 0; i < data.pm.Length; i++)
            GUILayout.Label("Nominal " + (i + 1) + ": P = " + data.pm[i].p.ToString("G6")
                + " kN; |M| = " + data.pm[i].m.ToString("G6") + " kN·m");
        GUILayout.Label("Demanda " + loadCase + ": P = " + p.ToString("G6")
            + " kN; |M| = " + m.ToString("G6") + " kN·m");
        GUILayout.Label("La línea solo une puntos calculados. Comparación uniaxial; no verifica flexión biaxial, esbeltez ni capacidad normativa.");
        GUILayout.Space(6);
        GUILayout.Label("Tensión–deformación σ–ε");
        material = GUILayout.Toolbar(material, new[] { "Hormigón", "Acero de armadura" });
        DrawChart(material == 0 ? concrete : steel, "ε [m/m]", "σ [MPa]");
        GUILayout.Label(material == 0
            ? "Concrete01: compresión negativa, sin resistencia a tracción. f’c = " + data.fc_MPa.ToString("G4") + " MPa."
            : "Steel01: elastoplástico perfecto; fy = " + data.fy_MPa.ToString("G4") + " MPa. Se muestra el entorno de fluencia.");
        GUILayout.Label("Envolvente monotónica del material; no es la historia de fibras del elemento seleccionado.");
    }

    private static Chart MaterialChart(Stress[] points, Color color)
    {
        if (points == null || points.Length < 2) throw new Exception("Faltan datos σ–ε");
        List<Vector2> values = new List<Vector2>();
        foreach (Stress point in points) values.Add(new Vector2(point.strain, point.stress_MPa));
        Chart chart = Create(values);
        for (int i = 1; i < values.Count; i++) Line(chart, values[i-1], values[i], color);
        chart.texture.Apply(); return chart;
    }

    private Chart InteractionChart(float p, float m)
    {
        List<Vector2> values = new List<Vector2>();
        foreach (PM point in data.pm) values.Add(new Vector2(point.m, point.p));
        Vector2 demand = new Vector2(m,p);
        List<Vector2> bounds = new List<Vector2>(values); bounds.Add(demand);
        Chart chart = Create(bounds);
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) Line(chart, values[i-1], values[i], Blue);
            Dot(chart.texture, Pixel(chart, values[i]), 3, Blue);
        }
        Dot(chart.texture, Pixel(chart, demand), 5, Red);
        chart.texture.Apply(); return chart;
    }

    private static Chart Create(List<Vector2> points)
    {
        Chart c = new Chart();
        foreach (Vector2 point in points)
        {
            c.xmin = Mathf.Min(c.xmin,point.x); c.xmax = Mathf.Max(c.xmax,point.x);
            c.ymin = Mathf.Min(c.ymin,point.y); c.ymax = Mathf.Max(c.ymax,point.y);
        }
        float dx = Mathf.Max(c.xmax-c.xmin,1e-8f), dy = Mathf.Max(c.ymax-c.ymin,1e-8f);
        if (c.xmin < 0) c.xmin -= dx*.08f; if (c.xmax > 0) c.xmax += dx*.08f;
        if (c.ymin < 0) c.ymin -= dy*.08f; if (c.ymax > 0) c.ymax += dy*.08f;
        c.texture = new Texture2D(W,H,TextureFormat.RGBA32,false);
        Color[] pixels = new Color[W*H]; for (int i=0;i<pixels.Length;i++) pixels[i]=Color.white;
        c.texture.SetPixels(pixels);
        for (int i=0;i<=4;i++)
        {
            float x=Mathf.Lerp(c.xmin,c.xmax,i/4f), y=Mathf.Lerp(c.ymin,c.ymax,i/4f);
            Line(c,new Vector2(x,c.ymin),new Vector2(x,c.ymax),new Color(.88f,.9f,.92f));
            Line(c,new Vector2(c.xmin,y),new Vector2(c.xmax,y),new Color(.88f,.9f,.92f));
        }
        Line(c,new Vector2(c.xmin,0),new Vector2(c.xmax,0),Color.black);
        Line(c,new Vector2(0,c.ymin),new Vector2(0,c.ymax),Color.black);
        return c;
    }

    private static Vector2 Pixel(Chart c, Vector2 p)
    {
        // Texture2D Y increases upwards. Only GUI label coordinates are inverted.
        return new Vector2(Mathf.Lerp(Left,Right,Mathf.InverseLerp(c.xmin,c.xmax,p.x)),
            Mathf.Lerp(Bottom,Top,Mathf.InverseLerp(c.ymin,c.ymax,p.y)));
    }
    private static void Line(Chart c, Vector2 a, Vector2 b, Color color)
    {
        Vector2 start=Pixel(c,a), finish=Pixel(c,b);
        int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,finish)));
        for(int i=0;i<=count;i++) Dot(c.texture,Vector2.Lerp(start,finish,(float)i/count),0,color);
    }
    private static void Dot(Texture2D tex,Vector2 p,int radius,Color color)
    {
        int x=Mathf.RoundToInt(p.x),y=Mathf.RoundToInt(p.y);
        for(int dx=-radius;dx<=radius;dx++) for(int dy=-radius;dy<=radius;dy++)
            if(dx*dx+dy*dy<=radius*radius && x+dx>=0 && x+dx<W && y+dy>=0 && y+dy<H) tex.SetPixel(x+dx,y+dy,color);
    }
    private void DrawChart(Chart chart,string xlabel,string ylabel)
    {
        if(small==null) small=new GUIStyle(GUI.skin.label){fontSize=10,normal={textColor=Color.black}};
        Rect r=GUILayoutUtility.GetRect(W,H,GUILayout.Width(W),GUILayout.Height(H));
        GUI.DrawTexture(r,chart.texture);
        GUI.Label(new Rect(r.x+Left,r.y+1,260,18),ylabel,small);
        GUI.Label(new Rect(r.x+110,r.y+H-17,210,17),xlabel,small);
        for(int i=0;i<=4;i++)
        {
            float x=Mathf.Lerp(chart.xmin,chart.xmax,i/4f),y=Mathf.Lerp(chart.ymin,chart.ymax,i/4f);
            GUI.Label(new Rect(r.x+Mathf.Lerp(Left,Right,i/4f)-18,r.y+H-Bottom+3,58,17),x.ToString("G3"),small);
            GUI.Label(new Rect(r.x+2,r.y+H-Mathf.Lerp(Bottom,Top,i/4f)-8,52,17),y.ToString("G3"),small);
        }
    }
    private static void Destroy(Chart c) { if(c!=null && c.texture!=null) UnityEngine.Object.Destroy(c.texture); }
    public void Dispose() { Destroy(concrete); Destroy(steel); Destroy(interaction); }
}
