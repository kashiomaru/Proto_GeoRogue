using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

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

    private bool _isLevelUpUIOpen = false; // レベルアップUIが開いているか

    void Start()
    {
        // パネルを隠す
        levelUpPanel?.SetActive(false);
        gameOverPanel?.SetActive(false);

        // リトライボタンのイベントを設定
        retryButton?.onClick.RemoveAllListeners();
        retryButton?.onClick.AddListener(OnRetryButtonClicked);

        // ゲームクリアOKボタンのイベントを設定
        gameClearOkButton?.onClick.RemoveAllListeners();
        gameClearOkButton?.onClick.AddListener(OnGameClearOkButtonClicked);

        // スタートボタンのイベントを設定
        startButton?.onClick.RemoveAllListeners();
        startButton?.onClick.AddListener(OnStartButtonClicked);

        // HPバーの初期化
        UpdateHpBar();
        
        // 経験値バーの初期化
        UpdateExpBar();
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
        if (gameManager != null && gameManager.CurrentMode == GameMode.Boss)
        {
            UpdateBossHpBar();
        }
        
        // レベルアップ可能フラグをチェック
        if (player != null && player.CanLevelUp && _isLevelUpUIOpen == false)
        {
            ShowLevelUpUI();
        }
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
        if (countdownText != null && gameManager != null)
        {
            if (gameManager.CurrentMode != GameMode.Normal)
            {
                if (countdownText.gameObject.activeSelf)
                {
                    countdownText.gameObject.SetActive(false);
                }
                
                return;
            }
            
            // タイマーが表示されていない場合は表示
            if (countdownText.gameObject.activeSelf == false)
            {
                countdownText.gameObject.SetActive(true);
            }
            
            float remainingTime = gameManager.GetCountdownTime();
            
            // MM:SS形式で表示
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            
            countdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    
    // タイマーを非表示にする
    public void HideCountdownTimer()
    {
        countdownText?.gameObject.SetActive(false);
    }
    
    // タイマーを表示する
    public void ShowCountdownTimer()
    {
        countdownText?.gameObject.SetActive(true);
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
    
    // レベルアップUIを表示（内部メソッド）
    private void ShowLevelUpUI()
    {
        if (levelUpManager == null || levelUpPanel == null)
        {
            return;
        }

        _isLevelUpUIOpen = true;
        
        // ゲームを止める
        Time.timeScale = 0f;
        
        // レベルアップオプションを取得
        List<UpgradeData> upgradeOptions = levelUpManager.GetRandomUpgrades(3);
        
        // パネルを表示
        levelUpPanel.SetActive(true);
        
        // オプションをUIに反映
        for (int i = 0; i < optionButtons.Length && i < optionTexts.Length && i < upgradeOptions.Count; i++)
        {
            UpgradeData data = upgradeOptions[i];
            
            // UI反映
            if (optionTexts[i] != null)
            {
                optionTexts[i].text = data.title;
            }
            
            // ボタンのクリックイベントをリセットして登録
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.RemoveAllListeners();
                int index = i; // クロージャ用
                optionButtons[i].onClick.AddListener(() => OnUpgradeButtonClicked(data.type));
            }
        }
    }
    
    private void OnUpgradeButtonClicked(UpgradeType type)
    {
        levelUpManager?.ApplyUpgrade(type);
        player?.LevelUp();
        levelUpPanel?.SetActive(false);
        
        // フラグをリセット
        _isLevelUpUIOpen = false;
        
        // ゲーム再開
        Time.timeScale = 1f;
    }
    
    // レベルアップUIを表示（外部から呼ばれる場合用、互換性のため残す）
    public void ShowLevelUpOptions(List<UpgradeData> upgradeOptions, Action<UpgradeType> onUpgradeSelected)
    {
        _onUpgradeSelected = onUpgradeSelected;
        
        // ゲームを止める
        Time.timeScale = 0f;
        
        levelUpPanel?.SetActive(true);

        // オプションをUIに反映
        for (int i = 0; i < optionButtons.Length && i < optionTexts.Length && i < upgradeOptions.Count; i++)
        {
            UpgradeData data = upgradeOptions[i];
            if (optionTexts[i] != null)
            {
                optionTexts[i].text = data.title;
            }
            optionButtons[i]?.onClick.RemoveAllListeners();
            optionButtons[i]?.onClick.AddListener(() => OnUpgradeButtonClicked(data.type));
        }
    }
    
    // ゲームオーバーUIを表示（タイムスケールは GameOverGameState.OnEnter で設定）
    public void ShowGameOver(Action onRetryClicked)
    {
        _onRetryClicked = onRetryClicked;
        gameOverPanel?.SetActive(true);
    }
    
    public void HideGameOver()
    {
        gameOverPanel?.SetActive(false);
    }

    private void OnRetryButtonClicked()
    {
        gameOverPanel?.SetActive(false);

        // コールバックを呼び出し
        _onRetryClicked?.Invoke();
    }
}
