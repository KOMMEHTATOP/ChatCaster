using System;
using System.Threading.Tasks;

namespace ChatCaster.Windows.Interfaces
{
    /// <summary>
    /// Базовый интерфейс для менеджеров захвата пользовательского ввода
    /// </summary>
    /// <typeparam name="T">Тип захватываемого объекта</typeparam>
    public interface ICaptureManager<T> : IDisposable
    {
        #region Events

        /// <summary>
        /// Событие успешного захвата
        /// </summary>
        event Action<T>? CaptureCompleted;

        /// <summary>
        /// Событие таймаута захвата
        /// </summary>
        event Action? CaptureTimeout;

        /// <summary>
        /// Событие изменения статуса захвата
        /// </summary>
        event Action<string>? StatusChanged;

        /// <summary>
        /// Событие ошибки захвата
        /// </summary>
        event Action<string>? CaptureError;

        #endregion

        #region Properties

        /// <summary>
        /// Активен ли процесс захвата
        /// </summary>
        bool IsCapturing { get; }

        /// <summary>
        /// Доступен ли менеджер для захвата
        /// </summary>
        bool IsAvailable { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Начинает процесс захвата
        /// </summary>
        /// <param name="timeoutSeconds">Время ожидания в секундах</param>
        /// <returns>Task для ожидания завершения операции</returns>
        Task StartCaptureAsync(int timeoutSeconds);

        /// <summary>
        /// Останавливает процесс захвата
        /// </summary>
        void StopCapture();

        #endregion
    }

    /// <summary>
    /// Интерфейс для управления статусом устройств ввода
    /// </summary>
    public interface IInputStatusManager : IDisposable
    {
        #region Events

        /// <summary>
        /// Событие изменения статуса
        /// </summary>
        event Action<string, string>? StatusChanged; // текст, цвет

        #endregion

        #region Properties

        /// <summary>
        /// Текущий статус
        /// </summary>
        string StatusText { get; }

        /// <summary>
        /// Цвет статуса
        /// </summary>
        string StatusColor { get; }

        /// <summary>
        /// Доступно ли устройство
        /// </summary>
        bool IsAvailable { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Обновляет статус устройства
        /// </summary>
        Task RefreshStatusAsync();

        #endregion
    }
}