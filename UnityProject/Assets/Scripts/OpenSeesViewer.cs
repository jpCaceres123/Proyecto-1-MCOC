using UnityEngine;

public class OpenSeesViewer : MonoBehaviour
{
    public float deformationScale = 1000f;
    public bool showUndeformed = true;
    public bool showDeformed = true;

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
        float uz = p.y > 2.9f ? -0.0001f : 0f;
        return p + new Vector3(0f, 0f, uz * scale);
    }

    private void DrawAxes()
    {
        Gizmos.color = Color.red; Gizmos.DrawLine(Vector3.zero, Vector3.right * 1.2f);
        Gizmos.color = Color.green; Gizmos.DrawLine(Vector3.zero, Vector3.forward * 1.2f);
        Gizmos.color = Color.blue; Gizmos.DrawLine(Vector3.zero, Vector3.up * 1.2f);
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
