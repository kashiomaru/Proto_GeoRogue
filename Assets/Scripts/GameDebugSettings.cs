#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// ゲームのデバッグ設定を一括管理するコンポーネント（エディタ専用）。
/// GameManager に参照を渡し、この GameObject のインスペクタ上だけでデバッグ項目を表示・On/Off できる。
/// 一括 Off のときはすべてのデバッグオプションが無効になる。
/// </summary>
public class GameDebugSettings : MonoBehaviour
{
    [Header("一括 On/Off")]
    [Tooltip("オフのとき、以下すべてのデバッグオプションが無効になります。")]
    [SerializeField] private bool enableDebug = false;

    [Header("初期ゲームモード")]
    [Tooltip("有効時、ゲーム開始時のモードを指定できます。ビルドでは無効時は常にタイトルから開始。")]
    [SerializeField] private GameMode initialGameMode = GameMode.None;

    [Header("カウントダウン")]
    [SerializeField] private bool enableDebugCountdown = false;
    [SerializeField] private float debugCountdownTime = 10f;

    [Header("ボスHP")]
    [SerializeField] private bool enableDebugBossHp = false;
    [SerializeField] private float debugBossHp = 10f;

    [Header("プレイヤーHP（エディタのみ）")]
    [SerializeField] private bool enableDebugPlayerHp = false;
    [SerializeField] private int debugPlayerHp = 1;

    public bool EnableDebug => enableDebug;
    public GameMode InitialGameMode => initialGameMode;
    public bool EnableDebugCountdown => enableDebugCountdown;
    public float DebugCountdownTime => debugCountdownTime;
    public bool EnableDebugBossHp => enableDebugBossHp;
    public float DebugBossHp => debugBossHp;
    public bool EnableDebugPlayerHp => enableDebugPlayerHp;
    public int DebugPlayerHp => debugPlayerHp;
}
#endif
