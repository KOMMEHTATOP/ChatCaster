using ChatCaster.Windows.Managers;
using ChatCaster.Windows.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChatCaster.Windows.ViewModels
{
    /// <summary>
    /// Базовый класс для компонентов захвата (клавиатура/геймпад)
    /// </summary>
    public abstract partial class BaseCaptureComponentViewModel : ObservableObject, IDisposable
    {
        protected CaptureUIStateManager? _uiManager;
        
        // Общие свойства для всех capture компонентов
        [ObservableProperty]
        private string _comboText = "";

        [ObservableProperty]
        private string _comboTextColor = "White";

        [ObservableProperty]
        private bool _isWaitingForInput;

        [ObservableProperty]
        private int _captureTimeLeft;

        [ObservableProperty]
        private bool _showTimer;

        // События
        public event Action<string>? StatusMessageChanged;
        public event Func<Task>? SettingChanged;

        protected virtual void OnStatusMessageChanged(string message)
        {
            StatusMessageChanged?.Invoke(message);
        }

        protected virtual async Task OnSettingChangedAsync()
        {
            if (SettingChanged != null)
            {
                await SettingChanged.Invoke();
            }
        }

        public abstract Task StartCaptureAsync();
        
        protected virtual void OnUIStateChanged(CaptureUIState state)
        {
            ComboText = state.Text;
            ComboTextColor = state.TextColor;
            ShowTimer = state.ShowTimer;
            CaptureTimeLeft = state.TimeLeft;
            
            if (!string.IsNullOrEmpty(state.StatusMessage))
            {
                OnStatusMessageChanged(state.StatusMessage);
            }
        }

        public virtual void Dispose()
        {
            _uiManager?.Dispose();
            _uiManager = null;
        }
    }
}
