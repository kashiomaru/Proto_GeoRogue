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
    
    [Header("HP Bar")]
    [SerializeField] private Slider hpBar;
    [SerializeField] private Player player;
    
    [Header("Exp Bar")]
    [SerializeField] private Slider expBar;
    
    [Header("Level Up")]
    [SerializeField] private LevelUpManager levelUpManager;
    
    // レベルアップ選択時のコールバック
    private Action<UpgradeType> _onUpgradeSelected;
    // リトライ時のコールバック
    private Action _onRetryClicked;
    
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
        
        // レベルアップ可能フラグをチェック
        if (player != null && player.CanLevelUp && !_isLevelUpUIOpen)
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
    
    // レベルアップUIを表示（内部メソッド）
    private void ShowLevelUpUI()
    {
        if (levelUpManager == null || levelUpPanel == null) return;
        
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
                optionTexts[i].text = $"{data.title}\n<size=70%>{data.description}</size>";
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
                optionTexts[i].text = $"{data.title}\n<size=70%>{data.description}</size>";
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
    
    // ゲームオーバーUIを表示
    public void ShowGameOver(Action onRetryClicked)
    {
        _onRetryClicked = onRetryClicked;
        
        // ゲームを止める
        Time.timeScale = 0f;
        
        // パネルを表示
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }
    
    private void OnRetryButtonClicked()
    {
        // ゲームを再開
        Time.timeScale = 1f;
        
        // パネルを隠す
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // コールバックを呼び出し
        _onRetryClicked?.Invoke();
    }
}
