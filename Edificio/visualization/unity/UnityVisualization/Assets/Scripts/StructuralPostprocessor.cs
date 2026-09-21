using System;
using System.Collections.Generic;
using UnityEngine;

// All mechanics are evaluated in OpenSees' right-handed XYZ frame. The final
// point/vector alone is mapped to Unity (X,Z,Y), which reverses handedness.
public sealed class StructuralPostprocessor : MonoBehaviour
{
    [Serializable] public class Node { public int id; public float[] xyz; }
    [Serializable] public class Bar { public int id, i, j; public float[] x, y, z; }
    [Serializable] public class Shell { public int id, wall; public int[] nodes; }
    [Serializable] public class Motion { public int id; public float[] u, r; }
    [Serializable] public class Station
    {
        public float s, n, vy, vz, t, my, mz;
        public float Value(int index)
        {
            if (index == 0) return n; if (index == 1) return vy; if (index == 2) return vz;
            if (index == 3) return t; if (index == 4) return my; return mz;
        }
    }
    [Serializable] public class BarResponse { public int id; public float[] s, n, vy, vz, t, my, mz; }
    [Serializable] public class Case { public string name; public Motion[] nodes; public BarResponse[] bars; }
    [Serializable] public class SectionData
    {
        public float A_m2, Iy_m4, Iz_m4, J_m4;
        public float outer_width_m, wall_thickness_m;
    }
    [Serializable] public class MaterialData { public float E_kPa, nu, density_kg_m3; }
    [Serializable] public class Restraints { public bool i, j; }
    [Serializable] public class ElementMetadata
    {
        public int id, i, j;
        public string type, section, material;
        public SectionData sectionData;
        public MaterialData materialData;
        public Restraints restraints;
    }
    [Serializable] public class Data
    {
        public int schema;
        public string memberLoads;
        public Node[] nodes;
        public Bar[] bars;
        public Shell[] shells;
        public ElementMetadata[] elementMetadata;
        public Case[] cases;
    }

    private Data data;
    private readonly Dictionary<int, Vector3> nodes = new Dictionary<int, Vector3>();
    private readonly Dictionary<int, Bar> bars = new Dictionary<int, Bar>();
    private readonly Dictionary<int, ElementMetadata> metadata = new Dictionary<int, ElementMetadata>();
    private readonly Dictionary<string, Dictionary<int, Motion>> motions = new Dictionary<string, Dictionary<int, Motion>>();
    private readonly Dictionary<string, Dictionary<int, Station[]>> stationResults = new Dictionary<string, Dictionary<int, Station[]>>();
    private string error, diagramKey;
    private bool loaded;
    private Transform deformedRoot, diagramRoot;
    private Material lineMaterial;
    private Texture2D plot;
    private int component = 1;
    private readonly string[] components = { "Ocultar", "N", "Vy", "Vz", "My", "Mz" };
    private const int Width = 330, Height = 155;

    public static Vector3 Vector(float[] v) { return new Vector3(v[0], v[1], v[2]); }
    public static Vector3 ToUnity(Vector3 v) { return new Vector3(v.x, v.z, v.y); }

    public static StructuralPostprocessor Get(GameObject owner)
    {
        StructuralPostprocessor instance = owner.GetComponent<StructuralPostprocessor>();
        return instance != null ? instance : owner.AddComponent<StructuralPostprocessor>();
    }

