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
    private readonly Dictionary<int, string> nodeAxes = new Dictionary<int, string>();
    private readonly HashSet<int> restrainedNodes = new HashSet<int>();
    private readonly List<Element> elements = new List<Element>();
    private readonly List<Wall> walls = new List<Wall>();
    private readonly List<Slab> slabs = new List<Slab>();
    private readonly Dictionary<Transform, bool> levelObjects = new Dictionary<Transform, bool>();
    private Transform nodeRoot, beamRoot, columnRoot, wallRoot, diaphragmRoot;
    private Transform supportRoot, localAxisRoot, tributaryRoot;
    private GUIStyle panelStyle, titleStyle, smallStyle;
    private int selectedSlab = -1;
    private float level = -1f;
    private bool showBeams = true, showColumns = true, showWalls = true;
    private bool showSupports = true, showDiaphragms = true, showLocalAxes;
    private bool showIds = true, showTributary = true;
    private Vector2 scroll;
    private int steelColumnCount;
    private static bool shaderWarningLogged;
    private ElementInspector inspector;
    private readonly string[] levelNames = { "Todos los niveles", "Subterraneo", "Piso 1", "Piso 2", "Piso 3", "Piso 4" };
    private readonly float[] levelValues = { -1f, 0f, 3.96f, 7.92f, 11.88f, 15.84f };
    private int levelChoice;
    private bool levelMenu;
    private Material concreteBeamMaterial, grassMaterial, skyMaterial;

    private struct Element { public int id, i, j; public string type; }
    private struct Wall { public int id; public Vector3 a, b; public float zMin, zMax, thickness; }
    private struct SlabVoid { public float xMin, xMax, zMin, zMax; }
    private class Slab
    {
        public int id;
        public Vector3[] corners;
        public float thickness;
        public readonly List<SlabVoid> voids = new List<SlabVoid>();
        public float Area
        {
            get
            {
                float area = Mathf.Abs((corners[1].x - corners[0].x) * (corners[2].z - corners[1].z));
                foreach (SlabVoid hole in voids) area -= Mathf.Abs((hole.xMax - hole.xMin) * (hole.zMax - hole.zMin));
                return Mathf.Max(0.0f, area);
            }
        }
    }

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
        Debug.Log("Apoyos cargados desde restraint: " + restrainedNodes.Count);
        Debug.Log("Elementos cargados: " + elements.Count);
        Debug.Log("Muros cargados: " + walls.Count);
        Debug.Log("Losas cargadas: " + slabs.Count);
        inspector = gameObject.AddComponent<ElementInspector>();
        ConfigureEnvironment();
        BuildScene();
        if (GetComponent<Semana3Visualizer>() == null) gameObject.AddComponent<Semana3Visualizer>();
        if (GetComponent<MovingLoadViewer>() == null) gameObject.AddComponent<MovingLoadViewer>();
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
                    if (p.Length > 12) nodeAxes[id] = p[12].Trim();
                    // La restriccion se exporta explicitamente. Algunos ejes
                    // comienzan sobre Z=0 (H/I en 3.96 m; J en 15.84 m).
                    if (p.Length > 11 && p[11].Trim().Equals("true", StringComparison.OrdinalIgnoreCase)) restrainedNodes.Add(id);
                }
                else if (p[0] == "E" && p[2] != "WALL")
                {
                    elements.Add(new Element { id = int.Parse(p[1]), type = p[2], i = int.Parse(p[3]), j = int.Parse(p[4]) });
                    if (p[2] == "STEEL_COLUMN_SHS300x20") steelColumnCount++;
                }
                else if (p[0] == "W") walls.Add(new Wall { id = int.Parse(p[1]), a = StructuralToUnity(ParseFloat(p[3]), ParseFloat(p[4]), 0), b = StructuralToUnity(ParseFloat(p[6]), ParseFloat(p[7]), 0), zMin = ParseFloat(p[5]), zMax = ParseFloat(p[8]), thickness = ParseFloat(p[9]) });
                else if (p[0] == "S") slabs.Add(new Slab { id = int.Parse(p[1]), corners = new[] { nodes[int.Parse(p[2])], nodes[int.Parse(p[3])], nodes[int.Parse(p[4])], nodes[int.Parse(p[5])] }, thickness = ParseFloat(p[7]) });
                else if (p[0] == "V")
                {
                    Slab slab = slabs.Find(s => s.id == int.Parse(p[1]));
                    if (slab != null) slab.voids.Add(new SlabVoid { xMin = ParseFloat(p[2]), xMax = ParseFloat(p[3]), zMin = ParseFloat(p[4]), zMax = ParseFloat(p[5]) });
                }
            }
            catch (Exception error) { Debug.LogWarning("Fila CSV ignorada: " + error.Message); }
        }
    }

    private static Vector3 StructuralToUnity(float x, float y, float z) { return new Vector3(x, z, y); }
    private static float ParseFloat(string value) { return float.Parse(value, CultureInfo.InvariantCulture); }
    private Transform Root(string name) { return new GameObject(name).transform; }

    private void ConfigureEnvironment()
    {
        Camera camera = Camera.main;
        if (camera)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(.47f, .72f, .91f);
        }
        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader)
        {
            skyMaterial = new Material(skyShader);
            skyMaterial.SetColor("_SkyTint", new Color(.63f, .79f, .96f));
            skyMaterial.SetColor("_GroundColor", new Color(.38f, .43f, .34f));
            skyMaterial.SetFloat("_AtmosphereThickness", 1.0f);
            skyMaterial.SetFloat("_SunSize", .035f);
            RenderSettings.skybox = skyMaterial;
        }
        else if (camera) camera.clearFlags = CameraClearFlags.SolidColor;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.66f, .78f, .88f);
        RenderSettings.ambientEquatorColor = new Color(.55f, .58f, .55f);
        RenderSettings.ambientGroundColor = new Color(.28f, .30f, .25f);

        Shader standard = Shader.Find("Standard");
        if (!standard) return;
        concreteBeamMaterial = new Material(standard);
        concreteBeamMaterial.name = "Hormigon texturado";
        concreteBeamMaterial.color = new Color(.82f, .83f, .82f);
        concreteBeamMaterial.mainTexture = MakeConcreteTexture();
        concreteBeamMaterial.mainTextureScale = new Vector2(1.2f, 1.2f);
        concreteBeamMaterial.SetFloat("_Glossiness", .12f);

        grassMaterial = new Material(standard);
        grassMaterial.name = "Pasto texturado";
        grassMaterial.color = Color.white;
        grassMaterial.mainTexture = MakeGrassTexture();
        grassMaterial.mainTextureScale = new Vector2(24f, 24f);
        grassMaterial.SetFloat("_Glossiness", .05f);
    }

    private static Texture2D MakeConcreteTexture()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        texture.name = "Hormigon_grano_fino";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        var random = new System.Random(60423);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float noise = (float)random.NextDouble() - .5f;
                float fleck = random.NextDouble() < .035 ? -.16f : 0f;
                float shade = Mathf.Clamp(.69f + noise * .12f + fleck, .36f, .82f);
                texture.SetPixel(x, y, new Color(shade, shade * 1.01f, shade * 1.02f));
            }
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeGrassTexture()
    {
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
        texture.name = "Pasto_variacion_natural";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        var random = new System.Random(60424);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float noise = (float)random.NextDouble() - .5f;
                float blade = random.NextDouble() < .16 ? .10f : 0f;
                float green = Mathf.Clamp(.34f + noise * .18f + blade, .16f, .58f);
                texture.SetPixel(x, y, new Color(green * .55f, green, green * .40f));
            }
        texture.Apply();
        return texture;
    }

    private void CreateGround()
    {
        if (nodes.Count == 0 || !grassMaterial) return;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        foreach (Vector3 point in nodes.Values)
        {
            minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
            minY = Mathf.Min(minY, point.y);
        }
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Terreno_pasto";
        ground.transform.position = new Vector3((minX + maxX) * .5f, minY - .58f, (minZ + maxZ) * .5f);
        ground.transform.localScale = new Vector3((maxX - minX + 16f) / 10f, 1f, (maxZ - minZ + 16f) / 10f);
        ground.GetComponent<Renderer>().sharedMaterial = grassMaterial;
        ground.GetComponent<Collider>().enabled = false;
    }

    private void BuildScene()
    {
        nodeRoot = Root("Nodos"); beamRoot = Root("Vigas"); columnRoot = Root("Columnas"); wallRoot = Root("Muros");
        diaphragmRoot = Root("Diafragmas"); supportRoot = Root("Apoyos"); localAxisRoot = Root("EjesLocales"); tributaryRoot = Root("AreaTributaria");
        CreateGround();
        foreach (Element e in elements)
        {
            if (!nodes.ContainsKey(e.i) || !nodes.ContainsKey(e.j)) continue;
            bool steelColumn = e.type == "STEEL_COLUMN_SHS300x20";
            bool column = e.type == "COLUMN" || steelColumn;
            try
            {
                CreateMember(e.type + "_ID_" + e.id, e.type, nodes[e.i], nodes[e.j], steelColumn ? new Color(.15f, .78f, .72f) : (column ? new Color(.85f, .18f, .12f) : new Color(.10f, .35f, .85f)), column ? columnRoot : beamRoot);
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
        Vector3 d = wall.b - wall.a;
        const float floorHeight = 3.96f;
        int firstFloor = Mathf.FloorToInt((wall.zMin + 1e-4f) / floorHeight);
        int lastFloor = Mathf.CeilToInt((wall.zMax - 1e-4f) / floorHeight);
        for (int floor = firstFloor; floor < lastFloor; floor++)
        {
            float z0 = Mathf.Max(wall.zMin, floor * floorHeight);
            float z1 = Mathf.Min(wall.zMax, (floor + 1) * floorHeight);
            if (z1 - z0 < 1e-4f) continue;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Muro_ID_" + wall.id + "_Piso_" + floor;
            go.transform.SetParent(wallRoot);
            go.transform.position = new Vector3((wall.a.x + wall.b.x) / 2, (z0 + z1) / 2, (wall.a.z + wall.b.z) / 2);
            go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            go.transform.localScale = new Vector3(wall.thickness, z1 - z0, d.magnitude);
            SetMaterial(go.GetComponent<Renderer>(), new Color(.27f, .33f, .40f));
            CreateIdLabel(go, wall.id, go.transform.position);
            int segment = floor - firstFloor;
            string levelName = floor == 0 ? "1S" : floor.ToString(CultureInfo.InvariantCulture);
            inspector.Register(go, wall.id, "Muro",
                "Piso: " + levelName + "\nLongitud: " + d.magnitude.ToString("F3") + " m\nEspesor: " + wall.thickness.ToString("F2") + " m\nAltura: " + (z1-z0).ToString("F2") + " m",
                wall.id, segment);
        }
    }

    private static Texture2D Solid(Color color)
    {
        Texture2D texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); return texture;
    }

    private void CreateSlab(Slab slab)
    {
        float minX = slab.corners[0].x, maxX = minX, minZ = slab.corners[0].z, maxZ = minZ, y = slab.corners[0].y;
        foreach (Vector3 c in slab.corners) { minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); y = Mathf.Max(y, c.y); }
        List<float> xs = new List<float> { minX, maxX }, zs = new List<float> { minZ, maxZ };
        foreach (SlabVoid hole in slab.voids)
        {
            if (hole.xMin > minX && hole.xMin < maxX) xs.Add(hole.xMin);
            if (hole.xMax > minX && hole.xMax < maxX) xs.Add(hole.xMax);
            if (hole.zMin > minZ && hole.zMin < maxZ) zs.Add(hole.zMin);
            if (hole.zMax > minZ && hole.zMax < maxZ) zs.Add(hole.zMax);
        }
        xs.Sort(); zs.Sort();
        int piece = 0;
        for (int xi = 0; xi < xs.Count - 1; xi++) for (int zi = 0; zi < zs.Count - 1; zi++)
        {
            float x0 = xs[xi], x1 = xs[xi + 1], z0 = zs[zi], z1 = zs[zi + 1];
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            bool insideVoid = false;
            foreach (SlabVoid hole in slab.voids) if (cx > hole.xMin && cx < hole.xMax && cz > hole.zMin && cz < hole.zMax) { insideVoid = true; break; }
            if (insideVoid) continue;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Diafragma_ID_" + slab.id + (piece == 0 ? "" : "_Pieza_" + piece); go.transform.SetParent(diaphragmRoot);
            go.transform.position = new Vector3((x0 + x1) / 2, y + .40f - slab.thickness / 2, (z0 + z1) / 2); go.transform.localScale = new Vector3(x1 - x0, slab.thickness, z1 - z0);
            SetMaterial(go.GetComponent<Renderer>(), new Color(.10f, .56f, .78f, .34f), true);
            inspector.Register(go, slab.id, "Losa", "Área geométrica: " + slab.Area.ToString("F2") + " m²\nEspesor: " + slab.thickness.ToString("F2") + " m");
            if (piece == 0) CreateIdLabel(go, slab.id, go.transform.position + Vector3.up * .1f);
            piece++;
        }
    }

    private void CreateMember(string name, string type, Vector3 a, Vector3 b, Color color, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent); Vector3 d = b - a;
        go.transform.position = (a + b) / 2; go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d);
        bool steelColumn = type == "STEEL_COLUMN_SHS300x20";
        float width = steelColumn ? .30f : (type == "COLUMN" ? .70f : (type == "BEAM_SMALL" ? .30f : (type == "BEAM_40x60" ? .40f : .60f)));
        float depth = steelColumn ? .30f : (type == "COLUMN" ? .70f : (type == "BEAM_SMALL" ? .45f : (type == "BEAM_40x60" ? .60f : (type == "BEAM_VARIABLE" ? .35f : .80f))));
        go.transform.localScale = new Vector3(width, d.magnitude, depth);
        Renderer memberRenderer = go.GetComponent<Renderer>();
        SetMaterial(memberRenderer, color);
        if (!steelColumn && parent == beamRoot && concreteBeamMaterial)
        {
            Material colorMaterial = memberRenderer.sharedMaterial;
            memberRenderer.sharedMaterial = concreteBeamMaterial;
            if (colorMaterial) Destroy(colorMaterial);
        }
        CreateIdLabel(go, idFromName(name), go.transform.position);
        inspector.Register(go, idFromName(name), type.Contains("COLUMN") ? "Columna" : "Viga", "Tipo: " + type + "\nLongitud: " + d.magnitude.ToString("F2") + " m");
    }

    private void CreateNode(int id, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = "Nodo_ID_" + id; go.transform.SetParent(nodeRoot); go.transform.position = position; go.transform.localScale = Vector3.one * memberThickness * 1.8f; SetMaterial(go.GetComponent<Renderer>(), new Color(1f, .72f, .10f));
        if (showNodeLabels) { GameObject label = new GameObject("ID_" + id); label.transform.SetParent(go.transform); label.transform.position = position + Vector3.up * .15f; TextMesh mesh = label.AddComponent<TextMesh>(); mesh.text = id.ToString(); mesh.characterSize = .08f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; }
    }

    private void CreateSupport(int id, Vector3 position)
    {
        string axis; nodeAxes.TryGetValue(id, out axis);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "Apoyo_" + axis + "_ID_" + id; go.transform.SetParent(supportRoot); go.transform.position = position - Vector3.up * .28f; go.transform.localScale = new Vector3(.55f, .28f, .55f); SetMaterial(go.GetComponent<Renderer>(), new Color(.95f, .42f, .08f));
        GameObject label = new GameObject("ID_" + id); label.transform.SetParent(go.transform); label.transform.position = position + Vector3.up * .12f; TextMesh mesh = label.AddComponent<TextMesh>(); mesh.text = (string.IsNullOrEmpty(axis) ? "" : axis + " / ") + id; mesh.characterSize = .08f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
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
        Vector3 origin = (a + b) / 2;
        Vector3 localX, localY, localZ;
        if (!StructuralPostprocessor.Get(gameObject).TryAxes(id, out localX, out localY, out localZ)) return;
        CreateAxisLine("EjeLocalX_ID_" + id, origin, origin + localX * 1.0f, Color.red); CreateAxisLine("EjeLocalY_ID_" + id, origin, origin + localY * 1.0f, Color.green); CreateAxisLine("EjeLocalZ_ID_" + id, origin, origin + localZ * 1.0f, Color.blue);
    }
    private void CreateAxisLine(string name, Vector3 a, Vector3 b, Color color) { GameObject go = new GameObject(name); go.transform.SetParent(localAxisRoot); LineRenderer line = go.AddComponent<LineRenderer>(); line.positionCount = 2; line.SetPositions(new[] { a, b }); line.startWidth = .035f; line.endWidth = .01f; SetMaterial(line, color); }

    private void ApplyVisibility()
    {
        if (nodeRoot) nodeRoot.gameObject.SetActive(showNodes); if (beamRoot) beamRoot.gameObject.SetActive(showBeams); if (columnRoot) columnRoot.gameObject.SetActive(showColumns); if (wallRoot) wallRoot.gameObject.SetActive(showWalls); if (supportRoot) supportRoot.gameObject.SetActive(showSupports); if (localAxisRoot) localAxisRoot.gameObject.SetActive(showLocalAxes); if (diaphragmRoot) diaphragmRoot.gameObject.SetActive(showDiaphragms); if (tributaryRoot) tributaryRoot.gameObject.SetActive(showTributary);
        foreach (Transform root in new[] { nodeRoot, beamRoot, columnRoot, wallRoot, supportRoot, localAxisRoot, diaphragmRoot }) foreach (Transform label in root.GetComponentsInChildren<Transform>(true)) if (label.name.StartsWith("ID_", StringComparison.Ordinal)) label.gameObject.SetActive(showIds);
        if (level < 0)
        {
            foreach (Transform root in new[] { nodeRoot, beamRoot, columnRoot, wallRoot, supportRoot, localAxisRoot, diaphragmRoot })
                foreach (Transform child in root) child.gameObject.SetActive(levelObjects[child]);
            return;
        }
        float upperLevel = level + 3.96f;
        if(MovingLoadViewer.Active) {
            foreach(Transform root in new[]{nodeRoot,beamRoot,columnRoot,wallRoot,supportRoot,localAxisRoot,diaphragmRoot})
                foreach(Transform child in root) {
                    bool vertical=root==columnRoot || root==wallRoot;
                    child.gameObject.SetActive(levelObjects[child] && (vertical
                        ? child.position.y>=level-3.96f-.02f && child.position.y<level-.02f
                        : child.position.y>=level-.02f && child.position.y<=level+.45f));
                }
            return;
        }
        foreach (Transform root in new[] { nodeRoot, beamRoot, columnRoot, wallRoot, supportRoot, localAxisRoot, diaphragmRoot })
            foreach (Transform child in root)
                child.gameObject.SetActive(levelObjects[child] && (level < 0 ||
                    (child.position.y >= level - .02f && child.position.y <= upperLevel + .02f)));
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
        if (lx >= lz)
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
        Color fill = edge.StartsWith("Borde inferior") ? new Color(.133f, .773f, .365f, .55f) :
            edge.StartsWith("Borde superior") ? new Color(.659f, .333f, .969f, .55f) :
            edge.StartsWith("Borde izquierdo") ? new Color(.976f, .451f, .086f, .55f) :
            new Color(.925f, .282f, .6f, .55f);
        SetMaterial(renderer, fill, true); renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        LineRenderer outline = zone.AddComponent<LineRenderer>(); outline.positionCount = points.Length; outline.SetPositions(points); outline.loop = true; outline.startWidth = .045f; outline.endWidth = .045f; SetMaterial(outline, new Color(.067f, .094f, .129f, 1f)); outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; outline.receiveShadows = false;
    }

    public void SelectSlab(int id)
    {
        selectedSlab = id;
        UpdateTributary();
    }

    public void DrawTopLevel()
    {
        if(MovingLoadViewer.Active) { GUILayout.Label("Nivel SQ4: +"+level.ToString("F2")+" m",GUILayout.Width(150));return; }
        string label = levelNames[(int)Mathf.Clamp(levelChoice, 0, levelNames.Length - 1)];
        if (GUILayout.Button("Nivel: " + label + " v", GUILayout.Width(150)))
        {
            levelMenu = !levelMenu;
        }
    }

    public void DrawTopLevelPopup()
    {
        if (!levelMenu) return;
        Rect popup = new Rect(390, 54, 170, 24 * levelNames.Length + 8);
        GUI.Box(popup, "");
        for (int i = 0; i < levelNames.Length; i++)
        {
            Rect option = new Rect(popup.x + 4, popup.y + 4 + i * 24, popup.width - 8, 22);
            if (GUI.Button(option, levelNames[i]))
            {
                levelChoice = i;
                level = levelValues[i];
                levelMenu = false;
                ApplyVisibility();
            }
        }
    }

    public void FocusMovingPanel(int slabId, float elevation)
    {
        level=elevation;
        levelChoice=Array.FindIndex(levelValues,value=>Mathf.Abs(value-elevation)<.01f);
        if(levelChoice<0) levelChoice=0;
        showNodes=false;showIds=false;showTributary=false;showDiaphragms=false;
        SelectSlab(slabId);ApplyVisibility();
    }

    private float savedMovingLevel;
    private int savedMovingChoice,savedMovingSlab;
    private bool savedMovingNodes,savedMovingIds,savedMovingTributary,savedMovingSlabs;
    public void BeginMovingView()
    {
        savedMovingLevel=level;savedMovingChoice=levelChoice;savedMovingSlab=selectedSlab;
        savedMovingNodes=showNodes;savedMovingIds=showIds;savedMovingTributary=showTributary;
        savedMovingSlabs=showDiaphragms;
    }
    public void EndMovingView()
    {
        level=savedMovingLevel;levelChoice=savedMovingChoice;showNodes=savedMovingNodes;
        showIds=savedMovingIds;showTributary=savedMovingTributary;showDiaphragms=savedMovingSlabs;SelectSlab(savedMovingSlab);ApplyVisibility();
    }

    private void OnGUI()
    {
        if (panelStyle == null) { panelStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(12, 12, 10, 10) }; panelStyle.normal.background = Solid(new Color(.055f, .09f, .13f, .97f)); titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } }; smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true, normal = { textColor = new Color(.82f, .87f, .91f) } }; }
        GUILayout.BeginArea(new Rect(12, 74, 240, Screen.height - 86), panelStyle); scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("MODELO", titleStyle); GUILayout.Label(nodes.Count + " nodos\n" + elements.Count + " barras\n" + walls.Count + " muros\n" + slabs.Count + " losas\n4 niveles", smallStyle); GUILayout.Space(8);
        GUILayout.Label("CAPAS", titleStyle);
        var moving=GetComponent<MovingLoadViewer>();
        if(moving!=null) moving.DrawLauncher();
        showBeams = GUILayout.Toggle(showBeams, "Vigas"); showColumns = GUILayout.Toggle(showColumns, "Columnas"); showWalls = GUILayout.Toggle(showWalls, "Muros"); showDiaphragms = GUILayout.Toggle(showDiaphragms, "Losas"); showNodes = GUILayout.Toggle(showNodes, "Nodos"); showSupports = GUILayout.Toggle(showSupports, "Apoyos");
        GUILayout.Space(8); GUILayout.Label("VISUALIZACION", titleStyle);
        showIds = GUILayout.Toggle(showIds, "Mostrar IDs"); showLocalAxes = GUILayout.Toggle(showLocalAxes, "Ejes locales"); showTributary = GUILayout.Toggle(showTributary, "Area tributaria");
        Semana3Visualizer results = GetComponent<Semana3Visualizer>();
        GUI.enabled=!MovingLoadViewer.Active;
        if (results != null)
        {
            bool deformed = results.ShowDeformed;
            bool forces = results.ShowForces;
            bool nextDeformed = GUILayout.Toggle(deformed, "Deformada");
            bool nextForces = GUILayout.Toggle(forces, "Fuerzas sismicas / CM");
            if (nextDeformed != deformed || nextForces != forces) results.SetDisplay(nextDeformed, nextForces, results.DeformationScale);
            if (nextDeformed)
            {
                GUILayout.Label("Escala: " + results.DeformationScale.ToString("F0") + "x", smallStyle);
                float scale = GUILayout.HorizontalSlider(results.DeformationScale, 1f, 5000f);
                if (Mathf.Abs(scale - results.DeformationScale) > 1f) results.SetDisplay(nextDeformed, nextForces, scale);
            }
        }
        GUI.enabled=true;
        if(MovingLoadViewer.Active) {
            GUILayout.Label("LECTURA SQ4",titleStyle);
            GUILayout.Label("Carga vertical localizada\nReparto según apoyos de la losa\nTurquesa / ámbar: receptores\nRosa: deformada amplificada\n\nLa respuesta Δ corresponde sólo a la carga móvil.\n\nClic: colocar carga\nArrastrar LMB: orbitar\nMMB: desplazar · rueda: zoom\nW/A/S/D: caminar sin cruzar vacíos",smallStyle);
        } else {
        GUILayout.Label("Area tributaria", titleStyle);
        GUILayout.Label(selectedSlab < 0 ? "Seleccione una losa directamente en el visor." : "Losa seleccionada: " + selectedSlab, smallStyle);
        if (selectedSlab >= 0) { Slab slab = slabs.Find(s => s.id == selectedSlab); if (slab != null) GUILayout.Label("ID: " + slab.id + "\nArea: " + slab.Area.ToString("F2") + " m2\nMetodo: reparto por cuatro bordes\nZona resaltada: centro hacia cada borde", smallStyle); }
        if (GUILayout.Button("Reiniciar seleccion")) { inspector.SelectSlabById(-1); }
        GUILayout.Label("Haz clic directamente sobre una losa para seleccionarla.\nLMB orbitar | MMB desplazar | rueda zoom\nLas zonas coloreadas muestran el reparto tributario hacia cada borde.", smallStyle);
        }
        GUILayout.EndScrollView(); GUILayout.EndArea(); ApplyVisibility();
    }
}
