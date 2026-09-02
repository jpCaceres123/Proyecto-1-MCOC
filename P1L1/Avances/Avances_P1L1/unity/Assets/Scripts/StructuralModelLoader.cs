using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class StructuralModelLoader : MonoBehaviour
{
    [SerializeField] private TextAsset modelJson;
    [SerializeField] private float nodeRadius = 0.12f;
    [SerializeField] private float columnRadius = 0.18f;
    [SerializeField] private float beamRadius = 0.14f;

    private readonly Dictionary<int, Vector3> nodePositions = new();
    private readonly Dictionary<string, SectionData> sections = new();
    private Transform modelRoot;

    private void Start()
    {
        string json = modelJson != null ? modelJson.text : ReadExternalJson();
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("No se encontro unity_model.json.");
            return;
        }

        StructuralModelData model = JsonUtility.FromJson<StructuralModelData>(json);
        if (model == null || model.nodes == null || model.elements == null)
        {
            Debug.LogError("El JSON no tiene el contrato structural_model_unity_v1.");
            return;
        }

        modelRoot = new GameObject("LT1_Model").transform;
        LoadSections(model.sections);
        LoadNodes(model.nodes);
        LoadElements(model.elements);
        CreateCameraIfNeeded(model.nodes);
        Debug.Log($"Modelo LT1 cargado: {model.nodes.Length} nodos, {model.elements.Length} elementos.");
    }

    private string ReadExternalJson()
    {
        string path = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "..", "results", "unity_model.json"));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private void LoadSections(SectionData[] sectionData)
    {
        if (sectionData == null) return;
        foreach (SectionData section in sectionData) sections[section.sectionId] = section;
    }

    private void LoadNodes(NodeData[] nodes)
    {
        foreach (NodeData node in nodes)
        {
            Vector3 position = new(node.position_unity_m.x_m, node.position_unity_m.y_m,
                                   node.position_unity_m.z_m);
            nodePositions[node.nodeTag] = position;
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = $"Node_{node.nodeTag}";
            point.transform.SetParent(modelRoot, false);
            point.transform.position = position;
            point.transform.localScale = Vector3.one * nodeRadius;
            SetColor(point, node.isSupport ? new Color(0.9f, 0.2f, 0.1f) : Color.black);
        }
    }

    private void LoadElements(ElementData[] elements)
    {
        foreach (ElementData element in elements)
        {
            if (!nodePositions.TryGetValue(element.nodeI, out Vector3 a) ||
                !nodePositions.TryGetValue(element.nodeJ, out Vector3 b))
            {
                Debug.LogError($"Elemento {element.elementTag} referencia un nodo inexistente.");
                continue;
            }

            float radius = element.kind == "column" ? columnRadius : beamRadius;
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = $"Element_{element.elementTag}_{element.kind}";
            bar.transform.SetParent(modelRoot, false);
            Vector3 direction = b - a;
            bar.transform.position = (a + b) * 0.5f;
            bar.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction);
            bar.transform.localScale = new Vector3(radius, direction.magnitude * 0.5f, radius);
            SetColor(bar, element.kind == "column" ? new Color(0.18f, 0.22f, 0.30f) :
                (element.kind == "beam_x" ? new Color(0.05f, 0.35f, 0.85f) :
                 new Color(0.9f, 0.45f, 0.05f)));
        }
    }

    private static void SetColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        renderer.material.color = color;
    }

    private void CreateCameraIfNeeded(NodeData[] nodes)
    {
        if (Camera.main != null) return;
        GameObject cameraObject = new("ModelCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        Vector3 target = new(10.0f, 2.0f, 8.0f);
        cameraObject.transform.position = new Vector3(31.0f, 20.0f, -28.0f);
        cameraObject.transform.LookAt(target);
        camera.fieldOfView = 55.0f;

        GameObject lightObject = new("ModelLight");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(35.0f, -30.0f, 0.0f);
    }
}

[Serializable]
public class StructuralModelData
{
    public string schema;
    public NodeData[] nodes;
    public ElementData[] elements;
    public SectionData[] sections;
}

[Serializable]
public class NodeData
{
    public int nodeTag;
    public int levelIndex;
    public PositionData position_unity_m;
    public bool isSupport;
}

[Serializable]
public class PositionData
{
    public float x_m;
    public float y_m;
    public float z_m;
}

[Serializable]
public class ElementData
{
    public int elementTag;
    public string kind;
    public int nodeI;
    public int nodeJ;
    public string sectionId;
}

[Serializable]
public class SectionData
{
    public string sectionId;
    public string kind;
    public float width_m;
    public float height_m;
}
