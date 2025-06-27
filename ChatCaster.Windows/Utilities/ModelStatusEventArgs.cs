using ChatCaster.Windows.ViewModels.Settings;

namespace ChatCaster.Windows.Utilities;

public class ModelStatusEventArgs : EventArgs
{
    public string Status { get; }
    public string ColorHex { get; }
    public AudioSettingsViewModel.ModelState State { get; }

    public ModelStatusEventArgs(string status, string colorHex, AudioSettingsViewModel.ModelState state)
    {
        Status = status ?? throw new ArgumentNullException(nameof(status));
        ColorHex = colorHex ?? throw new ArgumentNullException(nameof(colorHex));
        State = state;
    }
}