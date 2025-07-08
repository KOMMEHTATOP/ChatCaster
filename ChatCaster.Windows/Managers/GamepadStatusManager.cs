using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Windows.Interfaces;
using ChatCaster.Windows.Services.GamepadService;

namespace ChatCaster.Windows.Managers
{
    /// <summary>
    /// Менеджер для отслеживания статуса геймпада
    /// </summary>
    public sealed class GamepadStatusManager : IInputStatusManager
    {
        #region Events

        /// <summary>
        /// Событие изменения статуса геймпада (текст, цвет)
        /// </summary>
        public event Action<string, string>? StatusChanged;

        #endregion

        #region Private Fields

        private readonly MainGamepadService _gamepadService;
        private string _statusText = "Геймпад не найден";
        private string _statusColor = "#f44336"; // Красный
        private bool _isDisposed;

        #endregion

        #region Color Constants

        private const string ConnectedColor = "#4caf50";    // Зеленый
        private const string DisconnectedColor = "#f44336"; // Красный
        private const string ErrorColor = "#ff9800";        // Оранжевый

        #endregion

        #region Constructor

        public GamepadStatusManager(MainGamepadService gamepadService)
        {
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            
            // Подписываемся на события геймпада
            SubscribeToGamepadEvents();
        }

        #endregion

        #region IInputStatusManager Implementation

        /// <summary>
        /// Текущий текст статуса
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    NotifyStatusChanged();
                }
            }
        }

        /// <summary>
        /// Цвет статуса
        /// </summary>
        public string StatusColor
        {
            get => _statusColor;
            private set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    NotifyStatusChanged();
                }
            }
        }

        /// <summary>
        /// Доступен ли геймпад
        /// </summary>
        public bool IsAvailable { get; private set; }

        /// <summary>
        /// Обновляет статус геймпада
        /// </summary>
        public async Task RefreshStatusAsync()
        {
            if (_isDisposed) return;

            try
            {
                var gamepad = await _gamepadService.GetConnectedGamepadAsync();
                
                if (gamepad != null)
                {
                    SetConnectedStatus(gamepad.Name);
                }
                else
                {
                    SetDisconnectedStatus();
                }
            }
            catch (Exception ex)
            {
                SetErrorStatus($"Ошибка проверки геймпада: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private void SubscribeToGamepadEvents()
        {
            _gamepadService.GamepadEvent += OnGamepadEvent;
        }

        private void UnsubscribeFromGamepadEvents()
        {
            _gamepadService.GamepadEvent -= OnGamepadEvent;
        }
        
        private void OnGamepadEvent(object? sender, GamepadEvent e)
        {
            if (_isDisposed) return;

            switch (e.EventType)
            {
                case GamepadEventType.Connected:
                    SetConnectedStatus(e.GamepadInfo.Name);
                    break;
        
                case GamepadEventType.Disconnected:
                    SetDisconnectedStatus();
                    break;
            }
        }

        private void SetConnectedStatus(string gamepadName)
        {
            IsAvailable = true;
            StatusText = $"Геймпад подключен: {gamepadName}";
            StatusColor = ConnectedColor;
        }

        private void SetDisconnectedStatus()
        {
            IsAvailable = false;
            StatusText = "Геймпад отключен";
            StatusColor = DisconnectedColor;
        }

        private void SetErrorStatus(string errorMessage)
        {
            IsAvailable = false;
            StatusText = errorMessage;
            StatusColor = ErrorColor;
        }

        private void NotifyStatusChanged()
        {
            StatusChanged?.Invoke(StatusText, StatusColor);
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы менеджера
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            UnsubscribeFromGamepadEvents();
            StatusChanged = null;
            _isDisposed = true;
        }

        #endregion
    }
}