using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// UIルートにアタッチし、子以下の全 Selectable（Button 等）の通常色・選択色を一括で設定する。
/// インスペクタで色を変更したあと、インスペクタ下部の「子以下の Selectable に色を適用」ボタンで反映する。
/// </summary>
public class ButtonColorBulkSetter : MonoBehaviour
{
    [Header("一括適用する色")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.964f, 0.964f, 0.964f); // デフォルトの selected に近い値

    /// <summary>
    /// 子階層内の全 Selectable に、設定した通常色・選択色を適用する。
    /// エディタのインスペクタボタンまたはコンテキストメニューから呼ぶ。
    /// </summary>
    public void ApplyToChildSelectables()
    {
        Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
        foreach (Selectable sel in selectables)
        {
            if (sel == null) continue;

            ColorBlock block = sel.colors;
            block.normalColor = normalColor;
            block.selectedColor = selectedColor;
            sel.colors = block;
        }
    }

    [ContextMenu("子以下の Selectable に色を適用")]
    private void ApplyFromContextMenu()
    {
        ApplyToChildSelectables();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ButtonColorBulkSetter))]
public class ButtonColorBulkSetterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(4f);

        if (GUILayout.Button("子以下の Selectable に色を適用"))
        {
            var setter = (ButtonColorBulkSetter)target;
            ApplyAndRecordUndo(setter);
        }
    }

    private static void ApplyAndRecordUndo(ButtonColorBulkSetter setter)
    {
        Selectable[] selectables = setter.GetComponentsInChildren<Selectable>(true);
        if (selectables.Length == 0)
        {
            Debug.Log("[ButtonColorBulkSetter] 子階層に Selectable が見つかりませんでした。", setter);
            return;
        }

        Undo.RecordObjects(selectables, "ButtonColorBulkSetter Apply Colors");
        setter.ApplyToChildSelectables();
        EditorUtility.SetDirty(setter);
        foreach (var sel in selectables)
        {
            if (sel != null)
                EditorUtility.SetDirty(sel);
        }
    }
}
#endif
