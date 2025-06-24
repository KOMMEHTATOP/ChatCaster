using ChatCaster.Core.Models;
using ChatCaster.Windows.Interfaces;
using ChatCaster.Windows.Services.GamepadService;
using ChatCaster.Windows.Utilities;

namespace ChatCaster.Windows.Managers
{
    /// <summary>
    /// Менеджер захвата комбинаций геймпада
    /// </summary>
    public sealed class GamepadCaptureManager : ICaptureManager<GamepadShortcut>
    {
        #region Events

        /// <summary>
        /// Событие успешного захвата комбинации геймпада
        /// </summary>
        public event Action<GamepadShortcut>? CaptureCompleted;

        /// <summary>
        /// Событие таймаута захвата
        /// </summary>
        public event Action? CaptureTimeout;

        /// <summary>
        /// Событие изменения статуса захвата
        /// </summary>
        public event Action<string>? StatusChanged;

        /// <summary>
        /// Событие ошибки захвата
        /// </summary>
        public event Action<string>? CaptureError;

        #endregion

        #region Private Fields

        private readonly MainGamepadService _gamepadService;
        private readonly InputCaptureTimer _captureTimer;
        private GamepadCaptureService? _gamepadCaptureService;
        private bool _isDisposed;

        #endregion

        #region Constructor

        public GamepadCaptureManager(MainGamepadService gamepadService)
        {
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            _captureTimer = new InputCaptureTimer();
            
            // Подписываемся на события таймера
            _captureTimer.TimerExpired += OnCaptureTimerExpired;
        }

        #endregion

        #region ICaptureManager Implementation

        /// <summary>
        /// Активен ли процесс захвата
        /// </summary>
        public bool IsCapturing => _captureTimer.IsRunning;

        /// <summary>
        /// Доступен ли геймпад для захвата
        /// </summary>
        public bool IsAvailable { get; private set; } = true;

        /// <summary>
        /// Начинает процесс захвата комбинации геймпада
        /// </summary>
        /// <param name="timeoutSeconds">Время ожидания в секундах</param>
        public async Task StartCaptureAsync(int timeoutSeconds)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(GamepadCaptureManager));

            if (IsCapturing)
                throw new InvalidOperationException("Захват уже активен");

            try
            {
                // Проверяем доступность геймпада
                var gamepad = await _gamepadService.GetConnectedGamepadAsync();
                if (gamepad == null)
                {
                    CaptureError?.Invoke("Геймпад не подключен");
                    return;
                }

                // Создаем сервис захвата если еще не создан
                if (_gamepadCaptureService == null)
                {
                    _gamepadCaptureService = new GamepadCaptureService(_gamepadService);
                    _gamepadCaptureService.ShortcutCaptured += OnGamepadShortcutCaptured;
                    _gamepadCaptureService.CaptureStatusChanged += OnGamepadCaptureStatusChanged;
                }

                // Запускаем таймер
                _captureTimer.Start(timeoutSeconds);
                
                // Начинаем захват
                await _gamepadCaptureService.StartCaptureAsync(timeoutSeconds);
                
                StatusChanged?.Invoke("Нажмите комбинацию кнопок на геймпаде...");
            }
            catch (Exception ex)
            {
                _captureTimer.Stop();
                CaptureError?.Invoke($"Ошибка начала захвата: {ex.Message}");
            }
        }

        /// <summary>
        /// Останавливает процесс захвата
        /// </summary>
        public void StopCapture()
        {
            if (_isDisposed) return;

            _captureTimer.Stop();
            _gamepadCaptureService?.StopCapture();
            
            StatusChanged?.Invoke("Захват остановлен");
        }

        #endregion

        #region Private Event Handlers

        private void OnCaptureTimerExpired()
        {
            if (_isDisposed) return;

            _gamepadCaptureService?.StopCapture();
            CaptureTimeout?.Invoke();
        }

        private void OnGamepadShortcutCaptured(object? sender, GamepadShortcut capturedShortcut)
        {
            System.Diagnostics.Debug.WriteLine($"🎮 [GamepadCaptureManager] OnGamepadShortcutCaptured: {capturedShortcut.DisplayText}");
            
            if (_isDisposed) 
            {
                System.Diagnostics.Debug.WriteLine($"🎮 [GamepadCaptureManager] ОТКЛОНЕНО - объект disposed");
                return;
            }

            try
            {
                // Останавливаем таймер
                _captureTimer.Stop();
                System.Diagnostics.Debug.WriteLine($"🎮 [GamepadCaptureManager] Таймер остановлен");
                
                // Уведомляем о успешном захвате
                StatusChanged?.Invoke("Комбинация захвачена!");
                System.Diagnostics.Debug.WriteLine($"🎮 [GamepadCaptureManager] StatusChanged вызвано");
                
                CaptureCompleted?.Invoke(capturedShortcut);
                System.Diagnostics.Debug.WriteLine($"🎮 [GamepadCaptureManager] CaptureCompleted вызвано");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [GamepadCaptureManager] Ошибка: {ex.Message}");
                CaptureError?.Invoke($"Ошибка обработки захвата: {ex.Message}");
            }
        }
        
        private void OnGamepadCaptureStatusChanged(object? sender, string status)
        {
            if (_isDisposed) return;

            // Фильтруем сообщения о остановке захвата
            if (!status.Contains("Захват остановлен"))
            {
                StatusChanged?.Invoke(status);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Обновляет доступность геймпада
        /// </summary>
        /// <returns>true если геймпад доступен</returns>
        public async Task<bool> UpdateAvailabilityAsync()
        {
            if (_isDisposed) return false;

            try
            {
                var gamepad = await _gamepadService.GetConnectedGamepadAsync();
                IsAvailable = gamepad != null;
                return IsAvailable;
            }
            catch
            {
                IsAvailable = false;
                return false;
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы менеджера
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            // Останавливаем захват
            StopCapture();

            // Освобождаем таймер
            _captureTimer.TimerExpired -= OnCaptureTimerExpired;
            _captureTimer.Dispose();

            // Освобождаем сервис захвата геймпада
            if (_gamepadCaptureService != null)
            {
                _gamepadCaptureService.ShortcutCaptured -= OnGamepadShortcutCaptured;
                _gamepadCaptureService.CaptureStatusChanged -= OnGamepadCaptureStatusChanged;
                _gamepadCaptureService.Dispose();
                _gamepadCaptureService = null;
            }

            // Очищаем события
            CaptureCompleted = null;
            CaptureTimeout = null;
            StatusChanged = null;
            CaptureError = null;

            _isDisposed = true;
        }

        #endregion
    }
}