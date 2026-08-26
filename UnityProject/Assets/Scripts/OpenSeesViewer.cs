using UnityEngine;

public class OpenSeesViewer : MonoBehaviour
{
    public float deformationScale = 1000f;
    public bool showUndeformed = true;
    public bool showDeformed = true;
    public bool showLoads = true;
    public bool showTributaryAreas = true;

    private readonly Vector3[] nodes =
    {
        new(0, 0, 0), new(6, 0, 0), new(6, 0, 5), new(0, 0, 5),
        new(0, 3, 0), new(6, 3, 0), new(6, 3, 5), new(0, 3, 5)
    };

    private readonly int[,] members =
    {
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
        { 4, 5 }, { 7, 6 }, { 4, 7 }, { 5, 6 }
    };

    private void Start()
    {
        BuildRuntimeModel();
        SetupCamera();
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        if (showUndeformed) DrawFrame(Color.gray, 0f);
        if (showDeformed) DrawFrame(Color.cyan, deformationScale);
        if (showTributaryAreas) DrawTributaryAreasGizmos();
        if (showLoads) DrawLoadsGizmos();
        DrawAxes();
    }

    private void DrawFrame(Color color, float scale)
    {
        Gizmos.color = color;
        for (int i = 0; i < members.GetLength(0); i++)
        {
            Vector3 a = Deformed(nodes[members[i, 0]], scale);
            Vector3 b = Deformed(nodes[members[i, 1]], scale);
            Gizmos.DrawLine(a, b);
        }
        for (int i = 0; i < nodes.Length; i++)
            Gizmos.DrawSphere(Deformed(nodes[i], scale), 0.08f);
    }

    private Vector3 Deformed(Vector3 p, float scale)
    {
        // Approximate the computed vertical displacement using the reported node-7 value.
        float uz = p.y > 2.9f ? -0.00005f : 0f;
        return p + new Vector3(0f, uz * scale, 0f);
    }

