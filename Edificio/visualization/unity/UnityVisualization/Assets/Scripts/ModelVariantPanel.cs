using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ModelVariantPanel : MonoBehaviour
{
    [Serializable] public class Edit
    {
        public string kind; public int id;
        public bool changeLoad, changeSection;
        public double q, b, h;
    }
    [Serializable] public class Request { public List<Edit> changes = new List<Edit>(); }
    [Serializable] private class Completion { public string resources, status; }
    private static Request draft = new Request(), applied = new Request();
    private static string message = "Modelo base", selectedKind;
    private static int selectedId;
    private bool open, busy, section, load;
    private int editingId = -1;
    private string editingKind, q = "0", b = "0.6", h = "0.8";
    private string repository, python;
    private Process process;
    private readonly StringBuilder log = new StringBuilder();
    public static ModelVariantPanel Get(GameObject owner)
    {
        var panel = owner.GetComponent<ModelVariantPanel>();
        return panel ? panel : owner.AddComponent<ModelVariantPanel>();
    }
    public static bool SectionChanged(string kind, int id) =>
        applied.changes.Exists(e => e.kind == kind && e.id == id && e.changeSection);
    private void Awake()
    {
        repository = PlayerPrefs.GetString("AnalysisRepository", "");
        python = PlayerPrefs.GetString("AnalysisPython", "python");
        if (string.IsNullOrEmpty(repository))
            for (var dir = new DirectoryInfo(Application.dataPath); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Edificio/analysis/load_cases/unity_reanalizar.py")))
                { repository = dir.FullName; break; }
    }
    private IEnumerator Start()
    {
        yield return null;
        if (selectedKind != null) GetComponent<ElementInspector>().SelectElementById(selectedKind, selectedId);
    }
    private static double Parse(string text) => double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
    public void Draw(string kind, int id)
    {
        if (GUILayout.Button(open ? "Cerrar configuración del elemento" : "Configurar carga / sección")) open = !open;
        GUILayout.Label(message);
        if (!open) return;
        if (editingId != id || editingKind != kind)
        {
            editingId = id; editingKind = kind;
            var prior = draft.changes.Find(e => e.kind == kind && e.id == id);
            section = prior != null && prior.changeSection; load = prior != null && prior.changeLoad;
            q = prior != null ? prior.q.ToString("G6", CultureInfo.InvariantCulture) : "0";
            double width, height;
            StructuralPostprocessor.Get(gameObject).Dimensions(kind, id, out width, out height);
            b = (prior != null && section ? prior.b : width).ToString("G6", CultureInfo.InvariantCulture);
            h = (prior != null && section ? prior.h : height).ToString("G6", CultureInfo.InvariantCulture);
        }
        GUILayout.Label("Cambios por ID · OpenSees en Windows");
        bool wasEnabled = GUI.enabled; GUI.enabled = wasEnabled && !busy;
        load = GUILayout.Toggle(load, "Modificar carga Q del elemento");
        if (load)
        {
            GUILayout.Label(kind == "Losa" ? "Q superficial total [kN/m²] (reemplaza Q de esta losa)" :
                kind == "Muro" ? "Q adicional en borde superior [kN/m], vertical −Z" : "Q adicional uniforme [kN/m], vertical −Z");
            q = GUILayout.TextField(q);
        }
        section = GUILayout.Toggle(section, "Modificar sección / espesor");
        if (section)
        {
            bool surface = kind == "Muro" || kind == "Losa";
            bool steel = StructuralPostprocessor.Get(gameObject).IsSteel(id) && kind == "Columna";
            if (!surface) { GUILayout.Label(steel ? "Ancho exterior SHS [m]" : "b [m] (convención de sección OpenSees)"); b = GUILayout.TextField(b); }
            GUILayout.Label(surface || steel ? "Espesor [m]" : "h [m]"); h = GUILayout.TextField(h);
        }
        if (kind == "Losa") GUILayout.Label("Losa tributaria: espesor cambia peso y masa, sin rigidez de placa FE.");
        GUILayout.Label("Requiere reanálisis: actualiza G/Q, masa, sismo y respuesta. Los resultados visibles siguen siendo los últimos calculados.");
        GUILayout.Label("Raíz del repositorio (contiene Edificio)"); repository = GUILayout.TextField(repository ?? "");
        GUILayout.Label("Python: ejecutable o ruta a python.exe"); python = GUILayout.TextField(python);
        if (GUILayout.Button("Aplicar elemento y reanalizar"))
        {
            try
            {
                var edit = new Edit { kind = kind, id = id, changeLoad = load, changeSection = section,
                    q = load ? Parse(q) : 0, b = section && kind != "Losa" && kind != "Muro" ? Parse(b) : 0,
                    h = section ? Parse(h) : 0 };
                if (double.IsNaN(edit.q) || double.IsInfinity(edit.q) || edit.q < 0 ||
                    (section && (double.IsNaN(edit.h) || double.IsInfinity(edit.h) || edit.h <= 0)) ||
                    (section && kind != "Losa" && kind != "Muro" && (double.IsNaN(edit.b) || double.IsInfinity(edit.b) || edit.b <= 0)))
                    throw new Exception("Cargas >= 0 y dimensiones positivas y finitas.");
                draft.changes.RemoveAll(e => e.kind == kind && e.id == id);
                if (section || load) draft.changes.Add(edit);
                selectedKind = kind; selectedId = id;
                StartCoroutine(Calculate(JsonUtility.ToJson(draft, true)));
            }
            catch (Exception ex) { message = ex.Message; }
        }
        GUILayout.Label("Elementos en variante: " + draft.changes.Count);
        if (GUILayout.Button("Restaurar modelo base"))
        {
            draft = new Request(); applied = new Request();
            AnalysisResources.Activate(null); message = "Modelo base restaurado";
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        GUI.enabled = wasEnabled;
    }
    private IEnumerator Calculate(string request)
    {
        string job = null;
        try
        {
            string script = Path.Combine(repository, "Edificio/analysis/load_cases/unity_reanalizar.py");
            if (!File.Exists(script)) throw new Exception("No se encontró unity_reanalizar.py en esa raíz.");
            job = Path.Combine(Application.persistentDataPath, "AnalysisJobs", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(job);
            string path = Path.Combine(job, "request.json"); File.WriteAllText(path, request);
            PlayerPrefs.SetString("AnalysisRepository", repository); PlayerPrefs.SetString("AnalysisPython", python);
            log.Clear();
            process = new Process { StartInfo = new ProcessStartInfo {
                FileName = python, Arguments = "-u \"" + script + "\" --request \"" + path + "\"",
                WorkingDirectory = repository, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true } };
            process.OutputDataReceived += Capture; process.ErrorDataReceived += Capture;
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            busy = true; message = "OpenSees calculando… puede tardar varios minutos. Carpeta: " + job;
        }
        catch (Exception ex)
        {
            message = "No se inició el cálculo: " + ex.Message;
            if (process != null) { process.Dispose(); process = null; }
        }
        if (!busy) yield break;
        while (!process.HasExited) yield return new WaitForSeconds(.3f);
        process.WaitForExit();
        int code = process.ExitCode; process.Dispose(); process = null; busy = false;
        lock (log) File.WriteAllText(Path.Combine(job, "analysis.log"), log.ToString());
        try
        {
            if (code != 0) throw new Exception("OpenSees no aprobó la variante (código " + code + "). Revisar " + Path.Combine(job, "analysis.log"));
            var result = JsonUtility.FromJson<Completion>(File.ReadAllText(Path.Combine(job, "complete.json")));
            if (result.status != "OK") throw new Exception("Resultado incompleto");
            applied = JsonUtility.FromJson<Request>(request);
            AnalysisResources.Activate(result.resources);
            message = "Variante calculada · " + applied.changes.Count + " elementos. SQ4 requiere bases nuevas; disponible en modelo base.";
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        catch (Exception ex) { message = ex.Message; }
    }
    private void Capture(object sender, DataReceivedEventArgs args) { if (args.Data != null) lock (log) log.AppendLine(args.Data); }
    private void OnDestroy()
    {
        if (process == null) return;
        if (!process.HasExited)
        {
            // Stop the Python worker and its analysis children when closing Play/the application.
            using (var kill = Process.Start(new ProcessStartInfo("taskkill", "/PID " + process.Id + " /T /F") { UseShellExecute = false, CreateNoWindow = true }))
                kill.WaitForExit(5000);
        }
        process.Dispose(); process = null;
    }
}
