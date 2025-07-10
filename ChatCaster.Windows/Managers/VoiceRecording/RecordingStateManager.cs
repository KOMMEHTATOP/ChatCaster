using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using Serilog;

namespace ChatCaster.Windows.Managers.VoiceRecording;

/// <summary>
/// Менеджер полного жизненного цикла состояния записи
/// </summary>
public class RecordingStateManager
{
    public event EventHandler<RecordingStatusChangedEvent>? StatusChanged;

    private RecordingState _currentState = new RecordingState();
    private readonly object _stateLock = new object();

    public RecordingState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
        private set
        {
            lock (_stateLock)
            {
                if (_currentState.Status != value.Status)
                {
                    var oldStatus = _currentState.Status;
                    _currentState = value;
                    Log.Information($"🔄 Состояние записи: {oldStatus} → {value.Status}");

                    StatusChanged?.Invoke(this, new RecordingStatusChangedEvent
                    {
                        OldStatus = oldStatus, 
                        NewStatus = value.Status, 
                        Reason = null
                    });
                }
                else
                {
                    _currentState = value;
                }
            }
        }
    }

    public bool IsRecording => CurrentState.Status == RecordingStatus.Recording;

    /// <summary>
    /// Начать запись - устанавливает состояние Recording с текущим временем
    /// </summary>
    public void StartRecording()
    {
        CurrentState = new RecordingState
        {
            Status = RecordingStatus.Recording,
            StartTime = DateTime.Now,
            Duration = TimeSpan.Zero
        };
    }

    /// <summary>
    /// Начать обработку - переключает в состояние Processing
    /// </summary>
    public void StartProcessing()
    {
        CurrentState = new RecordingState
        {
            Status = RecordingStatus.Processing,
            StartTime = CurrentState.StartTime,
            Duration = CurrentState.Duration
        };
    }

    /// <summary>
    /// Установить ошибку с сообщением
    /// </summary>
    public void SetError(string errorMessage)
    {
        CurrentState = new RecordingState
        {
            Status = RecordingStatus.Error,
            ErrorMessage = errorMessage,
            StartTime = CurrentState.StartTime,
            Duration = CurrentState.Duration
        };
    }

    /// <summary>
    /// Завершить запись с результатом распознавания
    /// </summary>
    public void CompleteRecording(string? recognizedText, bool success, string? errorMessage = null)
    {
        CurrentState = new RecordingState
        {
            Status = success ? RecordingStatus.Completed : RecordingStatus.Error,
            LastRecognizedText = recognizedText,
            ErrorMessage = errorMessage,
            StartTime = CurrentState.StartTime,
            Duration = CurrentState.Duration
        };
    }

    /// <summary>
    /// Отменить запись
    /// </summary>
    public void CancelRecording()
    {
        CurrentState = new RecordingState
        {
            Status = RecordingStatus.Cancelled,
            StartTime = CurrentState.StartTime,
            Duration = CurrentState.Duration
        };
    }

    /// <summary>
    /// Вернуть в состояние ожидания
    /// </summary>
    public void SetIdle()
    {
        CurrentState = new RecordingState
        {
            Status = RecordingStatus.Idle
        };
    }
}