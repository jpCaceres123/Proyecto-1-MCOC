using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Visualizacion de los resultados generados por P1L3/ejecutar.py.
// OpenSees: X,Y,Z -> Unity: X,Z,Y.
public class Semana3Visualizer : MonoBehaviour
{
    private class Bar { public int id, i, j; }
    private class Floor
    {
        public string block;
        public float z, cmX, cmY, force, ux, uy, rz;
        public float mass, acceleration, accelerationG;
    }
    private class CurvePoint { public float p, phi, moment; }
    private class PMPoint { public float p, moment; }

    private readonly Dictionary<int, Vector3> nodePosition = new Dictionary<int, Vector3>();
    private readonly List<Bar> bars = new List<Bar>();
    private readonly Dictionary<string, Dictionary<int, Vector3>> displacements = new Dictionary<string, Dictionary<int, Vector3>>();
    private readonly Dictionary<string, List<Floor>> floors = new Dictionary<string, List<Floor>>();
    private readonly List<Vector3> concreteFibers = new List<Vector3>();
    private readonly List<Vector3> steelFibers = new List<Vector3>();
    private readonly List<CurvePoint> curves = new List<CurvePoint>();
    private readonly List<PMPoint> pm = new List<PMPoint>();
    private readonly Dictionary<string, string> metadata = new Dictionary<string, string>();

    private Transform responseRoot, forceRoot;
    private string selectedCase = "EX";
    private float deformationScale = 1000f;
    private bool showDeformed = true, showForces = true, showCapacity;
    private Vector2 scroll;
    private GUIStyle panelStyle, titleStyle, noteStyle;
    private Texture2D sectionPlot, mPhiPlot, pmPlot;
    private float maxDisplacement, maxRotation;
    private bool ready;
    public readonly double[] Combination = new double[4];
    public static readonly string[] BaseCases = { "G", "Q", "EX", "EY" };
    private readonly string[] coefficientText = new string[4];
    private string combinationError;

    private void ResetCombination()
    {
        for (int k=0;k<4;k++) coefficientText[k] = metadata["combinacion_"+BaseCases[k]];
        ApplyCombination();
    }

    private void ApplyCombination()
    {
        double[] next = new double[4];
        for (int k=0;k<4;k++)
            if (!double.TryParse(coefficientText[k].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out next[k])
                || double.IsNaN(next[k]) || double.IsInfinity(next[k]) || Math.Abs(next[k]) > 1000)
            { combinationError = "Introduce cuatro números finitos entre −1000 y 1000 (coma o punto decimal)."; return; }
        Dictionary<int, Vector3> combined = new Dictionary<int, Vector3>();
        foreach (int id in displacements["G"].Keys)
        {
            Vector3 value = Vector3.zero;
            for (int k=0;k<4;k++) value += displacements[BaseCases[k]][id] * (float)next[k];
            combined[id] = value;
        }
        List<Floor> combinedFloors = new List<Floor>();
        foreach (Floor reference in floors["G"])
        {
            Floor value = new Floor { block=reference.block, z=reference.z, cmX=reference.cmX, cmY=reference.cmY };
            for (int k=0;k<4;k++)
            {
                Floor source = floors[BaseCases[k]].Find(f => f.block==reference.block && Mathf.Abs(f.z-reference.z)<1e-4f);
                if (source == null) throw new Exception("Falta un piso para combinar " + BaseCases[k]);
                value.ux += source.ux*(float)next[k]; value.uy += source.uy*(float)next[k]; value.rz += source.rz*(float)next[k];
            }
            combinedFloors.Add(value);
        }
        Array.Copy(next,Combination,4); displacements["R"]=combined; floors["R"]=combinedFloors;
        combinationError=null;
        if (ready && selectedCase=="R") RebuildResponse();
    }

