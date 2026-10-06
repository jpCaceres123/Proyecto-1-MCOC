using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class CampusCardboardBuild
{
    [MenuItem("Campus/Cardboard/Configurar Android")]
    public static void Configure()
    {
        EnsureShaders();
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"cl.mcoc.campus.cardboard");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.Activity;
        PlayerSettings.Android.optimizedFramePacing=false;
        PlayerSettings.Android.forceInternetPermission=true;
        PlayerSettings.insecureHttpOption=InsecureHttpOption.AlwaysAllowed; // LAN-only client validates IP and token.
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
        PlayerSettings.companyName="MCOC";PlayerSettings.productName="Campus Cardboard";
        // This separate mobile project uses the new input backend only.
        var serialized=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input=serialized.FindProperty("activeInputHandler");if(input!=null)input.intValue=1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log("CAMPUS_CARDBOARD_CONFIGURED · SDK inicia manualmente al entrar, no al abrir campus.");
    }
    public static void EnsureShaders()
    {
        Directory.CreateDirectory("Assets/Resources");
        const string asset="Assets/Resources/CampusVRUnlit.mat";
        if(!File.Exists(asset))AssetDatabase.CreateAsset(new Material(Shader.Find("Unlit/Color")),asset);
    }
    [MenuItem("Campus/Cardboard/Construir APK")]
    public static void Build()
    {
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
            throw new InvalidOperationException("Instala Android Build Support, SDK/NDK y OpenJDK para Unity 6000.5.9f1 en Unity Hub.");
        Configure();
        if(!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))
            throw new InvalidOperationException("No fue posible cambiar a Android. Repite el menú después de importar los paquetes.");
        CampusBuilder.Scene();
        Directory.CreateDirectory("Build/Android");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/CampusCardboard.unity"},
            locationPathName="Build/Android/CampusCardboard.apk",target=BuildTarget.Android,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Falló APK: "+report.summary.result);
        using(var sha=System.Security.Cryptography.SHA256.Create()){
            string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Build/Android/CampusCardboard.apk"))).Replace("-","").ToLowerInvariant();
            string geometry=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/Resources/estructura_principal.csv"))).Replace("-","");
            string results=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes("Assets/Resources/inspeccion_estructural.json"))).Replace("-","");
            File.WriteAllText("Build/Android/delivery.json","{\"commit\":\""+SourceCommit()+"\",\"unity\":\""+Application.unityVersion+"\",\"apkSha256\":\""+hash+"\",\"geometrySha256\":\""+geometry+"\",\"resultsSha256\":\""+results+"\",\"physicalDeviceTest\":\"PENDING\"}");
        }
        Debug.Log("CAMPUS_CARDBOARD_APK_READY "+Path.GetFullPath("Build/Android/CampusCardboard.apk"));
    }
    private static string SourceCommit(){try{using(var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git","rev-parse HEAD"){UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true})){string value=p.StandardOutput.ReadToEnd().Trim();p.WaitForExit();return p.ExitCode==0&&value.Length==40?value:"UNAVAILABLE";}}catch{return "UNAVAILABLE";}}
}

#if UNITY_ANDROID
// Patch the generated Gradle project, never overwrite user templates.
public sealed class CampusCardboardGradle : UnityEditor.Android.IPostGenerateGradleAndroidProject
{
    public int callbackOrder { get { return 100; } }
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string build=Path.Combine(path,"build.gradle");
        string content=File.ReadAllText(build);
        const string marker="// Campus Cardboard dependencies";
        if(!content.Contains(marker)){
            int start=content.IndexOf("dependencies {",StringComparison.Ordinal);
            if(start<0)throw new BuildFailedException("No se encontró el bloque dependencies de Gradle.");
            content=content.Insert(start+"dependencies {".Length,"\n    "+marker+
                "\n    implementation 'androidx.appcompat:appcompat:1.6.1'"+
                "\n    implementation 'com.google.android.gms:play-services-vision:20.1.3'"+
                "\n    implementation 'com.google.android.material:material:1.12.0'"+
                "\n    implementation 'com.google.protobuf:protobuf-javalite:3.19.4'\n");
            File.WriteAllText(build,content);
        }
        string properties=Path.GetFullPath(Path.Combine(path,"..","gradle.properties"));
        string settings=File.Exists(properties)?File.ReadAllText(properties):"";
        foreach(string key in new[]{"android.useAndroidX","android.enableJetifier"}){
            settings=System.Text.RegularExpressions.Regex.Replace(settings,"(?m)^"+System.Text.RegularExpressions.Regex.Escape(key)+"=.*(?:\r?\n|$)","");
            settings+="\n"+key+"=true\n";
        }
        File.WriteAllText(properties,settings);
    }
}
#endif
