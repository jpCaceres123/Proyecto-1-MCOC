using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using StructuralLab;

public static class BuildLaboratory {
    public static void Build() {
        Directory.CreateDirectory("Assets/Scenes");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Laboratorio estructural").AddComponent<LaboratoryApp>();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Laboratorio.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Laboratorio.unity",true)};
        PlayerSettings.companyName="Laboratorio académico";
        PlayerSettings.productName="Laboratorio Estructural LT1 + LT2";
        PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
        foreach(string name in new[]{"Standard","Sprites/Default","Unlit/Texture","Unlit/Color"}) {
            var shader=Shader.Find(name);if(shader==null)throw new Exception("Missing shader "+name);
            bool exists=false;for(int n=0;n<shaders.arraySize;n++)if(shaders.GetArrayElementAtIndex(n).objectReferenceValue==shader)exists=true;
            if(exists)continue;
            int i=shaders.arraySize;shaders.InsertArrayElementAtIndex(i);shaders.GetArrayElementAtIndex(i).objectReferenceValue=shader;
        }
        graphics.ApplyModifiedProperties();
        // Prefer the legacy input API used by the standalone viewer.
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedProperties();}
        AssetDatabase.SaveAssets();
        string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Aplicacion/LaboratorioEstructural.exe"));
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/Laboratorio.unity"},locationPathName=target,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(target),"compilacion.txt"),report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nBytes: "+report.summary.totalSize);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
        Debug.Log("LABORATORY_BUILD_OK "+target);
    }
}