    private bool Load()
    {
        if (loaded) return error == null;
        loaded = true;
        try
        {
            TextAsset asset = Resources.Load<TextAsset>("semana4_resultados");
            if (asset == null) throw new Exception("Falta exportar semana4_resultados.json");
            data = JsonUtility.FromJson<Data>(asset.text);
            if (data == null || data.schema != 2 || data.memberLoads != "distributed_with_station_results")
                throw new Exception("Contrato incompatible: regenere los resultados con diagramas distribuidos.");
            foreach (Node n in data.nodes) nodes.Add(n.id, Vector(n.xyz));
            foreach (Bar b in data.bars) bars.Add(b.id, b);
            if (data.elementMetadata != null)
                foreach (ElementMetadata item in data.elementMetadata) metadata.Add(item.id, item);
            foreach (Case c in data.cases)
            {
                Dictionary<int, Motion> values = new Dictionary<int, Motion>();
                foreach (Motion n in c.nodes) values.Add(n.id, n);
                motions.Add(c.name, values);
                Dictionary<int, Station[]> barValues = new Dictionary<int, Station[]>();
                if (c.bars == null) throw new Exception("Faltan diagramas de barras para " + c.name);
                foreach (BarResponse b in c.bars)
                {
                    int count = b.s == null ? 0 : b.s.Length;
                    if (count < 2 || b.n.Length != count || b.vy.Length != count || b.vz.Length != count
                        || b.t.Length != count || b.my.Length != count || b.mz.Length != count)
                        throw new Exception("Estaciones inválidas en barra " + b.id + ", caso " + c.name);
                    Station[] stations = new Station[count];
                    for (int k = 0; k < count; k++) stations[k] = new Station {
                        s=b.s[k], n=b.n[k], vy=b.vy[k], vz=b.vz[k], t=b.t[k], my=b.my[k], mz=b.mz[k] };
                    barValues.Add(b.id, stations);
                }
                stationResults.Add(c.name, barValues);
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new Exception("Shader de líneas no disponible");
            lineMaterial = new Material(shader);
        }
        catch (Exception ex) { error = ex.Message; Debug.LogError("Postproceso: " + ex); }
        return error == null;
    }

    public bool TryAxes(int id, out Vector3 x, out Vector3 y, out Vector3 z)
    {
        x = y = z = Vector3.zero;
        Bar b;
        if (!Load() || !bars.TryGetValue(id, out b)) return false;
        x = ToUnity(Vector(b.x)); y = ToUnity(Vector(b.y)); z = ToUnity(Vector(b.z));
        return true;
    }

    private Vector3 Response(string name, int id, bool rotation, Semana3Visualizer viewer)
    {
        if (name == "R")
        {
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < 4; k++)
                sum += Response(Semana3Visualizer.BaseCases[k], id, rotation, viewer) * (float)viewer.Combination[k];
            return sum;
        }
        if (name == "EX" || name == "EY")
            return Response(name + "G", id, rotation, viewer) * (float)viewer.MassG
                + Response(name + "Q", id, rotation, viewer) * (float)viewer.MassQ;
        Motion m = motions[name][id];
        return Vector(rotation ? m.r : m.u);
    }

    // Cubic Euler-Bernoulli interpolation. theta cross ex gives the two
    // transverse slopes; axial displacement is interpolated linearly.
    public static Vector3 Displacement(float s, float length, Vector3 ex,
        Vector3 ui, Vector3 uj, Vector3 ri, Vector3 rj)
    {
        float h1 = 1 - 3 * s * s + 2 * s * s * s;
        float h2 = s - 2 * s * s + s * s * s;
        float h3 = 3 * s * s - 2 * s * s * s;
        float h4 = -s * s + s * s * s;
        Vector3 ai = ex * Vector3.Dot(ui, ex), aj = ex * Vector3.Dot(uj, ex);
        return Vector3.Lerp(ai, aj, s) + h1 * (ui - ai) + h3 * (uj - aj)
            + length * (h2 * Vector3.Cross(ri, ex) + h4 * Vector3.Cross(rj, ex));
    }

    // Compression-positive N. All other resultants act on the +local-x cut
    // face: -localForce_i at i, +localForce_j at j. End actions remain unchanged.
    public static double CutValue(double[] actions, int index, double s)
    {
        double sign = index == 0 ? 1 : -1;
        return sign * ((1 - s) * actions[index] - s * actions[index + 6]);
    }

