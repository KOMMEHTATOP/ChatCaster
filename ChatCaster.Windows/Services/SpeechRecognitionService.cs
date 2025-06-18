using ChatCaster.Core.Services;
using ChatCaster.Core.Models;

namespace ChatCaster.Windows.Services;

/// <summary>
/// ЗАГЛУШКА - Реализация распознавания речи (пока без Whisper.net)
/// </summary>
public class SpeechRecognitionService : ISpeechRecognitionService, IDisposable
{
    private bool _isDisposed;

    public bool IsInitialized { get; private set; }
    public WhisperModel CurrentModel { get; private set; } = WhisperModel.Base;

    public async Task<bool> InitializeAsync(WhisperConfig config)
    {
        await Task.Delay(100); // Имитация инициализации
        CurrentModel = config.Model;
        IsInitialized = true;
        return true;
    }

    public async Task<VoiceProcessingResult> RecognizeAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            return new VoiceProcessingResult
            {
                Success = false,
                ErrorMessage = "Сервис не инициализирован"
            };
        }

        await Task.Delay(300, cancellationToken); // Имитация обработки
        
        return new VoiceProcessingResult
        {
            Success = true,
            RecognizedText = "Тестовое распознавание (заглушка)",
            Confidence = 0.95f,
            ProcessingTime = TimeSpan.FromMilliseconds(300)
        };
    }

    public async Task<bool> ChangeModelAsync(WhisperModel model)
    {
        await Task.Delay(50);
        CurrentModel = model;
        return true;
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
            IsInitialized = false;
            _isDisposed = true;
        }
    }
}