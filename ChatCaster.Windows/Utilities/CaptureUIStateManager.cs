using System.Windows;
using Serilog;

namespace ChatCaster.Windows.Utilities
{
    /// <summary>
    /// Состояния процесса захвата пользовательского ввода
    /// </summary>
    public enum CaptureState
    {
        Idle,       // Бездействие
        Capturing,  // Процесс захвата
        Success,    // Успешный захват
        Error,      // Ошибка
        Timeout     // Таймаут
    }

    /// <summary>
    /// Данные о состоянии UI для захвата
    /// </summary>
    public class CaptureUIState
    {
        public string Text { get; set; } = string.Empty;
        public string TextColor { get; set; } = "White";
        public string StatusMessage { get; set; } = string.Empty;
        public bool ShowTimer { get; set; }
        public int TimeLeft { get; set; }
        public CaptureState State { get; set; } = CaptureState.Idle;
    }

    /// <summary>
    /// Менеджер для управления состояниями UI захвата пользовательского ввода
    /// </summary>
    public sealed class CaptureUIStateManager : IDisposable
    {
        private readonly static ILogger _logger = Log.ForContext<CaptureUIStateManager>();

        #region Events

        /// <summary>
        /// Событие изменения состояния UI
        /// </summary>
        public event Action<CaptureUIState>? StateChanged;

        #endregion

        #region Private Fields

        private CaptureUIState _currentState;
        private string _originalText = string.Empty;
        private bool _isDisposed;
        private Timer? _countdownTimer;
        #endregion

        #region Color Constants

        private const string IdleColor = "White";
        private const string CapturingColor = "#ff6b6b";  // Красноватый
        private const string SuccessColor = "#4caf50";    // Зеленый
        private const string ErrorColor = "#f44336";      // Красный

        #endregion

        #region Constructor

        public CaptureUIStateManager()
        {
            _currentState = new CaptureUIState();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Текущее состояние UI
        /// </summary>
        public CaptureUIState CurrentState 
        { 
            get => _currentState; 
            private set
            {
                _currentState = value;
                StateChanged?.Invoke(_currentState);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Устанавливает исходное состояние с базовым текстом
        /// </summary>
        /// <param name="text">Базовый текст (например, текущая комбинация)</param>
        public void SetIdleState(string text)
        {
            if (_isDisposed) return;

            _originalText = text;
            CurrentState = new CaptureUIState
            {
                Text = text,
                TextColor = IdleColor,
                StatusMessage = string.Empty,
                ShowTimer = false,
                TimeLeft = 0,
                State = CaptureState.Idle
            };
        }

        /// <summary>
        /// Начинает процесс захвата
        /// </summary>
        /// <param name="capturingMessage">Сообщение во время захвата</param>
        /// <param name="timeoutSeconds">Время таймаута</param>
        public void StartCapture(string capturingMessage, int timeoutSeconds)
        {
            if (_isDisposed) return;

            // ОСТАНАВЛИВАЕМ предыдущий таймер если есть
            _countdownTimer?.Dispose();

            var timeLeft = timeoutSeconds;
            
            CurrentState = new CaptureUIState
            {
                Text = capturingMessage,
                TextColor = CapturingColor,
                StatusMessage = "Ожидание ввода...",
                ShowTimer = true,
                TimeLeft = timeLeft,
                State = CaptureState.Capturing
            };

            _logger.Debug("Начат захват: {Message}, таймаут: {Timeout}с", capturingMessage, timeoutSeconds);

            // ЗАПУСКАЕМ таймер обратного отсчета
            _countdownTimer = new Timer(_ =>
            {
                if (_isDisposed || CurrentState.State != CaptureState.Capturing) return;

                timeLeft--;

                if (timeLeft >= 0)
                {
                    // Обновляем счетчик
                    Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        if (!_isDisposed && CurrentState.State == CaptureState.Capturing)
                        {
                            CurrentState = new CaptureUIState
                            {
                                Text = CurrentState.Text,
                                TextColor = CurrentState.TextColor,
                                StatusMessage = CurrentState.StatusMessage,
                                ShowTimer = true,
                                TimeLeft = timeLeft,
                                State = CaptureState.Capturing
                            };
                        }
                    });
                }

                if (timeLeft <= 0)
                {
                    _countdownTimer?.Dispose();
                    _countdownTimer = null;
                }
            }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Завершает захват успешно
        /// </summary>
        /// <param name="successText">Текст успешного результата</param>
        /// <param name="statusMessage">Сообщение о статусе</param>
        public async Task CompleteSuccessAsync(string successText, string statusMessage = "Комбинация сохранена!")
        {
            if (_isDisposed) return;

            _originalText = successText;

            CurrentState = new CaptureUIState
            {
                Text = successText,
                TextColor = SuccessColor,
                StatusMessage = statusMessage,
                ShowTimer = false,
                TimeLeft = 0,
                State = CaptureState.Success
            };

            _logger.Information("Захват завершен успешно: {Text}", successText);

            // Через 2 секунды возвращаем к исходному состоянию
            await Task.Delay(2000);
            if (!_isDisposed)
            {
                ReturnToIdle();
            }
        }
        
        /// <summary>
        /// Завершает захват с ошибкой
        /// </summary>
        /// <param name="errorMessage">Сообщение об ошибке</param>
        public async Task CompleteWithErrorAsync(string errorMessage)
        {
            if (_isDisposed) return;

            CurrentState = new CaptureUIState
            {
                Text = _originalText,
                TextColor = ErrorColor,
                StatusMessage = errorMessage,
                ShowTimer = false,
                TimeLeft = 0,
                State = CaptureState.Error
            };

            _logger.Warning("Захват завершен с ошибкой: {Error}", errorMessage);

            // Через 3 секунды возвращаем к исходному состоянию
            await Task.Delay(3000);
            if (!_isDisposed)
            {
                ReturnToIdle();
            }
        }

        /// <summary>
        /// Завершает захват по таймауту
        /// </summary>
        public async Task CompleteWithTimeoutAsync()
        {
            if (_isDisposed) return;

            CurrentState = new CaptureUIState
            {
                Text = _originalText,
                TextColor = IdleColor,
                StatusMessage = "Время ожидания истекло",
                ShowTimer = false,
                TimeLeft = 0,
                State = CaptureState.Timeout
            };

            _logger.Debug("Захват завершен по таймауту");

            // Через 2 секунды очищаем сообщение
            await Task.Delay(2000);
            if (!_isDisposed)
            {
                ReturnToIdle();
            }
        }

        /// <summary>
        /// Возвращает к исходному состоянию
        /// </summary>
        private void ReturnToIdle()
        {
            CurrentState = new CaptureUIState
            {
                Text = _originalText,
                TextColor = IdleColor,
                StatusMessage = string.Empty,
                ShowTimer = false,
                TimeLeft = 0,
                State = CaptureState.Idle
            };
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы менеджера
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            
            // ОСТАНАВЛИВАЕМ таймер при освобождении ресурсов
            _countdownTimer?.Dispose();
            _countdownTimer = null;
            
            _isDisposed = true;
            StateChanged = null;
        }

        #endregion
    }
}