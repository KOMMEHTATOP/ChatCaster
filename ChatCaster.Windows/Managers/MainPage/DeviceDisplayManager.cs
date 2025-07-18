using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.System;
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
        private readonly ILocalizationService _localizationService;

        public DeviceDisplayManager(
            IAudioCaptureService audioService,
            IConfigurationService configurationService,
            ILocalizationService localizationService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
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
                    var notSelectedText = _localizationService.GetString("Device_NotSelected");
                    var deviceLabelText = _localizationService.GetString("Device_Label");
                    return new DeviceDisplayInfo(notSelectedText, $"{deviceLabelText}: {notSelectedText}");
                }

                // Получаем список доступных устройств
                var devices = await _audioService.GetAvailableDevicesAsync();
                var selectedDevice = devices.FirstOrDefault(d => d.Id == selectedDeviceId);

                if (selectedDevice != null)
                {
                    var deviceLabelText = _localizationService.GetString("Device_Label");
                    return new DeviceDisplayInfo(selectedDevice.Name, $"{deviceLabelText}: {selectedDevice.Name}");
                }
                else
                {
                    Log.Warning("DeviceDisplayManager: устройство не найдено: {DeviceId}", selectedDeviceId);
                    var unavailableText = _localizationService.GetString("Device_Unavailable");
                    var deviceLabelText = _localizationService.GetString("Device_Label");
                    return new DeviceDisplayInfo(unavailableText, $"{deviceLabelText}: {unavailableText} ({selectedDeviceId})");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DeviceDisplayManager: ошибка получения информации об устройстве");
                var errorText = _localizationService.GetString("Device_Error");
                var deviceLabelText = _localizationService.GetString("Device_Label");
                return new DeviceDisplayInfo(errorText, $"{deviceLabelText}: {errorText}");
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