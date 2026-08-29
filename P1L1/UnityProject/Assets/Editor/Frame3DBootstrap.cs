#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Frame3DBootstrap
{
    static Frame3DBootstrap()
    {
        EditorApplication.delayCall += EnsureViewerObject;
    }

    private static void EnsureViewerObject()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.path.EndsWith("Frame3D.unity")) return;
        if (Object.FindFirstObjectByType<OpenSeesViewer>() != null) return;

        GameObject viewer = new GameObject("OpenSees 3D Frame");
        viewer.AddComponent<OpenSeesViewer>();
        Undo.RegisterCreatedObjectUndo(viewer, "Create OpenSees 3D Frame");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
#endif
