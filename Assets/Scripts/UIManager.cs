using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;

public class UIManager : MonoBehaviour
{
    [Header("Level Up UI")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Button[] optionButtons;
    [SerializeField] private TextMeshProUGUI[] optionTexts;
    
    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button retryButton;
    
    [Header("Title UI")]
    [SerializeField] private GameObject titlePanel; // タイトルのベースパネル
    [SerializeField] private Button startButton;   // Startボタン

    [Header("Game Clear UI")]
    [SerializeField] private GameObject gameClearPanel; // ゲームクリアのベースパネル
    [SerializeField] private Button gameClearOkButton; // タイトルに戻るOKボタン

    [Header("Pause UI")]
    [SerializeField] private GameObject pausePanel;   // ポーズ画面のベースパネル
    [SerializeField] private Button continueButton;   // Continue ボタン
    
    [Header("Status (HP / EXP Bar)")]
    [SerializeField] private GameObject statusParent; // HPバーとEXPバーの親
    [SerializeField] private Slider hpBar;
    [SerializeField] private Player player;
    [SerializeField] private Slider expBar;
    
    [Header("Level Up")]
    [SerializeField] private LevelUpManager levelUpManager;
    
    [Header("Countdown Timer")]
    [SerializeField] private GameManager gameManager; // GameManagerへの参照
    [SerializeField] private TextMeshProUGUI countdownText; // カウントダウン表示用テキスト

    [Header("Stage Name")]
    [SerializeField] private StageNameDisplay stageNameDisplay; // ステージ開始時のステージ名表示

    [Header("Boss HP Bar")]
    [SerializeField] private Slider bossHpBar; // ボス戦時のみ表示するボスHPバー

    // レベルアップ選択時のコールバック
    private Action<UpgradeType> _onUpgradeSelected;
    // リトライ時のコールバック
    private Action _onRetryClicked;
    // ゲームクリアOKクリック時のコールバック
    private Action _onGameClearOkClicked;
    // スタートボタンクリック時のコールバック
    private Action _onStartClicked;
    // ポーズ画面の Continue クリック時のコールバック
    private Action _onContinueClicked;

    void Start()
    {
        // パネルを隠す
        levelUpPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);
        pausePanel?.SetActive(false);

        // リトライボタンのイベントを設定
        retryButton?.onClick.RemoveAllListeners();
        retryButton?.onClick.AddListener(OnRetryButtonClicked);

        // ゲームクリアOKボタンのイベントを設定
        gameClearOkButton?.onClick.RemoveAllListeners();
        gameClearOkButton?.onClick.AddListener(OnGameClearOkButtonClicked);

        // スタートボタンのイベントを設定
        startButton?.onClick.RemoveAllListeners();
        startButton?.onClick.AddListener(OnStartButtonClicked);

        // ポーズの Continue ボタンのイベントを設定
        continueButton?.onClick.RemoveAllListeners();
        continueButton?.onClick.AddListener(OnContinueButtonClicked);

        // HPバーの初期化
        UpdateHpBar();
        
        // 経験値バーの初期化
        UpdateExpBar();

        LockCursor();
    }
    
    void Update()
    {
        // HPバーを更新
        UpdateHpBar();
        
        // 経験値バーを更新
        UpdateExpBar();
        
        // カウントダウンタイマーを更新
        UpdateCountdownTimer();

        // ボスモード時はボスHPバーを更新
        UpdateBossHpBar();
    }
    
    void UpdateHpBar()
    {
        if (hpBar != null && player != null)
        {
            // 現在のHPと最大HPから割合を計算
            float hpRatio = (float)player.CurrentHp / player.MaxHp;
            hpBar.value = hpRatio;
        }
    }
    
    void UpdateExpBar()
    {
        if (expBar != null && player != null)
        {
            // 現在の経験値と次のレベルまでの必要経験値から割合を計算
            int currentExp = player.CurrentExp;
            int nextLevelExp = player.NextLevelExp;

            if (nextLevelExp > 0)
            {
                float expRatio = (float)currentExp / nextLevelExp;
                expBar.value = expRatio;
            }
            else
            {
                expBar.value = 1f; // 最大レベルの場合は満タン表示
            }
        }
    }
    
