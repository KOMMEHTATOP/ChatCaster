using NAudio.Wave;
using NAudio.CoreAudioApi;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация аудио захвата через NAudio для Windows
/// </summary>
public class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private static readonly ILogger _logger = Log.ForContext<AudioCaptureService>();
    
    public event EventHandler<float>? VolumeChanged;
    public event EventHandler<byte[]>? AudioDataReceived;

    private WaveInEvent? _waveIn;
    private AudioConfig? _currentConfig;
    private readonly Lock _lockObject = new();
    private bool _isDisposed;

    public bool IsCapturing { get; private set; }
    public float CurrentVolume { get; private set; }
    public AudioDevice? ActiveDevice { get; private set; }

    public AudioCaptureService()
    {
        _logger.Information("🔴 AudioCaptureService СОЗДАН - {HashCode}", GetHashCode());
    }


    
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
                        SupportedSampleRates = new[]
                        {
                            8000, 11025, 16000, 22050, 44100, 48000
                        }
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
                            IsDefault =
                                device.ID == deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                                    .ID,
                            IsEnabled = device.State == DeviceState.Active,
                            Type = DetectDeviceType(device.FriendlyName),
                            MaxChannels = device.AudioClient.MixFormat.Channels,
                            SupportedSampleRates = new[]
                            {
                                8000, 16000, 22050, 44100, 48000
                            }
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
                throw new InvalidOperationException($"Ошибка получения аудио устройств: {ex.Message}", ex);
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
        try
        {
            // Останавливаем текущий захват если идет
            if (IsCapturing)
            {
                await StopCaptureAsync();
            }

            // Запоминаем активное устройство
            var devices = await GetAvailableDevicesAsync();
            ActiveDevice = devices.FirstOrDefault(d => d.Id == deviceId);

            return ActiveDevice != null;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Ошибка установки аудио устройства {deviceId}: {ex.Message}", ex);
        }
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
                        throw new InvalidOperationException("Захват уже запущен");
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

                    // Создаем WaveIn - принудительно устанавливаем 16kHz для Whisper
                    _logger.Information("Устанавливаем аудио формат: 16000Hz, 16bit, 1ch для Whisper");

                    _waveIn = new WaveInEvent
                    {
                        DeviceNumber = deviceNumber,
                        WaveFormat = new WaveFormat(16000, 16, 1), // Принудительно 16kHz для Whisper
                        BufferMilliseconds = 50
                    };

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
                    throw new InvalidOperationException($"Ошибка запуска захвата аудио: {ex.Message}", ex);
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
                        _waveIn.DataAvailable -= OnDataAvailable;  
                        _waveIn.RecordingStopped -= OnRecordingStopped;  
                        _waveIn.StopRecording();
                        _waveIn.Dispose();
                        _waveIn = null;
                    }

                    IsCapturing = false;
                    CurrentVolume = 0;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Ошибка остановки захвата аудио: {ex.Message}", ex);
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
            
            if (_currentConfig == null || !(volume >= _currentConfig.VolumeThreshold))
                return;
            
            // Отправляем аудио данные
            var audioData = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, audioData, e.BytesRecorded);
            AudioDataReceived?.Invoke(this, audioData);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка обработки аудио данных");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.Error(e.Exception, "Запись остановлена с ошибкой");
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

    public async Task<bool> TestMicrophoneAsync()
    {
        try
        {
            if (ActiveDevice == null)
            {
                _logger.Information("Нет активного аудио устройства для тестирования");
                return false;
            }

            // Пробуем кратковременный захват аудио
            var testConfig = new AudioConfig
            {
                SelectedDeviceId = ActiveDevice.Id,
                SampleRate = 16000,
                Channels = 1,
                BitsPerSample = 16,
                MaxRecordingSeconds = _currentConfig!.MinRecordingSeconds
            };

            bool captureStarted = await StartCaptureAsync(testConfig);

            if (captureStarted)
            {
                await Task.Delay(500); // Записываем полсекунды
                await StopCaptureAsync();
                _logger.Information("Тест микрофона успешен");
                return true;
            }

            _logger.Information("Не удалось запустить захват для тестирования");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка тестирования микрофона");
            return false;
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            try
            {
                _logger.Information("AudioCaptureService Dispose начат");

                lock (_lockObject)
                {
                    // ✅ ИСПРАВЛЕНИЕ: Останавливаем захват синхронно
                    if (_waveIn != null && IsCapturing)
                    {
                        _logger.Information("Останавливаем WaveIn...");
                        _waveIn.StopRecording();
                        _waveIn.DataAvailable -= OnDataAvailable;
                        _waveIn.RecordingStopped -= OnRecordingStopped;
                        _waveIn.Dispose();
                        _waveIn = null;
                        _logger.Information("WaveIn остановлен");
                    }

                    IsCapturing = false;
                    CurrentVolume = 0;
                    ActiveDevice = null;
                    _currentConfig = null;

                    _logger.Information("AudioCaptureService Dispose завершен");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Ошибка в AudioCaptureService.Dispose");
            }
            finally
            {
                _isDisposed = true;
            }
        }
    }
}