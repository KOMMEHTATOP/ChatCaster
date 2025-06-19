using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using System.IO;
using System.Net.Http;
using Whisper.net;
using Whisper.net.Ggml;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация распознавания речи через Whisper.net
/// </summary>
public class SpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _processor;
    private bool _isDisposed;

    public bool IsInitialized { get; private set; }
    public WhisperModel CurrentModel { get; private set; } = WhisperModel.Tiny;

    public async Task<bool> InitializeAsync(WhisperConfig config)
    {
        try
        {
            CurrentModel = config.Model;

            // Определяем путь к модели
            string modelPath = GetModelPath(config.Model);

            // Проверяем существует ли модель, если нет - скачиваем
            if (!File.Exists(modelPath))
            {
                await DownloadModelAsync(config.Model, modelPath);
            }

            // Создаем WhisperFactory
            Console.WriteLine($"Загружаем модель: {modelPath}");
            _whisperFactory = WhisperFactory.FromPath(modelPath);
            Console.WriteLine("WhisperFactory создан успешно");

            _processor = _whisperFactory.CreateBuilder()
                .WithLanguage("ru") // или из config.Language
                .Build();

            IsInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка инициализации Whisper: {ex.Message}");
            return false;
        }
    }

    private string GetModelPath(WhisperModel model)
    {
        string modelName = model.ToString().ToLower();
        return Path.Combine("Models", $"ggml-{modelName}.bin");
    }

    private async Task DownloadModelAsync(WhisperModel model, string modelPath)
    {
        try
        {
            // Создаем папку Models если её нет
            Directory.CreateDirectory("Models");

            // Whisper.net может автоматически скачать модель
            var modelType = GetGgmlType(model);

            // Создаем HttpClient и загрузчик
            using var httpClient = new HttpClient();
            var downloader = new WhisperGgmlDownloader(httpClient);

            // Скачиваем модель
            using var modelStream = await downloader.GetGgmlModelAsync(modelType);

            // Сохраняем в файл
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream);

            System.Diagnostics.Debug.WriteLine($"Модель {model} скачана: {modelPath}");
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка скачивания модели {model}: {ex.Message}", ex);
        }
    }


    private GgmlType GetGgmlType(WhisperModel model)
    {
        return model switch
        {
            WhisperModel.Tiny => GgmlType.Tiny,
            WhisperModel.Base => GgmlType.Base,
            WhisperModel.Small => GgmlType.Small,
            WhisperModel.Medium => GgmlType.Medium,
            WhisperModel.Large => GgmlType.LargeV3,
            _ => GgmlType.Tiny
        };
    }

    public async Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized || _processor == null)
        {
            return new VoiceProcessingResult
            {
                Success = false, ErrorMessage = "Сервис не инициализирован"
            };
        }

        try
        {
            var startTime = DateTime.Now;

            Console.WriteLine($"Получено аудио данных: {audioData.Length} байт");

            // Создаем временный WAV файл
            var tempFile = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid()}.wav");
            CreateWavFile(tempFile, audioData, 16000, 16, 1);

            Console.WriteLine($"Создан временный файл: {tempFile}");

            // Распознаем речь из файла
            try
            {
                // Ждем немного, чтобы файл точно записался
                await Task.Delay(100, cancellationToken);

                using var fileStream = File.OpenRead(tempFile);

                await foreach (var result in _processor.ProcessAsync(fileStream, cancellationToken))
                {
                    Console.WriteLine($"Whisper результат: '{result.Text}'");

                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        return new VoiceProcessingResult
                        {
                            Success = true,
                            RecognizedText = result.Text.Trim(),
                            Confidence = 0.95f,
                            ProcessingTime = DateTime.Now - startTime
                        };
                    }
                }

                return new VoiceProcessingResult
                {
                    Success = false, ErrorMessage = "Не удалось распознать речь"
                };
            }
            finally
            {
                // Удаляем файл в блоке finally
                try
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ОШИБКА WHISPER: {ex.Message}");
            return new VoiceProcessingResult
            {
                Success = false, ErrorMessage = $"Ошибка распознавания: {ex.Message}"
            };
        }
    }

    private void CreateWavFile(string filename, byte[] audioData, int sampleRate, int bitsPerSample, int channels)
    {
        using var fileStream = new FileStream(filename, FileMode.Create);
        using var writer = new BinaryWriter(fileStream);

        var dataLength = audioData.Length;

        // WAV заголовок
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write((short)bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        writer.Write(audioData);
    }
    private float[] ConvertBytesToFloat(byte[] audioData)
    {
        // NAudio дает нам 16-bit PCM данные
        int sampleCount = audioData.Length / 2;
        var floatArray = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // Конвертируем 16-bit signed integer в float (-1.0 to 1.0)
            short sample = BitConverter.ToInt16(audioData, i * 2);
            floatArray[i] = sample / 32768.0f;
        }

        return floatArray;
    }

    public async Task<bool> ChangeModelAsync(WhisperModel model)
    {
        try
        {
            // Останавливаем текущий процессор
            _processor?.Dispose();
            _whisperFactory?.Dispose();

            CurrentModel = model;

            // Инициализируем с новой моделью
            var config = new WhisperConfig
            {
                Model = model
            };
            return await InitializeAsync(config);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка смены модели: {ex.Message}");
            return false;
        }
    }

    public async Task<long> GetModelSizeAsync(WhisperModel model)
    {
        return await Task.Run(() =>
        {
            return model switch
            {
                WhisperModel.Tiny => 39L * 1024 * 1024,
                WhisperModel.Base => 74L * 1024 * 1024,
                WhisperModel.Small => 244L * 1024 * 1024,
                WhisperModel.Medium => 769L * 1024 * 1024,
                WhisperModel.Large => 1550L * 1024 * 1024,
                _ => 74L * 1024 * 1024
            };
        });
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _processor?.Dispose();
            _whisperFactory?.Dispose();
            IsInitialized = false;
            _isDisposed = true;
        }
    }
}
