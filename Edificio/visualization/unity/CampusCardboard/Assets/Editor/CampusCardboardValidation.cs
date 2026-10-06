using UnityEditor;
using UnityEditor.SceneManagement;

public static class CampusCardboardValidation
{
    // Use -executeMethod CampusCardboardValidation.Run -campus-vr-check (without -quit).
    [MenuItem("Campus/Cardboard/Probar interacción en Play")]
    public static void Run()
    {
        CampusCardboardBuild.Configure();
        EditorSceneManager.OpenScene("Assets/CampusCardboard.unity");
        SessionState.SetBool("CampusVRValidation",true);
        EditorApplication.isPlaying=true;
    }
}
