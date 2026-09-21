using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Forces are OpenSees localForce end actions, without changing their signs.
public class ElementInspector : MonoBehaviour
{
    private class Item
    {
        public int id;
        public int graphId, segmentIndex;
        public string kind, details;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public readonly List<Color> colors = new List<Color>();
    }
    private readonly List<Item> items = new List<Item>();
    private readonly Dictionary<Collider, Item> targets = new Dictionary<Collider, Item>();
    private readonly Dictionary<string, double[]> forces = new Dictionary<string, double[]>();
    private readonly Dictionary<string, double[]> axials = new Dictionary<string, double[]>();
    private readonly Dictionary<int, string> columnsAbove = new Dictionary<int, string>();
    private string axialError;
    private Item selected;
    private int filter;
    private string search = "";
    private string dataError;
    private Vector2 listScroll, mouseDown;
    private bool tracking;
    private readonly string[] filters = { "Todos", "Viga", "Columna", "Muro", "Losa" };
    private SectionGraphs graphs;
    private WallSectionGraphs wallGraphs;
    private readonly Dictionary<int, double[]> slabWeights = new Dictionary<int, double[]>();
    private string slabError;
    private readonly Dictionary<int,List<string[]>> slabReceivers = new Dictionary<int,List<string[]>>();
    private string receiverError;
    private int resultTab;
    private bool showElementList;

    public void Register(GameObject go, int id, string kind, string details)
    {
        Register(go, id, kind, details, id, -1);
    }

    public void Register(GameObject go, int id, string kind, string details, int graphId, int segmentIndex)
    {
        Item item = items.Find(value => value.id == id && value.kind == kind &&
            (kind != "Muro" || value.segmentIndex == segmentIndex));
        if (item == null) { item = new Item { id = id, kind = kind, details = details, graphId = graphId, segmentIndex = segmentIndex }; items.Add(item); }
        Renderer renderer = go.GetComponent<Renderer>();
        item.renderers.Add(renderer); item.colors.Add(renderer.material.color);
        targets[go.GetComponent<Collider>()] = item;
    }

