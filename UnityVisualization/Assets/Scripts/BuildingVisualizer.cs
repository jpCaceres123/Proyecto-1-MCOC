using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class BuildingVisualizer : MonoBehaviour
{
    public TextAsset modelCsv;
    public bool showNodes = true;
    public bool showNodeLabels = true;
    public float memberThickness = 0.12f;

    private readonly Dictionary<int, Vector3> nodes = new Dictionary<int, Vector3>();
    private readonly List<(int id, string type, int i, int j)> elements = new List<(int, string, int, int)>();
    private Transform nodeRoot;
    private Transform elementRoot;

    private void Start()
    {
        if (modelCsv == null)
            modelCsv = Resources.Load<TextAsset>("model_3d");
        if (modelCsv == null)
        {
            Debug.LogError("No se encontro Resources/model_3d.csv");
            return;
        }
        ReadCsv(modelCsv.text);
        BuildScene();
    }

    private void ReadCsv(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("kind")) continue;
            string[] p = line.Split(',');
            if (p.Length < 5) continue;
            if (p[0] == "N")
                // Structural Z is vertical; Unity uses Y as its vertical axis.
                nodes[int.Parse(p[1])] = new Vector3(ParseFloat(p[6]), ParseFloat(p[8]), ParseFloat(p[7]));
            else if (p[0] == "E")
                elements.Add((int.Parse(p[1]), p[2], int.Parse(p[3]), int.Parse(p[4])));
        }
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }

    private void BuildScene()
    {
        nodeRoot = new GameObject("Nodos").transform;
        elementRoot = new GameObject("Elementos").transform;
        foreach (var e in elements)
        {
            if (!nodes.ContainsKey(e.i) || !nodes.ContainsKey(e.j)) continue;
            Color color = e.type == "COLUMN" ? new Color(0.85f, 0.18f, 0.12f) : new Color(0.10f, 0.35f, 0.85f);
            CreateMember(e.type + "_" + e.id, e.type, nodes[e.i], nodes[e.j], color);
        }
        if (showNodes)
            foreach (var n in nodes) CreateNode(n.Key, n.Value);
    }

    private void CreateMember(string name, string type, Vector3 a, Vector3 b, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(elementRoot);
        Vector3 delta = b - a;
        go.transform.position = (a + b) * 0.5f;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        float width = type == "COLUMN" ? 0.70f : (type == "BEAM_SMALL" ? 0.30f : (type == "BEAM_40x60" ? 0.40f : 0.60f));
        float depth = type == "COLUMN" ? 0.70f : (type == "BEAM_SMALL" ? 0.45f : (type == "BEAM_40x60" ? 0.60f : (type == "BEAM_VARIABLE" ? 0.35f : 0.80f)));
        go.transform.localScale = new Vector3(width, delta.magnitude, depth);
        go.GetComponent<Renderer>().material.color = color;
    }

    private void CreateNode(int id, Vector3 position)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Nodo_" + id;
        go.transform.SetParent(nodeRoot);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * memberThickness * 1.8f;
        go.GetComponent<Renderer>().material.color = Color.yellow;
        if (showNodeLabels)
        {
            GameObject label = new GameObject("ID_" + id);
            label.transform.SetParent(go.transform);
            label.transform.position = position + Vector3.up * 0.15f;
            TextMesh mesh = label.AddComponent<TextMesh>();
            mesh.text = id.ToString();
            mesh.characterSize = 0.08f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
        }
    }
}
