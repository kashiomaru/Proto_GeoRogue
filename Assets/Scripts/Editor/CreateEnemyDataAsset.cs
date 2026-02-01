using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 敵データ用 ScriptableObject アセットをメニューから作成するエディタ拡張。
/// </summary>
public static class CreateEnemyDataAsset
{
    private const string DefaultPath = "Assets/Data/EnemyDatas";
    private const string DefaultFileName = "EnemyData_Default";

    [MenuItem("Geo Rogue/Create Default Enemy Data")]
    public static void Create()
    {
        if (!Directory.Exists(Application.dataPath + "/Data"))
        {
            AssetDatabase.CreateFolder("Assets", "Data");
        }
        if (!Directory.Exists(Application.dataPath + "/Data/EnemyDatas"))
        {
            AssetDatabase.CreateFolder("Assets/Data", "EnemyDatas");
        }

        string assetPath = $"{DefaultPath}/{DefaultFileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(assetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"EnemyData は既に存在します: {assetPath}");
            return;
        }

        var enemyData = ScriptableObject.CreateInstance<EnemyData>();
        AssetDatabase.CreateAsset(enemyData, assetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = enemyData;
        EditorGUIUtility.PingObject(enemyData);
        Debug.Log($"EnemyData を作成しました: {assetPath} （Mesh / Material はインスペクタで割り当ててください）");
    }
}
