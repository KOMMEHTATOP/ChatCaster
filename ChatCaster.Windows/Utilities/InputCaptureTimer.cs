using System.Windows;

namespace ChatCaster.Windows.Utilities
{
    /// <summary>
    /// Универсальный таймер обратного отсчета для захвата пользовательского ввода
    /// </summary>
    public sealed class InputCaptureTimer : IDisposable
    {
        #region Events

        /// <summary>
        /// Событие тика таймера (каждую секунду)
        /// </summary>
        public event Action<int>? TimerTick;

        /// <summary>
        /// Событие истечения времени таймера
        /// </summary>
        public event Action? TimerExpired;

        #endregion

        #region Private Fields

        private Timer? _timer;
        private int _remainingSeconds;
        private readonly object _lockObject = new();
        private bool _isDisposed;

        #endregion

        #region Public Properties

        /// <summary>
        /// Активен ли таймер в данный момент
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Оставшееся время в секундах
        /// </summary>
        public int RemainingSeconds
        {
            get
            {
                lock (_lockObject)
                {
                    return _remainingSeconds;
                }
            }
        }

        /// <summary>
        /// Общее время таймера в секундах
        /// </summary>
        public int TotalSeconds { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Запускает таймер на указанное количество секунд
        /// </summary>
        /// <param name="timeoutSeconds">Время в секундах</param>
        /// <exception cref="ArgumentException">Если время меньше или равно нулю</exception>
        /// <exception cref="InvalidOperationException">Если таймер уже запущен</exception>
        public void Start(int timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
                throw new ArgumentException("Время должно быть больше нуля", nameof(timeoutSeconds));

            lock (_lockObject)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(InputCaptureTimer));

                if (IsRunning)
                    throw new InvalidOperationException("Таймер уже запущен");

                TotalSeconds = timeoutSeconds;
                _remainingSeconds = timeoutSeconds;
                IsRunning = true;

                // Запускаем таймер с интервалом 1 секунда
                _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }

            // Вызываем первый тик сразу
            DispatchTimerTick(_remainingSeconds);
        }

        /// <summary>
        /// Останавливает таймер
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (!IsRunning)
                    return;

                _timer?.Dispose();
                _timer = null;
                IsRunning = false;
                _remainingSeconds = 0;
            }
        }

        /// <summary>
        /// Перезапускает таймер с тем же временем
        /// </summary>
        public void Restart()
        {
            var totalSeconds = TotalSeconds;
            Stop();
            Start(totalSeconds);
        }

        #endregion

        #region Private Methods

        private void OnTimerTick(object? state)
        {
            lock (_lockObject)
            {
                if (!IsRunning || _isDisposed)
                    return;

                _remainingSeconds--;

                if (_remainingSeconds <= 0)
                {
                    // Время истекло
                    _timer?.Dispose();
                    _timer = null;
                    IsRunning = false;
                    _remainingSeconds = 0;

                    DispatchTimerExpired();
                }
                else
                {
                    // Обычный тик
                    DispatchTimerTick(_remainingSeconds);
                }
            }
        }

        private void DispatchTimerTick(int remainingSeconds)
        {
            // Вызываем события в UI потоке
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() => TimerTick?.Invoke(remainingSeconds));
            }
            else
            {
                TimerTick?.Invoke(remainingSeconds);
            }
        }

        private void DispatchTimerExpired()
        {
            // Вызываем события в UI потоке
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() => TimerExpired?.Invoke());
            }
            else
            {
                TimerExpired?.Invoke();
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы таймера
        /// </summary>
        public void Dispose()
        {
            lock (_lockObject)
            {
                if (_isDisposed)
                    return;

                _timer?.Dispose();
                _timer = null;
                IsRunning = false;
                _isDisposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Аргументы события для таймера захвата
    /// </summary>
    public class CaptureTimerEventArgs : EventArgs
    {
        /// <summary>
        /// Оставшееся время в секундах
        /// </summary>
        public int RemainingSeconds { get; }

        /// <summary>
        /// Общее время таймера
        /// </summary>
        public int TotalSeconds { get; }

        /// <summary>
        /// Прогресс в процентах (0-100)
        /// </summary>
        public double ProgressPercent => TotalSeconds > 0 ? (double)(TotalSeconds - RemainingSeconds) / TotalSeconds * 100 : 0;

        public CaptureTimerEventArgs(int remainingSeconds, int totalSeconds)
        {
            RemainingSeconds = remainingSeconds;
            TotalSeconds = totalSeconds;
        }
    }
}