using ChatCaster.Core.Models;
using SharpDX.XInput;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Простая реализация XInput провайдера без расширений
/// Использует только стандартные XInput возможности для максимальной совместимости
/// </summary>
public class XInputProvider : IXInputProvider
{
    private readonly Controller[] _controllers;
    private bool _isXInputAvailable = true;

    public XInputProvider()
    {
        // Инициализируем контроллеры для всех 4 слотов
        _controllers = new Controller[4];
        for (int i = 0; i < 4; i++)
        {
            _controllers[i] = new Controller((UserIndex)i);
        }
        
        // Проверяем доступность XInput
        CheckXInputAvailability();
    }

    public bool IsControllerConnected(int controllerIndex)
    {
        if (!IsValidControllerIndex(controllerIndex) || !_isXInputAvailable)
            return false;

        try
        {
            return _controllers[controllerIndex].IsConnected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public GamepadState? GetControllerState(int controllerIndex)
    {
        if (!IsValidControllerIndex(controllerIndex) || !_isXInputAvailable)
            return null;

        try
        {
            var controller = _controllers[controllerIndex];
            if (!controller.IsConnected)
                return null;

            var xinputState = controller.GetState();
            return ConvertToGamepadState(xinputState.Gamepad);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public int FindFirstConnectedController()
    {
        if (!_isXInputAvailable)
            return -1;

        for (int i = 0; i < 4; i++)
        {
            if (IsControllerConnected(i))
                return i;
        }
        
        return -1;
    }

    public GamepadInfo? GetControllerInfo(int controllerIndex)
    {
        if (!IsValidControllerIndex(controllerIndex) || !_isXInputAvailable)
            return null;

        try
        {
            var controller = _controllers[controllerIndex];
            var isConnected = controller.IsConnected;

            return new GamepadInfo
            {
                Index = controllerIndex,
                Name = $"Xbox Controller #{controllerIndex + 1}",
                Type = GamepadType.XboxController,
                IsConnected = isConnected,
                LastUpdateTime = DateTime.Now
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool IsXInputAvailable()
    {
        return _isXInputAvailable;
    }

    /// <summary>
    /// Конвертирует XInput состояние в наше внутреннее представление
    /// </summary>
    private static GamepadState ConvertToGamepadState(Gamepad xinputGamepad)
    {
        var state = new GamepadState();

        // Основные кнопки
        state.SetButtonState(GamepadButton.A, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.A));
        state.SetButtonState(GamepadButton.B, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.B));
        state.SetButtonState(GamepadButton.X, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.X));
        state.SetButtonState(GamepadButton.Y, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.Y));

        // Бамперы
        state.SetButtonState(GamepadButton.LeftBumper, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder));
        state.SetButtonState(GamepadButton.RightBumper, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder));

        // Системные кнопки
        state.SetButtonState(GamepadButton.Back, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.Back));
        state.SetButtonState(GamepadButton.Start, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.Start));

        // Стики (нажатие)
        state.SetButtonState(GamepadButton.LeftStick, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb));
        state.SetButtonState(GamepadButton.RightStick, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb));

        // D-Pad
        state.SetButtonState(GamepadButton.DPadUp, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp));
        state.SetButtonState(GamepadButton.DPadDown, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown));
        state.SetButtonState(GamepadButton.DPadLeft, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft));
        state.SetButtonState(GamepadButton.DPadRight, xinputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight));

        // Триггеры как кнопки (порог активации 25%)
        const byte triggerThreshold = 64; // 25% от 255
        state.SetButtonState(GamepadButton.LeftTrigger, xinputGamepad.LeftTrigger > triggerThreshold);
        state.SetButtonState(GamepadButton.RightTrigger, xinputGamepad.RightTrigger > triggerThreshold);

        // Примечание: Guide кнопка недоступна в стандартном XInput
        // Для неё нужны расширенные API, которые мы сознательно не используем

        return state;
    }

    /// <summary>
    /// Проверяет валидность индекса контроллера
    /// </summary>
    private static bool IsValidControllerIndex(int controllerIndex)
    {
        return controllerIndex >= 0 && controllerIndex < 4;
    }

    /// <summary>
    /// Проверяет доступность XInput в системе
    /// </summary>
    private void CheckXInputAvailability()
    {
        try
        {
            // Пытаемся создать контроллер и проверить его состояние
            var testController = new Controller(UserIndex.One);
            _ = testController.IsConnected; // Просто пытаемся получить статус
            _isXInputAvailable = true;
        }
        catch (Exception)
        {
            _isXInputAvailable = false;
        }
    }
}