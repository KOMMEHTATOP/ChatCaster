using System.Timers;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Exceptions;
using SharpDX.XInput;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация сервиса геймпада через XInput для Windows
/// Мониторит первый найденный контроллер
/// </summary>
public class GamepadService : IGamepadService, IDisposable
{
    public event EventHandler<GamepadConnectedEvent>? GamepadConnected;
    public event EventHandler<GamepadDisconnectedEvent>? GamepadDisconnected;
    public event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    private System.Timers.Timer? _monitoringTimer;
    private InputConfig? _currentConfig;
    private Controller? _activeController;
    private int _activeControllerIndex = -1;
    private GamepadState _previousState = new();
    private bool _wasConnected;
    private readonly object _lockObject = new();
    private bool _isDisposed;

    public bool IsMonitoring { get; private set; }
    public int ConnectedGamepadCount => _activeController?.IsConnected == true ? 1 : 0;

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
                    _previousState = new GamepadState();
                    _wasConnected = false;
                    _activeController = null;
                    _activeControllerIndex = -1;

                    // Ищем первый подключенный контроллер
                    FindActiveController();

                    // Создаем таймер с частотой из конфигурации
                    _monitoringTimer = new System.Timers.Timer(config.GamepadPollingRateMs);
                    _monitoringTimer.Elapsed += OnTimerElapsed;
                    _monitoringTimer.AutoReset = true;
                    _monitoringTimer.Start();

                    IsMonitoring = true;
                    Console.WriteLine($"Мониторинг геймпада запущен с частотой {config.GamepadPollingRateMs}ms");
                    
                    if (_activeController != null)
                    {
                        Console.WriteLine($"Найден активный контроллер в слоте {_activeControllerIndex}");
                    }
                    else
                    {
                        Console.WriteLine("Контроллеры не найдены, ожидание подключения...");
                    }
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
                // Возвращаем только активный контроллер, если он подключен
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
        if (_activeController == null || _activeControllerIndex != index)
            return null;

        try
        {
            if (!_activeController.IsConnected)
                return null;

            var state = _activeController.GetState();
            return ConvertToGamepadState(state.Gamepad);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка получения состояния геймпада {index}: {ex.Message}");
            return null;
        }
    }

    private void FindActiveController()
    {
        // Ищем первый подключенный контроллер в слотах 0-3
        for (int i = 0; i < 4; i++)
        {
            var controller = new Controller((UserIndex)i);
            if (controller.IsConnected)
            {
                _activeController = controller;
                _activeControllerIndex = i;
                Console.WriteLine($"Найден контроллер в слоте {i}");
                return;
            }
        }

        _activeController = null;
        _activeControllerIndex = -1;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!IsMonitoring || _currentConfig == null)
            return;

        try
        {
            // Если нет активного контроллера, ищем
            if (_activeController == null)
            {
                FindActiveController();
                
                // Если нашли - генерируем событие подключения
                if (_activeController != null)
                {
                    _wasConnected = true;
                    
                    var gamepadInfo = new GamepadInfo
                    {
                        Index = _activeControllerIndex,
                        Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                        Type = GamepadType.XboxSeries,
                        IsConnected = true
                    };

                    GamepadConnected?.Invoke(this, new GamepadConnectedEvent
                    {
                        GamepadIndex = _activeControllerIndex,
                        GamepadInfo = gamepadInfo
                    });

                    Console.WriteLine($"Геймпад {_activeControllerIndex} подключен");
                }
                return;
            }

            // Проверяем состояние активного контроллера
            bool isConnected = _activeController.IsConnected;

            // Если контроллер отключился
            if (!isConnected && _wasConnected)
            {
                _wasConnected = false;

                GamepadDisconnected?.Invoke(this, new GamepadDisconnectedEvent
                {
                    GamepadIndex = _activeControllerIndex,
                    GamepadInfo = new GamepadInfo { Index = _activeControllerIndex, IsConnected = false }
                });

                Console.WriteLine($"Геймпад {_activeControllerIndex} отключен");
                
                // Сбрасываем активный контроллер, будем искать новый в следующем тике
                _activeController = null;
                _activeControllerIndex = -1;
                return;
            }

            // Если контроллер подключен, проверяем shortcut
            if (isConnected)
            {
                if (!_wasConnected)
                {
                    _wasConnected = true;
                    
                    var gamepadInfo = new GamepadInfo
                    {
                        Index = _activeControllerIndex,
                        Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                        Type = GamepadType.XboxSeries,
                        IsConnected = true
                    };

                    GamepadConnected?.Invoke(this, new GamepadConnectedEvent
                    {
                        GamepadIndex = _activeControllerIndex,
                        GamepadInfo = gamepadInfo
                    });

                    Console.WriteLine($"Геймпад {_activeControllerIndex} переподключен");
                }

                CheckShortcutPressed();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка в мониторинге геймпада: {ex.Message}");
        }
    }

    private void CheckShortcutPressed()
    {
        if (_currentConfig?.GamepadShortcut == null || _activeController == null)
            return;

        try
        {
            var state = _activeController.GetState();
            var gamepad = state.Gamepad;
            var shortcut = _currentConfig.GamepadShortcut;

            // Проверяем нажатие комбинации кнопок
            bool primaryPressed = IsButtonPressed(gamepad, shortcut.PrimaryButton);
            bool secondaryPressed = IsButtonPressed(gamepad, shortcut.SecondaryButton);

            bool shortcutTriggered = shortcut.RequireBothButtons 
                ? (primaryPressed && secondaryPressed)
                : (primaryPressed || secondaryPressed);

            // Проверяем, что комбинация была нажата (не была нажата в предыдущем состоянии)
            bool wasPrimaryPressed = IsButtonPressed(_previousState, shortcut.PrimaryButton);
            bool wasSecondaryPressed = IsButtonPressed(_previousState, shortcut.SecondaryButton);

            bool wasShortcutTriggered = shortcut.RequireBothButtons 
                ? (wasPrimaryPressed && wasSecondaryPressed)
                : (wasPrimaryPressed || wasSecondaryPressed);

            // Триггерим событие только при переходе с false на true
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

            // Сохраняем текущее состояние для следующей проверки
            _previousState = ConvertToGamepadState(gamepad);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка проверки shortcut на геймпаде {_activeControllerIndex}: {ex.Message}");
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
            GamepadButton.LeftTrigger => gamepad.LeftTrigger > 128, // XInput триггеры 0-255
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

    private static bool IsButtonPressed(GamepadState state, GamepadButton button)
    {
        if (state.Buttons.TryGetValue(button, out bool isPressed))
            return isPressed;
        return false;
    }

    private static GamepadState ConvertToGamepadState(Gamepad xInputGamepad)
    {
        var state = new GamepadState();

        // Конвертируем кнопки
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

        // Конвертируем оси (нормализуем к диапазону -1.0 до 1.0)
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
            StopMonitoringAsync().Wait();
            _isDisposed = true;
        }
    }
}