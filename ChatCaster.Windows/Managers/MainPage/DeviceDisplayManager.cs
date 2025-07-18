using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using Serilog;

namespace ChatCaster.Windows.Managers.MainPage
{
    /// <summary>
    /// Менеджер для отображения информации о текущем аудио устройстве
    /// </summary>
    public class DeviceDisplayManager
    {
        private readonly IAudioCaptureService _audioService;
        private readonly IConfigurationService _configurationService;

        public DeviceDisplayManager(
            IAudioCaptureService audioService,
            IConfigurationService configurationService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        }

        /// <summary>
        /// Получает информацию о текущем устройстве для отображения
        /// </summary>
        public async Task<DeviceDisplayInfo> GetCurrentDeviceDisplayAsync()
        {
            try
            {
                var currentConfig = _configurationService.CurrentConfig;
                var selectedDeviceId = currentConfig.Audio.SelectedDeviceId;
                
                if (string.IsNullOrEmpty(selectedDeviceId))
                {
                    return new DeviceDisplayInfo("Не выбран", "Устройство: Не выбрано");
                }

                // Получаем список доступных устройств
                var devices = await _audioService.GetAvailableDevicesAsync();
                var selectedDevice = devices.FirstOrDefault(d => d.Id == selectedDeviceId);

                if (selectedDevice != null)
                {
                    return new DeviceDisplayInfo(selectedDevice.Name, $"Устройство: {selectedDevice.Name}");
                }
                else
                {
                    Log.Warning("DeviceDisplayManager: устройство не найдено: {DeviceId}", selectedDeviceId);
                    return new DeviceDisplayInfo("Недоступно", $"Устройство: Недоступно ({selectedDeviceId})");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceDisplayManager: ошибка получения информации об устройстве");
                return new DeviceDisplayInfo("Ошибка", "Устройство: Ошибка получения");
            }
        }
    }

    #region Helper Classes

    /// <summary>
    /// Информация об устройстве для отображения
    /// </summary>
    public class DeviceDisplayInfo
    {
        public string ShortName { get; }
        public string FullDisplayText { get; }

        public DeviceDisplayInfo(string shortName, string fullDisplayText)
        {
            ShortName = shortName;
            FullDisplayText = fullDisplayText;
        }
    }

    #endregion
}