using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BuildProject
{
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraObject.AddComponent<OrbitCamera>();

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        lightObject.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);

        GameObject visualizer = new GameObject("Building Visualizer");
        BuildingVisualizer component = visualizer.AddComponent<BuildingVisualizer>();
        component.showNodes = true;
        component.showNodeLabels = true;
        component.memberThickness = 0.18f;

        EditorSceneManager.SaveScene(scene, "Assets/Main.unity");
        BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Main.unity" },
            locationPathName = "Build/EdificioViewer.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
    }
}
