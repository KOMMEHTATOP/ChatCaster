using System.Collections.ObjectModel;
using ChatCaster.Windows.Services;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Settings.Audio
{
    /// <summary>
    /// Менеджер для управления аудио устройствами и микрофоном
    /// </summary>
    public class AudioDeviceManager
    {
        #region Events
        public event EventHandler<MicrophoneStatusChangedEventArgs>? MicrophoneStatusChanged;
        #endregion

        #region Private Fields
        private readonly AudioCaptureService? _audioCaptureService;
        private bool _isTestingMicrophone = false;
        #endregion

        #region Public Properties
        public ObservableCollection<AudioDeviceItem> AvailableDevices { get; } = new();
        public AudioDeviceItem? SelectedDevice { get; set; }
        public bool IsTestingMicrophone
        {
            get => _isTestingMicrophone;
        }

        #endregion

        #region Constructor
        public AudioDeviceManager(AudioCaptureService? audioCaptureService)
        {
            _audioCaptureService = audioCaptureService;
            Log.Debug("AudioDeviceManager инициализирован");
        }
        #endregion

        #region Public Methods

        /// <summary>
        /// Загружает доступные аудио устройства
        /// </summary>
        public async Task LoadDevicesAsync()
        {
            try
            {
                if (_audioCaptureService == null)
                {
                    Log.Warning("AudioCaptureService недоступен при загрузке устройств");
                    RaiseMicrophoneStatusChanged("Сервис недоступен", "#f44336");
                    return;
                }

                var devices = await _audioCaptureService.GetAvailableDevicesAsync();

                AvailableDevices.Clear();
                AudioDeviceItem? defaultDevice = null;

                foreach (var device in devices)
                {
                    var deviceItem = new AudioDeviceItem(device.Id, device.Name, device.IsDefault);
                    AvailableDevices.Add(deviceItem);

                    // Запоминаем устройство по умолчанию
                    if (device.IsDefault)
                    {
                        defaultDevice = deviceItem;
                    }
                }

                // Выбираем устройство по умолчанию если нет выбранного
                if (SelectedDevice == null && defaultDevice != null)
                {
                    SelectedDevice = defaultDevice;
                }

                RaiseMicrophoneStatusChanged("Микрофон готов", "#4caf50");
                Log.Debug("Загружено аудио устройств: {DeviceCount}", AvailableDevices.Count);
            }
            catch (Exception ex)
            {
                RaiseMicrophoneStatusChanged($"Ошибка загрузки устройств: {ex.Message}", "#f44336");
                Log.Error(ex, "Ошибка при загрузке аудио устройств");
            }
        }

        /// <summary>
        /// Тестирует выбранный микрофон
        /// </summary>
        public async Task TestMicrophoneAsync()
        {
            if (_isTestingMicrophone || _audioCaptureService == null) return;

            try
            {
                _isTestingMicrophone = true;
                RaiseMicrophoneStatusChanged("Тестируется...", "#ff9800");
                Log.Debug("Начинаем тестирование микрофона");

                // Устанавливаем выбранное устройство
                if (SelectedDevice != null)
                {
                    await _audioCaptureService.SetActiveDeviceAsync(SelectedDevice.Id);
                    Log.Debug("Установлено активное устройство: {DeviceId}", SelectedDevice.Id);
                }

                // Тестируем микрофон
                bool testResult = await _audioCaptureService.TestMicrophoneAsync();

                if (testResult)
                {
                    RaiseMicrophoneStatusChanged("Микрофон работает", "#4caf50");
                    Log.Debug("Тестирование микрофона успешно завершено");
                }
                else
                {
                    RaiseMicrophoneStatusChanged("Проблема с микрофоном", "#f44336");
                    Log.Warning("Тестирование микрофона завершилось неудачей");
                }
            }
            catch (Exception ex)
            {
                RaiseMicrophoneStatusChanged($"Ошибка тестирования: {ex.Message}", "#f44336");
                Log.Error(ex, "Ошибка при тестировании микрофона");
            }
            finally
            {
                _isTestingMicrophone = false;
            }
        }

        /// <summary>
        /// Применяет выбранное устройство к сервису
        /// </summary>
        public async Task ApplySelectedDeviceAsync()
        {
            try
            {
                if (_audioCaptureService != null && SelectedDevice != null)
                {
                    await _audioCaptureService.SetActiveDeviceAsync(SelectedDevice.Id);
                    Log.Debug("Применено новое аудио устройство: {DeviceId}", SelectedDevice.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при применении аудио устройства");
                throw;
            }
        }

        /// <summary>
        /// Находит устройство по ID
        /// </summary>
        public AudioDeviceItem? FindDeviceById(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return null;
            return AvailableDevices.FirstOrDefault(d => d.Id == deviceId);
        }

        #endregion

        #region Private Methods
        private void RaiseMicrophoneStatusChanged(string status, string colorHex)
        {
            MicrophoneStatusChanged?.Invoke(this, new MicrophoneStatusChangedEventArgs(status, colorHex));
        }
        #endregion
    }

    #region Event Args
    /// <summary>
    /// Аргументы события изменения статуса микрофона
    /// </summary>
    public class MicrophoneStatusChangedEventArgs : EventArgs
    {
        public string Status { get; }
        public string ColorHex { get; }

        public MicrophoneStatusChangedEventArgs(string status, string colorHex)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
            ColorHex = colorHex ?? throw new ArgumentNullException(nameof(colorHex));
        }
    }
    #endregion
}