    private void DrawAxes()
    {
        Gizmos.color = Color.red; Gizmos.DrawLine(Vector3.zero, Vector3.right * 1.2f);
        Gizmos.color = Color.green; Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1.2f);
        Gizmos.color = Color.blue; Gizmos.DrawLine(Vector3.zero, Vector3.up * 1.2f);
    }

    private void DrawLoadsGizmos()
    {
        Gizmos.color = new Color(1f, 0.12f, 0.05f);
        DrawLoadLineGizmos(nodes[4], nodes[5], 9, 0.85f, true);
        DrawLoadLineGizmos(nodes[7], nodes[6], 9, 0.85f, true);
        DrawLoadLineGizmos(nodes[4], nodes[7], 7, 0.85f, false);
        DrawLoadLineGizmos(nodes[5], nodes[6], 7, 0.85f, false);
        DrawLoadEnvelopeGizmos(nodes[4], nodes[5], 24, 0.85f, true);
        DrawLoadEnvelopeGizmos(nodes[7], nodes[6], 24, 0.85f, true);
        DrawLoadEnvelopeGizmos(nodes[4], nodes[7], 20, 0.85f, false);
        DrawLoadEnvelopeGizmos(nodes[5], nodes[6], 20, 0.85f, false);
    }

    private void DrawTributaryAreasGizmos()
    {
        Vector3 p00 = new(0f, 3.03f, 0f);
        Vector3 p60 = new(6f, 3.03f, 0f);
        Vector3 p65 = new(6f, 3.03f, 5f);
        Vector3 p05 = new(0f, 3.03f, 5f);
        Vector3 p25 = new(2.5f, 3.03f, 2.5f);
        Vector3 p35 = new(3.5f, 3.03f, 2.5f);

        Gizmos.color = new Color(1f, 0.85f, 0.05f, 0.85f);
        DrawPolygonOutline(p00, p60, p35, p25);
        DrawPolygonOutline(p05, p25, p35, p65);
        Gizmos.color = new Color(0.35f, 1f, 0.25f, 0.85f);
        DrawPolygonOutline(p00, p25, p05);
        DrawPolygonOutline(p60, p65, p35);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(p00, p25);
        Gizmos.DrawLine(p05, p25);
        Gizmos.DrawLine(p25, p35);
        Gizmos.DrawLine(p60, p35);
        Gizmos.DrawLine(p65, p35);
    }

    private void DrawPolygonOutline(params Vector3[] points)
    {
        for (int i = 0; i < points.Length; i++)
            Gizmos.DrawLine(points[i], points[(i + 1) % points.Length]);
    }

    private void DrawLoadLineGizmos(Vector3 a, Vector3 b, int count, float maxLength, bool alongX)
    {
        for (int i = 1; i <= count; i++)
        {
            Vector3 basePoint = Vector3.Lerp(a, b, i / (count + 1f)) + Vector3.up * 0.15f;
            float length = maxLength * LoadRatio(basePoint, alongX);
            Vector3 top = basePoint + Vector3.up * length;
            Gizmos.DrawLine(top, basePoint);
            Gizmos.DrawLine(basePoint, basePoint + new Vector3(-0.10f, 0.16f, 0f));
            Gizmos.DrawLine(basePoint, basePoint + new Vector3(0.10f, 0.16f, 0f));
        }
    }

    private float LoadRatio(Vector3 p, bool alongX)
    {
        float tributaryWidth = alongX ? Mathf.Min(p.x, 2.5f, 6f - p.x) : Mathf.Min(p.z, 5f - p.z);
        return Mathf.Clamp01(tributaryWidth / 2.5f);
    }

    private void DrawLoadEnvelopeGizmos(Vector3 a, Vector3 b, int segments, float maxLength, bool alongX)
    {
        Vector3 previousBase = a + Vector3.up * 0.15f;
        Vector3 previousTop = previousBase + Vector3.up * (maxLength * LoadRatio(previousBase, alongX));

        for (int i = 1; i <= segments; i++)
        {
            Vector3 currentBase = Vector3.Lerp(a, b, i / (float)segments) + Vector3.up * 0.15f;
            Vector3 currentTop = currentBase + Vector3.up * (maxLength * LoadRatio(currentBase, alongX));
            Gizmos.DrawLine(previousBase, currentBase);
            Gizmos.DrawLine(previousTop, currentTop);
            previousBase = currentBase;
            previousTop = currentTop;
        }
    }

    private void BuildRuntimeModel()
    {
        Material frameMaterial = MakeMaterial(new Color(0.05f, 0.65f, 0.95f));
        Material nodeMaterial = MakeMaterial(new Color(1f, 0.65f, 0.05f));
        for (int i = 0; i < members.GetLength(0); i++)
        {
            float thickness = i < 4 ? 0.30f : 0.25f;
            MakeMember(nodes[members[i, 0]], nodes[members[i, 1]], frameMaterial,
                i < 4 ? "Column" : "Beam", thickness);
        }
        for (int i = 0; i < nodes.Length; i++)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = "Node " + (i + 1);
            node.transform.position = nodes[i];
            node.transform.localScale = Vector3.one * 0.22f;
            node.GetComponent<Renderer>().material = nodeMaterial;
            node.transform.SetParent(transform);
        }
        for (int i = 0; i < 4; i++) MakeFixedSupport(nodes[i]);
        MakeMember(Vector3.zero, Vector3.right * 1.5f, MakeMaterial(Color.red), "Global X", 0.035f);
        MakeMember(Vector3.zero, Vector3.forward * 1.5f, MakeMaterial(Color.green), "Global Y", 0.035f);
        MakeMember(Vector3.zero, Vector3.up * 1.5f, MakeMaterial(Color.blue), "Global Z", 0.035f);
        if (showTributaryAreas) MakeTributaryAreas();
        if (showLoads) MakeLoads();
    }

    private void MakeTributaryAreas()
    {
        Vector3 p00 = new(0f, 3.03f, 0f);
        Vector3 p60 = new(6f, 3.03f, 0f);
        Vector3 p65 = new(6f, 3.03f, 5f);
        Vector3 p05 = new(0f, 3.03f, 5f);
        Vector3 p25 = new(2.5f, 3.03f, 2.5f);
        Vector3 p35 = new(3.5f, 3.03f, 2.5f);

        Material trapezoidMaterial = MakeTransparentMaterial(new Color(1f, 0.75f, 0.05f, 0.38f));
        Material triangleMaterial = MakeTransparentMaterial(new Color(0.25f, 1f, 0.25f, 0.34f));
        Material lineMaterial = MakeMaterial(Color.white);

        MakeArea("Tributary trapezoid to 6 m beam", trapezoidMaterial, p00, p60, p35, p25);
        MakeArea("Tributary trapezoid to 6 m beam", trapezoidMaterial, p05, p25, p35, p65);
        MakeArea("Tributary triangle to 5 m beam", triangleMaterial, p00, p25, p05);
        MakeArea("Tributary triangle to 5 m beam", triangleMaterial, p60, p65, p35);

        MakeMember(p00, p25, lineMaterial, "45 degree tributary line", 0.025f);
        MakeMember(p05, p25, lineMaterial, "45 degree tributary line", 0.025f);
        MakeMember(p25, p35, lineMaterial, "Tributary middle line", 0.025f);
        MakeMember(p60, p35, lineMaterial, "45 degree tributary line", 0.025f);
        MakeMember(p65, p35, lineMaterial, "45 degree tributary line", 0.025f);

        MakeLabel("Trapecio", new Vector3(3f, 3.09f, 0.9f), trapezoidMaterial.color);
        MakeLabel("Trapecio", new Vector3(3f, 3.09f, 4.1f), trapezoidMaterial.color);
        MakeLabel("Triangulo", new Vector3(0.85f, 3.09f, 2.5f), triangleMaterial.color);
        MakeLabel("Triangulo", new Vector3(5.15f, 3.09f, 2.5f), triangleMaterial.color);
    }

    private void MakeArea(string name, Material material, params Vector3[] vertices)
    {
        GameObject area = new GameObject(name);
        Mesh mesh = new Mesh { name = name + " mesh" };
        mesh.vertices = vertices;
        mesh.triangles = vertices.Length == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        area.AddComponent<MeshFilter>().mesh = mesh;
        area.AddComponent<MeshRenderer>().material = material;
        area.transform.SetParent(transform);
    }

    private void MakeLoads()
    {
        Material loadMaterial = MakeMaterial(new Color(1f, 0.08f, 0.02f));
        Material envelopeMaterial = MakeMaterial(new Color(1f, 0.26f, 0.05f));
        MakeLoadSet(nodes[4], nodes[5], 9, 0.85f, "trapezoidal max 12.5 kN/m", true);
        MakeLoadSet(nodes[7], nodes[6], 9, 0.85f, "trapezoidal max 12.5 kN/m", true);
        MakeLoadSet(nodes[4], nodes[7], 7, 0.85f, "triangular max 12.5 kN/m", false);
        MakeLoadSet(nodes[5], nodes[6], 7, 0.85f, "triangular max 12.5 kN/m", false);
        MakeLoadEnvelope(nodes[4], nodes[5], 24, 0.85f, true, envelopeMaterial);
        MakeLoadEnvelope(nodes[7], nodes[6], 24, 0.85f, true, envelopeMaterial);
        MakeLoadEnvelope(nodes[4], nodes[7], 20, 0.85f, false, envelopeMaterial);
        MakeLoadEnvelope(nodes[5], nodes[6], 20, 0.85f, false, envelopeMaterial);

        void MakeLoadSet(Vector3 a, Vector3 b, int count, float maxArrowLength, string label, bool alongX)
        {
            for (int i = 1; i <= count; i++)
            {
                Vector3 basePoint = Vector3.Lerp(a, b, i / (count + 1f)) + Vector3.up * 0.15f;
                MakeLoadArrow(basePoint, maxArrowLength * LoadRatio(basePoint, alongX), loadMaterial);
            }

            Vector3 mid = Vector3.Lerp(a, b, 0.5f) + Vector3.up * 1.15f;
            MakeLabel(label, mid, loadMaterial.color);
        }
    }

    private void MakeLoadEnvelope(Vector3 a, Vector3 b, int segments, float maxLength, bool alongX, Material material)
    {
        Vector3 previousBase = a + Vector3.up * 0.15f;
        Vector3 previousTop = previousBase + Vector3.up * (maxLength * LoadRatio(previousBase, alongX));

        for (int i = 1; i <= segments; i++)
        {
            Vector3 currentBase = Vector3.Lerp(a, b, i / (float)segments) + Vector3.up * 0.15f;
            Vector3 currentTop = currentBase + Vector3.up * (maxLength * LoadRatio(currentBase, alongX));
            MakeMember(previousBase, currentBase, material, "Load diagram top line", 0.035f);
            MakeMember(previousTop, currentTop, material, "Load diagram upper line", 0.045f);
            previousBase = currentBase;
            previousTop = currentTop;
        }
    }

    private void MakeLoadArrow(Vector3 basePoint, float length, Material material)
    {
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Distributed slab load arrow";
        shaft.transform.position = basePoint + Vector3.up * (length * 0.5f);
        shaft.transform.localScale = new Vector3(0.035f, length * 0.45f, 0.035f);
        shaft.GetComponent<Renderer>().material = material;
        shaft.transform.SetParent(transform);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        head.name = "Load arrow head";
        head.transform.position = basePoint;
        head.transform.localScale = new Vector3(0.13f, 0.08f, 0.13f);
        head.GetComponent<Renderer>().material = material;
        head.transform.SetParent(transform);
    }

    private void MakeLabel(string text, Vector3 position, Color color)
    {
        GameObject label = new GameObject("Load label " + text);
        TextMesh mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.characterSize = 0.12f;
        mesh.fontSize = 28;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        label.transform.SetParent(transform);
    }

    private void MakeMember(Vector3 a, Vector3 b, Material material, string name, float thickness)
    {
        Vector3 delta = b - a;
        GameObject member = GameObject.CreatePrimitive(PrimitiveType.Cube);
        member.name = name;
        member.transform.position = (a + b) * 0.5f;
        member.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        // Unity primitives have unit length; local Y is aligned with the member axis.
        member.transform.localScale = new Vector3(thickness, delta.magnitude, thickness);
        member.GetComponent<Renderer>().material = material;
        member.transform.SetParent(transform);
    }

    private void MakeFixedSupport(Vector3 position)
    {
        Material supportMaterial = MakeMaterial(new Color(0.16f, 0.17f, 0.20f));
        Material boltMaterial = MakeMaterial(new Color(0.95f, 0.72f, 0.10f));

        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Fixed support base plate";
        plate.transform.position = position + Vector3.down * 0.10f;
        plate.transform.localScale = new Vector3(0.85f, 0.20f, 0.85f);
        plate.GetComponent<Renderer>().material = supportMaterial;
        plate.transform.SetParent(transform);

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "Fixed support pedestal";
        pedestal.transform.position = position + Vector3.up * 0.10f;
        pedestal.transform.localScale = new Vector3(0.62f, 0.25f, 0.62f);
        pedestal.GetComponent<Renderer>().material = supportMaterial;
        pedestal.transform.SetParent(transform);

        foreach (Vector3 offset in new[] {
            new Vector3(-0.27f, 0.12f, -0.27f), new Vector3(-0.27f, 0.12f, 0.27f),
            new Vector3(0.27f, 0.12f, -0.27f), new Vector3(0.27f, 0.12f, 0.27f) })
        {
            GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bolt.name = "Anchor bolt";
            bolt.transform.position = position + offset;
            bolt.transform.localScale = new Vector3(0.055f, 0.12f, 0.055f);
            bolt.GetComponent<Renderer>().material = boltMaterial;
            bolt.transform.SetParent(transform);
        }
    }

    private Material MakeMaterial(Color color)
    {
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
        Material material = new Material(shader) { color = color };
        return material;
    }

    private Material MakeTransparentMaterial(Color color)
    {
        Material material = MakeMaterial(color);
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
        return material;
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
        }
        camera.transform.position = new Vector3(11f, 9f, 11f);
        camera.transform.LookAt(new Vector3(3f, 1.5f, 2.5f));
        camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        camera.clearFlags = CameraClearFlags.SolidColor;
    }
}
