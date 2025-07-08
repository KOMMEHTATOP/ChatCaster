using ChatCaster.Core.Models;

namespace ChatCaster.Core.Utilities;

/// <summary>
/// Утилиты для отображения элементов управления в UI
/// </summary>
public static class InputDisplayHelper
{
    /// <summary>
    /// Получить отображаемое имя для кнопки геймпада
    /// </summary>
    public static string GetButtonDisplayName(GamepadButton button)
    {
        return button switch
        {
            GamepadButton.LeftBumper => "LB",
            GamepadButton.RightBumper => "RB",
            GamepadButton.LeftTrigger => "LT",
            GamepadButton.RightTrigger => "RT",
            GamepadButton.A => "A",
            GamepadButton.B => "B",
            GamepadButton.X => "X",
            GamepadButton.Y => "Y",
            GamepadButton.Start => "Start",
            GamepadButton.Back => "Back",
            GamepadButton.Guide => "Guide",
            GamepadButton.LeftStick => "LS",
            GamepadButton.RightStick => "RS",
            GamepadButton.DPadUp => "D-Up",
            GamepadButton.DPadDown => "D-Down",
            GamepadButton.DPadLeft => "D-Left",
            GamepadButton.DPadRight => "D-Right",
            _ => button.ToString()
        };
    }

    /// <summary>
    /// Получить отображаемое имя для клавиши
    /// </summary>
    public static string GetKeyDisplayName(Key key)
    {
        return key switch
        {
            Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
            Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
            Key.NumPad0 => "NumPad0", Key.NumPad1 => "NumPad1", Key.NumPad2 => "NumPad2",
            Key.NumPad3 => "NumPad3", Key.NumPad4 => "NumPad4", Key.NumPad5 => "NumPad5",
            Key.NumPad6 => "NumPad6", Key.NumPad7 => "NumPad7", Key.NumPad8 => "NumPad8",
            Key.NumPad9 => "NumPad9",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Escape => "Esc",
            _ => key.ToString()
        };
    }

    /// <summary>
    /// Получить отображаемые имена для модификаторов
    /// </summary>
    public static IEnumerable<string> GetModifierDisplayNames(ModifierKeys modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");

        return parts;
    }
}