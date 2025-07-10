using System.Timers;
using Serilog;

namespace ChatCaster.Windows.Managers.VoiceRecording;

/// <summary>
/// Менеджер полного жизненного цикла таймера автоостановки
/// </summary>
public class RecordingTimerManager : IDisposable
{
    public event EventHandler? AutoStopTriggered;

    private System.Timers.Timer? _recordingTimer;

    /// <summary>
    /// Запустить таймер автоостановки на указанное время
    /// </summary>
    public void StartAutoStopTimer(int maxSeconds)
    {
        // Останавливаем предыдущий таймер если есть
        StopTimer();

        _recordingTimer = new System.Timers.Timer(maxSeconds * 1000);
        _recordingTimer.Elapsed += OnRecordingTimerElapsed;
        _recordingTimer.AutoReset = false;
        _recordingTimer.Start();
        
        Log.Information($"⏰ Таймер автоостановки запущен на {maxSeconds} сек");
    }

    /// <summary>
    /// Остановить таймер (при ручной остановке записи)
    /// </summary>
    public void StopTimer()
    {
        if (_recordingTimer != null)
        {
            _recordingTimer.Stop();
            _recordingTimer.Elapsed -= OnRecordingTimerElapsed;
            _recordingTimer.Dispose();
            _recordingTimer = null;
        }
    }

    private void OnRecordingTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        var maxSeconds = ((System.Timers.Timer)sender!).Interval / 1000;
        Log.Information($"⏰ Время записи ({maxSeconds} сек) истекло, автоостановка");
        
        // Останавливаем таймер
        StopTimer();
        
        // Уведомляем о необходимости автоостановки
        AutoStopTriggered?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        StopTimer();
    }
}
