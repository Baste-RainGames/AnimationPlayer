using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ReserializeEverything
{
    [MenuItem("Commands/Assets/Reserialize/Reserialize everything!")]
    public static void Reserialize()
    {
        ReserializeAll("EVERYTHING", null, null);
    }

    private static void ReserializeAll(string displayName, string folder, string typeSearchName)
    {
        string[] allAssetPaths;
        if (typeSearchName == null)
        {
            if (folder == null)
                allAssetPaths = AssetDatabase.GetAllAssetPaths();
            else
                allAssetPaths = AssetDatabase.FindAssets("", new[] {folder}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }
        else
        {
            if (folder == null)
                allAssetPaths = AssetDatabase.FindAssets("t:" + typeSearchName).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            else
                allAssetPaths = AssetDatabase.FindAssets("t:" + typeSearchName, new[] {folder}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }


        var assetCount     = allAssetPaths.Length;
        var assetsEachStep = Mathf.Max(1, assetCount / 15);

        string[] workPath = new string[assetsEachStep];

        var reserialize = assetCount < 50 ||
                          EditorUtility.DisplayDialog("Are you sure?",
                              $"This takes a while, you're reserializing {assetCount} assets!",
                              $"Reserialize {displayName}");
        if (reserialize)
        {
            try
            {
                for (int i = 0; i < assetCount; i += assetsEachStep)
                {
                    var cancel = EditorUtility.DisplayCancelableProgressBar("Reserializing",
                        $"Reserializing asset {(i + 1)} out of {assetCount}",
                        i / (float) assetCount);
                    if (cancel)
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    var assetsThisStep = assetsEachStep;
                    if (assetCount <= i + assetsEachStep)
                    {
                        assetsThisStep = assetCount - i;
                        workPath       = new string[assetsThisStep];
                    }

                    for (int j = 0; j < assetsThisStep; j++)
                    {
                        workPath[j] = allAssetPaths[i + j];
                    }

                    AssetDatabase.ForceReserializeAssets(workPath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        AssetDatabase.SaveAssets();
    }
}