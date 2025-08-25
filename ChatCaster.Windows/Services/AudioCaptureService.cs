using NAudio.Wave;
using NAudio.CoreAudioApi;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация аудио захвата через NAudio для Windows
/// </summary>
public class AudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<AudioCaptureService>();
    private readonly WindowsAudioCompatibility _compatibility;
    
    public event EventHandler<float>? VolumeChanged;
    public event EventHandler<byte[]>? AudioDataReceived;

    private WaveInEvent? _waveIn;
    private WasapiCapture? _wasapiCapture; // Добавляем поддержку WASAPI
    private readonly Lock _lockObject = new();
    private bool _isDisposed;

    public bool IsCapturing { get; private set; }
    public float CurrentVolume { get; private set; }
    public AudioDevice? ActiveDevice { get; private set; }

    public AudioCaptureService(WindowsAudioCompatibility compatibility)
    {
        _compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
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
                    try
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
                        
                        _logger.Information("Найдено WaveIn устройство {Index}: {Name}", i, caps.ProductName);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Ошибка получения информации о WaveIn устройстве {Index}", i);
                    }
                }

                // WASAPI устройства (современный Windows Audio API) - только если поддерживается
                if (_compatibility.IsWasapiSupported())
                {
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
                        
                        _logger.Information("Найдено WASAPI устройство: {Name} (ID: {Id})", device.FriendlyName, device.ID);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Ошибка получения информации о WASAPI устройстве {Id}", device.ID);
                    }
                }
                }
                else
                {
                    _logger.Information("WASAPI не поддерживается на данной системе, используем только WaveIn");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Общая ошибка получения аудио устройств");
                throw new InvalidOperationException($"Ошибка получения аудио устройств: {ex.Message}", ex);
            }

            var finalDevices = devices.DistinctBy(d => d.Name).ToList();
            _logger.Information("Всего найдено уникальных устройств: {Count}", finalDevices.Count);
            
            return finalDevices;
        });
    }

    public async Task<AudioDevice?> GetDefaultDeviceAsync()
    {
        var devices = await GetAvailableDevicesAsync();
        var defaultDevice = devices.FirstOrDefault(d => d.IsDefault);
        
        if (defaultDevice == null)
        {
            // Если нет устройства по умолчанию, берем первое доступное
            defaultDevice = devices.FirstOrDefault(d => d.IsEnabled);
        }
        
        _logger.Information("Устройство по умолчанию: {Device}", defaultDevice?.Name ?? "не найдено");
        return defaultDevice;
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

            if (ActiveDevice != null)
            {
                _logger.Information("Активное устройство установлено: {Name} (ID: {Id})", ActiveDevice.Name, ActiveDevice.Id);
                return true;
            }
            else
            {
                _logger.Warning("Устройство с ID {DeviceId} не найдено", deviceId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка установки аудио устройства {DeviceId}", deviceId);
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
                    
                    var deviceId = config.SelectedDeviceId;
                    _logger.Information("Попытка запуска захвата для устройства: {DeviceId}", deviceId);

                    if (string.IsNullOrEmpty(deviceId))
                    {
                        throw new InvalidOperationException("Не указан ID устройства");
                    }

                    if (deviceId.StartsWith("wavein:"))
                    {
                        return StartWaveInCapture(deviceId, config);
                    }
                    else if (deviceId.StartsWith("wasapi:"))
                    {
                        return StartWasapiCapture(deviceId, config);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Неизвестный тип устройства: {deviceId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка запуска захвата аудио");
                    throw new InvalidOperationException($"Ошибка запуска захвата аудио: {ex.Message}", ex);
                }
            }
        });
    }

    private bool StartWaveInCapture(string deviceId, AudioConfig config)
    {
        if (!int.TryParse(deviceId.Substring(7), out int deviceNumber))
        {
            throw new InvalidOperationException($"Некорректный номер WaveIn устройства: {deviceId}");
        }

        // Проверяем, что устройство существует
        if (deviceNumber >= WaveInEvent.DeviceCount || deviceNumber < 0)
        {
            throw new InvalidOperationException($"WaveIn устройство {deviceNumber} не существует. Доступно устройств: {WaveInEvent.DeviceCount}");
        }

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1), // Принудительно 16kHz для Whisper
            BufferMilliseconds = 50
        };

        // Подписываемся на события
        _waveIn.DataAvailable += OnWaveInDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        // Запускаем захват
        _waveIn.StartRecording();
        IsCapturing = true;
        
        _logger.Information("WaveIn захват запущен для устройства {DeviceNumber}", deviceNumber);
        return true;
    }

    private bool StartWasapiCapture(string deviceId, AudioConfig config)
    {
        var wasapiDeviceId = deviceId.Substring(7); // Убираем префикс "wasapi:"
        
        using var deviceEnumerator = new MMDeviceEnumerator();
        MMDevice? device = null;
        
        // Ищем устройство по ID
        var devices = deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        foreach (var d in devices)
        {
            if (d.ID == wasapiDeviceId)
            {
                device = d;
                break;
            }
        }

        if (device == null)
        {
            throw new InvalidOperationException($"WASAPI устройство {wasapiDeviceId} не найдено");
        }

        _wasapiCapture = new WasapiCapture(device);
        
        // Подписываемся на события
        _wasapiCapture.DataAvailable += OnWasapiDataAvailable;
        _wasapiCapture.RecordingStopped += OnRecordingStopped;

        // Запускаем захват
        _wasapiCapture.StartRecording();
        IsCapturing = true;
        
        _logger.Information("WASAPI захват запущен для устройства {DeviceName}", device.FriendlyName);
        return true;
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
                        _waveIn.DataAvailable -= OnWaveInDataAvailable;  
                        _waveIn.RecordingStopped -= OnRecordingStopped;  
                        _waveIn.StopRecording();
                        _waveIn.Dispose();
                        _waveIn = null;
                        _logger.Information("WaveIn захват остановлен");
                    }

                    if (_wasapiCapture != null && IsCapturing)
                    {
                        _wasapiCapture.DataAvailable -= OnWasapiDataAvailable;
                        _wasapiCapture.RecordingStopped -= OnRecordingStopped;
                        _wasapiCapture.StopRecording();
                        _wasapiCapture.Dispose();
                        _wasapiCapture = null;
                        _logger.Information("WASAPI захват остановлен");
                    }

                    IsCapturing = false;
                    CurrentVolume = 0;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Ошибка остановки захвата аудио");
                    throw new InvalidOperationException($"Ошибка остановки захвата аудио: {ex.Message}", ex);
                }
            }
        });
    }

    private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
    {
        ProcessAudioData(e.Buffer, e.BytesRecorded);
    }

    private void OnWasapiDataAvailable(object? sender, WaveInEventArgs e)
    {
        ProcessAudioData(e.Buffer, e.BytesRecorded);
    }

    private void ProcessAudioData(byte[] buffer, int bytesRecorded)
    {
        try
        {
            // Вычисляем уровень громкости
            float volume = CalculateVolume(buffer, bytesRecorded);
            CurrentVolume = volume;
            VolumeChanged?.Invoke(this, volume);
        
            // Передаем аудио данные
            var audioData = new byte[bytesRecorded];
            Array.Copy(buffer, audioData, bytesRecorded);
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
        else
        {
            _logger.Information("Запись остановлена нормально");
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

        if (name.Contains("usb") || name.Contains("rode")) return AudioDeviceType.UsbMicrophone;
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
            AudioDevice? activeDevice;
        
            // Читаем под блокировкой
            lock (_lockObject)
            {
                activeDevice = ActiveDevice;
            }
    
            if (activeDevice == null)
            {
                _logger.Information("Нет активного аудио устройства для тестирования");
                return false;
            }

            // Создаем тестовую конфигурацию
            var testConfig = new AudioConfig
            {
                SelectedDeviceId = activeDevice.Id,
                SampleRate = 16000,
                Channels = 1,
                BitsPerSample = 16,
                MaxRecordingSeconds = 1,
                MinRecordingSeconds = 1,
                VolumeThreshold = 0.01f
            };

            _logger.Information("Тестируем устройство: {Name} (ID: {Id})", activeDevice.Name, activeDevice.Id);

            // Пробуем кратковременный захват аудио
            bool captureStarted = await StartCaptureAsync(testConfig);

            if (captureStarted)
            {
                await Task.Delay(500); // Записываем полсекунды
                await StopCaptureAsync();
                _logger.Information("Тест микрофона успешен для устройства: {Name}", activeDevice.Name);
                return true;
            }

            _logger.Warning("Не удалось запустить захват для тестирования устройства: {DeviceName}", activeDevice.Name);
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
                lock (_lockObject)
                {
                    // Останавливаем захват синхронно
                    if (_waveIn != null && IsCapturing)
                    {
                        _waveIn.StopRecording();
                        _waveIn.DataAvailable -= OnWaveInDataAvailable;
                        _waveIn.RecordingStopped -= OnRecordingStopped;
                        _waveIn.Dispose();
                        _waveIn = null;
                    }

                    if (_wasapiCapture != null && IsCapturing)
                    {
                        _wasapiCapture.StopRecording();
                        _wasapiCapture.DataAvailable -= OnWasapiDataAvailable;
                        _wasapiCapture.RecordingStopped -= OnRecordingStopped;
                        _wasapiCapture.Dispose();
                        _wasapiCapture = null;
                    }

                    IsCapturing = false;
                    CurrentVolume = 0;
                    ActiveDevice = null;
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