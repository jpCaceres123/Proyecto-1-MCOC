using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildMovingPreview
{
    public static void Build()
    {
        var character=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/AmongUs.fbx");
        if(character==null)throw new BuildFailedException("Falta el personaje FBX en Resources.");
        foreach(var renderer in character.GetComponentsInChildren<Renderer>(true))
            Debug.Log("SQ4_MODEL_PART "+renderer.name+" "+renderer.GetType().Name+
                      " material="+string.Join(",",Array.ConvertAll(renderer.sharedMaterials,m=>m?m.name:"null"))+
                      " bounds="+renderer.bounds.size);
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
