using System.Timers;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Exceptions;
using SharpDX.XInput;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Оптимизированная реализация сервиса геймпада через XInput для Windows
/// Использует только один активный контроллер для экономии ресурсов
/// </summary>
public class GamepadService : IGamepadService, IDisposable
{
    public event EventHandler<GamepadConnectedEvent>? GamepadConnected;
    public event EventHandler<GamepadDisconnectedEvent>? GamepadDisconnected;
    public event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    private System.Timers.Timer? _monitoringTimer;
    private InputConfig? _currentConfig;
    
    // Оптимизация: отслеживаем только один активный геймпад
    private int _activeControllerIndex = -1;
    private Controller? _activeController;
    private GamepadState? _previousState;
    private bool _wasConnected = false;
    
    private readonly object _lockObject = new();
    private bool _isDisposed;

    public bool IsMonitoring { get; private set; }
    public int ConnectedGamepadCount => (_activeController?.IsConnected == true) ? 1 : 0;

    public async Task StartMonitoringAsync(InputConfig config)
    {
        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                try
                {
                    if (IsMonitoring)
                    {
                        throw new GamepadException("Мониторинг геймпада уже запущен");
                    }

                    _currentConfig = config;
                    // Валидация частоты опроса
                    if (config.GamepadPollingRateMs < 10)
                        config.GamepadPollingRateMs = 10;

                    // Сбрасываем состояние
                    _activeControllerIndex = -1;
                    _activeController = null;
                    _previousState = null;
                    _wasConnected = false;

                    // Ищем первый доступный геймпад
                    FindActiveController();

                    _monitoringTimer = new System.Timers.Timer(config.GamepadPollingRateMs);
                    _monitoringTimer.Elapsed += OnTimerElapsed;
                    _monitoringTimer.AutoReset = true;
                    _monitoringTimer.Start();
                    IsMonitoring = true;
                    Console.WriteLine($"Мониторинг геймпада запущен с частотой {config.GamepadPollingRateMs}ms");
                }
                catch (Exception ex)
                {
                    throw new GamepadException($"Ошибка запуска мониторинга геймпада: {ex.Message}", ex);
                }
            }
        });
    }

    public async Task StopMonitoringAsync()
    {
        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                try
                {
                    if (_monitoringTimer != null)
                    {
                        _monitoringTimer.Stop();
                        _monitoringTimer.Dispose();
                        _monitoringTimer = null;
                    }
                    
                    _activeController = null;
                    _activeControllerIndex = -1;
                    _previousState = null;
                    _wasConnected = false;
                    IsMonitoring = false;
                    Console.WriteLine("Мониторинг геймпада остановлен");
                }
                catch (Exception ex)
                {
                    throw new GamepadException($"Ошибка остановки мониторинга геймпада: {ex.Message}", ex);
                }
            }
        });
    }

    public async Task<IEnumerable<GamepadInfo>> GetConnectedGamepadsAsync()
    {
        return await Task.Run(() =>
        {
            var gamepads = new List<GamepadInfo>();
            try
            {
                if (_activeController?.IsConnected == true)
                {
                    var state = _activeController.GetState();
                    gamepads.Add(new GamepadInfo
                    {
                        Index = _activeControllerIndex,
                        Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                        Type = GamepadType.XboxSeries,
                        IsConnected = true,
                        State = ConvertToGamepadState(state.Gamepad)
                    });
                }
            }
            catch (Exception ex)
            {
                throw new GamepadException($"Ошибка получения списка геймпадов: {ex.Message}", ex);
            }
            return gamepads;
        });
    }

    public GamepadState? GetGamepadState(int index)
    {
        if (_activeControllerIndex != index || _activeController?.IsConnected != true)
            return null;
            
        try
        {
            var state = _activeController.GetState();
            return ConvertToGamepadState(state.Gamepad);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения состояния геймпада {index}: {ex.Message}");
            return null;
        }
    }

    private void FindActiveController()
    {
        // Ищем первый доступный геймпад
        for (int i = 0; i < 4; i++)
        {
            var controller = new Controller((UserIndex)i);
            if (controller.IsConnected)
            {
                _activeController = controller;
                _activeControllerIndex = i;
                _previousState = new GamepadState();
                Console.WriteLine($"Активный геймпад: слот {i}");
                return;
            }
        }
        
        // Геймпады не найдены
        _activeController = null;
        _activeControllerIndex = -1;
        _previousState = null;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!IsMonitoring || _currentConfig == null)
            return;

        try
        {
            // Проверяем активный геймпад
            if (_activeController != null)
            {
                if (_activeController.IsConnected)
                {
                    // Геймпад подключен - проверяем кнопки
                    CheckShortcutPressed();
                    return; // Выходим - все в порядке
                }
                else
                {
                    // Активный геймпад отключился
                    if (_wasConnected)
                    {
                        GamepadDisconnected?.Invoke(this, new GamepadDisconnectedEvent
                        {
                            GamepadIndex = _activeControllerIndex,
                            GamepadInfo = new GamepadInfo { Index = _activeControllerIndex, IsConnected = false }
                        });
                        Console.WriteLine($"Геймпад отключен из слота {_activeControllerIndex}");
                        _wasConnected = false;
                    }
                    
                    _activeController = null;
                    _activeControllerIndex = -1;
                    _previousState = null;
                }
            }
            
            // Ищем новый активный геймпад (только если текущего нет)
            FindActiveController();
            
            // Если нашли новый геймпад - уведомляем
            if (_activeController != null && !_wasConnected)
            {
                GamepadConnected?.Invoke(this, new GamepadConnectedEvent
                {
                    GamepadIndex = _activeControllerIndex,
                    GamepadInfo = new GamepadInfo
                    {
                        Index = _activeControllerIndex,
                        Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                        Type = GamepadType.XboxSeries,
                        IsConnected = true
                    }
                });
                Console.WriteLine($"Геймпад подключен в слот {_activeControllerIndex}");
                _wasConnected = true;
            }
            
            // Если геймпадов нет вообще
            if (_activeController == null && _wasConnected)
            {
                Console.WriteLine("Геймпады не найдены");
                _wasConnected = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в мониторинге геймпада: {ex.Message}");
            // Не останавливаем мониторинг при единичных ошибках
        }
    }

    private void CheckShortcutPressed()
    {
        if (_currentConfig?.GamepadShortcut == null || _activeController == null || _previousState == null)
            return;

        try
        {
            var state = _activeController.GetState();
            var gamepad = state.Gamepad;
            var shortcut = _currentConfig.GamepadShortcut;

            bool primaryPressed = IsButtonPressed(gamepad, shortcut.PrimaryButton);
            bool secondaryPressed = IsButtonPressed(gamepad, shortcut.SecondaryButton);
            bool shortcutTriggered = shortcut.RequireBothButtons
                ? (primaryPressed && secondaryPressed)
                : (primaryPressed || secondaryPressed);

            bool wasPrimaryPressed = _previousState.Buttons.TryGetValue(shortcut.PrimaryButton, out bool p) && p;
            bool wasSecondaryPressed = _previousState.Buttons.TryGetValue(shortcut.SecondaryButton, out bool s) && s;
            bool wasShortcutTriggered = shortcut.RequireBothButtons
                ? (wasPrimaryPressed && wasSecondaryPressed)
                : (wasPrimaryPressed || wasSecondaryPressed);

            if (shortcutTriggered && !wasShortcutTriggered)
            {
                ShortcutPressed?.Invoke(this, new GamepadShortcutPressedEvent
                {
                    GamepadIndex = _activeControllerIndex,
                    Shortcut = shortcut,
                    GamepadInfo = new GamepadInfo
                    {
                        Index = _activeControllerIndex,
                        Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                        IsConnected = true
                    }
                });
                Console.WriteLine($"Геймпад {_activeControllerIndex}: нажата комбинация {shortcut.PrimaryButton}+{shortcut.SecondaryButton}");
            }

            _previousState = ConvertToGamepadState(gamepad);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки shortcut на геймпаде {_activeControllerIndex}: {ex.Message}");
        }
    }

    private static bool IsButtonPressed(Gamepad gamepad, GamepadButton button)
    {
        return button switch
        {
            GamepadButton.A => gamepad.Buttons.HasFlag(GamepadButtonFlags.A),
            GamepadButton.B => gamepad.Buttons.HasFlag(GamepadButtonFlags.B),
            GamepadButton.X => gamepad.Buttons.HasFlag(GamepadButtonFlags.X),
            GamepadButton.Y => gamepad.Buttons.HasFlag(GamepadButtonFlags.Y),
            GamepadButton.LeftBumper => gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder),
            GamepadButton.RightBumper => gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder),
            GamepadButton.LeftTrigger => gamepad.LeftTrigger > 128,
            GamepadButton.RightTrigger => gamepad.RightTrigger > 128,
            GamepadButton.Back => gamepad.Buttons.HasFlag(GamepadButtonFlags.Back),
            GamepadButton.Start => gamepad.Buttons.HasFlag(GamepadButtonFlags.Start),
            GamepadButton.LeftStick => gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb),
            GamepadButton.RightStick => gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb),
            GamepadButton.DPadUp => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp),
            GamepadButton.DPadDown => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown),
            GamepadButton.DPadLeft => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft),
            GamepadButton.DPadRight => gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight),
            _ => false
        };
    }

    private static GamepadState ConvertToGamepadState(Gamepad xInputGamepad)
    {
        var state = new GamepadState();
        state.Buttons[GamepadButton.A] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.A);
        state.Buttons[GamepadButton.B] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.B);
        state.Buttons[GamepadButton.X] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.X);
        state.Buttons[GamepadButton.Y] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
        state.Buttons[GamepadButton.LeftBumper] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
        state.Buttons[GamepadButton.RightBumper] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
        state.Buttons[GamepadButton.Back] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.Back);
        state.Buttons[GamepadButton.Start] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
        state.Buttons[GamepadButton.LeftStick] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
        state.Buttons[GamepadButton.RightStick] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
        state.Buttons[GamepadButton.DPadUp] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
        state.Buttons[GamepadButton.DPadDown] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
        state.Buttons[GamepadButton.DPadLeft] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
        state.Buttons[GamepadButton.DPadRight] = xInputGamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);
        state.Axes[GamepadAxis.LeftStickX] = xInputGamepad.LeftThumbX / 32768.0f;
        state.Axes[GamepadAxis.LeftStickY] = xInputGamepad.LeftThumbY / 32768.0f;
        state.Axes[GamepadAxis.RightStickX] = xInputGamepad.RightThumbX / 32768.0f;
        state.Axes[GamepadAxis.RightStickY] = xInputGamepad.RightThumbY / 32768.0f;
        state.Axes[GamepadAxis.LeftTriggerAxis] = xInputGamepad.LeftTrigger / 255.0f;
        state.Axes[GamepadAxis.RightTriggerAxis] = xInputGamepad.RightTrigger / 255.0f;
        state.LastUpdateTime = DateTime.Now;
        return state;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            // В Dispose можно использовать GetAwaiter().GetResult()
            StopMonitoringAsync().GetAwaiter().GetResult();
            _isDisposed = true;
        }
    }
}