using System.Collections.Generic;
using System.IO;
using UnityEngine;

// A completed job is a coherent snapshot. Never mix missing variant data with base results.
public static class AnalysisResources
{
    public static string DirectoryPath { get; private set; }
    public static bool IsVariant => !string.IsNullOrEmpty(DirectoryPath);
    private static readonly Dictionary<string, TextAsset> cache = new Dictionary<string, TextAsset>();
    public static void Activate(string directory)
    {
        foreach (var asset in cache.Values) Object.Destroy(asset);
        cache.Clear(); DirectoryPath = directory;
    }
    public static TextAsset Load(string name)
    {
        if (!IsVariant) return Resources.Load<TextAsset>(name);
        TextAsset asset;
        if (cache.TryGetValue(name, out asset)) return asset;
        foreach (string extension in new[] { ".csv", ".json", ".txt" })
        {
            string path = Path.Combine(DirectoryPath, name + extension);
            if (!File.Exists(path)) continue;
            asset = new TextAsset(File.ReadAllText(path)); cache[name] = asset; return asset;
        }
        return null;
    }
}
