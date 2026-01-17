using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f; // 回転速度
    
    [Header("Camera Reference")]
    [SerializeField] private Camera playerCamera; // カメラ参照（未設定の場合はMainCameraを自動取得）
    
    [Header("Experience")]
    public int currentExp = 0; // 現在の経験値
    
    [Header("Health")]
    [SerializeField] private int maxHp = 10;
    [SerializeField] private float invincibleDuration = 1.0f; // 無敵時間（秒）
    
    private int _currentHp;
    private float _invincibleTimer = 0f;
    private bool _isInvincible = false;
    
    private float _currentRotationVelocity; // 回転の滑らかさ用
    
    public int CurrentHp => _currentHp;
    public int MaxHp => maxHp;
    public bool IsInvincible => _isInvincible;
    public bool IsDead => _currentHp <= 0;
    
    // 経験値を追加するメソッド
    public void AddExp(int amount)
    {
        currentExp += amount;
    }
    
    private void Start()
    {
        // カメラが未設定の場合、MainCameraを自動取得
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        
        // HP初期化
        _currentHp = maxHp;
    }
    
    private void Update()
    {
        // 無敵時間の更新
        if (_isInvincible)
        {
            _invincibleTimer -= Time.deltaTime;
            if (_invincibleTimer <= 0f)
            {
                _isInvincible = false;
            }
        }
        
        HandleMovement();
    }
    
    // ダメージを受ける
    public void TakeDamage(int damage)
    {
        if (_isInvincible || _currentHp <= 0) return;
        
        _currentHp -= damage;
        if (_currentHp < 0) _currentHp = 0;
        
        // 無敵時間を開始
        if (_currentHp > 0)
        {
            _isInvincible = true;
            _invincibleTimer = invincibleDuration;
        }
    }
    
    // リセット処理
    public void ResetPlayer()
    {
        _currentHp = maxHp;
        _isInvincible = false;
        _invincibleTimer = 0f;
        currentExp = 0;
    }
    
    private void HandleMovement()
    {
        // 新しいInput SystemでWASDキー入力を取得
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        
        float horizontal = 0f;
        float vertical = 0f;
        
        // 左右移動
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            horizontal += 1f;
        
        // 前後移動
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            vertical += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            vertical -= 1f;
        
        // 入力方向を計算
        Vector2 moveInput = new Vector2(horizontal, vertical);
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        bool hasInput = direction.magnitude >= 0.1f;
        
        // 移動処理
        if (hasInput && playerCamera != null)
        {
            // カメラの向きを基準に移動方向を計算
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + playerCamera.transform.eulerAngles.y;
            
            // キャラクターの向きを滑らかに補間
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            
            // 移動ベクトルを作成（カメラ基準）
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            transform.position += moveDir.normalized * moveSpeed * Time.deltaTime;
        }
        else if (hasInput)
        {
            // カメラがない場合は従来通りワールド空間で移動
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            
            // キャラクターの向きを滑らかに補間
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref _currentRotationVelocity, 1.0f / rotationSpeed);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
            
        // 移動
        transform.position += direction * moveSpeed * Time.deltaTime;
        }
    }
    
    // LevelUpManager用のパラメータ取得・設定メソッド
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    
    public void SetMoveSpeed(float value)
    {
        moveSpeed = value;
    }
}
