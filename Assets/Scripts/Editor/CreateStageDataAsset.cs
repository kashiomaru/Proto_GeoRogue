using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// ステージ用 ScriptableObject アセットをメニューから作成するエディタ拡張。
/// 既存コードには依存しない。
/// </summary>
public static class CreateStageDataAsset
{
    private const string DefaultPath = "Assets/Data/Stages";
    private const string DefaultFileName = "Stage_01";

    [MenuItem("Geo Rogue/Create Default Stage Data")]
    public static void Create()
    {
        // Application.dataPath は "Assets" フォルダの絶対パス
        if (!Directory.Exists(Application.dataPath + "/Data"))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }
        if (!Directory.Exists(Application.dataPath + "/Data/Stages"))
        {
            AssetDatabase.CreateFolder("Assets/Data", "Stages");
        }

        string assetPath = $"{DefaultPath}/{DefaultFileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<StageData>(assetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"StageData は既に存在します: {assetPath}");
            return;
        }

        var stageData = ScriptableObject.CreateInstance<StageData>();
        AssetDatabase.CreateAsset(stageData, assetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = stageData;
        EditorGUIUtility.PingObject(stageData);
        Debug.Log($"StageData を作成しました: {assetPath} （Mesh / Material / Boss Prefab はインスペクタで割り当ててください）");
    }
}
