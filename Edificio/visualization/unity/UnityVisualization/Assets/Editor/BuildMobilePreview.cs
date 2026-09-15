using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class BuildMobilePreview
{
    [MenuItem("View/Vista para celular")]
    public static void ToggleMobile()
    {
        MobileViewerUI.Preview=!MobileViewerUI.Preview;
        OrbitCamera camera=UnityEngine.Object.FindAnyObjectByType<OrbitCamera>();
        if(camera) camera.ResetView();
    }
    public static void Build()
    {
        string path=Path.GetFullPath("Build/MobilePreview/Edificio.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Main.unity"},locationPathName=path,
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        });
        if(report.summary.result!=BuildResult.Succeeded) throw new BuildFailedException("Falló la compilación de prueba móvil");
    }
}