    private void Start()
    {
        graphs = new SectionGraphs();
        wallGraphs = new WallSectionGraphs();
        try
        {
            TextAsset csv = Resources.Load<TextAsset>("semana3_axiales_columnas");
            if (csv == null) throw new Exception("Falta exportar semana3_axiales_columnas.csv");
            foreach (string line in csv.text.Split('\n'))
            {
                string[] p = line.Trim().Split(',');
                if (p.Length < 2 || p[0].TrimStart('\uFEFF') == "caso") continue;
                if (p.Length != 16) throw new Exception("Fila de trazabilidad axial incompleta");
                int id = int.Parse(p[1]);
                // P del tramo, suma de P superiores y aporte vertical neto del nudo.
                axials.Add(p[0] + ":" + p[1], new double[] {
                    double.Parse(p[12], CultureInfo.InvariantCulture),
                    double.Parse(p[13], CultureInfo.InvariantCulture),
                    double.Parse(p[14], CultureInfo.InvariantCulture) });
                if (!columnsAbove.ContainsKey(id)) columnsAbove[id] = p[10];
            }
        }
        catch (Exception error) { axialError = error.Message; Debug.LogError(axialError); }
        try
        {
            TextAsset csv=Resources.Load<TextAsset>("semana3_reparto_losas");
            if(csv==null) throw new Exception("Falta exportar semana3_reparto_losas.csv");
            foreach(string line in csv.text.Split('\n'))
            {
                string[] p=line.Trim().Split(',');
                if(p.Length<2 || p[0].TrimStart('\uFEFF')=="losa_id") continue;
                if(p.Length!=7) throw new Exception("Reparto de losa incompleto");
                int id=int.Parse(p[0]);
                if(!slabReceivers.ContainsKey(id)) slabReceivers[id]=new List<string[]>();
                slabReceivers[id].Add(p);
            }
        }
        catch(Exception error) { receiverError=error.Message; Debug.LogError(receiverError); }
        try
        {
            TextAsset csv = Resources.Load<TextAsset>("semana3_pesos_losas");
            if (csv == null) throw new Exception("Falta exportar semana3_pesos_losas.csv");
            foreach (string line in csv.text.Split('\n'))
            {
                string[] fields = line.Trim().Split(',');
                if (fields.Length < 2 || fields[0].TrimStart('\uFEFF') == "losa_id") continue;
                if (fields.Length != 9) throw new Exception("Fila de pesos de losa incompleta");
                double[] values = new double[8];
                for (int i = 0; i < values.Length; i++) values[i] = double.Parse(fields[i+1], CultureInfo.InvariantCulture);
                slabWeights.Add(int.Parse(fields[0]), values);
            }
        }
        catch (Exception error) { slabError = error.Message; Debug.LogError(slabError); }
        try
        {
            TextAsset csv = Resources.Load<TextAsset>("semana3_esfuerzos_locales");
            if (csv == null) throw new Exception("Falta exportar semana3_esfuerzos_locales.csv");
            foreach (string line in csv.text.Split('\n'))
            {
                string[] fields = line.Trim().Split(',');
                if (fields.Length < 2 || fields[0].TrimStart('\uFEFF') == "caso") continue;
                if (fields.Length != 14) throw new Exception("Fila de esfuerzos incompleta");
                double[] values = new double[12];
                for (int i = 0; i < 12; i++) values[i] = double.Parse(fields[i + 2], CultureInfo.InvariantCulture);
                forces.Add(fields[0] + ":" + fields[1], values);
            }
            items.Sort((a, b) => a.kind == b.kind ? a.id.CompareTo(b.id) : string.CompareOrdinal(a.kind, b.kind));
        }
        catch (Exception error) { dataError = error.Message; Debug.LogError(dataError); }
    }

