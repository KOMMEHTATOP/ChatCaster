using NAudio.Wave;
using NAudio.CoreAudioApi;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Exceptions;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация аудио захвата через NAudio для Windows
/// </summary>
public class AudioCaptureService : IAudioCaptureService, IDisposable
{
    public event EventHandler<float>? VolumeChanged;
    public event EventHandler<byte[]>? AudioDataReceived;

    private WaveInEvent? _waveIn;
    private AudioConfig? _currentConfig;
    private readonly object _lockObject = new();
    private bool _isDisposed;

    public bool IsCapturing { get; private set; }
    public float CurrentVolume { get; private set; }
    public AudioDevice? ActiveDevice { get; private set; }

    public async Task<IEnumerable<AudioDevice>> GetAvailableDevicesAsync()
    {
        return await Task.Run(() =>
        {
            var devices = new List<AudioDevice>();

            try
            {
                // WaveIn устройства (включая USB микрофоны)
                for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    
                    devices.Add(new AudioDevice
                    {
                        Id = $"wavein:{i}",
                        Name = caps.ProductName,
                        Description = $"WaveIn Device #{i}",
                        IsDefault = i == 0,
                        IsEnabled = true,
                        Type = DetectDeviceType(caps.ProductName),
                        MaxChannels = caps.Channels,
                        SupportedSampleRates = new[] { 8000, 11025, 16000, 22050, 44100, 48000 }
                    });
                }

                // WASAPI устройства (современный Windows Audio API)
                using var deviceEnumerator = new MMDeviceEnumerator();
                var wasapiDevices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                
                foreach (var device in wasapiDevices)
                {
                    try
                    {
                        devices.Add(new AudioDevice
                        {
                            Id = $"wasapi:{device.ID}",
                            Name = device.FriendlyName,
                            Description = device.DeviceFriendlyName,
                            IsDefault = device.ID == deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications).ID,
                            IsEnabled = device.State == DeviceState.Active,
                            Type = DetectDeviceType(device.FriendlyName),
                            MaxChannels = device.AudioClient.MixFormat.Channels,
                            SupportedSampleRates = new[] { 8000, 16000, 22050, 44100, 48000 }
                        });
                    }
                    catch
                    {
                        // Пропускаем проблемные устройства
                    }
                }
            }
            catch (Exception ex)
            {
                throw new AudioException($"Ошибка получения аудио устройств: {ex.Message}", ex);
            }

            return devices.DistinctBy(d => d.Name).ToList();
        });
    }

    public async Task<AudioDevice?> GetDefaultDeviceAsync()
    {
        var devices = await GetAvailableDevicesAsync();
        return devices.FirstOrDefault(d => d.IsDefault);
    }

    public async Task<bool> SetActiveDeviceAsync(string deviceId)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Останавливаем текущий захват если идет
                if (IsCapturing)
                {
                    StopCaptureAsync().Wait();
                }

                // Запоминаем активное устройство
                var devices = GetAvailableDevicesAsync().Result;
                ActiveDevice = devices.FirstOrDefault(d => d.Id == deviceId);
                
                return ActiveDevice != null;
            }
            catch (Exception ex)
            {
                throw new AudioException($"Ошибка установки аудио устройства {deviceId}: {ex.Message}", ex);
            }
        });
    }

    public async Task<bool> StartCaptureAsync(AudioConfig config)
    {
        return await Task.Run(() =>
        {
            lock (_lockObject)
            {
                try
                {
                    if (IsCapturing)
                    {
                        throw new AudioException("Захват уже запущен");
                    }

                    _currentConfig = config;

                    // Определяем устройство
                    int deviceNumber = 0;
                    if (!string.IsNullOrEmpty(config.SelectedDeviceId))
                    {
                        if (config.SelectedDeviceId.StartsWith("wavein:"))
                        {
                            int.TryParse(config.SelectedDeviceId.Substring(7), out deviceNumber);
                        }
                    }

                    // Создаем WaveIn
                    Console.WriteLine($"Исходный формат: {config.SampleRate}Hz, {config.BitsPerSample}bit, {config.Channels}ch");
                    Console.WriteLine("Принудительно устанавливаем: 16000Hz, 16bit, 1ch для Whisper");

                    _waveIn = new WaveInEvent
                    {
                        DeviceNumber = deviceNumber,
                        WaveFormat = new WaveFormat(16000, 16, 1), // Принудительно 16kHz для Whisper
                        BufferMilliseconds = 50
                    };
                    Console.WriteLine($"Аудио формат: {config.SampleRate}Hz, {config.BitsPerSample}bit, {config.Channels}ch");

                    // Подписываемся на события
                    _waveIn.DataAvailable += OnDataAvailable;
                    _waveIn.RecordingStopped += OnRecordingStopped;

                    // Запускаем захват
                    _waveIn.StartRecording();
                    IsCapturing = true;

                    return true;
                }
                catch (Exception ex)
                {
                    throw new AudioException($"Ошибка запуска захвата аудио: {ex.Message}", ex);
                }
            }
        });
    }

    public async Task StopCaptureAsync()
    {
        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                try
                {
                    if (_waveIn != null && IsCapturing)
                    {
                        _waveIn.StopRecording();
                        _waveIn.Dispose();
                        _waveIn = null;
                    }

                    IsCapturing = false;
                    CurrentVolume = 0;
                }
                catch (Exception ex)
                {
                    throw new AudioException($"Ошибка остановки захвата аудио: {ex.Message}", ex);
                }
            }
        });
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            // Вычисляем уровень громкости
            float volume = CalculateVolume(e.Buffer, e.BytesRecorded);
            CurrentVolume = volume;
            VolumeChanged?.Invoke(this, volume);

            // Отправляем аудио данные
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);
            AudioDataReceived?.Invoke(this, audioData);
        }
        catch (Exception ex)
        {
            // Логируем ошибку, но не останавливаем захват
            System.Diagnostics.Debug.WriteLine($"Ошибка обработки аудио данных: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            System.Diagnostics.Debug.WriteLine($"Запись остановлена с ошибкой: {e.Exception.Message}");
        }
    }

    private static float CalculateVolume(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded == 0) return 0;

        // Преобразуем байты в 16-битные сэмплы
        float sum = 0;
        int sampleCount = bytesRecorded / 2;

        for (int i = 0; i < bytesRecorded; i += 2)
        {
            if (i + 1 < bytesRecorded)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                sum += Math.Abs(sample);
            }
        }

        // Нормализуем к диапазону 0-1
        float average = sum / sampleCount;
        return Math.Min(average / 32768.0f, 1.0f);
    }

    private static AudioDeviceType DetectDeviceType(string deviceName)
    {
        var name = deviceName.ToLower();
        
        if (name.Contains("usb")) return AudioDeviceType.UsbMicrophone;
        if (name.Contains("bluetooth")) return AudioDeviceType.BluetoothMicrophone;
        if (name.Contains("webcam") || name.Contains("camera")) return AudioDeviceType.WebcamMicrophone;
        if (name.Contains("headset")) return AudioDeviceType.HeadsetMicrophone;
        if (name.Contains("line")) return AudioDeviceType.LineIn;
        
        return AudioDeviceType.Microphone;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            StopCaptureAsync().Wait();
            _isDisposed = true;
        }
    }
}