    private void DrawCombination()
    {
        GUILayout.Label("R = λG·G + λQ·Q + λEX·EX + λEY·EY",noteStyle);
        for(int k=0;k<4;k++)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label("λ"+BaseCases[k],GUILayout.Width(55));
            coefficientText[k]=GUILayout.TextField(coefficientText[k]); GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Aplicar λ")) ApplyCombination();
        if(GUILayout.Button("Restablecer")) ResetCombination();
        GUILayout.EndHorizontal();
        if(combinationError!=null) GUILayout.Label(combinationError,noteStyle);
        GUILayout.Label("Aplicados: "+Combination[0]+" G; "+Combination[1]+" Q; "+Combination[2]+" EX; "+Combination[3]+" EY",noteStyle);
        GUILayout.Label("Superposición elástica. Cambios solo en esta sesión de Play; EX/EY y sus masas permanecen definidos por el análisis base.",noteStyle);
    }

    private static float F(string value) { return float.Parse(value, CultureInfo.InvariantCulture); }
    private static Vector3 StructuralToUnity(float x, float y, float z) { return new Vector3(x, z, y); }

    private void Start()
    {
        try
        {
            ReadModel(Load("model_3d"));
            ReadDisplacements(Load("semana3_desplazamientos"));
            ReadFloors(Load("semana3_pisos"));
            ReadAccelerations(Load("semana3_aceleraciones"));
            ReadFibers(Load("semana3_fibras"));
            ReadCurves(Load("semana3_momento_curvatura"));
            ReadPM(Load("semana3_pm"));
            ReadMetadata(Load("semana3_resumen"));
            ResetCombination();
            responseRoot = new GameObject("Semana3_Deformada").transform;
            forceRoot = new GameObject("Semana3_Fuerzas_CM").transform;
            BuildPlots();
            RebuildResponse();
            ready = true;
            Debug.Log("Semana 3 cargada: casos G, Q, EX, EY y R.");
            if (Application.isBatchMode) Application.Quit(0);
        }
        catch (Exception error)
        {
            Debug.LogError("No se pudo cargar la visualizacion de Semana 3: " + error);
        }
    }

    private static string Load(string name)
    {
        TextAsset asset = Resources.Load<TextAsset>(name);
        if (asset == null) throw new Exception("Falta Assets/Resources/" + name + ".csv");
        return asset.text;
    }

