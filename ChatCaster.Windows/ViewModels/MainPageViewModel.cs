using System.Collections.ObjectModel;
using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Windows.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ChatCaster.Windows.ViewModels
{
    public partial class MainPageViewModel : ViewModelBase
    {
        #region Services

        private readonly IAudioCaptureService _audioService;
        private readonly IVoiceRecordingService _voiceRecordingService;
        private readonly AppConfig _config;

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private string _currentMicrophone = "Не выбран";

        [ObservableProperty]
        private float _microphoneLevel = 0.0f;

        [ObservableProperty]
        private bool _isRecording = false;

        [ObservableProperty]
        private string _recordingStatusText = "Готов к записи";

        [ObservableProperty]
        private string _lastRecognizedText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> _recentRecognitions = new();

        #endregion

        #region Commands

        [RelayCommand]
        private async Task ToggleRecording()
        {
            try
            {
                if (_voiceRecordingService.IsRecording)
                {
                    Log.Debug("Останавливаем запись через главную страницу");
                    var result = await _voiceRecordingService.StopRecordingAsync();
                    
                    if (result.Success && !string.IsNullOrEmpty(result.RecognizedText))
                    {
                        LastRecognizedText = result.RecognizedText;
                        AddToRecentRecognitions(result.RecognizedText);
                    }
                }
                else
                {
                    Log.Debug("Начинаем запись через главную страницу");
                    await _voiceRecordingService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка переключения записи на главной странице");
                RecordingStatusText = $"Ошибка: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TestMicrophone()
        {
            try
            {
                Log.Debug("Тестирование микрофона");
                var result = await _audioService.TestMicrophoneAsync();
                RecordingStatusText = result ? "Микрофон работает" : "Проблема с микрофоном";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка тестирования микрофона");
                RecordingStatusText = $"Ошибка тестирования: {ex.Message}";
            }
        }

        #endregion

        #region Constructor

        // ✅ ИСПРАВЛЕНО: Конструктор без ServiceContext
        public MainPageViewModel(
            IAudioCaptureService audioService,
            IVoiceRecordingService voiceRecordingService,
            AppConfig config)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _voiceRecordingService = voiceRecordingService ?? throw new ArgumentNullException(nameof(voiceRecordingService));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Подписываемся на события
            SubscribeToEvents();

            // Инициализируем начальные значения
            InitializeInitialValues();

            Log.Debug("MainPageViewModel инициализирован без ServiceContext");
        }

        #endregion

        #region Initialization

        private void InitializeInitialValues()
        {
            try
            {
                // Устанавливаем текущий микрофон
                var activeDevice = _audioService.ActiveDevice;
                CurrentMicrophone = activeDevice?.Name ?? "Не выбран";
                
                // Устанавливаем начальный статус
                RecordingStatusText = "Готов к записи";
                
                Log.Debug("Начальные значения MainPage установлены");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка установки начальных значений MainPage");
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            try
            {
                // Подписываемся на изменения уровня микрофона
                _audioService.VolumeChanged += OnVolumeChanged;

                // Подписываемся на события записи
                _voiceRecordingService.StatusChanged += OnRecordingStatusChanged;
                _voiceRecordingService.RecognitionCompleted += OnRecognitionCompleted;

                Log.Debug("События MainPage подписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка подписки на события MainPage");
            }
        }

        private void UnsubscribeFromEvents()
        {
            try
            {
                // Отписываемся от событий аудио
                _audioService.VolumeChanged -= OnVolumeChanged;

                // Отписываемся от событий записи
                _voiceRecordingService.StatusChanged -= OnRecordingStatusChanged;
                _voiceRecordingService.RecognitionCompleted -= OnRecognitionCompleted;

                Log.Debug("События MainPage отписаны");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка отписки от событий MainPage");
            }
        }

        #endregion

        #region Event Handlers

        private void OnVolumeChanged(object? sender, float volume)
        {
            try
            {
                MicrophoneLevel = volume;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки изменения уровня микрофона");
            }
        }

        private void OnRecordingStatusChanged(object? sender, RecordingStatusChangedEvent e)
        {
            try
            {
                IsRecording = e.NewStatus == RecordingStatus.Recording;

                RecordingStatusText = e.NewStatus switch
                {
                    RecordingStatus.Idle => "Готов к записи",
                    RecordingStatus.Recording => "Идет запись...",
                    RecordingStatus.Processing => "Обработка...",
                    RecordingStatus.Error => $"Ошибка: {e.Reason}",
                    _ => "Неизвестный статус"
                };

                Log.Debug("Статус записи изменен на MainPage: {NewStatus}", e.NewStatus);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки изменения статуса записи");
            }
        }

        private void OnRecognitionCompleted(object? sender, VoiceRecognitionCompletedEvent e)
        {
            try
            {
                if (e.Result.Success && !string.IsNullOrEmpty(e.Result.RecognizedText))
                {
                    LastRecognizedText = e.Result.RecognizedText;
                    AddToRecentRecognitions(e.Result.RecognizedText);
                    RecordingStatusText = "Распознавание завершено";
                    
                    Log.Information("Распознавание завершено на MainPage: {Text}", e.Result.RecognizedText);
                }
                else
                {
                    RecordingStatusText = $"Ошибка распознавания: {e.Result.ErrorMessage}";
                    Log.Warning("Ошибка распознавания на MainPage: {Error}", e.Result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка обработки завершения распознавания");
            }
        }

        #endregion

        #region Helper Methods

        private void AddToRecentRecognitions(string text)
        {
            try
            {
                // Добавляем в начало списка
                RecentRecognitions.Insert(0, text);

                // Ограничиваем количество записей
                while (RecentRecognitions.Count > 10)
                {
                    RecentRecognitions.RemoveAt(RecentRecognitions.Count - 1);
                }

                Log.Debug("Добавлен текст в недавние распознавания: {Text}", text);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка добавления текста в недавние распознавания");
            }
        }

        #endregion

        #region Cleanup

        public void Cleanup()
        {
            try
            {
                Log.Debug("Cleanup MainPageViewModel начат");

                UnsubscribeFromEvents();

                // Очищаем коллекции
                RecentRecognitions.Clear();

                Log.Information("Cleanup MainPageViewModel завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при cleanup MainPageViewModel");
            }
        }

        #endregion
    }
}