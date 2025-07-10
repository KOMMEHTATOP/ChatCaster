using ChatCaster.Core.Models;
using Serilog;

// Алиасы для ясности
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;
using CoreKey = ChatCaster.Core.Models.Key;
using CoreModifierKeys = ChatCaster.Core.Models.ModifierKeys;

namespace ChatCaster.Windows.Converters;

/// <summary>
/// Статический конвертер для преобразования между WPF и Core моделями клавиш
/// </summary>
public static class WpfCoreConverter
{
    private readonly static ILogger _logger = Log.ForContext(typeof(WpfCoreConverter));

    #region Key Conversion Dictionary

    private readonly static Dictionary<WpfKey, CoreKey> KeyMap = new()
    {
        // Буквы
        [WpfKey.A] = CoreKey.A, [WpfKey.B] = CoreKey.B, [WpfKey.C] = CoreKey.C,
        [WpfKey.D] = CoreKey.D, [WpfKey.E] = CoreKey.E, [WpfKey.F] = CoreKey.F,
        [WpfKey.G] = CoreKey.G, [WpfKey.H] = CoreKey.H, [WpfKey.I] = CoreKey.I,
        [WpfKey.J] = CoreKey.J, [WpfKey.K] = CoreKey.K, [WpfKey.L] = CoreKey.L,
        [WpfKey.M] = CoreKey.M, [WpfKey.N] = CoreKey.N, [WpfKey.O] = CoreKey.O,
        [WpfKey.P] = CoreKey.P, [WpfKey.Q] = CoreKey.Q, [WpfKey.R] = CoreKey.R,
        [WpfKey.S] = CoreKey.S, [WpfKey.T] = CoreKey.T, [WpfKey.U] = CoreKey.U,
        [WpfKey.V] = CoreKey.V, [WpfKey.W] = CoreKey.W, [WpfKey.X] = CoreKey.X,
        [WpfKey.Y] = CoreKey.Y, [WpfKey.Z] = CoreKey.Z,

        // Цифры
        [WpfKey.D0] = CoreKey.D0, [WpfKey.D1] = CoreKey.D1, [WpfKey.D2] = CoreKey.D2,
        [WpfKey.D3] = CoreKey.D3, [WpfKey.D4] = CoreKey.D4, [WpfKey.D5] = CoreKey.D5,
        [WpfKey.D6] = CoreKey.D6, [WpfKey.D7] = CoreKey.D7, [WpfKey.D8] = CoreKey.D8,
        [WpfKey.D9] = CoreKey.D9,

        // Функциональные клавиши
        [WpfKey.F1] = CoreKey.F1, [WpfKey.F2] = CoreKey.F2, [WpfKey.F3] = CoreKey.F3,
        [WpfKey.F4] = CoreKey.F4, [WpfKey.F5] = CoreKey.F5, [WpfKey.F6] = CoreKey.F6,
        [WpfKey.F7] = CoreKey.F7, [WpfKey.F8] = CoreKey.F8, [WpfKey.F9] = CoreKey.F9,
        [WpfKey.F10] = CoreKey.F10, [WpfKey.F11] = CoreKey.F11, [WpfKey.F12] = CoreKey.F12,

        // Numpad
        [WpfKey.NumPad0] = CoreKey.NumPad0, [WpfKey.NumPad1] = CoreKey.NumPad1,
        [WpfKey.NumPad2] = CoreKey.NumPad2, [WpfKey.NumPad3] = CoreKey.NumPad3,
        [WpfKey.NumPad4] = CoreKey.NumPad4, [WpfKey.NumPad5] = CoreKey.NumPad5,
        [WpfKey.NumPad6] = CoreKey.NumPad6, [WpfKey.NumPad7] = CoreKey.NumPad7,
        [WpfKey.NumPad8] = CoreKey.NumPad8, [WpfKey.NumPad9] = CoreKey.NumPad9,

        // Дополнительные Numpad клавиши
        [WpfKey.Add] = CoreKey.NumPadAdd,
        [WpfKey.Subtract] = CoreKey.NumPadSubtract, 
        [WpfKey.Multiply] = CoreKey.NumPadMultiply,
        [WpfKey.Divide] = CoreKey.NumPadDivide,
        [WpfKey.Decimal] = CoreKey.NumPadDecimal,

        // Навигационные клавиши
        [WpfKey.Insert] = CoreKey.Insert, [WpfKey.Delete] = CoreKey.Delete,
        [WpfKey.Home] = CoreKey.Home, [WpfKey.End] = CoreKey.End,
        [WpfKey.PageUp] = CoreKey.PageUp, [WpfKey.PageDown] = CoreKey.PageDown,
        [WpfKey.Up] = CoreKey.Up, [WpfKey.Down] = CoreKey.Down,
        [WpfKey.Left] = CoreKey.Left, [WpfKey.Right] = CoreKey.Right,

        // Специальные клавиши
        [WpfKey.Space] = CoreKey.Space, [WpfKey.Enter] = CoreKey.Enter,
        [WpfKey.Tab] = CoreKey.Tab, [WpfKey.Escape] = CoreKey.Escape
    };

