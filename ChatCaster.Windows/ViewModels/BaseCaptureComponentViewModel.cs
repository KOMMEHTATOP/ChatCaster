using ChatCaster.Windows.Managers;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

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

        protected void OnStatusMessageChanged(string message)
        {
            StatusMessageChanged?.Invoke(message);
        }

        protected async Task OnSettingChangedAsync()
        {
            if (SettingChanged != null)
            {
                await SettingChanged.Invoke();
            }
        }

        public abstract Task StartCaptureAsync();
        
        protected void OnUIStateChanged(CaptureUIState state)
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