    void UpdateCountdownTimer()
    {
        // 表示判定は行わず、値の更新のみ。表示・非表示は ShowCountdownTimer / HideCountdownTimer の呼び出しで制御する。
        if (countdownText != null && gameManager != null)
        {
            float remainingTime = gameManager.GetCountdownTime();
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            countdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    /// <summary>タイマーを表示する。呼び出し元（ステート等）で表示タイミングを制御する。</summary>
    public void ShowCountdownTimer()
    {
        countdownText?.gameObject.SetActive(true);
    }

    /// <summary>タイマーを非表示にする。呼び出し元（ステート等）で非表示タイミングを制御する。</summary>
    public void HideCountdownTimer()
    {
        countdownText?.gameObject.SetActive(false);
    }

    /// <summary>
    /// ステージ名を表示する（Normal 開始時などに呼ぶ）。0.5秒表示後アルファアウト。
    /// </summary>
    public void ShowStageName(string stageName)
    {
        stageNameDisplay?.Show(stageName);
    }

    /// <summary>
    /// タイトルを表示する。スタートボタン押下時に onStartClicked が呼ばれる。
    /// </summary>
    public void ShowTitle(Action onStartClicked)
    {
        _onStartClicked = onStartClicked;
        titlePanel?.SetActive(true);
        SetSelectedGameObjectAfterOneFrameAsync(startButton?.gameObject).Forget();
    }

    private void OnStartButtonClicked()
    {
        _onStartClicked?.Invoke();
    }
    
    /// <summary>
    /// タイトルを非表示にする
    /// </summary>
    public void HideTitle()
    {
        titlePanel?.SetActive(false);
    }
    
    /// <summary>
    /// ゲームクリアを表示する。OKボタン押下時に onOkClicked が呼ばれる。
    /// </summary>
    public void ShowGameClear(Action onOkClicked)
    {
        _onGameClearOkClicked = onOkClicked;
        gameClearPanel?.SetActive(true);
        SetSelectedGameObjectAfterOneFrameAsync(gameClearOkButton?.gameObject).Forget();
    }

    private void OnGameClearOkButtonClicked()
    {
        _onGameClearOkClicked?.Invoke();
    }
    
    /// <summary>
    /// ゲームクリアを非表示にする
    /// </summary>
    public void HideGameClear()
    {
        gameClearPanel?.SetActive(false);
    }
    
    /// <summary>
    /// HPバーとEXPバーの親を表示する
    /// </summary>
    public void ShowStatus()
    {
        statusParent?.SetActive(true);
    }
    
    /// <summary>
    /// HPバーとEXPバーの親を非表示にする
    /// </summary>
    public void HideStatus()
    {
        statusParent?.SetActive(false);
    }

    /// <summary>
    /// ボスHPバーを表示する（ボスステート進入時に呼ぶ）
    /// </summary>
    public void ShowBossHpBar()
    {
        if (bossHpBar != null)
        {
            bossHpBar.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// ボスHPバーを非表示にする（ボスステート終了時に呼ぶ）
    /// </summary>
    public void HideBossHpBar()
    {
        if (bossHpBar != null)
        {
            bossHpBar.gameObject.SetActive(false);
        }
    }

    void UpdateBossHpBar()
    {
        if (bossHpBar == null || gameManager?.EnemyManager == null)
        {
            return;
        }

        var boss = gameManager.EnemyManager.GetCurrentBossComponent();
        if (boss != null)
        {
            int max = boss.MaxHp;
            bossHpBar.value = max > 0 ? (float)boss.CurrentHp / max : 0f;
        }
    }
    
    /// <summary>レベルアップUIを表示する。オプション選択時に onSelected が呼ばれる。表示・非表示は呼び出し元（ステート等）で制御する。</summary>
    public void ShowLevelUp(List<UpgradeData> upgradeOptions, Action<UpgradeType> onSelected)
    {
        if (levelUpPanel == null)
            return;

        _onUpgradeSelected = onSelected;
        levelUpPanel.SetActive(true);
        GameObject firstOption = (optionButtons != null && optionButtons.Length > 0) ? optionButtons[0]?.gameObject : null;
        SetSelectedGameObjectAfterOneFrameAsync(firstOption).Forget();

        for (int i = 0; i < optionButtons.Length && i < optionTexts.Length && i < upgradeOptions.Count; i++)
        {
            UpgradeData data = upgradeOptions[i];
            if (optionTexts[i] != null)
                optionTexts[i].text = data.title;
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.RemoveAllListeners();
                UpgradeType type = data.type;
                optionButtons[i].onClick.AddListener(() => OnUpgradeButtonClicked(type));
            }
        }
    }

    /// <summary>レベルアップUIを非表示にする。呼び出し元（ステート等）で非表示タイミングを制御する。</summary>
    public void HideLevelUp()
    {
        levelUpPanel?.SetActive(false);
    }

    private void OnUpgradeButtonClicked(UpgradeType type)
    {
        _onUpgradeSelected?.Invoke(type);
    }

    /// <summary>レベルアップUIを表示（互換性のため残す。通常は LevelUpGameState から ShowLevelUp を使用する。）</summary>
    public void ShowLevelUpOptions(List<UpgradeData> upgradeOptions, Action<UpgradeType> onUpgradeSelected)
    {
        Time.timeScale = 0f;
        ShowLevelUp(upgradeOptions, (UpgradeType type) =>
        {
            onUpgradeSelected(type);
            player?.LevelUp();
            HideLevelUp();
            Time.timeScale = 1f;
        });
    }
    
    // ゲームオーバーUIを表示（タイムスケールは GameOverGameState.OnEnter で設定）
    public void ShowGameOver(Action onRetryClicked)
    {
        _onRetryClicked = onRetryClicked;
        gameOverPanel?.SetActive(true);
        SetSelectedGameObjectAfterOneFrameAsync(retryButton?.gameObject).Forget();
    }
    
    public void HideGameOver()
    {
        gameOverPanel?.SetActive(false);
    }

    /// <summary>
    /// ポーズ画面を表示する。Continue ボタン押下時に onContinueClicked が呼ばれる。
    /// </summary>
    public void ShowPause(Action onContinueClicked)
    {
        _onContinueClicked = onContinueClicked;
        pausePanel?.SetActive(true);
        SetSelectedGameObjectAfterOneFrameAsync(continueButton?.gameObject).Forget();
    }

    /// <summary>
    /// ポーズ画面を非表示にする。
    /// </summary>
    public void HidePause()
    {
        pausePanel?.SetActive(false);
    }

    private void OnContinueButtonClicked()
    {
        _onContinueClicked?.Invoke();
    }

    private void OnRetryButtonClicked()
    {
        gameOverPanel?.SetActive(false);

        // コールバックを呼び出し
        _onRetryClicked?.Invoke();
    }

    /// <summary>
    /// UIをアクティブにした後、1フレーム待ってから EventSystem の選択対象を設定する。
    /// </summary>
    private async UniTaskVoid SetSelectedGameObjectAfterOneFrameAsync(GameObject target)
    {
        await UniTask.Yield();
        if (target != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(target);
        }
    }

    void UpdateCursorState()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