    private readonly static Dictionary<CoreKey, WpfKey> ReverseKeyMap;

    #endregion

    #region Constructor

    static WpfCoreConverter()
    {
        // Создаем обратный словарь для конверсии Core → WPF
        ReverseKeyMap = new Dictionary<CoreKey, WpfKey>();
        foreach (var kvp in KeyMap)
        {
            ReverseKeyMap[kvp.Value] = kvp.Key;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Конвертирует WPF клавишу в Core клавишу
    /// </summary>
    /// <param name="wpfKey">WPF клавиша</param>
    /// <returns>Core клавиша или null если конверсия невозможна</returns>
    private static CoreKey? ConvertToCore(WpfKey wpfKey)
    {
        return KeyMap.TryGetValue(wpfKey, out var coreKey) ? coreKey : null;
    }

    /// <summary>
    /// Конвертирует Core клавишу в WPF клавишу
    /// </summary>
    /// <param name="coreKey">Core клавиша</param>
    /// <returns>WPF клавиша или null если конверсия невозможна</returns>
    public static WpfKey? ConvertToWpf(CoreKey coreKey)
    {
        return ReverseKeyMap.TryGetValue(coreKey, out var wpfKey) ? wpfKey : null;
    }

    /// <summary>
    /// Конвертирует WPF модификаторы в Core модификаторы
    /// </summary>
    /// <param name="wpfModifiers">WPF модификаторы</param>
    /// <returns>Core модификаторы</returns>
    private static CoreModifierKeys ConvertToCore(WpfModifierKeys wpfModifiers)
    {
        var coreModifiers = CoreModifierKeys.None;

        if (wpfModifiers.HasFlag(WpfModifierKeys.Control))
            coreModifiers |= CoreModifierKeys.Control;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Shift))
            coreModifiers |= CoreModifierKeys.Shift;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Alt))
            coreModifiers |= CoreModifierKeys.Alt;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Windows))
            coreModifiers |= CoreModifierKeys.Windows;

        return coreModifiers;
    }

    /// <summary>
    /// Конвертирует Core модификаторы в WPF модификаторы
    /// </summary>
    /// <param name="coreModifiers">Core модификаторы</param>
    /// <returns>WPF модификаторы</returns>
    public static WpfModifierKeys ConvertToWpf(CoreModifierKeys coreModifiers)
    {
        var wpfModifiers = WpfModifierKeys.None;

        if (coreModifiers.HasFlag(CoreModifierKeys.Control))
            wpfModifiers |= WpfModifierKeys.Control;
        if (coreModifiers.HasFlag(CoreModifierKeys.Shift))
            wpfModifiers |= WpfModifierKeys.Shift;
        if (coreModifiers.HasFlag(CoreModifierKeys.Alt))
            wpfModifiers |= WpfModifierKeys.Alt;
        if (coreModifiers.HasFlag(CoreModifierKeys.Windows))
            wpfModifiers |= WpfModifierKeys.Windows;

        return wpfModifiers;
    }

    /// <summary>
    /// Создает KeyboardShortcut из WPF клавиши и модификаторов
    /// </summary>
    /// <param name="wpfKey">WPF клавиша</param>
    /// <param name="wpfModifiers">WPF модификаторы</param>
    /// <returns>KeyboardShortcut или null если конверсия невозможна</returns>
    public static KeyboardShortcut? CreateKeyboardShortcut(WpfKey wpfKey, WpfModifierKeys wpfModifiers)
    {
        var coreKey = ConvertToCore(wpfKey);
        if (coreKey == null)
        {
            _logger.Debug("Неподдерживаемая клавиша: {WpfKey}", wpfKey);
            return null;
        }

        var coreModifiers = ConvertToCore(wpfModifiers);
        
        var shortcut = new KeyboardShortcut
        {
            Key = coreKey.Value,
            Modifiers = coreModifiers
        };
        
        _logger.Debug("Создан shortcut: {WpfKey}+{WpfModifiers} → {DisplayText}", wpfKey, wpfModifiers, shortcut.DisplayText);
        
        return shortcut;
    }

    #endregion
}