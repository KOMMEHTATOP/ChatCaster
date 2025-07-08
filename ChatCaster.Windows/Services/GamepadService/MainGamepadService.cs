using System.Timers;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using Serilog;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Главный сервис для работы с геймпадами
/// Координирует работу всех компонентов: мониторинг, детекция, провайдер
/// </summary>
public class MainGamepadService : IGamepadService, IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<MainGamepadService>();

    // Интерфейсные события
    public event EventHandler<GamepadEvent>? GamepadEvent;
    public event EventHandler<GamepadShortcutPressedEvent>? ShortcutPressed;

    // Компоненты
    private readonly IXInputProvider _inputProvider;
    private readonly GamepadMonitor _monitor;
    private readonly ShortcutDetector _detector;
    
    // Мониторинг кнопок
    private System.Timers.Timer? _pollingTimer;
    private readonly object _lockObject = new();
    
    // Состояние
    private bool _isMonitoring;
    private bool _isDisposed;
    private GamepadShortcut? _currentShortcut;
    private readonly int _pollingRateMs; 
    
    public MainGamepadService(IXInputProvider inputProvider, IConfigurationService configurationService)
    {
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
        _monitor = new GamepadMonitor(_inputProvider);
        _detector = new ShortcutDetector();
        _pollingRateMs = configurationService.CurrentConfig.Input.GamepadPollingRateMs;

        
        // Подписываемся на события компонентов
        _monitor.GamepadEvent += OnGamepadEvent;
        _detector.ShortcutPressed += OnShortcutPressed;
        
        // Проверяем доступность XInput
        try 
        {
            bool isAvailable = _inputProvider.IsXInputAvailable();
        
            if (isAvailable)
            {
                _inputProvider.FindFirstConnectedController();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка проверки XInput");
        }
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
                    _logger.Information("Мониторинг геймпада запущен: {Shortcut}", shortcut.DisplayText);
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
        return await Task.Run(() => _monitor.GetActiveGamepadInfo());
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
    
    #region Public Properties для доступа к компонентам

    /// <summary>
    /// Доступ к детектору комбинаций для подписки на события
    /// </summary>
    public ShortcutDetector ShortcutDetector => _detector;

    #endregion

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
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при остановке мониторинга геймпада");
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
            _logger.Error(ex, "Ошибка в опросе кнопок геймпада");
        }
    }

    #endregion

    #region Event Handlers

    
    /// <summary>
    /// Обработчик событий геймпада (подключение/отключение)
    /// </summary>
    private void OnGamepadEvent(object? sender, GamepadEvent e)
    {
        // Пробрасываем событие дальше
        GamepadEvent?.Invoke(this, e);

        switch (e.EventType)
        {
            case GamepadEventType.Connected:
                // Принудительно проверяем подключение для ускорения настройки детектора
                _monitor.ForceConnectionCheck();
                break;
        
            case GamepadEventType.Disconnected:
                // Сбрасываем состояние детектора
                _detector.ResetState();
                break;
        }
    }
    
    /// <summary>
    /// Обработчик срабатывания комбинации
    /// </summary>
    private void OnShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        // Пробрасываем событие дальше
        ShortcutPressed?.Invoke(this, e);
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
                _monitor.GamepadEvent -= OnGamepadEvent;
                _detector.ShortcutPressed -= OnShortcutPressed;
                
                // Освобождаем компоненты
                _monitor.Dispose();
                
                _isDisposed = true;
            }
        }
    }

    #endregion
}