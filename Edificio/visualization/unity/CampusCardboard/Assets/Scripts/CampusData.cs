using System.IO;
using UnityEngine;

// Static revision survives scene reload. A variant never falls back to missing base files.
public static class CampusData
{
    public static string DirectoryPath { get; private set; }
    public static void Activate(string directory){DirectoryPath=directory;}
    public static TextAsset Load(string name){if(string.IsNullOrEmpty(DirectoryPath))return Resources.Load<TextAsset>(name);
        foreach(string ext in new[]{".json",".csv"}){string path=Path.Combine(DirectoryPath,name+ext);if(File.Exists(path))return new TextAsset(File.ReadAllText(path));}
        throw new FileNotFoundException("Variante incompleta: "+name);}
}
