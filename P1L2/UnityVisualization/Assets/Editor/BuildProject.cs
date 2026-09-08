using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildProject
{
    [MenuItem("Build/Edificio Viewer/Construir EXE")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Detén el modo Play antes de construir el EXE.");
            return;
        }
        // Build the same scene used by Play mode, avoiding a second scene with
        // different camera or visualizer settings.
        Scene scene = EditorSceneManager.OpenScene("Assets/Main.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("No se pudo abrir Assets/Main.unity"); return; }
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Main.unity", true) };
        PlayerSettings.productName = "Edificio Viewer";
        PlayerSettings.companyName = "MCOC";
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        string outputPath = Path.GetFullPath("Build/EdificioViewer.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        Debug.Log("=== INICIO BUILD EDIFICIO VIEWER ===");
        Debug.Log("Ruta EXE: " + outputPath);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Main.unity" },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            // Reuse Unity's incremental build cache. Cleaning it on every build
            // forces script serialization and compilation from scratch.
            options = BuildOptions.None
        });
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("=== BUILD COMPLETADO: " + report.summary.totalSize + " bytes ===");
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Edificio Viewer", "Build completado.\n\n" + outputPath, "Aceptar");
        }
        else
        {
            Debug.LogError("=== BUILD FALLIDO: " + report.summary.result + " ===");
            Debug.LogError(report.summary.result + ": " + report.summary.totalErrors + " errores");
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Edificio Viewer", "El Build fallo. Revisa la Console para ver el error.", "Aceptar");
        }
    }
}
