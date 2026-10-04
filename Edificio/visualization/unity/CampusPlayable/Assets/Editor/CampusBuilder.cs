using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampusBuilder
{
    [MenuItem("Campus/Preparar escena")]
    public static void Scene()
    {
        Directory.CreateDirectory("Assets/Resources");
        if(!File.Exists("Assets/Resources/CampusStandard.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Resources/CampusStandard.mat");
        if(!File.Exists("Assets/Resources/CampusSky.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Skybox/Procedural")),"Assets/Resources/CampusSky.mat");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Campus · geometria principal").AddComponent<CampusWorld>();
        EditorSceneManager.SaveScene(scene,"Assets/Campus.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("CAMPUS_SCENE_CREATED");
    }
    [MenuItem("Campus/Construir Windows")]
    public static void Build()
    {
        try {
            Scene();
            PlayerSettings.companyName="MCOC";
            PlayerSettings.productName="Campus Ingenieria";
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.stripEngineCode=false;
            var output=Path.GetFullPath("Build/Windows/CampusIngenieria.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Campus.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            Debug.Log("CAMPUS_BUILD_RESULT "+report.summary.result+" errors="+report.summary.totalErrors+" bytes="+report.summary.totalSize);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("No se pudo construir Campus: "+report.summary.result);
        }catch(Exception error){Debug.LogException(error);if(Application.isBatchMode)EditorApplication.Exit(2);else throw;}
    }
}