    private static IEnumerable<string[]> Csv(string text)
    {
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string[] fields = line.Split(','); fields[0] = fields[0].TrimStart('\uFEFF'); yield return fields;
        }
    }

    private void ReadModel(string text)
    {
        foreach (string[] p in Csv(text))
        {
            if (p[0] == "N") nodePosition[int.Parse(p[1])] = StructuralToUnity(F(p[6]), F(p[7]), F(p[8]));
            else if (p[0] == "E" && p[2] != "WALL") bars.Add(new Bar { id = int.Parse(p[1]), i = int.Parse(p[3]), j = int.Parse(p[4]) });
        }
    }

    private void ReadDisplacements(string text)
    {
        foreach (string[] p in Csv(text))
        {
            if (p[0] == "caso") continue;
            if (!displacements.ContainsKey(p[0])) displacements[p[0]] = new Dictionary<int, Vector3>();
            // Desplazamiento OpenSees X,Y,Z -> Unity X,Z,Y.
            displacements[p[0]][int.Parse(p[1])] = StructuralToUnity(F(p[2]), F(p[3]), F(p[4]));
        }
    }

    private void ReadFloors(string text)
    {
        foreach (string[] p in Csv(text))
        {
            if (p[0] == "caso") continue;
            if (!floors.ContainsKey(p[0])) floors[p[0]] = new List<Floor>();
            floors[p[0]].Add(new Floor { block = p[1], z = F(p[2]), cmX = F(p[3]), cmY = F(p[4]),
                force = F(p[5]), ux = F(p[6]), uy = F(p[7]), rz = F(p[8]) });
        }
    }

    private void ReadAccelerations(string text)
    {
        HashSet<string> seen = new HashSet<string>();
        foreach(string[] p in Csv(text))
        {
            if(p[0]=="bloque") continue;
            if(p.Length!=6 || !seen.Add(p[0]+":"+p[1])) throw new Exception("Aceleraciones de piso incompletas o duplicadas");
            float z=F(p[1]), mass=F(p[2]), acceleration=F(p[4]), fraction=F(p[5]);
            if(mass<=0 || float.IsNaN(acceleration) || float.IsInfinity(acceleration)) throw new Exception("Aceleración de piso inválida");
            foreach(string caseName in new[]{"EX","EY"})
            {
                Floor floor=floors[caseName].Find(f => f.block==p[0] && Mathf.Abs(f.z-z)<1e-4f);
                if(floor==null) throw new Exception("Piso inexistente en " + caseName);
                if(Mathf.Abs(floor.force/mass-acceleration)>1e-4f) throw new Exception("Aceleración inconsistente con F/m en " + caseName);
                floor.mass=mass; floor.acceleration=acceleration; floor.accelerationG=fraction;
            }
        }
        foreach(string caseName in new[]{"EX","EY"})
            foreach(Floor floor in floors[caseName]) if(floor.mass<=0) throw new Exception("Falta aceleración de un piso en " + caseName);
    }

    private void DrawFloorAccelerations()
    {
        GUILayout.Space(6);
        GUILayout.Label("ACELERACIÓN EQUIVALENTE POR PISO",titleStyle);
        string direction=selectedCase=="EX"?"X":"Y";
        GUILayout.Label("Caso "+selectedCase+" · dirección global "+direction+"\na = F/m; carga estática equivalente, no respuesta dinámica.",noteStyle);
        List<float> levels=new List<float>();
        foreach(Floor floor in floors[selectedCase])
            if(!levels.Exists(z => Mathf.Abs(z-floor.z)<1e-4f)) levels.Add(floor.z);
        levels.Sort();
        for(int i=0;i<levels.Count;i++)
        {
            List<Floor> levelFloors=floors[selectedCase].FindAll(f => Mathf.Abs(f.z-levels[i])<1e-4f);
            float force=0,mass=0;
            foreach(Floor floor in levelFloors) { force+=floor.force; mass+=floor.mass; }
            float acceleration=force/mass;
            GUILayout.Label("Piso "+(i+1)+": a"+direction+" = "+acceleration.ToString("F5")+" m/s² = "+(acceleration/9.80665f).ToString("F3")+" g",noteStyle);
        }
    }

    private void ReadFibers(string text)
    {
        foreach (string[] p in Csv(text))
        {
            if (p[0] == "y_m") continue;
            Vector3 point = new Vector3(F(p[1]), F(p[0]), F(p[2]));
            if (p[3].Trim() == "2") steelFibers.Add(point); else concreteFibers.Add(point);
        }
    }

    private void ReadCurves(string text)
    {
        foreach (string[] p in Csv(text))
        {
            if (p[0] == "P_objetivo_kN") continue;
            curves.Add(new CurvePoint { p = F(p[0]), phi = F(p[1]), moment = F(p[2]) });
        }
    }

    private void ReadPM(string text)
    {
        foreach (string[] p in Csv(text))
        {
            if (p[0] == "P_kN") continue;
            pm.Add(new PMPoint { p = F(p[0]), moment = F(p[1]) });
        }
    }

    private void ReadMetadata(string text)
    {
        foreach (string[] p in Csv(text)) if (p[0] != "clave") metadata[p[0]] = p[1] + (p.Length > 2 && p[2].Length > 0 ? " " + p[2] : "");
    }

    private static void SetMaterial(Renderer renderer, Color color)
    {
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader); material.color = color; renderer.material = material;
    }

    private static LineRenderer Line(Transform parent, string name, Vector3 a, Vector3 b, Color color, float width)
    {
        GameObject go = new GameObject(name); go.transform.SetParent(parent);
        LineRenderer line = go.AddComponent<LineRenderer>(); line.positionCount = 2;
        line.SetPositions(new[] { a, b }); line.startWidth = width; line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        SetMaterial(line, color); return line;
    }

    private void Clear(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
    }

    private void RebuildResponse()
    {
        if (responseRoot == null || forceRoot == null) return;
        Clear(responseRoot); Clear(forceRoot);
        maxDisplacement = 0; maxRotation = 0;
        Dictionary<int, Vector3> u;
        if (!displacements.TryGetValue(selectedCase, out u)) return;
        Color caseColor = selectedCase == "EX" ? new Color(1f,.25f,.08f) : selectedCase == "EY" ? new Color(.2f,.85f,.3f) : selectedCase == "R" ? new Color(.85f,.1f,.85f) : new Color(1f,.8f,.1f);
        if (showDeformed)
        {
            foreach (Bar bar in bars)
            {
                if (!nodePosition.ContainsKey(bar.i) || !nodePosition.ContainsKey(bar.j) || !u.ContainsKey(bar.i) || !u.ContainsKey(bar.j)) continue;
                Vector3 a = nodePosition[bar.i] + u[bar.i] * deformationScale;
                Vector3 b = nodePosition[bar.j] + u[bar.j] * deformationScale;
                Line(responseRoot, "Deformada_" + selectedCase + "_ID_" + bar.id, a, b, caseColor, .075f);
            }
        }
        foreach (Vector3 value in u.Values) maxDisplacement = Mathf.Max(maxDisplacement, value.magnitude);
        List<Floor> caseFloors;
        if (!floors.TryGetValue(selectedCase, out caseFloors)) return;
        foreach (Floor floor in caseFloors)
        {
            maxRotation = Mathf.Max(maxRotation, Mathf.Abs(floor.rz));
            if (!showForces || (selectedCase != "EX" && selectedCase != "EY")) continue;
            Vector3 cm = StructuralToUnity(floor.cmX, floor.cmY, floor.z);
            Vector3 direction = selectedCase == "EX" ? Vector3.right : Vector3.forward;
            float length = 2.5f + 4f * floor.force / Mathf.Max(1f, MaxFloorForce(caseFloors));
            Line(forceRoot, "F_" + selectedCase + "_" + floor.block + "_Z_" + floor.z, cm, cm + direction * length, caseColor, .13f);
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "CM_" + floor.block + "_Z_" + floor.z; marker.transform.SetParent(forceRoot);
            marker.transform.position = cm; marker.transform.localScale = Vector3.one * .45f; SetMaterial(marker.GetComponent<Renderer>(), Color.cyan);
            GameObject label = new GameObject("Etiqueta_CM"); label.transform.SetParent(marker.transform); label.transform.position = cm + Vector3.up * .5f;
            TextMesh mesh = label.AddComponent<TextMesh>(); mesh.text = floor.block + " CM\nF=" + floor.force.ToString("F0") + " kN\na" + (selectedCase=="EX"?"X":"Y") + "=" + floor.acceleration.ToString("F3") + " m/s² (" + floor.accelerationG.ToString("F2") + " g)";
            mesh.characterSize = .12f; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center;
        }
    }

    private static float MaxFloorForce(List<Floor> values)
    {
        float result = 0; foreach (Floor f in values) result = Mathf.Max(result, f.force); return result;
    }

    private void BuildPlots()
    {
        sectionPlot = Plot(350, 250, new Color(.96f,.97f,.98f), delegate(Texture2D tex, Rect area)
        {
            foreach (Vector3 f in concreteFibers) Dot(tex, Map(f.x,-.36f,.36f,area.xMin,area.xMax), Map(f.y,-.36f,.36f,area.yMax,area.yMin), 1, new Color(.35f,.55f,.72f));
            foreach (Vector3 f in steelFibers) Dot(tex, Map(f.x,-.36f,.36f,area.xMin,area.xMax), Map(f.y,-.36f,.36f,area.yMax,area.yMin), 5, new Color(.78f,.16f,.10f));
        });
        mPhiPlot = Plot(350, 250, Color.white, delegate(Texture2D tex, Rect area)
        {
            float maxX=0,maxY=0; foreach (CurvePoint p in curves) { maxX=Mathf.Max(maxX,p.phi); maxY=Mathf.Max(maxY,p.moment); }
            float[] ps = UniqueAxialLoads(); Color[] colors={new Color(.1f,.4f,.8f),new Color(1f,.45f,.05f),new Color(.1f,.65f,.2f)};
            for(int k=0;k<ps.Length;k++)
            {
                Vector2? previous=null;
                foreach(CurvePoint p in curves) if(Mathf.Abs(p.p-ps[k])<.1f)
                {
                    Vector2 current=new Vector2(Map(p.phi,0,maxX,area.xMin,area.xMax),Map(p.moment,0,maxY,area.yMin,area.yMax));
                    if(previous.HasValue) PixelLine(tex,previous.Value,current,colors[k%colors.Length]); previous=current;
                }
            }
        });
        pmPlot = Plot(350, 250, Color.white, delegate(Texture2D tex, Rect area)
        {
            float maxX=0,maxY=0; foreach(PMPoint p in pm){maxX=Mathf.Max(maxX,p.moment);maxY=Mathf.Max(maxY,p.p);}
            Vector2? previous=null; foreach(PMPoint p in pm)
            {
                Vector2 current=new Vector2(Map(p.moment,0,maxX,area.xMin,area.xMax),Map(p.p,0,maxY,area.yMin,area.yMax));
                if(previous.HasValue) PixelLine(tex,previous.Value,current,new Color(.15f,.35f,.65f)); Dot(tex,(int)current.x,(int)current.y,4,new Color(.1f,.25f,.55f)); previous=current;
            }
        });
    }

    private float[] UniqueAxialLoads()
    {
        List<float> result=new List<float>();
        foreach(CurvePoint p in curves){bool exists=false;foreach(float v in result)if(Mathf.Abs(v-p.p)<.1f)exists=true;if(!exists)result.Add(p.p);}
        return result.ToArray();
    }

    private delegate void Plotter(Texture2D texture, Rect area);
    private static Texture2D Plot(int width,int height,Color background,Plotter draw)
    {
        Texture2D tex=new Texture2D(width,height,TextureFormat.RGBA32,false); Color[] pixels=new Color[width*height];
        for(int i=0;i<pixels.Length;i++)pixels[i]=background;tex.SetPixels(pixels);
        Rect area=new Rect(35,15,width-48,height-38); PixelLine(tex,new Vector2(area.xMin,area.yMax),new Vector2(area.xMax,area.yMax),Color.black);
        PixelLine(tex,new Vector2(area.xMin,area.yMin),new Vector2(area.xMin,area.yMax),Color.black);draw(tex,area);tex.Apply();return tex;
    }

    private static int Map(float value,float min,float max,float outMin,float outMax)
    {
        if(Mathf.Abs(max-min)<1e-12f)return Mathf.RoundToInt(outMin);return Mathf.RoundToInt(Mathf.Lerp(outMin,outMax,(value-min)/(max-min)));
    }
    private static void Dot(Texture2D tex,int x,int y,int radius,Color color)
    {
        for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)if(dx*dx+dy*dy<=radius*radius)SafePixel(tex,x+dx,y+dy,color);
    }
    private static void SafePixel(Texture2D tex,int x,int y,Color color){if(x>=0&&x<tex.width&&y>=0&&y<tex.height)tex.SetPixel(x,y,color);}
    private static void PixelLine(Texture2D tex,Vector2 a,Vector2 b,Color color)
    {
        int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));
        for(int i=0;i<=steps;i++){Vector2 p=Vector2.Lerp(a,b,(float)i/steps);Dot(tex,Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.y),1,color);}
    }

    private void CaseButton(string value)
    {
        GUI.backgroundColor = selectedCase == value ? new Color(.35f,.85f,1f) : Color.white;
        if (GUILayout.Button(value, GUILayout.Height(30)) && selectedCase != value) { selectedCase = value; RebuildResponse(); }
        GUI.backgroundColor = Color.white;
    }

    private void OnGUI()
    {
        if (!ready) return;
        if(panelStyle==null){panelStyle=new GUIStyle(GUI.skin.window){padding=new RectOffset(12,12,10,10)};titleStyle=new GUIStyle(GUI.skin.label){fontSize=16,fontStyle=FontStyle.Bold};noteStyle=new GUIStyle(GUI.skin.label){fontSize=11,wordWrap=true};}
        GUILayout.BeginArea(new Rect(Screen.width-400,12,388,Screen.height-24),panelStyle);scroll=GUILayout.BeginScrollView(scroll);
        GUILayout.Label("SEMANA 3",titleStyle);GUILayout.Label("Carga viva · sismo · superposición · capacidad HA",noteStyle);GUILayout.Space(6);
        GUILayout.BeginHorizontal();foreach(string c in new[]{"G","Q","EX","EY","R"})CaseButton(c);GUILayout.EndHorizontal();
        if(selectedCase=="R") DrawCombination();
        bool nextDeformed=GUILayout.Toggle(showDeformed,"Deformada amplificada");bool nextForces=GUILayout.Toggle(showForces,"Fuerzas sísmicas y centros de masa");
        GUILayout.Label("Escala deformada: "+deformationScale.ToString("F0")+"×");float nextScale=GUILayout.HorizontalSlider(deformationScale,1,5000);
        if(nextDeformed!=showDeformed||nextForces!=showForces||Mathf.Abs(nextScale-deformationScale)>1f){showDeformed=nextDeformed;showForces=nextForces;deformationScale=nextScale;RebuildResponse();}
        GUILayout.Label("|u| máximo: "+(maxDisplacement*1000).ToString("G4")+" mm\n|giro Z| máximo: "+(maxRotation*1000).ToString("G4")+" mrad",noteStyle);
        if(selectedCase=="EX"||selectedCase=="EY")GUILayout.Label("a = "+Meta("aceleracion_fraccion_g")+" · masa = (G + "+Meta("fraccion_Q_masa")+" Q)/g\nCarga lateral total: "+Meta("corte_total_kN"),noteStyle);
        if(selectedCase=="EX"||selectedCase=="EY") DrawFloorAccelerations();
        GUILayout.Space(8);showCapacity=GUILayout.Toggle(showCapacity,"Mostrar capacidad HA");
        ElementInspector inspector = GetComponent<ElementInspector>();
        if (inspector != null) inspector.Draw(selectedCase);
        if(showCapacity)
        {
            GUILayout.Label("Fiber Section 0,70 × 0,70 m",titleStyle);GUILayout.Label("Hormigón azul · acero rojo\nf'c="+Meta("fc_MPa")+" · fy="+Meta("fy_MPa")+" · As="+Meta("As_mm2"),noteStyle);
            GUILayout.Label(sectionPlot,GUILayout.Width(350),GUILayout.Height(250));
            GUILayout.Label("Momento–curvatura",titleStyle);GUILayout.Label("P = 0, 3376 y 6752 kN",noteStyle);GUILayout.Label(mPhiPlot,GUILayout.Width(350),GUILayout.Height(250));
            GUILayout.Label("Primeros puntos P–M",titleStyle);GUILayout.Label(pmPlot,GUILayout.Width(350),GUILayout.Height(250));
            GUILayout.Label("Capacidad nominal del modelo de sección. La armadura es un supuesto pendiente de confirmar con planos.",noteStyle);
        }
        GUILayout.Space(6);GUILayout.Label("La estructura gris/coloreada original es la geometría sin deformar. Las líneas de color son la respuesta del caso seleccionado.",noteStyle);
        GUILayout.EndScrollView();GUILayout.EndArea();
    }

    private string Meta(string key){string value;return metadata.TryGetValue(key,out value)?value:"?";}
}