    private Transform Root(string name)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(transform, false); return go.transform;
    }
    private void Clear(Transform root)
    {
        foreach (Transform child in root) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
    }
    private void Line(Transform root, string name, Vector3[] points, Color color, float width)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(root, false);
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial; line.useWorldSpace = true;
        line.positionCount = points.Length; line.SetPositions(points);
        line.startColor = line.endColor = color; line.startWidth = line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    public void Deform(string name, Semana3Visualizer viewer, bool visible, float scale)
    {
        if (!Load()) return;
        if (deformedRoot == null) deformedRoot = Root("Deformada_Barras_y_Shells");
        Clear(deformedRoot);
        if (!visible) return;
        Dictionary<int, Vector3> u = new Dictionary<int, Vector3>(), r = new Dictionary<int, Vector3>();
        foreach (int id in nodes.Keys) { u[id] = Response(name, id, false, viewer); r[id] = Response(name, id, true, viewer); }
        foreach (Bar b in data.bars)
        {
            Vector3 a = nodes[b.i], end = nodes[b.j], ex = Vector(b.x);
            float length = Vector3.Distance(a, end);
            Vector3[] samples = new Vector3[17];
            for (int k = 0; k < samples.Length; k++)
            {
                float s = k / (float)(samples.Length - 1);
                samples[k] = ToUnity(Vector3.Lerp(a, end, s)
                    + scale * Displacement(s, length, ex, u[b.i], u[b.j], r[b.i], r[b.j]));
            }
            Line(deformedRoot, "Barra_" + b.id + "_" + name, samples, new Color(.85f, .1f, .85f), .045f);
        }
        // Display actual analytical shell connectivity, not a displaced wall box.
        HashSet<string> edges = new HashSet<string>();
        foreach (Shell shell in data.shells)
            for (int k = 0; k < 4; k++)
            {
                int i = shell.nodes[k], j = shell.nodes[(k + 1) % 4];
                string key = Math.Min(i, j) + ":" + Math.Max(i, j);
                if (edges.Add(key))
                    Line(deformedRoot, "Muro_" + shell.wall + "_Shell_" + shell.id + "_" + key,
                        new[] { ToUnity(nodes[i] + scale * u[i]), ToUnity(nodes[j] + scale * u[j]) },
                        new Color(.05f, .8f, .85f), .035f);
            }
    }

    public void ClearSelection()
    {
        if (diagramRoot != null) Clear(diagramRoot);
        diagramKey = null;
    }

    public void DrawMember(int id, string caseName, double[] actions)
    {
        if (!Load()) { GUILayout.Label(error); return; }
        Bar b;
        if (!bars.TryGetValue(id, out b)) { GUILayout.Label("Barra sin ejes exportados"); return; }
        GUILayout.Space(6);
        GUILayout.Label("DIAGRAMA DE ESFUERZOS · " + caseName);
        GUILayout.Label("Nodos i: " + b.i + " → j: " + b.j);
        ElementMetadata item;
        if (metadata.TryGetValue(id, out item))
        {
            GUILayout.Label("Tipo: " + item.type);
            GUILayout.Label("Sección: " + item.section + " · A = " + item.sectionData.A_m2.ToString("G5") + " m²");
            GUILayout.Label("Material: " + item.material + " · E = " + item.materialData.E_kPa.ToString("G5") + " kN/m²");
            GUILayout.Label("Restricciones: i = " + (item.restraints.i ? "fijo" : "libre") +
                " · j = " + (item.restraints.j ? "fijo" : "libre"));
        }
        GUILayout.Label("Ejes OpenSees global XYZ: x=" + Vector(b.x).ToString("F2")
            + "  y=" + Vector(b.y).ToString("F2") + "  z=" + Vector(b.z).ToString("F2"));
        component = GUILayout.Toolbar(component, components);
        if (component == 0) { ClearSelection(); return; }
        int index = component <= 3 ? component - 1 : component;
        Station[] stations = ResolveStations(caseName, id);
        if (stations == null || stations.Length < 2) { GUILayout.Label("No hay estaciones para esta barra."); return; }
        double[] values = new double[stations.Length];
        double minimum = double.PositiveInfinity, maximum = double.NegativeInfinity, absoluteMaximum = 0;
        int maximumIndex = 0;
        for (int k = 0; k < stations.Length; k++)
        {
            values[k] = stations[k].Value(index);
            minimum = Math.Min(minimum, values[k]); maximum = Math.Max(maximum, values[k]);
            if (Math.Abs(values[k]) > absoluteMaximum) { absoluteMaximum = Math.Abs(values[k]); maximumIndex = k; }
        }
        double first = values[0], last = values[values.Length-1];
        string key = id + ":" + component + ":" + caseName;
        for (int k = 0; k < values.Length; k++) key += ":" + values[k].ToString("R");
        if (key != diagramKey)
        {
            diagramKey = key;
            BuildDiagram(b, stations, values, index);
        }
        string unit = index < 3 ? "kN" : "kN·m";
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(340));
        Rect rect = GUILayoutUtility.GetRect(Width, Height, GUILayout.Width(Width), GUILayout.Height(Height));
        GUI.DrawTexture(rect, plot);
        GUIStyle labels = new GUIStyle(GUI.skin.label); labels.normal.textColor = new Color(.82f,.87f,.91f); labels.fontSize = 10;
        double bound = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        GUI.Label(new Rect(rect.x + 3, rect.y + 2, 150, 20), "+" + bound.ToString("G4") + " " + unit, labels);
        GUI.Label(new Rect(rect.x + 3, rect.y + 67, 35, 20), "0", labels);
        GUI.Label(new Rect(rect.x + 3, rect.y + 117, 150, 20), "−" + bound.ToString("G4") + " " + unit, labels);
        GUI.Label(new Rect(rect.x + 55, rect.y + 136, 60, 18), "i: 0 m", labels);
        GUI.Label(new Rect(rect.x + 215, rect.y + 136, 115, 18), "j: " + Vector3.Distance(nodes[b.i], nodes[b.j]).ToString("F3") + " m", labels);
        GUILayout.EndVertical();
        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label(components[component] + " [" + unit + "]");
        GUILayout.Label("Extremo i: " + first.ToString("G6") + " " + unit);
        GUILayout.Label("Extremo j: " + last.ToString("G6") + " " + unit);
        GUILayout.Label("Máx. |" + components[component] + "|: " + absoluteMaximum.ToString("G6") + " " + unit
            + " en x = " + (stations[maximumIndex].s * Vector3.Distance(nodes[b.i], nodes[b.j])).ToString("G5") + " m");
        GUILayout.Label("Mín./máx.: " + minimum.ToString("G6") + " / " + maximum.ToString("G6") + " " + unit);
        GUILayout.Label("N: compresión +. V/M: acciones sobre cara +x. Los signos de corte difieren de las acciones nodales de extremo.");
        GUILayout.Label("Gráfico 3D normalizado a 1,5 m. Azul: positivo; rojo: negativo.");
        GUILayout.Label("Valores en 41 estaciones. Las cargas interiores producen V variable y M curvo; los casos sin carga transversal conservan M lineal.");
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private Station[] ResolveStations(string caseName, int id)
    {
        Semana3Visualizer viewer = GetComponent<Semana3Visualizer>();
        if (caseName != "R" && caseName != "EX" && caseName != "EY")
            return stationResults.ContainsKey(caseName) && stationResults[caseName].ContainsKey(id) ? stationResults[caseName][id] : null;
        if (viewer == null) return null;
        if (caseName == "EX" || caseName == "EY")
            return Combine(id, new[] { caseName + "G", caseName + "Q" }, new[] { viewer.MassG, viewer.MassQ });
        return Combine(id, Semana3Visualizer.BaseCases, viewer.Combination);
    }

    private Station[] Combine(int id, string[] names, double[] factors)
    {
        Station[] reference = stationResults[names[0]][id], result = new Station[reference.Length];
        for (int i = 0; i < reference.Length; i++)
        {
            result[i] = new Station { s = reference[i].s };
            for (int k = 0; k < names.Length; k++)
            {
                Station[] source = stationResults[names[k]][id];
                if (source.Length != reference.Length) throw new Exception("Mallas de estaciones incompatibles");
                float factor = (float)factors[k];
                result[i].n += factor*source[i].n; result[i].vy += factor*source[i].vy;
                result[i].vz += factor*source[i].vz; result[i].t += factor*source[i].t;
                result[i].my += factor*source[i].my; result[i].mz += factor*source[i].mz;
            }
        }
        return result;
    }

    private void BuildDiagram(Bar b, Station[] stations, double[] values, int index)
    {
        if (diagramRoot == null) diagramRoot = Root("Diagrama_NVM_Seleccionado");
        Clear(diagramRoot);
        if (plot != null) Destroy(plot);
        plot = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[Width * Height];
        for (int k = 0; k < pixels.Length; k++) pixels[k] = new Color(.055f,.09f,.13f,1f);
        plot.SetPixels(pixels);
        for (int x = 55; x <= 315; x++) plot.SetPixel(x, 78, new Color(.65f,.72f,.78f,1f));
        double bound = 1e-20;
        foreach (double value in values) bound = Math.Max(bound, Math.Abs(value));
        Vector3 a = ToUnity(nodes[b.i]), end = ToUnity(nodes[b.j]);
        Vector3 offset = ToUnity(Vector(index == 2 || index == 4 ? b.z : b.y));
        Vector3 previous = Vector3.zero;
        for (int k = 0; k < stations.Length; k++)
        {
            float s = stations[k].s;
            double value = values[k];
            Vector3 basePoint = Vector3.Lerp(a, end, s);
            Vector3 point = basePoint + offset * (float)(1.5 * value / bound);
            Color color = value >= 0 ? new Color(.1f, .35f, .9f) : new Color(.9f, .15f, .1f);
            if (k > 0) Line(diagramRoot, "Barra_" + b.id + "_" + k, new[] { previous, point }, color, .045f);
            if (k % Math.Max(1, (stations.Length-1)/10) == 0) Line(diagramRoot, "Ordenada_" + k, new[] { basePoint, point }, color, .02f);
            previous = point;
        }
        for (int x = 55; x <= 315; x++)
        {
            double s = (x - 55) / 260.0;
            double station = s*(values.Length-1); int lo = Math.Min(values.Length-2, (int)Math.Floor(station));
            double value = values[lo] + (values[lo+1]-values[lo])*(station-lo);
            int y = 78 + Mathf.RoundToInt((float)(value / bound * 55));
            for (int dy = -1; dy <= 1; dy++) plot.SetPixel(x, y + dy, value >= 0 ? Color.blue : Color.red);
        }
        plot.Apply();
    }

    private void OnDestroy()
    {
        if (plot != null) Destroy(plot);
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}
