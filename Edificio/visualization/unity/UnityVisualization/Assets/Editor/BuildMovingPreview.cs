using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class BuildMovingPreview
{
    public static void Build()
    {
        var args=Environment.GetCommandLineArgs();int k=Array.IndexOf(args,"-movingOutput");
        string path=k>=0?Path.GetFullPath(args[k+1]):Path.GetFullPath("Build/MovingPreview/Edificio.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Main.unity"},locationPathName=path,target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.Development
        });
        if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Carga móvil: compilación fallida");
    }
}
