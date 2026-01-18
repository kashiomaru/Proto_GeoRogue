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
    
    // レベルアップ選択時のコールバック
    private Action<UpgradeType> _onUpgradeSelected;
    // リトライ時のコールバック
    private Action _onRetryClicked;

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
    }
    
    void Update()
    {
        // HPバーを更新
        UpdateHpBar();
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
    
    // レベルアップUIを表示
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
    
    private void OnUpgradeButtonClicked(UpgradeType type)
    {
        // コールバックを呼び出し
        _onUpgradeSelected?.Invoke(type);
        
        // パネルを隠す
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }
        
        // ゲーム再開
        Time.timeScale = 1f;
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
