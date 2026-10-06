using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampusBuilder
{
    [MenuItem("Campus/Cardboard/Preparar escena")]
    public static void Scene()
    {
        CampusCardboardBuild.EnsureShaders();
        Directory.CreateDirectory("Assets/Resources");
        if(!File.Exists("Assets/Resources/CampusStandard.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")),"Assets/Resources/CampusStandard.mat");
        if(!File.Exists("Assets/Resources/CampusSky.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Skybox/Procedural")),"Assets/Resources/CampusSky.mat");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Campus · geometria principal").AddComponent<CampusWorld>();
        EditorSceneManager.SaveScene(scene,"Assets/CampusCardboard.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("CAMPUS_SCENE_CREATED");
    }
}
