using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.System;
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
        private readonly ILocalizationService _localizationService;
        private string _statusText = "";
        private string _statusColor = "#f44336"; // Красный
        private bool _isDisposed;
        private string _currentGamepadName = "";

        #endregion

        #region Color Constants

        private const string ConnectedColor = "#4caf50";    // Зеленый
        private const string DisconnectedColor = "#f44336"; // Красный
        private const string ErrorColor = "#ff9800";        // Оранжевый

        #endregion

        #region Constructor

        public GamepadStatusManager(MainGamepadService gamepadService, ILocalizationService localizationService)
        {
            _gamepadService = gamepadService ?? throw new ArgumentNullException(nameof(gamepadService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            
            // Подписываемся на события
            SubscribeToGamepadEvents();
            SubscribeToLocalizationEvents();
            
            // Инициализируем локализованные строки
            UpdateLocalizedStrings();
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
                SetErrorStatus($"{_localizationService.GetString("Gamepad_ErrorPrefix")}: {ex.Message}");
            }
        }

        #endregion

        #region Localization

        private void SubscribeToLocalizationEvents()
        {
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        private void UnsubscribeFromLocalizationEvents()
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLocalizedStrings();
        }

        private void UpdateLocalizedStrings()
        {
            // Обновляем текущий статус в зависимости от состояния
            if (IsAvailable && !string.IsNullOrEmpty(_currentGamepadName))
            {
                // Геймпад подключен
                StatusText = string.Format(_localizationService.GetString("Gamepad_Connected"), _currentGamepadName);
            }
            else if (!IsAvailable && !string.IsNullOrEmpty(_currentGamepadName))
            {
                // Геймпад был подключен, но отключился
                StatusText = _localizationService.GetString("Gamepad_Disconnected");
            }
            else
            {
                // Геймпад не найден (начальное состояние)
                StatusText = _localizationService.GetString("Gamepad_NotFound");
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
            _currentGamepadName = gamepadName;
            StatusText = string.Format(_localizationService.GetString("Gamepad_Connected"), gamepadName);
            StatusColor = ConnectedColor;
        }

        private void SetDisconnectedStatus()
        {
            IsAvailable = false;
            // Сохраняем имя для возможного переподключения, но обновляем статус
            StatusText = _localizationService.GetString("Gamepad_Disconnected");
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
            UnsubscribeFromLocalizationEvents();
            StatusChanged = null;
            _isDisposed = true;
        }

        #endregion
    }
}