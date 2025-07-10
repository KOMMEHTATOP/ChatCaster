using Serilog;

namespace ChatCaster.Windows.Managers.VoiceRecording;

/// <summary>
/// Менеджер полного жизненного цикла буферизации аудио
/// </summary>
public class AudioBufferManager
{
    private readonly List<byte> _recordingBuffer = new();
    private bool _isRecording = false;

    /// <summary>
    /// Начать буферизацию - очищает буфер и включает прием данных
    /// </summary>
    public void StartBuffering()
    {
        lock (_recordingBuffer)
        {
            _recordingBuffer.Clear();
            _isRecording = true;
        }
    }

    /// <summary>
    /// Остановить буферизацию и получить все данные
    /// </summary>
    public byte[] StopBufferingAndGetData()
    {
        lock (_recordingBuffer)
        {
            _isRecording = false;
            var audioData = _recordingBuffer.ToArray();
            _recordingBuffer.Clear();
            
            Log.Information($"📤 Получено {audioData.Length} байт для распознавания");
            return audioData;
        }
    }

    /// <summary>
    /// Отменить буферизацию - очищает буфер
    /// </summary>
    public void CancelBuffering()
    {
        lock (_recordingBuffer)
        {
            _isRecording = false;
            _recordingBuffer.Clear();
        }
    }

    /// <summary>
    /// Добавить аудио данные (только если буферизация активна)
    /// </summary>
    public void OnAudioDataReceived(byte[] audioData)
    {
        if (_isRecording)
        {
            lock (_recordingBuffer)
            {
                _recordingBuffer.AddRange(audioData);
            }
        }
    }
}
