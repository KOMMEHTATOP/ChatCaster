using System.Timers;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using Serilog;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Мониторит подключение и отключение геймпадов
/// Отвечает только за отслеживание соединения, не за кнопки
/// </summary>
public class GamepadMonitor : IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<GamepadMonitor>();

    public event EventHandler<GamepadEvent>? GamepadEvent;

    private readonly IXInputProvider _inputProvider;
    private System.Timers.Timer? _connectionTimer;
    private readonly object _lockObject = new();
    private bool _isDisposed;

    // Состояние мониторинга
    private int _activeControllerIndex = -1;
    private bool _wasConnected;
    private bool _isMonitoring;

    // Настройки
    private int _connectionCheckIntervalMs = 1000; // Проверяем подключение раз в секунду

    public GamepadMonitor(IXInputProvider inputProvider)
    {
        _inputProvider = inputProvider ?? throw new ArgumentNullException(nameof(inputProvider));
    }

    /// <summary>
    /// Индекс активного геймпада (-1 если не подключен)
    /// </summary>
    public int ActiveControllerIndex
    {
        get
        {
            lock (_lockObject)
            {
                return _activeControllerIndex;
            }
        }
    }

    /// <summary>
    /// Проверяет подключен ли геймпад
    /// </summary>
    public bool IsGamepadConnected
    {
        get
        {
            lock (_lockObject)
            {
                return _activeControllerIndex >= 0 && _wasConnected;
            }
        }
    }

    /// <summary>
    /// Запускает мониторинг подключений
    /// </summary>
    /// <param name="connectionCheckIntervalMs">Интервал проверки подключения в миллисекундах</param>
    public void StartMonitoring(int connectionCheckIntervalMs = 1000)
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

            _connectionCheckIntervalMs = Math.Max(connectionCheckIntervalMs, 100); // Минимум 100ms

            // Сбрасываем состояние
            _activeControllerIndex = -1;
            _wasConnected = false;

            // Проверяем текущее состояние
            CheckConnectionStatus();

            // Запускаем таймер
            _connectionTimer = new System.Timers.Timer(_connectionCheckIntervalMs);
            _connectionTimer.Elapsed += OnConnectionCheckTimer;
            _connectionTimer.AutoReset = true;
            _connectionTimer.Start();

            _isMonitoring = true;
        }
    }

    /// <summary>
    /// Останавливает мониторинг подключений
    /// </summary>
    public void StopMonitoring()
    {
        lock (_lockObject)
        {
            if (!_isMonitoring)
            {
                return; // Уже остановлен
            }

            // Останавливаем и освобождаем таймер
            if (_connectionTimer != null)
            {
                _connectionTimer.Stop();
                _connectionTimer.Dispose();
                _connectionTimer = null;
            }

            // Если геймпад был подключен, отправляем событие отключения
            if (_wasConnected && _activeControllerIndex >= 0)
            {
                FireGamepadDisconnected(_activeControllerIndex);
            }

            // Сбрасываем состояние
            _activeControllerIndex = -1;
            _wasConnected = false;
            _isMonitoring = false;
        }
    }

    /// <summary>
    /// Получает информацию о текущем активном геймпаде
    /// </summary>
    public GamepadInfo? GetActiveGamepadInfo()
    {
        lock (_lockObject)
        {
            if (_activeControllerIndex < 0)
                return null;

            return _inputProvider.GetControllerInfo(_activeControllerIndex);
        }
    }

    /// <summary>
    /// Принудительная проверка подключения (синхронная)
    /// </summary>
    public void ForceConnectionCheck()
    {
        lock (_lockObject)
        {
            if (_isMonitoring)
            {
                CheckConnectionStatus();
            }
        }
    }

    /// <summary>
    /// Обработчик таймера проверки подключения
    /// </summary>
    private void OnConnectionCheckTimer(object? sender, ElapsedEventArgs e)
    {
        if (!_isMonitoring)
            return;

        try
        {
            lock (_lockObject)
            {
                CheckConnectionStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при проверке подключения геймпада");
        }
    }

    /// <summary>
    /// Проверяет текущее состояние подключения и отправляет события
    /// </summary>
    private void CheckConnectionStatus()
    {
        // Если у нас есть активный геймпад, проверяем его
        if (_activeControllerIndex >= 0)
        {
            bool isStillConnected = _inputProvider.IsControllerConnected(_activeControllerIndex);

            if (isStillConnected)
            {
                // Геймпад всё ещё подключен - ничего не делаем
                return;
            }

                // Активный геймпад отключился
                if (_wasConnected)
                {
                    FireGamepadDisconnected(_activeControllerIndex);
                    _wasConnected = false;
                }

                _activeControllerIndex = -1;
            
        }

        // Ищем новый геймпад (только если текущего нет)
        int newControllerIndex = _inputProvider.FindFirstConnectedController();

        if (newControllerIndex >= 0)
        {
            // Нашли новый геймпад
            _activeControllerIndex = newControllerIndex;

            if (!_wasConnected) // Отправляем событие только при новом подключении
            {
                FireGamepadConnected(newControllerIndex);
                _wasConnected = true;
            }
        }
        else
        {
            // Геймпадов не найдено
            if (_wasConnected)
            {
                _wasConnected = false;
            }
        }
    }

    /// <summary>
    /// Отправляет событие подключения геймпада
    /// </summary>
    private void FireGamepadConnected(int controllerIndex)
    {
        try
        {
            var gamepadInfo = _inputProvider.GetControllerInfo(controllerIndex);
            if (gamepadInfo != null)
            {
                var eventArgs = new GamepadEvent
                {
                    EventType = GamepadEventType.Connected,
                    GamepadIndex = controllerIndex,
                    GamepadInfo = gamepadInfo
                };

                GamepadEvent?.Invoke(this, eventArgs);
                _logger.Information("Геймпад подключен: слот {Slot}", controllerIndex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при отправке события подключения геймпада");
        }
    }
    
    /// <summary>
    /// Отправляет событие отключения геймпада
    /// </summary>
    private void FireGamepadDisconnected(int controllerIndex)
    {
        try
        {
            var eventArgs = new GamepadEvent
            {
                EventType = GamepadEventType.Disconnected,
                GamepadIndex = controllerIndex,
                GamepadInfo = new GamepadInfo
                {
                    Index = controllerIndex,
                    IsConnected = false
                }
            };

            GamepadEvent?.Invoke(this, eventArgs);
            _logger.Information("Геймпад отключен: слот {Slot}", controllerIndex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при отправке события отключения геймпада");
        }
    }
    
    public void Dispose()
    {
        if (!_isDisposed)
        {
            StopMonitoring();
            _isDisposed = true;
        }
    }
}