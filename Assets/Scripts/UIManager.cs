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
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // リトライボタンのイベントを設定
        if (retryButton != null)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetryButtonClicked);
        }

        // ゲームクリアOKボタンのイベントを設定
        if (gameClearOkButton != null)
        {
            gameClearOkButton.onClick.RemoveAllListeners();
            gameClearOkButton.onClick.AddListener(OnGameClearOkButtonClicked);
        }

        // スタートボタンのイベントを設定
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

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
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
    }
    
    // タイマーを表示する
    public void ShowCountdownTimer()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// タイトルを表示する。スタートボタン押下時に onStartClicked が呼ばれる。
    /// </summary>
    public void ShowTitle(Action onStartClicked)
    {
        _onStartClicked = onStartClicked;
        if (titlePanel != null)
        {
            titlePanel.SetActive(true);
        }
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
        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// ゲームクリアを表示する。OKボタン押下時に onOkClicked が呼ばれる。
    /// </summary>
    public void ShowGameClear(Action onOkClicked)
    {
        _onGameClearOkClicked = onOkClicked;
        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(true);
        }
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
        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(false);
        }
    }
    
    /// <summary>
    /// HPバーとEXPバーの親を表示する
    /// </summary>
    public void ShowStatus()
    {
        if (statusParent != null)
        {
            statusParent.SetActive(true);
        }
    }
    
    /// <summary>
    /// HPバーとEXPバーの親を非表示にする
    /// </summary>
    public void HideStatus()
    {
        if (statusParent != null)
        {
            statusParent.SetActive(false);
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
        // LevelUpManagerに処理を委譲
        if (levelUpManager != null)
        {
            levelUpManager.ApplyUpgrade(type);
        }
        
        // プレイヤーのレベルアップ処理を実行
        if (player != null)
        {
            player.LevelUp();
        }
        
        // パネルを隠す
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }
        
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
        
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
        }
        
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
    
    // ゲームオーバーUIを表示（タイムスケールは GameOverGameState.OnEnter で設定）
    public void ShowGameOver(Action onRetryClicked)
    {
        _onRetryClicked = onRetryClicked;
        
        // パネルを表示
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }
    
    public void HideGameOver()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    private void OnRetryButtonClicked()
    {
        // パネルを隠す
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // コールバックを呼び出し
        _onRetryClicked?.Invoke();
    }
}
