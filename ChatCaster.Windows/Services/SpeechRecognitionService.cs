using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
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
           Debug.WriteLine($"[SpeechRecognition] Начало инициализации модели: {config.Model}");
           CurrentModel = config.Model;

           // Определяем путь к модели
           string modelPath = GetModelPath(config.Model);
           Debug.WriteLine($"[SpeechRecognition] Путь к модели: {modelPath}");

           // Проверяем существует ли модель, если нет - скачиваем
           if (!File.Exists(modelPath))
           {
               Debug.WriteLine($"[SpeechRecognition] Модель не найдена, начинаем скачивание...");
               await DownloadModelAsync(config.Model, modelPath);
           }

           // Создаем WhisperFactory
           Debug.WriteLine($"[SpeechRecognition] Загружаем модель из файла...");
           _whisperFactory = WhisperFactory.FromPath(modelPath);
           Debug.WriteLine($"[SpeechRecognition] WhisperFactory создан успешно");

           _processor = _whisperFactory.CreateBuilder()
               .WithLanguage("ru") // или из config.Language
               .Build();

           IsInitialized = true;
           Debug.WriteLine($"[SpeechRecognition] Инициализация завершена успешно");
           return true;
       }
       catch (Exception ex)
       {
           Debug.WriteLine($"[SpeechRecognition] ОШИБКА инициализации: {ex.Message}");
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
           Debug.WriteLine($"[SpeechRecognition] Создаем папку Models...");
           Directory.CreateDirectory("Models");

           var modelType = GetGgmlType(model);
           Debug.WriteLine($"[SpeechRecognition] Тип модели для скачивания: {modelType}");

           // Создаем HttpClient и загрузчик
           using var httpClient = new HttpClient();
           var downloader = new WhisperGgmlDownloader(httpClient);

           Debug.WriteLine($"[SpeechRecognition] Начинаем скачивание модели...");
           // Скачиваем модель
           using var modelStream = await downloader.GetGgmlModelAsync(modelType);

           Debug.WriteLine($"[SpeechRecognition] Сохраняем модель в файл: {modelPath}");
           // Сохраняем в файл
           using var fileStream = File.Create(modelPath);
           await modelStream.CopyToAsync(fileStream);
           
           Debug.WriteLine($"[SpeechRecognition] Модель успешно скачана и сохранена");
       }
       catch (Exception ex)
       {
           Debug.WriteLine($"[SpeechRecognition] ОШИБКА скачивания модели: {ex.Message}");
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
           Debug.WriteLine($"[SpeechRecognition] ОШИБКА: Сервис не инициализирован");
           return new VoiceProcessingResult
           {
               Success = false, ErrorMessage = "Сервис не инициализирован"
           };
       }

       try
       {
           var startTime = DateTime.Now;
           Debug.WriteLine($"[SpeechRecognition] Начало распознавания, размер данных: {audioData?.Length ?? 0} байт");
           
           // Проверяем что данные не пустые
           if (audioData == null || audioData.Length == 0)
           {
               Debug.WriteLine($"[SpeechRecognition] ОШИБКА: Не получено аудио данных");
               return new VoiceProcessingResult
               {
                   Success = false, 
                   ErrorMessage = "Не получено аудио данных"
               };
           }

           // Минимальная проверка размера (хотя бы 0.5 секунды на 16kHz = 16000 байт)
           if (audioData.Length < 16000)
           {
               Debug.WriteLine($"[SpeechRecognition] ОШИБКА: Слишком короткая запись ({audioData.Length} байт)");
               return new VoiceProcessingResult
               {
                   Success = false, 
                   ErrorMessage = "Слишком короткая запись"
               };
           }

           Debug.WriteLine($"[SpeechRecognition] Создаем WAV поток в памяти...");
           // Создаем WAV поток в памяти
           using var wavStream = CreateWavMemoryStream(audioData, 16000, 16, 1);
           Debug.WriteLine($"[SpeechRecognition] WAV поток создан, размер: {wavStream.Length} байт");
           
           // Сбрасываем позицию потока в начало
           wavStream.Position = 0;
           
           // Распознаем речь из потока
           Debug.WriteLine($"[SpeechRecognition] Начинаем обработку через Whisper...");
           
           await foreach (var result in _processor.ProcessAsync(wavStream, cancellationToken))
           {
               Debug.WriteLine($"[SpeechRecognition] Получен результат: '{result.Text}', начало: {result.Start}, конец: {result.End}");
               
               if (!string.IsNullOrWhiteSpace(result.Text))
               {
                   var processingTime = DateTime.Now - startTime;
                   Debug.WriteLine($"[SpeechRecognition] УСПЕХ: Распознано за {processingTime.TotalMilliseconds:F0}мс");
                   
                   return new VoiceProcessingResult
                   {
                       Success = true,
                       RecognizedText = result.Text.Trim(),
                       Confidence = 0.95f,
                       ProcessingTime = processingTime
                   };
               }
           }

           Debug.WriteLine($"[SpeechRecognition] ОШИБКА: Не удалось распознать речь (пустой результат)");
           return new VoiceProcessingResult
           {
               Success = false, ErrorMessage = "Не удалось распознать речь"
           };
       }
       catch (Exception ex)
       {
           Debug.WriteLine($"[SpeechRecognition] КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
           Debug.WriteLine($"[SpeechRecognition] Stack trace: {ex.StackTrace}");
           return new VoiceProcessingResult
           {
               Success = false, ErrorMessage = $"Ошибка распознавания: {ex.Message}"
           };
       }
   }

   private MemoryStream CreateWavMemoryStream(byte[] audioData, int sampleRate, int bitsPerSample, int channels)
   {
       Debug.WriteLine($"[SpeechRecognition] Создание WAV потока: {sampleRate}Hz, {bitsPerSample}bit, {channels}ch");
       
       var stream = new MemoryStream();
       using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

       var dataLength = audioData.Length;
       var fileSize = 36 + dataLength;

       // WAV заголовок
       writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
       writer.Write(fileSize);
       writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
       writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
       writer.Write(16); // размер fmt секции
       writer.Write((short)1); // PCM формат
       writer.Write((short)channels);
       writer.Write(sampleRate);
       writer.Write(sampleRate * channels * bitsPerSample / 8); // байт в секунду
       writer.Write((short)(channels * bitsPerSample / 8)); // байт на семпл
       writer.Write((short)bitsPerSample);
       writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
       writer.Write(dataLength);
       writer.Write(audioData);

       Debug.WriteLine($"[SpeechRecognition] WAV поток создан, итоговый размер: {stream.Length} байт");
       return stream;
   }

   public async Task<bool> ChangeModelAsync(WhisperModel model)
   {
       try
       {
           Debug.WriteLine($"[SpeechRecognition] Смена модели с {CurrentModel} на {model}");
           
           // Останавливаем текущий процессор
           _processor?.Dispose();
           _whisperFactory?.Dispose();
           IsInitialized = false;

           CurrentModel = model;

           // Инициализируем с новой моделью
           var config = new WhisperConfig
           {
               Model = model
           };
           var result = await InitializeAsync(config);
           
           Debug.WriteLine($"[SpeechRecognition] Смена модели {(result ? "УСПЕШНА" : "НЕУДАЧНА")}");
           return result;
       }
       catch (Exception ex)
       {
           Debug.WriteLine($"[SpeechRecognition] ОШИБКА смены модели: {ex.Message}");
           return false;
       }
   }

   public async Task<long> GetModelSizeAsync(WhisperModel model)
   {
       return await Task.Run(() =>
       {
           // Реальные размеры моделей на основе ваших данных
           return model switch
           {
               WhisperModel.Tiny => 75_871L * 1024,      // ~75.8 MB
               WhisperModel.Base => 144_484L * 1024,     // ~144.5 MB  
               WhisperModel.Small => 476_174L * 1024,    // ~476.2 MB
               WhisperModel.Medium => 1_497_816L * 1024, // ~1.5 GB
               WhisperModel.Large => 3_022_494L * 1024,  // ~3.0 GB
               _ => 144_484L * 1024 // по умолчанию Base
           };
       });
   }

   public void Dispose()
   {
       if (!_isDisposed)
       {
           Debug.WriteLine($"[SpeechRecognition] Освобождение ресурсов...");
           _processor?.Dispose();
           _whisperFactory?.Dispose();
           IsInitialized = false;
           _isDisposed = true;
           Debug.WriteLine($"[SpeechRecognition] Ресурсы освобождены");
       }
   }
}