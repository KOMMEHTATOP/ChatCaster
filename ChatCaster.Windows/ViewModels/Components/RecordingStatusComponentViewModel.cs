using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.System;
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
        private readonly ILocalizationService _localizationService;

        [ObservableProperty]
        private bool _isRecording;

        [ObservableProperty]
        private string _recordingStatusText = "Готов к записи";

        [ObservableProperty]
        private string _recordButtonText = "Записать";

        [ObservableProperty]
        private string _statusColor = "#4caf50";

        [ObservableProperty]
        private float _microphoneLevel;

        // События для связи с родительской ViewModel
        public event Action<RecordingStateInfo>? StatusChanged;

        public RecordingStatusComponentViewModel(
            RecordingStatusManager statusManager,
            IAudioCaptureService audioService,
            ILocalizationService localizationService)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

            // Подписываемся на события аудио сервиса
            _audioService.VolumeChanged += OnVolumeChanged;
            
            // Подписываемся на смену языка
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// Обновляет статус записи
        /// </summary>
        public void UpdateRecordingStatus(RecordingStatusChangedEvent e)
        {
            try
            {
                var stateInfo = _statusManager.CreateStateInfo(e.NewStatus, e.Reason);

                // Обновляем Observable свойства с локализацией
                IsRecording = stateInfo.IsRecording;
                RecordingStatusText = GetLocalizedStatusText(e.NewStatus);
                RecordButtonText = GetLocalizedButtonText(e.NewStatus);
                StatusColor = stateInfo.StatusColor;

                // Уведомляем родительскую ViewModel
                StatusChanged?.Invoke(stateInfo);
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
                RecordingStatusText = GetLocalizedStatusText(RecordingStatus.Idle);
                RecordButtonText = GetLocalizedButtonText(RecordingStatus.Idle);
                StatusColor = stateInfo.StatusColor;
                MicrophoneLevel = 0.0f;
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
                RecordingStatusText = _localizationService.GetString("ErrorRecording");
                RecordButtonText = GetLocalizedButtonText(RecordingStatus.Error);
                StatusColor = stateInfo.StatusColor;

                StatusChanged?.Invoke(stateInfo);

                Log.Warning("RecordingStatusComponent: установлено состояние ошибки: {Error}", errorMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка установки состояния ошибки");
            }
        }

        /// <summary>
        /// Получает локализованный текст статуса
        /// </summary>
        private string GetLocalizedStatusText(RecordingStatus status)
        {
            return status switch
            {
                RecordingStatus.Idle => _localizationService.GetString("StatusReady"),
                RecordingStatus.Recording => _localizationService.GetString("StatusRecording"),
                RecordingStatus.Processing => _localizationService.GetString("StatusProcessing"),
                RecordingStatus.Error => _localizationService.GetString("ErrorRecording"),
                _ => _localizationService.GetString("StatusReady")
            };
        }

        /// <summary>
        /// Получает локализованный текст кнопки
        /// </summary>
        private string GetLocalizedButtonText(RecordingStatus status)
        {
            return status switch
            {
                RecordingStatus.Idle => _localizationService.GetString("ButtonRecord"),
                RecordingStatus.Recording => _localizationService.GetString("ButtonStop"),
                RecordingStatus.Processing => _localizationService.GetString("ButtonProcessing"),
                _ => _localizationService.GetString("ButtonRecord")
            };
        }

        /// <summary>
        /// Обновляет локализацию при смене языка
        /// </summary>
        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            try
            {
                // Обновляем текущие статусы с новой локализацией
                var currentStatus = IsRecording ? RecordingStatus.Recording : RecordingStatus.Idle;
                RecordingStatusText = GetLocalizedStatusText(currentStatus);
                RecordButtonText = GetLocalizedButtonText(currentStatus);

                Log.Debug("RecordingStatusComponent: обновлена локализация");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка обновления локализации");
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
                _localizationService.LanguageChanged -= OnLanguageChanged;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "RecordingStatusComponent: ошибка освобождения ресурсов");
            }
        }
    }
}