using System.Timers;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Exceptions;
using SharpDX.XInput;
using System.Runtime.InteropServices;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Оптимизированная реализация сервиса геймпада через XInput для Windows
/// Использует только один активный контроллер для экономии ресурсов
/// Поддерживает расширенные кнопки Elite/Pro контроллеров
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

    // Расширенное XInput API для получения скрытых кнопок
    // Пробуем разные варианты функции
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    private static extern int XInputGetStateEx100(int dwUserIndex, out XInputStateEx pState);
    
    [DllImport("xinput1_3.dll", EntryPoint = "#100")]
    private static extern int XInputGetStateEx100_v13(int dwUserIndex, out XInputStateEx pState);
    
    // Флаг для отключения расширенного режима если он не работает
    private bool _useExtendedInput = true;

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputStateEx
    {
        public uint dwPacketNumber;
        public XInputGamepadEx Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepadEx
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    // Расширенные флаги кнопок (включая скрытые)
    [Flags]
    public enum XInputButtonsEx : ushort
    {
        // Стандартные XInput кнопки
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        Guide = 0x0400,        // Xbox кнопка (скрытая в обычном XInput)
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
        
        // Экспериментальные значения для дополнительных кнопок
        // Эти значения нужно определить опытным путем для конкретных контроллеров
        Paddle1 = 0x0800,     
        Paddle2 = 0x0001,     
        Paddle3 = 0x0002,
        Paddle4 = 0x0004
    }

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
                    var state = GetExtendedGamepadState();
                    gamepads.Add(new GamepadInfo
                    {
                        Index = _activeControllerIndex,
                        Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                        Type = GamepadType.XboxSeries,
                        IsConnected = true,
                        State = state
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
            return GetExtendedGamepadState();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения состояния геймпада {index}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Синхронный метод для быстрого получения состояния активного геймпада
    /// Используется в UI сценариях где async может вызвать deadlock
    /// </summary>
    public GamepadInfo? GetActiveGamepadSync()
    {
        if (_activeController?.IsConnected == true)
        {
            try
            {
                var state = GetExtendedGamepadState();
                return new GamepadInfo
                {
                    Index = _activeControllerIndex,
                    Name = $"Xbox Controller #{_activeControllerIndex + 1}",
                    Type = GamepadType.XboxSeries,
                    IsConnected = true,
                    State = state
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения состояния активного геймпада: {ex.Message}");
            }
        }
        return null;
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
            var currentState = GetExtendedGamepadState();
            var shortcut = _currentConfig.GamepadShortcut;

            bool primaryPressed = currentState.Buttons.TryGetValue(shortcut.PrimaryButton, out bool p) && p;
            bool secondaryPressed = currentState.Buttons.TryGetValue(shortcut.SecondaryButton, out bool s) && s;
            bool shortcutTriggered = shortcut.RequireBothButtons
                ? (primaryPressed && secondaryPressed)
                : (primaryPressed || secondaryPressed);

            bool wasPrimaryPressed = _previousState.Buttons.TryGetValue(shortcut.PrimaryButton, out bool wp) && wp;
            bool wasSecondaryPressed = _previousState.Buttons.TryGetValue(shortcut.SecondaryButton, out bool ws) && ws;
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

            _previousState = currentState;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка проверки shortcut на геймпаде {_activeControllerIndex}: {ex.Message}");
        }
    }

    private GamepadState GetExtendedGamepadState()
    {
        var state = new GamepadState();
        
        if (_activeController?.IsConnected != true)
            return state;

        try
        {
            // Получаем стандартное состояние
            var standardState = _activeController.GetState();
            var gamepad = standardState.Gamepad;
            
            // Пытаемся получить расширенное состояние только если включено
            bool useExtended = false;
            XInputButtonsEx extendedButtons = 0;
            
            if (_useExtendedInput)
            {
                try
                {
                    // Пробуем разные варианты XInputGetStateEx
                    if (XInputGetStateEx100(_activeControllerIndex, out XInputStateEx extendedState) == 0)
                    {
                        extendedButtons = (XInputButtonsEx)extendedState.Gamepad.wButtons;
                        useExtended = true;
                        
                        // Выводим диагностику только если есть нажатые кнопки
                        if (extendedState.Gamepad.wButtons != 0)
                        {
                            Console.WriteLine($"[DEBUG] Расширенные кнопки: 0x{extendedState.Gamepad.wButtons:X4}");
                        }
                    }
                }
                catch (EntryPointNotFoundException)
                {
                    // Пробуем xinput1_3.dll
                    try
                    {
                        if (XInputGetStateEx100_v13(_activeControllerIndex, out XInputStateEx extendedState) == 0)
                        {
                            extendedButtons = (XInputButtonsEx)extendedState.Gamepad.wButtons;
                            useExtended = true;
                            
                            if (extendedState.Gamepad.wButtons != 0)
                            {
                                Console.WriteLine($"[DEBUG] Расширенные кнопки (v1.3): 0x{extendedState.Gamepad.wButtons:X4}");
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Расширенный режим недоступен, отключаем его навсегда
                        _useExtendedInput = false;
                        Console.WriteLine("[INFO] Расширенный XInput недоступен, используем стандартный режим");
                    }
                }
                catch (Exception ex)
                {
                    // Любая другая ошибка - отключаем расширенный режим
                    _useExtendedInput = false;
                    Console.WriteLine($"[INFO] Отключаем расширенный XInput: {ex.Message}");
                }
            }
            
            if (useExtended)
            {
                // Используем расширенное состояние
                state.Buttons[GamepadButton.A] = extendedButtons.HasFlag(XInputButtonsEx.A);
                state.Buttons[GamepadButton.B] = extendedButtons.HasFlag(XInputButtonsEx.B);
                state.Buttons[GamepadButton.X] = extendedButtons.HasFlag(XInputButtonsEx.X);
                state.Buttons[GamepadButton.Y] = extendedButtons.HasFlag(XInputButtonsEx.Y);
                state.Buttons[GamepadButton.LeftBumper] = extendedButtons.HasFlag(XInputButtonsEx.LeftShoulder);
                state.Buttons[GamepadButton.RightBumper] = extendedButtons.HasFlag(XInputButtonsEx.RightShoulder);
                state.Buttons[GamepadButton.Back] = extendedButtons.HasFlag(XInputButtonsEx.Back);
                state.Buttons[GamepadButton.Start] = extendedButtons.HasFlag(XInputButtonsEx.Start);
                state.Buttons[GamepadButton.LeftStick] = extendedButtons.HasFlag(XInputButtonsEx.LeftThumb);
                state.Buttons[GamepadButton.RightStick] = extendedButtons.HasFlag(XInputButtonsEx.RightThumb);
                state.Buttons[GamepadButton.DPadUp] = extendedButtons.HasFlag(XInputButtonsEx.DPadUp);
                state.Buttons[GamepadButton.DPadDown] = extendedButtons.HasFlag(XInputButtonsEx.DPadDown);
                state.Buttons[GamepadButton.DPadLeft] = extendedButtons.HasFlag(XInputButtonsEx.DPadLeft);
                state.Buttons[GamepadButton.DPadRight] = extendedButtons.HasFlag(XInputButtonsEx.DPadRight);
                
                // Расширенные кнопки
                state.Buttons[GamepadButton.Guide] = extendedButtons.HasFlag(XInputButtonsEx.Guide);
                
                // ЭКСПЕРИМЕНТАЛЬНО: Дополнительные кнопки
                state.Buttons[GamepadButton.Paddle1] = extendedButtons.HasFlag(XInputButtonsEx.Paddle1);
                state.Buttons[GamepadButton.Paddle2] = extendedButtons.HasFlag(XInputButtonsEx.Paddle2);
                state.Buttons[GamepadButton.Paddle3] = extendedButtons.HasFlag(XInputButtonsEx.Paddle3);
                state.Buttons[GamepadButton.Paddle4] = extendedButtons.HasFlag(XInputButtonsEx.Paddle4);
            }
            else
            {
                // Fallback на стандартное состояние
                state.Buttons[GamepadButton.A] = gamepad.Buttons.HasFlag(GamepadButtonFlags.A);
                state.Buttons[GamepadButton.B] = gamepad.Buttons.HasFlag(GamepadButtonFlags.B);
                state.Buttons[GamepadButton.X] = gamepad.Buttons.HasFlag(GamepadButtonFlags.X);
                state.Buttons[GamepadButton.Y] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Y);
                state.Buttons[GamepadButton.LeftBumper] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftShoulder);
                state.Buttons[GamepadButton.RightBumper] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightShoulder);
                state.Buttons[GamepadButton.Back] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Back);
                state.Buttons[GamepadButton.Start] = gamepad.Buttons.HasFlag(GamepadButtonFlags.Start);
                state.Buttons[GamepadButton.LeftStick] = gamepad.Buttons.HasFlag(GamepadButtonFlags.LeftThumb);
                state.Buttons[GamepadButton.RightStick] = gamepad.Buttons.HasFlag(GamepadButtonFlags.RightThumb);
                state.Buttons[GamepadButton.DPadUp] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadUp);
                state.Buttons[GamepadButton.DPadDown] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadDown);
                state.Buttons[GamepadButton.DPadLeft] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadLeft);
                state.Buttons[GamepadButton.DPadRight] = gamepad.Buttons.HasFlag(GamepadButtonFlags.DPadRight);
            }
            
            // Триггеры (одинаково для обоих режимов)
            state.Buttons[GamepadButton.LeftTrigger] = gamepad.LeftTrigger > 64;   // 25% нажатия
            state.Buttons[GamepadButton.RightTrigger] = gamepad.RightTrigger > 64; // 25% нажатия
            
            // Оси
            state.Axes[GamepadAxis.LeftStickX] = gamepad.LeftThumbX / 32768.0f;
            state.Axes[GamepadAxis.LeftStickY] = gamepad.LeftThumbY / 32768.0f;
            state.Axes[GamepadAxis.RightStickX] = gamepad.RightThumbX / 32768.0f;
            state.Axes[GamepadAxis.RightStickY] = gamepad.RightThumbY / 32768.0f;
            state.Axes[GamepadAxis.LeftTriggerAxis] = gamepad.LeftTrigger / 255.0f;
            state.Axes[GamepadAxis.RightTriggerAxis] = gamepad.RightTrigger / 255.0f;
            
            state.LastUpdateTime = DateTime.Now;
            return state;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка получения расширенного состояния геймпада: {ex.Message}");
            // Отключаем расширенный режим и fallback на простое состояние
            _useExtendedInput = false;
            return ConvertToGamepadState(_activeController.GetState().Gamepad);
        }
    }

    private static GamepadState ConvertToGamepadState(Gamepad xInputGamepad)
    {
        var state = new GamepadState();
        
        // Обычные кнопки
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
        
        // Триггеры как кнопки (с порогом активации)
        state.Buttons[GamepadButton.LeftTrigger] = xInputGamepad.LeftTrigger > 64;   // 25% нажатия
        state.Buttons[GamepadButton.RightTrigger] = xInputGamepad.RightTrigger > 64; // 25% нажатия
        
        // Оси (включая триггеры как аналоговые значения)
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