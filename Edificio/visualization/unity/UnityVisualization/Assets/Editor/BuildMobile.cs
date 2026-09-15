using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Exporta un proyecto Xcode; la firma e instalación se realizan en un Mac.
public static class BuildMobile
{
    [MenuItem("Build/Edificio Viewer/Exportar iOS (iPhone 15)")]
    public static void BuildIOS()
    {
        string destination = Path.GetFullPath("Build/iOS");
        Directory.CreateDirectory(destination);
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            string reason = "Falta iOS Build Support en este editor. No se creó proyecto Xcode ni IPA.";
            File.WriteAllText(Path.Combine(destination, "estado.txt"), reason);
            throw new BuildFailedException(reason);
        }
        PlayerSettings.productName = "Edificio Viewer";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "cl.mcoc.edificio.viewer");
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.targetOSVersionString = "16.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneOnly;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        BuildReport result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Main.unity" }, locationPathName = destination,
            target = BuildTarget.iOS, options = BuildOptions.Development
        });
        File.WriteAllText(Path.Combine(destination, "estado.txt"),
            "Exportación Xcode: " + result.summary.result + "; errores: " + result.summary.totalErrors +
            ". Dispositivo objetivo: iPhone 15 / iOS 26. Falta compilar, firmar y probar en Xcode.");
        if (result.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("Falló la exportación iOS. Revisar el log.");
    }
}