    private bool InViewport()
    {
        if (MovingLoadViewer.Active) return false;
        float y = Screen.height - Input.mousePosition.y;
        Semana3Visualizer viewer = GetComponent<Semana3Visualizer>();
        float modelBottom = viewer != null && viewer.ResultsOpen ? Screen.height - 245f : Screen.height - 74f;
        return Input.mousePosition.x > 264 && Input.mousePosition.x < Screen.width - 350 &&
            y > 74 && y < modelBottom;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Select(null);
        if (Input.GetMouseButtonDown(0)) { mouseDown = Input.mousePosition; tracking = InViewport(); }
        if (tracking && Vector2.Distance(mouseDown, Input.mousePosition) > 5) tracking = false;
        if (!Input.GetMouseButtonUp(0)) return;
        bool pick = tracking && InViewport(); tracking = false;
        if (!pick) return;
        Camera camera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
        if (camera == null) return;
        RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(Input.mousePosition));
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Item item;
            if (targets.TryGetValue(hit.collider, out item) && (filter == 0 || item.kind == filters[filter]))
            { Select(item); return; }
        }
        Select(null);
    }

    private void Select(Item item)
    {
        StructuralPostprocessor.Get(gameObject).ClearSelection();
        if (selected != null)
            for (int i = 0; i < selected.renderers.Count; i++) selected.renderers[i].material.color = selected.colors[i];
        selected = item;
        if (selected != null)
            foreach (Renderer renderer in selected.renderers) renderer.material.color = new Color(1f, .8f, .05f, .85f);
        GetComponent<BuildingVisualizer>().SelectSlab(item != null && item.kind == "Losa" ? item.id : -1);
    }

    public void SelectSlabById(int id) { Select(items.Find(item => item.kind == "Losa" && item.id == id)); }

    public void SelectElementById(string kind, int id)
    {
        Select(items.Find(item => item.kind == kind && item.id == id));
    }

    private void DrawSlabWeight(int id)
    {
        double[] w;
        if (!slabWeights.TryGetValue(id, out w)) { GUILayout.Label(slabError ?? "No hay peso exportado para esta losa."); return; }
        GUILayout.Label("PESO DE LA LOSA · sin mayorar");
        GUILayout.Label("Área neta: " + w[0].ToString("F3") + " m² · espesor: " + w[1].ToString("F3") + " m");
        GUILayout.Label("Densidad: " + w[2].ToString("F0") + " kg/m³");
        GUILayout.Label("Masa propia: " + w[3].ToString("F1") + " kg (" + (w[3]/1000).ToString("F3") + " t)");
        GUILayout.Label("Peso propio: " + w[4].ToString("F2") + " kN");
        GUILayout.Label("Permanente adicional: " + w[6].ToString("F2") + " kN");
        GUILayout.Label("G de la losa: " + w[5].ToString("F2") + " kN");
        GUILayout.Label("Sobrecarga Q: " + w[7].ToString("F2") + " kN");
        GUILayout.Label("Peso propio = área neta × espesor × densidad × 9,80665 / 1000. Vacíos descontados.");
        GUILayout.Label("G incluye peso propio y cargas permanentes adicionales de la losa. Excluye el peso de vigas, columnas y muros. Q se informa aparte; estos valores no cambian al seleccionar un caso.");
        GUILayout.Space(6); GUILayout.Label("APORTE A CADA RECEPTOR · kN sin mayorar");
        List<string[]> receivers;
        if(!slabReceivers.TryGetValue(id,out receivers)) { GUILayout.Label(receiverError ?? "Sin receptores exportados."); return; }
        double totalG=0,totalQ=0;
        foreach(string[] p in receivers)
        {
            double area=double.Parse(p[3],CultureInfo.InvariantCulture), own=double.Parse(p[4],CultureInfo.InvariantCulture);
            double g=double.Parse(p[5],CultureInfo.InvariantCulture), q=double.Parse(p[6],CultureInfo.InvariantCulture);
            totalG+=g; totalQ+=q;
            GUILayout.Label((p[1]=="beam"?"Viga ":"Muro ")+p[2]+" · área tributaria "+area.ToString("F3")+" m²");
            GUILayout.Label("PP: "+own.ToString("F2")+" · G: "+g.ToString("F2")+" · Q: "+q.ToString("F2")+" kN");
        }
        GUILayout.Label("Σ receptores: G = "+totalG.ToString("F2")+"; Q = "+totalQ.ToString("F2")+" kN");
        GUILayout.Label("PP está incluido en G. Son resultantes aportadas por esta losa, no cargas lineales ni esfuerzos de la viga.");
    }

    public void Draw(string loadCase)
    {
        GUILayout.Space(8);
        GUILayout.Label("INSPECTOR DE ELEMENTOS");
        if (selected == null) GUILayout.Label("Seleccione un elemento del modelo.");
        if (GUILayout.Button(showElementList ? "Ocultar resultados de busqueda" : "Buscar / listar elementos")) showElementList = !showElementList;
        if (showElementList)
        {
            filter = GUILayout.Toolbar(filter, filters);
            GUILayout.Label("Buscar ID:"); search = GUILayout.TextField(search);
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(90));
            foreach (Item item in items)
            {
                if ((filter != 0 && item.kind != filters[filter]) || !item.id.ToString().Contains(search)) continue;
                bool visible = item.renderers.Exists(r => r.gameObject.activeInHierarchy);
                string label = item.kind + " " + item.id;
                if (item.kind == "Muro") label += " · " + item.details.Split('\n')[0];
                if (!visible) label += " (oculto)";
                if (GUILayout.Button(label)) Select(item);
            }
            GUILayout.EndScrollView();
        }
        GUILayout.Label("Clic: seleccionar · arrastrar: orbitar · Esc: limpiar");
        if (selected == null) return;
        GUILayout.Label(selected.kind + " ID " + selected.id + " · Caso " + loadCase);
        GUILayout.Label(selected.details);
        if (selected.kind == "Losa")
        {
            DrawSlabWeight(selected.id);
            GUILayout.Label("Losa tributaria: el modelo no calcula esfuerzos internos de placa.");
        }
        else if (selected.kind != "Muro")
        {
            double[] values;
            if (!TryForces(loadCase, selected.id, out values))
                GUILayout.Label(dataError ?? "No hay esfuerzos disponibles para esta barra.");
            else
            {
                GUILayout.Label("Acciones de extremo en ejes locales OpenSees");
                string[] labels = { "N [kN]", "Vy [kN]", "Vz [kN]", "T [kN·m]", "My [kN·m]", "Mz [kN·m]" };
                GUILayout.BeginHorizontal(); GUILayout.Label("Componente", GUILayout.Width(100)); GUILayout.Label("Extremo i"); GUILayout.Label("Extremo j"); GUILayout.EndHorizontal();
                for (int k = 0; k < 6; k++)
                {
                    GUILayout.BeginHorizontal(); GUILayout.Label(labels[k], GUILayout.Width(100));
                    GUILayout.Label(values[k].ToString("G5"), GUILayout.Width(105));
                    GUILayout.Label(values[k + 6].ToString("G5")); GUILayout.EndHorizontal();
                }
                GUILayout.Label("Signos originales de localForce; valores en extremos, no máximos interiores.");
                if (selected.kind == "Columna") DrawAxialTrace(loadCase, selected.id);
            }
        }
        if (GUILayout.Button("Limpiar selección")) Select(null);
    }

    public void DrawSearchBar()
    {
        GUILayout.Label("Buscar ID", GUILayout.Width(75));
        search = GUILayout.TextField(search, GUILayout.Width(220));
        if (!string.IsNullOrEmpty(search))
        {
            foreach (Item item in items)
            {
                if ((filter != 0 && item.kind != filters[filter]) ||
                    !item.id.ToString().Contains(search)) continue;
                if (GUILayout.Button(item.kind + " " + item.id, GUILayout.Width(120))) Select(item);
            }
        }
    }

    public void DrawResults(string loadCase)
    {
        if (selected == null)
        {
            GUILayout.Label("Seleccione una viga, columna o muro para ver sus resultados.");
            return;
        }
        if (selected.kind == "Losa")
        {
            GUILayout.Label("La losa no tiene diagramas de esfuerzos internos; consulte sus cargas tributarias en el inspector.");
            return;
        }
        string[] tabs = selected.kind == "Viga"
            ? new[] { "Diagramas de esfuerzos" }
            : selected.kind == "Muro"
                ? new[] { "Diagrama de interaccion", "Seccion de fibras", "Momento-curvatura", "Puntos A-G" }
                : new[] { "Diagrama de interaccion", "Seccion de fibras", "Tension-deformacion", "Diagramas de esfuerzos", "Momento-curvatura", "Puntos A-G" };
        resultTab = (int)Mathf.Clamp(resultTab, 0, tabs.Length - 1);
        resultTab = GUILayout.Toolbar(resultTab, tabs);
        double[] values;
        if (selected.kind == "Muro")
        {
            if (resultTab == 0) wallGraphs.Draw(selected.graphId, loadCase, selected.segmentIndex, GetComponent<Semana3Visualizer>());
            else GetComponent<Semana3Visualizer>().DrawGlobalResult(resultTab);
            return;
        }
        if (!TryForces(loadCase, selected.id, out values))
        {
            GUILayout.Label(dataError ?? "No hay esfuerzos disponibles para esta barra.");
            return;
        }
        if (selected.kind == "Viga")
            StructuralPostprocessor.Get(gameObject).DrawMember(selected.id, loadCase, values);
        if (selected.kind == "Columna")
        {
            if (resultTab == 0) graphs.Draw(selected.id, loadCase, values, 0);
            else if (resultTab == 1) GetComponent<Semana3Visualizer>().DrawGlobalResult(1);
            else if (resultTab == 2) graphs.Draw(selected.id, loadCase, values, 1);
            else if (resultTab == 3) StructuralPostprocessor.Get(gameObject).DrawMember(selected.id, loadCase, values);
            else if (resultTab == 4) GetComponent<Semana3Visualizer>().DrawGlobalResult(2);
            else GetComponent<Semana3Visualizer>().DrawGlobalResult(3);
        }
    }

    private void OnDestroy() { if (graphs != null) graphs.Dispose(); if (wallGraphs != null) wallGraphs.Dispose(); }

    private void DrawAxialTrace(string loadCase, int id)
    {
        double[] a;
        if (!TryAxial(loadCase,id,out a)) { GUILayout.Label(axialError ?? "Sin trazabilidad axial para esta columna."); return; }
        string above;
        columnsAbove.TryGetValue(id,out above);
        GUILayout.Space(5);
        GUILayout.Label("TRAZABILIDAD AXIAL · compresión positiva");
        GUILayout.Label("P del tramo: " + a[0].ToString("F2") + " kN");
        GUILayout.Label("Σ P de columnas alineadas superiores: " + a[1].ToString("F2") + " kN" +
            (string.IsNullOrEmpty(above) ? " (último tramo)" : " · elementos " + above));
        GUILayout.Label("Aporte vertical neto en el nudo: " + a[2].ToString("+0.00;-0.00;0.00") + " kN");
        GUILayout.Label("Se cumple P tramo = ΣP superior + aporte neto. El aporte reúne la transferencia del piso (vigas, muros, cargas nodales y restricciones). Un valor negativo indica redistribución hacia otros apoyos.");
    }

    private bool TryAxial(string loadCase,int id,out double[] values)
    {
        if(loadCase!="R" && loadCase!="EX" && loadCase!="EY") return axials.TryGetValue(loadCase+":"+id,out values);
        values=new double[3];
        Semana3Visualizer viewer=GetComponent<Semana3Visualizer>();
        if(viewer==null) return false;
        if(loadCase=="EX" || loadCase=="EY")
        {
            double[] g,q;
            if(!axials.TryGetValue(loadCase+"G:"+id,out g) || !axials.TryGetValue(loadCase+"Q:"+id,out q)) return false;
            for(int i=0;i<3;i++) values[i]=viewer.MassG*g[i]+viewer.MassQ*q[i];
            return true;
        }
        for(int k=0;k<4;k++)
        {
            double[] source;
            if(!TryAxial(Semana3Visualizer.BaseCases[k],id,out source)) return false;
            for(int i=0;i<3;i++) values[i]+=viewer.Combination[k]*source[i];
        }
        return true;
    }

    private bool TryForces(string loadCase,int id,out double[] values)
    {
        if(loadCase!="R" && loadCase!="EX" && loadCase!="EY") return forces.TryGetValue(loadCase+":"+id,out values);
        values=new double[12];
        Semana3Visualizer viewer=GetComponent<Semana3Visualizer>();
        if(viewer==null) return false;
        if(loadCase=="EX" || loadCase=="EY")
        {
            double[] g,q;
            if(!forces.TryGetValue(loadCase+"G:"+id,out g) || !forces.TryGetValue(loadCase+"Q:"+id,out q)) return false;
            for(int i=0;i<12;i++) values[i]=viewer.MassG*g[i]+viewer.MassQ*q[i];
            return true;
        }
        for(int k=0;k<4;k++)
        {
            double[] source;
            if(!TryForces(Semana3Visualizer.BaseCases[k],id,out source)) return false;
            for(int i=0;i<12;i++) values[i]+=viewer.Combination[k]*source[i];
        }
        return true;
    }
}
