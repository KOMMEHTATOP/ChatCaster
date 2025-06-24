
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

        #endregion

        #region Color Constants

        private const string IdleColor = "White";
        private const string CapturingColor = "#ff6b6b";  // Красноватый
        private const string SuccessColor = "#4caf50";    // Зеленый
        private const string ErrorColor = "#f44336";      // Красный
        private const string ProgressColor = "#81c784";   // Светло-зеленый

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

            CurrentState = new CaptureUIState
            {
                Text = capturingMessage,
                TextColor = CapturingColor,
                StatusMessage = "Ожидание ввода...",
                ShowTimer = true,
                TimeLeft = timeoutSeconds,
                State = CaptureState.Capturing
            };
        }

        /// <summary>
        /// Обновляет состояние во время захвата
        /// </summary>
        /// <param name="statusMessage">Текущий статус</param>
        /// <param name="timeLeft">Оставшееся время</param>
        /// <param name="isProgress">Показывает ли сообщение прогресс</param>
        public void UpdateCapture(string statusMessage, int timeLeft, bool isProgress = false)
        {
            if (_isDisposed || CurrentState.State != CaptureState.Capturing) return;

            CurrentState = new CaptureUIState
            {
                Text = statusMessage,
                TextColor = isProgress ? ProgressColor : CapturingColor,
                StatusMessage = CurrentState.StatusMessage,
                ShowTimer = true,
                TimeLeft = timeLeft,
                State = CaptureState.Capturing
            };
        }

        /// <summary>
        /// Завершает захват успешно
        /// </summary>
        /// <param name="successText">Текст успешного результата</param>
        /// <param name="statusMessage">Сообщение о статусе</param>
        public async Task CompleteSuccessAsync(string successText, string statusMessage = "Комбинация сохранена!")
        {
            if (_isDisposed) return;

            CurrentState = new CaptureUIState
            {
                Text = successText,
                TextColor = SuccessColor,
                StatusMessage = statusMessage,
                ShowTimer = false,
                TimeLeft = 0,
                State = CaptureState.Success
            };

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

            // Через 2 секунды очищаем сообщение
            await Task.Delay(2000);
            if (!_isDisposed)
            {
                ReturnToIdle();
            }
        }

        /// <summary>
        /// Принудительно останавливает захват
        /// </summary>
        public void StopCapture()
        {
            if (_isDisposed) return;
            ReturnToIdle();
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
            
            _isDisposed = true;
            StateChanged = null;
        }

        #endregion
    }
}