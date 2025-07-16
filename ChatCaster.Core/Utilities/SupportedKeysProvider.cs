using ChatCaster.Core.Models;

namespace ChatCaster.Core.Utilities;

/// <summary>
/// Провайдер поддерживаемых клавиш и модификаторов для захвата комбинаций
/// Платформо-независимый класс в Core части
/// </summary>
public static class SupportedKeysProvider
{
    /// <summary>
    /// Все поддерживаемые клавиши для захвата горячих клавиш
    /// </summary>
    public static readonly Key[] AllSupportedKeys =
    {
        // Все буквы A-Z
        Key.A, Key.B, Key.C, Key.D, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J,
        Key.K, Key.L, Key.M, Key.N, Key.O, Key.P, Key.Q, Key.R, Key.S, Key.T,
        Key.U, Key.V, Key.W, Key.X, Key.Y, Key.Z,

        // Все цифры 0-9
        Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9,

        // Функциональные клавиши F1-F12
        Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,

        // Numpad 0-9
        Key.NumPad0, Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4, Key.NumPad5, Key.NumPad6, Key.NumPad7,
        Key.NumPad8, Key.NumPad9,

        // Дополнительные Numpad клавиши
        Key.NumPadAdd, Key.NumPadSubtract, Key.NumPadMultiply, Key.NumPadDivide, Key.NumPadDecimal, Key.NumPadEnter,

        // Навигационные клавиши
        Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown,

        // Стрелки
        Key.Up, Key.Down, Key.Left, Key.Right,

        // Специальные клавиши
        Key.Space, Key.Enter, Key.Tab, Key.Escape
    };

    /// <summary>
    /// Все поддерживаемые модификаторы для захвата горячих клавиш
    /// </summary>
    public static readonly ModifierKeys[] AllSupportedModifiers =
    {
        ModifierKeys.Control,
        ModifierKeys.Shift,
        ModifierKeys.Alt,
        ModifierKeys.Control | ModifierKeys.Shift,
        ModifierKeys.Control | ModifierKeys.Alt,
        ModifierKeys.Shift | ModifierKeys.Alt,
        ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt
    };

    /// <summary>
    /// Получает все возможные комбинации клавиш (для подсчета или итерации)
    /// </summary>
    /// <returns>Перечисление всех возможных комбинаций</returns>
    public static IEnumerable<(Key key, ModifierKeys modifiers)> GetAllSupportedCombinations()
    {
        foreach (var modifier in AllSupportedModifiers)
        {
            foreach (var key in AllSupportedKeys)
            {
                yield return (key, modifier);
            }
        }
    }

    /// <summary>
    /// Проверяет, поддерживается ли указанная клавиша
    /// </summary>
    /// <param name="key">Клавиша для проверки</param>
    /// <returns>true если клавиша поддерживается</returns>
    public static bool IsKeySupported(Key key)
    {
        return Array.IndexOf(AllSupportedKeys, key) >= 0;
    }

    /// <summary>
    /// Проверяет, поддерживается ли указанная комбинация модификаторов
    /// </summary>
    /// <param name="modifiers">Модификаторы для проверки</param>
    /// <returns>true если комбинация модификаторов поддерживается</returns>
    public static bool IsModifierCombinationSupported(ModifierKeys modifiers)
    {
        return Array.IndexOf(AllSupportedModifiers, modifiers) >= 0;
    }

    /// <summary>
    /// Проверяет, поддерживается ли указанная горячая клавиша полностью
    /// </summary>
    /// <param name="shortcut">Горячая клавиша для проверки</param>
    /// <returns>true если горячая клавиша поддерживается</returns>
    public static bool IsShortcutSupported(KeyboardShortcut shortcut)
    {
        return IsKeySupported(shortcut.Key) && IsModifierCombinationSupported(shortcut.Modifiers);
    }

    /// <summary>
    /// Получает количество всех возможных комбинаций
    /// </summary>
    /// <returns>Общее количество возможных комбинаций</returns>
    public static int GetTotalCombinationsCount()
    {
        return AllSupportedKeys.Length * AllSupportedModifiers.Length;
    }

    /// <summary>
    /// Получает рекомендуемые клавиши для игр (функциональные + numpad)
    /// </summary>
    /// <returns>Массив рекомендуемых клавиш</returns>
    public static Key[] GetGameFriendlyKeys()
    {
        return AllSupportedKeys.Where(key => 
            // Функциональные клавиши (не мешают игровому процессу)
            (key >= Key.F1 && key <= Key.F12) ||
            // Numpad (часто свободен в играх)
            (key >= Key.NumPad0 && key <= Key.NumPad9) ||
            key == Key.NumPadAdd || key == Key.NumPadSubtract || 
            key == Key.NumPadMultiply || key == Key.NumPadDivide ||
            // Навигационные (редко используются в играх)
            key == Key.Insert || key == Key.Delete || key == Key.Home || 
            key == Key.End || key == Key.PageUp || key == Key.PageDown
        ).ToArray();
    }

    /// <summary>
    /// Получает клавиши, которые могут конфликтовать с играми
    /// </summary>
    /// <returns>Массив потенциально конфликтных клавиш</returns>
    public static Key[] GetGameConflictKeys()
    {
        return AllSupportedKeys.Where(key => 
            // Буквы (часто используются для движения)
            (key >= Key.A && key <= Key.Z) ||
            // Цифры (часто для выбора оружия/предметов)
            (key >= Key.D0 && key <= Key.D9) ||
            // Стрелки (управление)
            key == Key.Up || key == Key.Down || key == Key.Left || key == Key.Right ||
            // Системные клавиши
            key == Key.Space || key == Key.Enter || key == Key.Tab || key == Key.Escape
        ).ToArray();
    }
}