using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using Serilog;

namespace ChatCaster.Windows.Managers.AudioSettings
{
    /// <summary>
    /// Менеджер для работы с аудио устройствами
    /// Централизует логику, которая дублировалась между MainPage и AudioSettings
    /// </summary>
    public class AudioDeviceManager
    {
        private readonly IAudioCaptureService _audioService;

        public AudioDeviceManager(IAudioCaptureService audioService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        /// <summary>
        /// Загружает список доступных аудио устройств
        /// </summary>
        public async Task<List<AudioDevice>> LoadAvailableDevicesAsync()
        {
            try
            {
                var devices = await _audioService.GetAvailableDevicesAsync();
                var deviceList = devices.ToList();
                
                Log.Information("AudioDeviceManager загружено {Count} аудио устройств", deviceList.Count);
                return deviceList;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка загрузки аудио устройств в AudioDeviceManager");
                return new List<AudioDevice>();
            }
        }

        /// <summary>
        /// Находит устройство по ID из списка доступных устройств
        /// </summary>
        public AudioDevice? FindDeviceById(List<AudioDevice> availableDevices, string? deviceId)
        {
            if (string.IsNullOrEmpty(deviceId) || !availableDevices.Any())
                return null;

            var device = availableDevices.FirstOrDefault(d => d.Id == deviceId);
            
            if (device != null)
            {
                Log.Debug("AudioDeviceManager найдено устройство: {DeviceName} для ID: {DeviceId}", 
                    device.Name, deviceId);
            }
            else
            {
                Log.Warning("AudioDeviceManager устройство не найдено для ID: {DeviceId}", deviceId);
            }

            return device;
        }

        /// <summary>
        /// Выбирает устройство по умолчанию из списка (дефолтное или первое)
        /// </summary>
        public AudioDevice? SelectDefaultDevice(List<AudioDevice> availableDevices)
        {
            if (!availableDevices.Any())
                return null;

            var defaultDevice = availableDevices.FirstOrDefault(d => d.IsDefault) 
                               ?? availableDevices.First();
            
            Log.Information("AudioDeviceManager выбрано устройство по умолчанию: {DeviceName}", defaultDevice.Name);
            return defaultDevice;
        }

        /// <summary>
        /// Устанавливает активное устройство в сервисе
        /// </summary>
        public async Task<bool> SetActiveDeviceAsync(string deviceId)
        {
            try
            {
                await _audioService.SetActiveDeviceAsync(deviceId);
                Log.Information("AudioDeviceManager установлено активное устройство: {DeviceId}", deviceId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка установки активного устройства: {DeviceId}", deviceId);
                return false;
            }
        }

        /// <summary>
        /// Тестирует микрофон
        /// </summary>
        public async Task<bool> TestMicrophoneAsync()
        {
            try
            {
                Log.Debug("AudioDeviceManager тестирование микрофона");
                var result = await _audioService.TestMicrophoneAsync();
                Log.Information("AudioDeviceManager тест микрофона: {Result}", result ? "успешно" : "неудачно");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка тестирования микрофона в AudioDeviceManager");
                return false;
            }
        }
    }
}