using ChatCaster.Core.Models;
using ChatCaster.Core.Services.UI;
using ChatCaster.Windows.Managers.AudioSettings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Components
{
    /// <summary>
    /// Компонент для управления аудио устройствами
    /// Содержит UI логику для выбора и тестирования микрофонов
    /// </summary>
    public partial class AudioDeviceComponentViewModel : ObservableObject
    {
        private readonly AudioDeviceManager _audioDeviceManager;
        private readonly INotificationService _notificationService;

        [ObservableProperty]
        private List<AudioDevice> _availableDevices = new();

        [ObservableProperty]
        private AudioDevice? _selectedDevice;

        [ObservableProperty]
        private bool _isTestingMicrophone;

        // События для связи с родительской ViewModel
        public event Func<Task>? DeviceChanged;
        public event Action<string>? StatusChanged;

        public AudioDeviceComponentViewModel(
            AudioDeviceManager audioDeviceManager,
            INotificationService notificationService)
        {
            _audioDeviceManager = audioDeviceManager ?? throw new ArgumentNullException(nameof(audioDeviceManager));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));

            Log.Debug("AudioDeviceComponentViewModel инициализирован");
        }

        /// <summary>
        /// Загружает список доступных устройств
        /// </summary>
        public async Task LoadDevicesAsync()
        {
            try
            {
                AvailableDevices = await _audioDeviceManager.LoadAvailableDevicesAsync();
                Log.Information("AudioDeviceComponent загружено {Count} устройств", AvailableDevices.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки устройств в AudioDeviceComponent");
                AvailableDevices = new List<AudioDevice>();
            }
        }

        /// <summary>
        /// Устанавливает выбранное устройство по ID из конфигурации
        /// </summary>
        public void SetSelectedDeviceFromConfig(string? deviceId)
        {
            if (!string.IsNullOrEmpty(deviceId))
            {
                SelectedDevice = _audioDeviceManager.FindDeviceById(AvailableDevices, deviceId);
                Log.Information("AudioDeviceComponent устройство из конфига: {DeviceId} -> {DeviceName}", 
                    deviceId, SelectedDevice?.Name ?? "не найдено");
            }

            // Если устройство не найдено или пустое - автовыбор
            if (SelectedDevice == null && AvailableDevices.Any())
            {
                SelectedDevice = _audioDeviceManager.SelectDefaultDevice(AvailableDevices);
                Log.Information("AudioDeviceComponent автовыбор устройства: {DeviceName}", SelectedDevice?.Name);
            }
        }

        /// <summary>
        /// Применяет выбранное устройство к сервису
        /// </summary>
        public async Task<bool> ApplySelectedDeviceAsync()
        {
            if (SelectedDevice == null)
            {
                Log.Warning("AudioDeviceComponent попытка применить null устройство");
                return false;
            }

            var success = await _audioDeviceManager.SetActiveDeviceAsync(SelectedDevice.Id);
            if (success)
            {
                Log.Information("AudioDeviceComponent устройство применено: {DeviceName}", SelectedDevice.Name);
            }
            return success;
        }

        [RelayCommand(CanExecute = nameof(CanTestMicrophone))]
        private async Task TestMicrophone()
        {
            try
            {
                IsTestingMicrophone = true;
                TestMicrophoneCommand.NotifyCanExecuteChanged();
                
                StatusChanged?.Invoke("Тестируется...");
                Log.Information("AudioDeviceComponent начинаем тест микрофона");

                // Проверяем, что устройство выбрано
                if (SelectedDevice == null)
                {
                    StatusChanged?.Invoke("Выберите устройство");
                    Log.Warning("AudioDeviceComponent устройство не выбрано для тестирования");
                    return;
                }

                Log.Information("AudioDeviceComponent тестируем устройство: {DeviceName}", SelectedDevice.Name);
                
                // Тестируем текущее активное устройство (НЕ устанавливаем новое!)
                var result = await _audioDeviceManager.TestMicrophoneAsync();
                
                if (result)
                {
                    StatusChanged?.Invoke("Микрофон работает");
                    _notificationService.NotifyMicrophoneTest(true, SelectedDevice.Name);
                    Log.Information("AudioDeviceComponent тест микрофона прошел успешно");
                }
                else
                {
                    StatusChanged?.Invoke("Проблема с микрофоном");
                    _notificationService.NotifyMicrophoneTest(false);
                    Log.Warning("AudioDeviceComponent тест микрофона не прошел");
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Ошибка тестирования: {ex.Message}");
                _notificationService.NotifyMicrophoneTest(false);
                Log.Error(ex, "Ошибка тестирования микрофона в AudioDeviceComponent");
            }
            finally
            {
                IsTestingMicrophone = false;
                TestMicrophoneCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanTestMicrophone() => !IsTestingMicrophone;

        partial void OnSelectedDeviceChanged(AudioDevice? value)
        {
            Log.Information("AudioDeviceComponent устройство изменено: {DeviceName} ({DeviceId})",
                value?.Name ?? "не выбрано", value?.Id ?? "");

            // Уведомляем родительскую ViewModel об изменении
            DeviceChanged?.Invoke();
        }
    }
}