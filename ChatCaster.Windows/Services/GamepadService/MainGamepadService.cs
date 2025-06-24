using System.Timers;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Главный сервис для работы с геймпадами
/// Координирует работу всех компонентов: мониторинг, детекция, провайдер
/// </summary>
public class MainGamepadService : IGamepadService, IDisposable
{
    // Интерфейсные события
    public event EventHandler<GamepadConnectedEvent>? GamepadConnected;
    public event EventHandler<GamepadDisconnectedEvent>? GamepadDisconnected;
    public event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    // Компоненты
    private readonly IXInputProvider _inputProvider;
    private readonly GamepadMonitor _monitor;
    private readonly ShortcutDetector _detector;
    
    // Мониторинг кнопок
    private System.Timers.Timer? _pollingTimer;
    private readonly object _lockObject = new();
    
    // Состояние
    private bool _isMonitoring = false;
    private bool _isDisposed = false;
    private GamepadShortcut? _currentShortcut;
    private int _pollingRateMs = 16; // ~60 FPS по умолчанию

    public MainGamepadService() : this(new XInputProvider())
    {
        Console.WriteLine("🎮 [MainGamepadService] Конструктор по умолчанию вызван");
    }

    public MainGamepadService(IXInputProvider inputProvider)
    {
        Console.WriteLine("🎮 [MainGamepadService] Конструктор с параметром вызван");
    
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _monitor = new GamepadMonitor(_inputProvider);
        _detector = new ShortcutDetector();

        Console.WriteLine("🎮 [MainGamepadService] Компоненты созданы");

        // Подписываемся на события компонентов
        _monitor.GamepadConnected += OnGamepadConnected;
        _monitor.GamepadDisconnected += OnGamepadDisconnected;
        _detector.ShortcutPressed += OnShortcutPressed;
    
        Console.WriteLine("🎮 [MainGamepadService] События подключены");
    
        // Проверяем доступность XInput
        try 
        {
            bool isAvailable = _inputProvider.IsXInputAvailable();
            Console.WriteLine($"🎮 [MainGamepadService] XInput доступен: {isAvailable}");
        
            if (isAvailable)
            {
                int firstController = _inputProvider.FindFirstConnectedController();
                Console.WriteLine($"🎮 [MainGamepadService] Первый подключенный контроллер: {firstController}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MainGamepadService] Ошибка проверки XInput: {ex.Message}");
        }
    
        Console.WriteLine("🎮 [MainGamepadService] Конструктор завершен");
    }

    #region IGamepadService Implementation

    public bool IsMonitoring
    {
        get
        {
            lock (_lockObject)
            {
                return _isMonitoring;
            }
        }
    }

    public bool IsGamepadConnected => _monitor.IsGamepadConnected;

    public async Task StartMonitoringAsync(GamepadShortcut shortcut)
    {
        if (shortcut == null)
            throw new ArgumentNullException(nameof(shortcut));

        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                if (_isMonitoring)
                {
                    return; // Уже запущен
                }

                if (!_inputProvider.IsXInputAvailable())
                {
                    throw new InvalidOperationException("XInput недоступен в системе");
                }

                if (!_detector.IsValidShortcut(shortcut))
                {
                    throw new ArgumentException("Некорректная комбинация кнопок", nameof(shortcut));
                }

                try
                {
                    _currentShortcut = shortcut;

                    // Запускаем мониторинг подключений
                    _monitor.StartMonitoring(1000); // Проверяем подключение раз в секунду

                    // Запускаем опрос кнопок
                    StartButtonPolling();

                    _isMonitoring = true;
                    Console.WriteLine($"[MainGamepadService] Мониторинг запущен: {shortcut.DisplayText}");
                }
                catch (Exception ex)
                {
                    // Очищаем при ошибке
                    StopMonitoringInternal();
                    throw new InvalidOperationException($"Ошибка запуска мониторинга геймпада: {ex.Message}", ex);
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
                StopMonitoringInternal();
            }
        });
    }

    public async Task<GamepadInfo?> GetConnectedGamepadAsync()
    {
        return await Task.Run(() =>
        {
            return _monitor.GetActiveGamepadInfo();
        });
    }

    public GamepadState? GetCurrentState()
    {
        lock (_lockObject)
        {
            int activeIndex = _monitor.ActiveControllerIndex;
            if (activeIndex < 0)
                return null;

            return _inputProvider.GetControllerState(activeIndex);
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        return await Task.Run(() =>
        {
            if (!_inputProvider.IsXInputAvailable())
                return false;

            int controllerIndex = _inputProvider.FindFirstConnectedController();
            return controllerIndex >= 0;
        });
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Внутренняя остановка мониторинга (без lock)
    /// </summary>
    private void StopMonitoringInternal()
    {
        if (!_isMonitoring)
            return;

        try
        {
            // Останавливаем опрос кнопок
            StopButtonPolling();

            // Останавливаем мониторинг подключений
            _monitor.StopMonitoring();

            // Сбрасываем состояние детектора
            _detector.ResetState();

            _currentShortcut = null;
            _isMonitoring = false;

            Console.WriteLine("[MainGamepadService] Мониторинг остановлен");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainGamepadService] Ошибка при остановке: {ex.Message}");
        }
    }

    /// <summary>
    /// Запускает опрос кнопок геймпада
    /// </summary>
    private void StartButtonPolling()
    {
        StopButtonPolling(); // Останавливаем если уже запущен

        _pollingTimer = new System.Timers.Timer(_pollingRateMs);
        _pollingTimer.Elapsed += OnPollingTimer;
        _pollingTimer.AutoReset = true;
        _pollingTimer.Start();
    }

    /// <summary>
    /// Останавливает опрос кнопок
    /// </summary>
    private void StopButtonPolling()
    {
        if (_pollingTimer != null)
        {
            _pollingTimer.Stop();
            _pollingTimer.Dispose();
            _pollingTimer = null;
        }
    }

    /// <summary>
    /// Обработчик таймера опроса кнопок
    /// </summary>
    private void OnPollingTimer(object? sender, ElapsedEventArgs e)
    {
        if (!_isMonitoring)
            return;

        try
        {
            // Получаем индекс активного геймпада
            int activeIndex = _monitor.ActiveControllerIndex;
            if (activeIndex < 0)
            {
                // Геймпад не подключен - сбрасываем состояние детектора
                _detector.ResetState();
                return;
            }

            // Получаем текущее состояние
            var currentState = _inputProvider.GetControllerState(activeIndex);
            if (currentState == null)
            {
                _detector.ResetState();
                return;
            }

            // Настраиваем детектор если нужно
            if (_currentShortcut != null && _detector.CurrentShortcut != _currentShortcut)
            {
                _detector.ConfigureShortcut(_currentShortcut, activeIndex);
            }

            // Обновляем состояние детектора
            _detector.UpdateState(currentState);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainGamepadService] Ошибка в опросе кнопок: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Обработчик подключения геймпада
    /// </summary>
    private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
    {
        Console.WriteLine($"[MainGamepadService] Геймпад подключен: {e.GamepadInfo.Name}");
        
        // Пробрасываем событие дальше
        GamepadConnected?.Invoke(this, e);

        // Принудительно проверяем подключение для ускорения настройки детектора
        _monitor.ForceConnectionCheck();
    }

    /// <summary>
    /// Обработчик отключения геймпада
    /// </summary>
    private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
    {
        Console.WriteLine($"[MainGamepadService] Геймпад отключен: слот {e.GamepadIndex}");
        
        // Сбрасываем состояние детектора
        _detector.ResetState();
        
        // Пробрасываем событие дальше
        GamepadDisconnected?.Invoke(this, e);
    }

    /// <summary>
    /// Обработчик срабатывания комбинации
    /// </summary>
    private void OnShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        Console.WriteLine($"[MainGamepadService] Комбинация сработала: {e.Shortcut.DisplayText} ({e.HoldTimeMs}ms)");
        
        // Пробрасываем событие дальше
        ShortcutPressed?.Invoke(this, e);
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Устанавливает частоту опроса кнопок
    /// </summary>
    /// <param name="pollingRateMs">Частота в миллисекундах (минимум 10ms)</param>
    public void SetPollingRate(int pollingRateMs)
    {
        lock (_lockObject)
        {
            _pollingRateMs = Math.Max(pollingRateMs, 10); // Минимум 10ms (100 FPS)
            
            // Если мониторинг активен, перезапускаем таймер
            if (_isMonitoring && _pollingTimer != null)
            {
                StartButtonPolling();
            }
        }
    }

    /// <summary>
    /// Получает текущую частоту опроса
    /// </summary>
    public int GetPollingRate()
    {
        lock (_lockObject)
        {
            return _pollingRateMs;
        }
    }

    /// <summary>
    /// Получает статистику детектора
    /// </summary>
    public (bool isPressed, int? holdTime) GetShortcutStatus()
    {
        return (_detector.IsShortcutCurrentlyPressed(), _detector.GetCurrentHoldTime());
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_isDisposed)
        {
            lock (_lockObject)
            {
                StopMonitoringInternal();
                
                // Отписываемся от событий
                _monitor.GamepadConnected -= OnGamepadConnected;
                _monitor.GamepadDisconnected -= OnGamepadDisconnected;
                _detector.ShortcutPressed -= OnShortcutPressed;
                
                // Освобождаем компоненты
                _monitor.Dispose();
                
                _isDisposed = true;
            }
        }
    }

    #endregion
}