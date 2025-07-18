using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using Serilog;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Детектор комбинаций кнопок геймпада
/// Отвечает только за анализ нажатий и детекцию shortcut'ов
/// </summary>
public class ShortcutDetector
{
    public event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    private readonly object _lockObject = new();
    private GamepadShortcut? _currentShortcut;
    private GamepadState? _previousState;
    private int _controllerIndex = -1;
    private bool _feedbackShown;

    // Состояние детекции
    private DateTime? _shortcutPressStartTime;
    private bool _shortcutWasPressed;

    public event EventHandler? LongHoldFeedbackTriggered;

    public ShortcutDetector()
    {
    }

    /// <summary>
    /// Настройка детектора на конкретную комбинацию
    /// </summary>
    /// <param name="shortcut">Комбинация кнопок для отслеживания</param>
    /// <param name="controllerIndex">Индекс контроллера</param>
    public void ConfigureShortcut(GamepadShortcut shortcut, int controllerIndex)
    {
        lock (_lockObject)
        {
            _currentShortcut = shortcut ?? throw new ArgumentNullException(nameof(shortcut));
            _controllerIndex = controllerIndex;
            
            // Сбрасываем состояние при изменении настроек
            ResetState();
        }
    }

    /// <summary>
    /// Обновляет состояние геймпада и проверяет комбинации
    /// </summary>
    /// <param name="currentState">Текущее состояние геймпада</param>
    public void UpdateState(GamepadState currentState)
    {
        lock (_lockObject)
        {
            if (_currentShortcut == null)
                return;

            try
            {
                // Проверяем нажата ли комбинация сейчас
                bool isShortcutPressed = _currentShortcut.IsPressed(currentState);
                
                // Проверяем была ли нажата в предыдущем состоянии
                bool wasShortcutPressed = _previousState != null && _currentShortcut.IsPressed(_previousState);

                HandleShortcutStateChange(isShortcutPressed, wasShortcutPressed, currentState);

                // Сохраняем текущее состояние для следующей итерации
                _previousState = CloneGamepadState(currentState);
            }
            catch (Exception ex)
            {
                Log.Information($"[ShortcutDetector] Ошибка при обработке состояния: {ex.Message}");
                ResetState();
            }
        }
    }

    /// <summary>
    /// Сбрасывает состояние детектора
    /// </summary>
    public void ResetState()
    {
        lock (_lockObject)
        {
            _previousState = null;
            _shortcutPressStartTime = null;
            _shortcutWasPressed = false;
        }
    }

    /// <summary>
    /// Получает текущую настроенную комбинацию
    /// </summary>
    public GamepadShortcut? CurrentShortcut
    {
        get
        {
            lock (_lockObject)
            {
                return _currentShortcut;
            }
        }
    }

    /// <summary>
    /// Обрабатывает изменение состояния комбинации
    /// </summary>
    private void HandleShortcutStateChange(bool isPressed, bool wasPressed, GamepadState currentState)
    {
        if (isPressed && !wasPressed)
        {
            // Комбинация только что нажата
            _shortcutPressStartTime = DateTime.Now;
            _shortcutWasPressed = true;
            _feedbackShown = false; // Сброс флага обратной связи
        }
        else if (!isPressed && wasPressed)
        {
            // Комбинация только что отпущена
            if (_shortcutWasPressed && _shortcutPressStartTime.HasValue)
            {
                var holdTime = DateTime.Now - _shortcutPressStartTime.Value;
                var holdTimeMs = (int)holdTime.TotalMilliseconds;

                // Проверяем минимальное время удержания
                if (holdTimeMs >= _currentShortcut!.HoldTimeMs)
                {
                    FireShortcutPressed(currentState, holdTimeMs);
                }
                else
                {
                    Log.Information($"[ShortcutDetector] Комбинация отпущена слишком быстро: {holdTimeMs}ms < {_currentShortcut.HoldTimeMs}ms");
                }
            }

            // Сбрасываем состояние нажатия
            _shortcutPressStartTime = null;
            _shortcutWasPressed = false;
        }
        else if (isPressed && wasPressed)
        {
            // Комбинация всё ещё удерживается - проверяем обратную связь
            CheckLongHoldFeedback();
        }
    }

    private void CheckLongHoldFeedback()
    {
        if (!_shortcutWasPressed || !_shortcutPressStartTime.HasValue || _feedbackShown)
            return;

        var currentHoldTime = (int)(DateTime.Now - _shortcutPressStartTime.Value).TotalMilliseconds;
   
        if (currentHoldTime >= 2000)
        {
            LongHoldFeedbackTriggered?.Invoke(this, EventArgs.Empty);
            _feedbackShown = true;
        }
    }
    /// <summary>
    /// Отправляет событие срабатывания комбинации
    /// </summary>
    private void FireShortcutPressed(GamepadState currentState, int holdTimeMs)
    {
        try
        {
            if (_currentShortcut == null)
                return;

            var eventArgs = new GamepadShortcutPressedEvent
            {
                GamepadIndex = _controllerIndex,
                Shortcut = _currentShortcut,
                GamepadInfo = new GamepadInfo
                {
                    Index = _controllerIndex,
                    Name = $"Xbox Controller #{_controllerIndex + 1}",
                    Type = GamepadType.XboxController,
                    IsConnected = true,
                    LastUpdateTime = DateTime.Now
                },
                CurrentState = CloneGamepadState(currentState),
                HoldTimeMs = holdTimeMs
            };

            ShortcutPressed?.Invoke(this, eventArgs);
            
            Log.Information("[ShortcutDetector] Комбинация сработала: {CurrentShortcutDisplayText}, удержание: {HoldTimeMs}ms", _currentShortcut.DisplayText, holdTimeMs);
        }
        catch (Exception ex)
        {
            Log.Information("[ShortcutDetector] Ошибка при отправке события: {ExMessage}", ex.Message);
        }
    }

    /// <summary>
    /// Создает копию состояния геймпада
    /// </summary>
    private static GamepadState CloneGamepadState(GamepadState original)
    {
        var clone = new GamepadState();
        
        // Копируем все нажатые кнопки
        foreach (var button in original.GetPressedButtons())
        {
            clone.SetButtonState(button, true);
        }
        
        return clone;
    }

    /// <summary>
    /// Проверяет валидность настроек комбинации
    /// </summary>
    public bool IsValidShortcut(GamepadShortcut shortcut)
    {
        // Базовая валидация
        if (shortcut.HoldTimeMs < 0 || shortcut.HoldTimeMs > 10000) // Максимум 10 секунд
            return false;

        // Если требуются обе кнопки, они должны быть разными
        if (shortcut.RequireBothButtons && shortcut.PrimaryButton == shortcut.SecondaryButton)
            return false;

        return true;
    }
    
}