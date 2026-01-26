using System.Collections.Generic;

/// <summary>
/// 汎用的なステートマシンクラス
/// </summary>
/// <typeparam name="TState">ステートの型（enumなど）</typeparam>
/// <typeparam name="TContext">ステートマシンのコンテキスト型</typeparam>
public class StateMachine<TState, TContext>
{
    private Dictionary<TState, IState<TContext>> _states;
    private IState<TContext> _currentState;
    private TState _currentStateKey;
    private TContext _context;
    
    /// <summary>
    /// 現在のステートキー
    /// </summary>
    public TState CurrentStateKey => _currentStateKey;
    
    /// <summary>
    /// 現在のステート
    /// </summary>
    public IState<TContext> CurrentState => _currentState;
    
    public StateMachine(TContext context)
    {
        _context = context;
        _states = new Dictionary<TState, IState<TContext>>();
    }
    
    /// <summary>
    /// ステートを登録する
    /// </summary>
    public void RegisterState(TState key, IState<TContext> state)
    {
        _states[key] = state;
    }
    
    /// <summary>
    /// ステートを切り替える
    /// </summary>
    public void ChangeState(TState newStateKey)
    {
        // 同じステートへの切り替えは無視
        if (EqualityComparer<TState>.Default.Equals(_currentStateKey, newStateKey))
        {
            return;
        }
        
        // 現在のステートから出る
        if (_currentState != null)
        {
            _currentState.OnExit(_context);
        }
        
        // 新しいステートに切り替え
        if (_states.TryGetValue(newStateKey, out IState<TContext> newState))
        {
            _currentState = newState;
            _currentStateKey = newStateKey;
            _currentState.OnEnter(_context);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"StateMachine: State '{newStateKey}' is not registered.");
        }
    }
    
    /// <summary>
    /// ステートマシンを更新する
    /// </summary>
    public void Update()
    {
        if (_currentState != null)
        {
            _currentState.OnUpdate(_context);
        }
    }
    
    /// <summary>
    /// ステートマシンを初期化する（最初のステートを設定）
    /// </summary>
    public void Initialize(TState initialStateKey)
    {
        ChangeState(initialStateKey);
    }
}
