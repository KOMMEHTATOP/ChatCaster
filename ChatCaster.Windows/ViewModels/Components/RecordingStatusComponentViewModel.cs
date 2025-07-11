using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Windows.Managers.MainPage;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Components
{
    /// <summary>
    /// Компонент для управления UI статусами записи
    /// Отвечает за отображение состояния записи и уровня микрофона
    /// </summary>
    public partial class RecordingStatusComponentViewModel : ObservableObject
    {
        private readonly RecordingStatusManager _statusManager;
        private readonly IAudioCaptureService _audioService;

        [ObservableProperty]
        private bool _isRecording = false;

        [ObservableProperty]
        private string _recordingStatusText = "Готов к записи";

        [ObservableProperty]
        private string _recordButtonText = "Записать";

        [ObservableProperty]
        private string _statusColor = "#4caf50";

        [ObservableProperty]
        private float _microphoneLevel = 0.0f;

        // События для связи с родительской ViewModel
        public event Action<RecordingStateInfo>? StatusChanged;

        public RecordingStatusComponentViewModel(
            RecordingStatusManager statusManager,
            IAudioCaptureService audioService)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));

            // Подписываемся на события аудио сервиса
            _audioService.VolumeChanged += OnVolumeChanged;

            Log.Debug("RecordingStatusComponentViewModel инициализирован");
        }

        /// <summary>
        /// Обновляет статус записи
        /// </summary>
        public void UpdateRecordingStatus(RecordingStatusChangedEvent e)
        {
            try
            {
                var stateInfo = _statusManager.CreateStateInfo(e.NewStatus, e.Reason);

                // Обновляем Observable свойства
                IsRecording = stateInfo.IsRecording;
                RecordingStatusText = stateInfo.StatusText;
                RecordButtonText = stateInfo.ButtonText;
                StatusColor = stateInfo.StatusColor;

                // Уведомляем родительскую ViewModel
                StatusChanged?.Invoke(stateInfo);

                Log.Debug("RecordingStatusComponent: статус обновлен: {Status}", e.NewStatus);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка обновления статуса");
            }
        }

        /// <summary>
        /// Устанавливает начальное состояние
        /// </summary>
        public void SetInitialState()
        {
            try
            {
                var stateInfo = _statusManager.CreateStateInfo(RecordingStatus.Idle);

                IsRecording = stateInfo.IsRecording;
                RecordingStatusText = stateInfo.StatusText;
                RecordButtonText = stateInfo.ButtonText;
                StatusColor = stateInfo.StatusColor;
                MicrophoneLevel = 0.0f;

                Log.Debug("RecordingStatusComponent: установлено начальное состояние");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка установки начального состояния");
            }
        }

        /// <summary>
        /// Проверяет можно ли начать запись
        /// </summary>
        public bool CanStartRecording()
        {
            return !IsRecording;
        }

        /// <summary>
        /// Проверяет можно ли остановить запись
        /// </summary>
        public bool CanStopRecording()
        {
            return IsRecording;
        }

        /// <summary>
        /// Устанавливает состояние ошибки
        /// </summary>
        public void SetErrorState(string errorMessage)
        {
            try
            {
                var stateInfo = _statusManager.CreateStateInfo(RecordingStatus.Error, errorMessage);

                IsRecording = stateInfo.IsRecording;
                RecordingStatusText = stateInfo.StatusText;
                RecordButtonText = stateInfo.ButtonText;
                StatusColor = stateInfo.StatusColor;

                StatusChanged?.Invoke(stateInfo);

                Log.Warning("RecordingStatusComponent: установлено состояние ошибки: {Error}", errorMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка установки состояния ошибки");
            }
        }

        private void OnVolumeChanged(object? sender, float volume)
        {
            try
            {
                MicrophoneLevel = volume;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка обработки изменения уровня микрофона");
            }
        }

        /// <summary>
        /// Очистка ресурсов
        /// </summary>
        public void Dispose()
        {
            try
            {
                _audioService.VolumeChanged -= OnVolumeChanged;
                Log.Debug("RecordingStatusComponent: ресурсы освобождены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка освобождения ресурсов");
            }
        }
    }
}