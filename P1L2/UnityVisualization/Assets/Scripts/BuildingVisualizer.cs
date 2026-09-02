using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Minimal structural viewer. The CSV remains the single input and the
// support/diaphragm/tributary overlays are derived from its nodes and slabs.
public class BuildingVisualizer : MonoBehaviour
{
    public TextAsset modelCsv;
    public bool showNodes = true;
    public bool showNodeLabels = true;
    public float memberThickness = 0.12f;

    private readonly Dictionary<int, Vector3> nodes = new Dictionary<int, Vector3>();
    private readonly HashSet<int> restrainedNodes = new HashSet<int>();
    private readonly List<Element> elements = new List<Element>();
    private readonly List<Wall> walls = new List<Wall>();
    private readonly List<Slab> slabs = new List<Slab>();
    private readonly Dictionary<Transform, bool> levelObjects = new Dictionary<Transform, bool>();
    private Transform nodeRoot, beamRoot, columnRoot, wallRoot, diaphragmRoot;
    private Transform supportRoot, localAxisRoot, tributaryRoot;
    private GUIStyle panelStyle, titleStyle, smallStyle;
    private int selectedSlab = -1;
    private int level = -1;
    private bool showBeams = true, showColumns = true, showWalls = true;
    private bool showSupports = true, showDiaphragms = true, showLocalAxes;
    private bool showIds = true, showTributary = true;
    private Vector2 scroll;
    private static bool shaderWarningLogged;

    private struct Element { public int id, i, j; public string type; }
    private struct Wall { public int id; public Vector3 a, b; public float zMin, zMax, thickness; }
    private class Slab { public int id; public Vector3[] corners; public float thickness; public float Area { get { return Mathf.Abs((corners[1].x - corners[0].x) * (corners[2].z - corners[1].z)); } } }

    private void Start()
    {
        Debug.Log("=== INICIO GENERACION MODELO ===");
        Debug.Log("DataPath: " + Application.dataPath);
        Debug.Log("StreamingAssets: " + Application.streamingAssetsPath);
        Debug.Log("PersistentDataPath: " + Application.persistentDataPath);
        if (modelCsv == null) modelCsv = Resources.Load<TextAsset>("model_3d");
        if (modelCsv == null) { Debug.LogError("No se encontro Resources/model_3d.csv"); return; }
        ReadCsv(modelCsv.text);
        Debug.Log("Nodos cargados: " + nodes.Count);
        Debug.Log("Elementos cargados: " + elements.Count);
        Debug.Log("Muros cargados: " + walls.Count);
        Debug.Log("Losas cargadas: " + slabs.Count);
        BuildScene();
    }

