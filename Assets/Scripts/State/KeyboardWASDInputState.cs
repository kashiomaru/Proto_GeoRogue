using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 入力モード「KeyboardWASD」：WASD＋矢印キー＋スペース＋Shift で移動・回転・発射を扱う。
/// </summary>
public class KeyboardWASDInputState : IState<Player>
{
    public void OnEnter(Player context) { }

    public void OnUpdate(Player context)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;

        bool isSpacePressed = keyboard.spaceKey.isPressed;
        bool shiftOnlyRotate = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

        context.ApplyMovementInput(horizontal, vertical, isSpacePressed, shiftOnlyRotate);
    }

    public void OnExit(Player context) { }
}