    private void ReadCsv(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string[] p = raw.Trim().Split(',');
            if (p.Length < 2 || p[0] == "kind") continue;
            try
            {
                if (p[0] == "N")
                {
                    int id = int.Parse(p[1]);
                    nodes[id] = StructuralToUnity(ParseFloat(p[6]), ParseFloat(p[7]), ParseFloat(p[8]));
                    if (p.Length > 10 && p[10].Trim() == "MANUAL" && Mathf.Abs(nodes[id].y) < 0.001f) restrainedNodes.Add(id);
                }
                else if (p[0] == "E") elements.Add(new Element { id = int.Parse(p[1]), type = p[2], i = int.Parse(p[3]), j = int.Parse(p[4]) });
                else if (p[0] == "W") walls.Add(new Wall { id = int.Parse(p[1]), a = StructuralToUnity(ParseFloat(p[3]), ParseFloat(p[4]), 0), b = StructuralToUnity(ParseFloat(p[6]), ParseFloat(p[7]), 0), zMin = ParseFloat(p[5]), zMax = ParseFloat(p[8]), thickness = ParseFloat(p[9]) });
                else if (p[0] == "S") slabs.Add(new Slab { id = int.Parse(p[1]), corners = new[] { nodes[int.Parse(p[2])], nodes[int.Parse(p[3])], nodes[int.Parse(p[4])], nodes[int.Parse(p[5])] }, thickness = ParseFloat(p[7]) });
            }
            catch (Exception error) { Debug.LogWarning("Fila CSV ignorada: " + error.Message); }
        }
    }

    private static Vector3 StructuralToUnity(float x, float y, float z) { return new Vector3(x, z, y); }
    private static float ParseFloat(string value) { return float.Parse(value, CultureInfo.InvariantCulture); }
    private Transform Root(string name) { return new GameObject(name).transform; }

    private void BuildScene()
    {
        nodeRoot = Root("Nodos"); beamRoot = Root("Vigas"); columnRoot = Root("Columnas"); wallRoot = Root("Muros");
        diaphragmRoot = Root("Diafragmas"); supportRoot = Root("Apoyos"); localAxisRoot = Root("EjesLocales"); tributaryRoot = Root("AreaTributaria");
        foreach (Element e in elements)
        {
            if (!nodes.ContainsKey(e.i) || !nodes.ContainsKey(e.j)) continue;
            bool column = e.type == "COLUMN";
            try
            {
                CreateMember(e.type + "_ID_" + e.id, e.type, nodes[e.i], nodes[e.j], column ? new Color(.85f, .18f, .12f) : new Color(.10f, .35f, .85f), column ? columnRoot : beamRoot);
                CreateLocalAxes(e.id, nodes[e.i], nodes[e.j]);
                if (e.id % 50 == 0) Debug.Log("Elementos generados hasta ID: " + e.id);
            }
            catch (Exception error) { Debug.LogError("Error generando elemento ID " + e.id + ": " + error); }
        }
        foreach (Wall wall in walls)
        {
            try { CreateWall(wall); }
            catch (Exception error) { Debug.LogError("Error generando muro ID " + wall.id + ": " + error); }
        }
        foreach (Slab slab in slabs)
        {
            try { CreateSlab(slab); }
            catch (Exception error) { Debug.LogError("Error generando losa ID " + slab.id + ": " + error); }
        }
        foreach (KeyValuePair<int, Vector3> node in nodes)
        {
            try
            {
                CreateNode(node.Key, node.Value);
                if (restrainedNodes.Contains(node.Key)) CreateSupport(node.Key, node.Value);
            }
            catch (Exception error) { Debug.LogError("Error generando nodo ID " + node.Key + ": " + error); }
        }
        foreach (Transform root in new[] { nodeRoot, beamRoot, columnRoot, wallRoot, supportRoot, localAxisRoot, diaphragmRoot }) foreach (Transform child in root) levelObjects[child] = true;
        ApplyVisibility();
        FitCameraToModel();
        Debug.Log("Generacion finalizada. Nodos: " + nodes.Count + ", elementos: " + elements.Count + ", losas: " + slabs.Count);
    }

    private void FitCameraToModel()
    {
        if (nodes.Count == 0) return;
        Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
        foreach (Vector3 point in nodes.Values) { min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
        OrbitCamera orbit = FindAnyObjectByType<OrbitCamera>();
        if (orbit == null) return;
        orbit.target = (min + max) / 2;
        orbit.distance = Mathf.Clamp((max - min).magnitude * 0.9f, 20.0f, 150.0f);
        orbit.Apply();
    }

    private void CreateWall(Wall wall)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Muro_ID_" + wall.id; go.transform.SetParent(wallRoot);
        Vector3 d = wall.b - wall.a; go.transform.position = new Vector3((wall.a.x + wall.b.x) / 2, (wall.zMin + wall.zMax) / 2, (wall.a.z + wall.b.z) / 2);
        go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up); go.transform.localScale = new Vector3(wall.thickness, wall.zMax - wall.zMin, d.magnitude);
        SetMaterial(go.GetComponent<Renderer>(), new Color(.27f, .33f, .40f)); CreateIdLabel(go, wall.id, go.transform.position);
    }

    private void CreateSlab(Slab slab)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Diafragma_ID_" + slab.id; go.transform.SetParent(diaphragmRoot);
        float minX = slab.corners[0].x, maxX = minX, minZ = slab.corners[0].z, maxZ = minZ, y = slab.corners[0].y;
        foreach (Vector3 c in slab.corners) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); y = Mathf.Max(y, c.y); }
        go.transform.position = new Vector3((minX + maxX) / 2, y + .40f - slab.thickness / 2, (minZ + maxZ) / 2); go.transform.localScale = new Vector3(maxX - minX, slab.thickness, maxZ - minZ);
        SetMaterial(go.GetComponent<Renderer>(), new Color(.10f, .56f, .78f, .34f), true); CreateIdLabel(go, slab.id, go.transform.position + Vector3.up * .1f);
    }

    private void CreateMember(string name, string type, Vector3 a, Vector3 b, Color color, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent); Vector3 d = b - a;
        go.transform.position = (a + b) / 2; go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d);
        float width = type == "COLUMN" ? .70f : (type == "BEAM_SMALL" ? .30f : (type == "BEAM_40x60" ? .40f : .60f));
        float depth = type == "COLUMN" ? .70f : (type == "BEAM_SMALL" ? .45f : (type == "BEAM_40x60" ? .60f : (type == "BEAM_VARIABLE" ? .35f : .80f)));
        go.transform.localScale = new Vector3(width, d.magnitude, depth); SetMaterial(go.GetComponent<Renderer>(), color); CreateIdLabel(go, idFromName(name), go.transform.position);
    }

    private void CreateNode(int id, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Nodo_ID_" + id; go.transform.SetParent(nodeRoot); go.transform.position = position; go.transform.localScale = Vector3.one * memberThickness * 1.8f; SetMaterial(go.GetComponent<Renderer>(), new Color(1f, .72f, .10f));
        if (showNodeLabels) { GameObject label = new GameObject("ID_" + id); label.transform.SetParent(go.transform); label.transform.position = position + Vector3.up * .15f; TextMesh mesh = label.AddComponent<TextMesh>(); mesh.text = id.ToString(); mesh.characterSize = .08f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; }
    }

    private void CreateSupport(int id, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Apoyo_ID_" + id; go.transform.SetParent(supportRoot); go.transform.position = position - Vector3.up * .28f; go.transform.localScale = new Vector3(.55f, .28f, .55f); SetMaterial(go.GetComponent<Renderer>(), new Color(.95f, .42f, .08f)); CreateIdLabel(go, id, position);
    }

    private static void SetMaterial(Renderer renderer, Color color, bool transparent = false)
    {
        Shader shader = Shader.Find("Standard"); if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            if (!shaderWarningLogged) { Debug.LogError("No se encontro shader Standard ni Unlit/Color. Se usara el material existente del primitive."); shaderWarningLogged = true; }
            renderer.material.color = color;
            return;
        }
        Material material = new Material(shader); material.color = color;
        if (shader.name == "Standard") { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(color.r, color.g, color.b, 1f) * .12f); }
        if (transparent && shader.name == "Standard") { material.SetFloat("_Mode", 3); material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); material.SetInt("_ZWrite", 0); material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); material.DisableKeyword("_ALPHATEST_ON"); material.EnableKeyword("_ALPHABLEND_ON"); material.renderQueue = 3000; }
        renderer.material = material;
    }

    private static int idFromName(string name) { int marker = name.LastIndexOf("_ID_", StringComparison.Ordinal); return int.Parse(name.Substring(marker + 4)); }
    private void CreateIdLabel(GameObject parent, int id, Vector3 position) { GameObject label = new GameObject("ID_" + id); label.transform.SetParent(parent.transform); label.transform.position = position + Vector3.up * .12f; TextMesh mesh = label.AddComponent<TextMesh>(); mesh.text = id.ToString(); mesh.characterSize = .08f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; }

    private void CreateLocalAxes(int id, Vector3 a, Vector3 b)
    {
        Vector3 origin = (a + b) / 2; Vector3 localX = (b - a).normalized; Vector3 localY = Mathf.Abs(Vector3.Dot(localX, Vector3.up)) > .95f ? Vector3.right : Vector3.up; Vector3 localZ = Vector3.Cross(localX, localY).normalized;
        CreateAxisLine("EjeLocalX_ID_" + id, origin, origin + localX * 1.0f, Color.red); CreateAxisLine("EjeLocalY_ID_" + id, origin, origin + localY * 1.0f, Color.green); CreateAxisLine("EjeLocalZ_ID_" + id, origin, origin + localZ * 1.0f, Color.blue);
    }
    private void CreateAxisLine(string name, Vector3 a, Vector3 b, Color color) { GameObject go = new GameObject(name); go.transform.SetParent(localAxisRoot); LineRenderer line = go.AddComponent<LineRenderer>(); line.positionCount = 2; line.SetPositions(new[] { a, b }); line.startWidth = .035f; line.endWidth = .01f; SetMaterial(line, color); }

    private void ApplyVisibility()
    {
        if (nodeRoot) nodeRoot.gameObject.SetActive(showNodes); if (beamRoot) beamRoot.gameObject.SetActive(showBeams); if (columnRoot) columnRoot.gameObject.SetActive(showColumns); if (wallRoot) wallRoot.gameObject.SetActive(showWalls); if (supportRoot) supportRoot.gameObject.SetActive(showSupports); if (localAxisRoot) localAxisRoot.gameObject.SetActive(showLocalAxes); if (diaphragmRoot) diaphragmRoot.gameObject.SetActive(showDiaphragms); if (tributaryRoot) tributaryRoot.gameObject.SetActive(showTributary);
        foreach (Transform root in new[] { nodeRoot, beamRoot, columnRoot, wallRoot, supportRoot, localAxisRoot, diaphragmRoot }) foreach (Transform label in root.GetComponentsInChildren<Transform>(true)) if (label.name.StartsWith("ID_", StringComparison.Ordinal)) label.gameObject.SetActive(showIds);
        if (level < 0) return;
        foreach (Transform root in new[] { nodeRoot, beamRoot, columnRoot, wallRoot, supportRoot, localAxisRoot, diaphragmRoot }) foreach (Transform child in root) child.gameObject.SetActive(levelObjects[child] && (level < 0 || child.position.y >= level - .02f));
    }

    private void UpdateTributary()
    {
        foreach (Transform child in tributaryRoot) Destroy(child.gameObject);
        if (!showTributary || selectedSlab < 0) return;
        Slab slab = slabs.Find(s => s.id == selectedSlab); if (slab == null) return;
        Vector3 p0 = slab.corners[0], p1 = slab.corners[1], p2 = slab.corners[2], p3 = slab.corners[3];
        // Slabs are raised by 0.40 m to align with the beam top. Keep the
        // overlay above that surface so it cannot be hidden by the slab.
        float x0 = p0.x, x1 = p1.x, z0 = p0.z, z1 = p2.z, y = Mathf.Max(p0.y, p1.y, p2.y, p3.y) + .56f;
        float xm = (x0 + x1) / 2, zm = (z0 + z1) / 2, lx = Mathf.Abs(x1 - x0), lz = Mathf.Abs(z1 - z0);
        float ratio = Mathf.Max(lx, lz) / Mathf.Min(lx, lz);
        if (ratio >= 2.0f)
        {
            if (lx >= lz) { CreateTributaryPolygon(slab.id, "Borde inferior", new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, zm), new Vector3(x0, y, zm) }, 0); CreateTributaryPolygon(slab.id, "Borde superior", new[] { new Vector3(x0, y, zm), new Vector3(x1, y, zm), new Vector3(x1, y, z1), new Vector3(x0, y, z1) }, 1); }
            else { CreateTributaryPolygon(slab.id, "Borde izquierdo", new[] { new Vector3(x0, y, z0), new Vector3(xm, y, z0), new Vector3(xm, y, z1), new Vector3(x0, y, z1) }, 0); CreateTributaryPolygon(slab.id, "Borde derecho", new[] { new Vector3(xm, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(xm, y, z1) }, 1); }
        }
        else if (lx >= lz)
        {
            float d = lz / 2; CreateTributaryPolygon(slab.id, "Borde izquierdo", new[] { new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x0 + d, y, zm) }, 0); CreateTributaryPolygon(slab.id, "Borde derecho", new[] { new Vector3(x1, y, z0), new Vector3(x1 - d, y, zm), new Vector3(x1, y, z1) }, 1); CreateTributaryPolygon(slab.id, "Borde inferior", new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1 - d, y, zm), new Vector3(x0 + d, y, zm) }, 2); CreateTributaryPolygon(slab.id, "Borde superior", new[] { new Vector3(x0, y, z1), new Vector3(x0 + d, y, zm), new Vector3(x1 - d, y, zm), new Vector3(x1, y, z1) }, 3);
        }
        else
        {
            float d = lx / 2; CreateTributaryPolygon(slab.id, "Borde inferior", new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(xm, y, z0 + d) }, 0); CreateTributaryPolygon(slab.id, "Borde superior", new[] { new Vector3(x0, y, z1), new Vector3(xm, y, z1 - d), new Vector3(x1, y, z1) }, 1); CreateTributaryPolygon(slab.id, "Borde izquierdo", new[] { new Vector3(x0, y, z0), new Vector3(xm, y, z0 + d), new Vector3(xm, y, z1 - d), new Vector3(x0, y, z1) }, 2); CreateTributaryPolygon(slab.id, "Borde derecho", new[] { new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(xm, y, z1 - d), new Vector3(xm, y, z0 + d) }, 3);
        }
        ApplyVisibility();
    }

    private void CreateTributaryPolygon(int slabId, string edge, Vector3[] points, int colorIndex)
    {
        GameObject zone = new GameObject("AreaTributaria_ID_" + slabId + "_" + edge); zone.transform.SetParent(tributaryRoot);
        Mesh mesh = new Mesh(); mesh.vertices = points; mesh.triangles = points.Length == 3 ? new[] { 0, 2, 1 } : new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateNormals();
        zone.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = zone.AddComponent<MeshRenderer>();
        Color[] colors = { new Color(1f, .18f, .08f, .68f), new Color(.82f, .12f, .92f, .68f), new Color(.08f, .55f, 1f, .68f), new Color(.10f, .78f, .35f, .68f) };
        SetMaterial(renderer, colors[colorIndex % colors.Length], true); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        LineRenderer outline = zone.AddComponent<LineRenderer>(); outline.positionCount = points.Length; outline.SetPositions(points); outline.loop = true; outline.startWidth = .045f; outline.endWidth = .045f; SetMaterial(outline, new Color(colors[colorIndex % colors.Length].r, colors[colorIndex % colors.Length].g, colors[colorIndex % colors.Length].b, 1f)); outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; outline.receiveShadows = false;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && Input.mousePosition.x > 285.0f)
        {
            Camera viewerCamera = FindAnyObjectByType<Camera>();
            Ray ray = viewerCamera != null ? viewerCamera.ScreenPointToRay(Input.mousePosition) : new Ray();
            if (viewerCamera != null)
            {
                string marker = "Diafragma_ID_";
                foreach (RaycastHit hit in Physics.RaycastAll(ray))
                {
                    int markerIndex = hit.collider.name.IndexOf(marker, StringComparison.Ordinal);
                    if (markerIndex >= 0) { int id; if (int.TryParse(hit.collider.name.Substring(markerIndex + marker.Length), out id)) { selectedSlab = id; UpdateTributary(); break; } }
                }
            }
        }
    }

    private void OnGUI()
    {
        if (panelStyle == null) { panelStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 10, 10) }; titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold }; smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true }; }
        GUILayout.BeginArea(new Rect(12, 12, 265, Screen.height - 24), panelStyle); scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("MODELO ESTRUCTURAL", titleStyle); GUILayout.Label(nodes.Count + " nodos | " + elements.Count + " elementos | " + slabs.Count + " losas", smallStyle); GUILayout.Space(8);
        showNodes = GUILayout.Toggle(showNodes, "Nodos"); showBeams = GUILayout.Toggle(showBeams, "Vigas"); showColumns = GUILayout.Toggle(showColumns, "Columnas"); showWalls = GUILayout.Toggle(showWalls, "Muros"); showSupports = GUILayout.Toggle(showSupports, "Apoyos"); showDiaphragms = GUILayout.Toggle(showDiaphragms, "Diafragmas / losas"); showIds = GUILayout.Toggle(showIds, "IDs"); showLocalAxes = GUILayout.Toggle(showLocalAxes, "Ejes locales"); showTributary = GUILayout.Toggle(showTributary, "Area tributaria");
        GUILayout.Space(8); GUILayout.Label("Nivel (-1 = todos)"); string levelText = GUILayout.TextField(level.ToString()); int parsed; if (int.TryParse(levelText, out parsed)) level = parsed;
        GUILayout.Label("Inspector de area tributaria", titleStyle); string[] options = new string[slabs.Count + 1]; options[0] = "Seleccionar losa"; for (int i = 0; i < slabs.Count; i++) options[i + 1] = "Losa ID " + slabs[i].id; int choice = slabs.FindIndex(s => s.id == selectedSlab) + 1; int next = GUILayout.SelectionGrid(choice, options, 1); if (next > 0 && next != choice) { selectedSlab = slabs[next - 1].id; UpdateTributary(); }
        if (selectedSlab >= 0) { Slab slab = slabs.Find(s => s.id == selectedSlab); if (slab != null) GUILayout.Label("ID: " + slab.id + "\nArea: " + slab.Area.ToString("F2") + " m2\nMetodo: reparto por cuatro bordes\nZona resaltada: centro hacia cada borde", smallStyle); }
        if (GUILayout.Button("Reiniciar seleccion")) { selectedSlab = -1; UpdateTributary(); }
        GUILayout.Label("Haz clic directamente sobre una losa para seleccionarla.\nLMB orbitar | MMB desplazar | rueda zoom\nLas zonas coloreadas muestran el reparto tributario hacia cada borde.", smallStyle); GUILayout.EndScrollView(); GUILayout.EndArea(); ApplyVisibility();
    }
}
