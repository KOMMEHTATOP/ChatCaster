using System.Runtime.InteropServices;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using NHotkey.Wpf;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Реализация системной интеграции для Windows
/// Фокус на прямом вводе текста для геймпад-управления
/// </summary>
public class SystemIntegrationService : ISystemIntegrationService, IDisposable
{
    public event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;

    private bool _isDisposed;
    private KeyboardShortcut? _registeredHotkey;
    private int _typingDelayMs = 5; // Настраиваемая задержка между символами

    // WinAPI для прямого ввода текста
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    // SendInput API - более надежный чем keybd_event
    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // Структуры для SendInput
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size => Marshal.SizeOf(typeof(INPUT));
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // Константы для SendInput
    private const int INPUT_KEYBOARD = 1;
    private const int KEYEVENTF_KEYUP = 0x0002;
    private const int KEYEVENTF_UNICODE = 0x0004;
    private const int KEYEVENTF_SCANCODE = 0x0008;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_RETURN = 0x0D;
    private const byte VK_TAB = 0x09;

    public async Task<bool> SendTextAsync(string text)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine("Текст для ввода пустой");
                    return false;
                }

                Console.WriteLine($"Отправляем текст: '{text}'");

                // Получаем активное окно для логирования
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    int length = GetWindowTextLength(foregroundWindow);
                    if (length > 0)
                    {
                        var windowTitle = new System.Text.StringBuilder(length + 1);
                        GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
                        Console.WriteLine($"Активное окно: {windowTitle}");
                    
                        // НОВАЯ ПРОВЕРКА: Не вводим текст в собственное окно!
                        if (windowTitle.ToString().Contains("ChatCaster"))
                        {
                            Console.WriteLine("Отказ: попытка ввода в собственное окно ChatCaster");
                            return false;
                        }
                    }
                }

                // Небольшая задержка для стабильности
                Thread.Sleep(100); // Увеличил до 100ms для Steam Input

                // Отправляем текст через SendInput API
                SendTextSendInput(text);

                Console.WriteLine("Текст отправлен");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки текста: {ex.Message}");
                return false;
            }
        });
    }
    
    private void SendTextSendInput(string text)
    {
        try
        {
            Console.WriteLine($"Отправка через SendInput: '{text}'");

            foreach (char c in text)
            {
                // Пропускаем управляющие символы
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    continue;

                // Обрабатываем специальные символы
                if (c == '\r' || c == '\n')
                {
                    SendKey(VK_RETURN);
                    Thread.Sleep(50);
                    continue;
                }

                if (c == '\t')
                {
                    SendKey(VK_TAB);
                    Thread.Sleep(50);
                    continue;
                }

                // Для Unicode символов используем KEYEVENTF_UNICODE
                if (c > 127 || char.IsLetter(c) && !IsAscii(c))
                {
                    SendUnicodeChar(c);
                }
                else
                {
                    // Для ASCII символов используем VK коды
                    var vk = VkKeyScan(c);
                    if (vk != -1)
                    {
                        byte virtualKey = (byte)(vk & 0xFF);
                        byte shiftState = (byte)((vk >> 8) & 0xFF);

                        // Если нужен Shift
                        if ((shiftState & 1) != 0)
                        {
                            SendKeyWithShift(virtualKey);
                        }
                        else
                        {
                            SendKey(virtualKey);
                        }
                    }
                    else
                    {
                        // Если VkKeyScan не смог преобразовать, используем Unicode
                        SendUnicodeChar(c);
                    }
                }

                Thread.Sleep(_typingDelayMs);
            }

            Console.WriteLine("Текст отправлен через SendInput");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка SendInput ввода: {ex.Message}");
        }
    }

    private void SendKey(byte virtualKey)
    {
        var inputs = new INPUT[2];
        
        // Key Down
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key Up
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        uint result = SendInput(2, inputs, INPUT.Size);
        if (result != 2)
        {
            Console.WriteLine($"SendInput failed for key {virtualKey}, result: {result}");
        }
    }

    private void SendKeyWithShift(byte virtualKey)
    {
        var inputs = new INPUT[4];
        
        // Shift Down
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_SHIFT,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key Down
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key Up
        inputs[2] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Shift Up
        inputs[3] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_SHIFT,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        uint result = SendInput(4, inputs, INPUT.Size);
        if (result != 4)
        {
            Console.WriteLine($"SendInput with Shift failed for key {virtualKey}, result: {result}");
        }
    }

    private void SendUnicodeChar(char c)
    {
        var inputs = new INPUT[2];
        
        // Unicode Key Down
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Unicode Key Up
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        uint result = SendInput(2, inputs, INPUT.Size);
        if (result != 2)
        {
            Console.WriteLine($"SendInput Unicode failed for char '{c}', result: {result}");
        }
    }

    private static bool IsAscii(char c)
    {
        return c <= 127;
    }

    public async Task<bool> RegisterGlobalHotkeyAsync(KeyboardShortcut shortcut)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[SYSTEM] RegisterGlobalHotkeyAsync НАЧАЛО: {shortcut.Modifiers}+{shortcut.Key}");
            
            // Отменяем предыдущий хоткей если есть
            if (_registeredHotkey != null)
            {
                await UnregisterGlobalHotkeyAsync();
            }

            // Конвертируем модификаторы
            var modifiers = WpfModifierKeys.None;
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Control))
                modifiers |= WpfModifierKeys.Control;
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Shift))
                modifiers |= WpfModifierKeys.Shift;
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Alt))
                modifiers |= WpfModifierKeys.Alt;
            if (shortcut.Modifiers.HasFlag(Core.Models.ModifierKeys.Windows))
                modifiers |= WpfModifierKeys.Windows;

            System.Diagnostics.Debug.WriteLine($"[SYSTEM] Конвертированные модификаторы: {modifiers}");

            // Конвертируем клавишу
            var key = ConvertKey(shortcut.Key);
            System.Diagnostics.Debug.WriteLine($"[SYSTEM] Конвертированная клавиша: {shortcut.Key} -> {key}");
            
            if (key == WpfKey.None)
            {
                System.Diagnostics.Debug.WriteLine($"[SYSTEM] ОШИБКА: Неподдерживаемая клавиша: {shortcut.Key}");
                Console.WriteLine($"Неподдерживаемая клавиша: {shortcut.Key}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[SYSTEM] Регистрируем хоткей: {modifiers} + {key}");

            // ИСПРАВЛЕНО: Регистрируем хоткей в UI потоке
            bool result = false;
            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        HotkeyManager.Current.AddOrReplace("ChatCasterVoiceInput", key, modifiers, 
                            (sender, e) => {
                                System.Diagnostics.Debug.WriteLine($"[SYSTEM] ⭐ СРАБОТАЛ ХОТКЕЙ: {modifiers}+{key}");
                                Console.WriteLine("⭐ СРАБОТАЛ ХОТКЕЙ В SYSTEM SERVICE!");
                                
                                if (_registeredHotkey != null)
                                {
                                    Console.WriteLine("Нажат глобальный хоткей для записи голоса");
                                    GlobalHotkeyPressed?.Invoke(this, _registeredHotkey);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[SYSTEM] ОШИБКА: _registeredHotkey is null!");
                                }
                            });

                        _registeredHotkey = shortcut;
                        result = true;
                        System.Diagnostics.Debug.WriteLine($"[SYSTEM] Хоткей успешно зарегистрирован!");
                        Console.WriteLine($"Зарегистрирован глобальный хоткей: {shortcut.Modifiers}+{shortcut.Key}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SYSTEM] ОШИБКА регистрации в UI потоке: {ex.Message}");
                        Console.WriteLine($"Ошибка регистрации глобального хоткея в UI потоке: {ex.Message}");
                        result = false;
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SYSTEM] ОШИБКА: Application.Current is null!");
                Console.WriteLine("Ошибка: UI поток недоступен");
                return false;
            }

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SYSTEM] ОШИБКА регистрации: {ex.Message}");
            Console.WriteLine($"Ошибка регистрации глобального хоткея: {ex.Message}");
            return false;
        }
    }
    
    public async Task<bool> UnregisterGlobalHotkeyAsync()
    {
        try
        {
            if (_registeredHotkey != null)
            {
                bool result = false;
                if (System.Windows.Application.Current != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            HotkeyManager.Current.Remove("ChatCasterVoiceInput");
                            result = true;
                            Console.WriteLine("Глобальный хоткей отменен");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка отмены глобального хоткея в UI потоке: {ex.Message}");
                            result = false;
                        }
                    });
                }
                else
                {
                    result = true; // Если UI поток недоступен, считаем что хоткей уже отменен
                }
                
                _registeredHotkey = null;
                return result;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка отмены глобального хоткея: {ex.Message}");
            return false;
        }
    }

    public void SetTypingDelay(int delayMs)
    {
        _typingDelayMs = Math.Max(1, delayMs); // Минимум 1ms
        Console.WriteLine($"Задержка ввода установлена: {_typingDelayMs}ms");
    }

    private WpfKey ConvertKey(Core.Models.Key key)
    {
        return key switch
        {
            Core.Models.Key.A => WpfKey.A,
            Core.Models.Key.B => WpfKey.B,
            Core.Models.Key.C => WpfKey.C,
            Core.Models.Key.D => WpfKey.D,
            Core.Models.Key.E => WpfKey.E,
            Core.Models.Key.F => WpfKey.F,
            Core.Models.Key.G => WpfKey.G,
            Core.Models.Key.H => WpfKey.H,
            Core.Models.Key.I => WpfKey.I,
            Core.Models.Key.J => WpfKey.J,
            Core.Models.Key.K => WpfKey.K,
            Core.Models.Key.L => WpfKey.L,
            Core.Models.Key.M => WpfKey.M,
            Core.Models.Key.N => WpfKey.N,
            Core.Models.Key.O => WpfKey.O,
            Core.Models.Key.P => WpfKey.P,
            Core.Models.Key.Q => WpfKey.Q,
            Core.Models.Key.R => WpfKey.R,
            Core.Models.Key.S => WpfKey.S,
            Core.Models.Key.T => WpfKey.T,
            Core.Models.Key.U => WpfKey.U,
            Core.Models.Key.V => WpfKey.V,
            Core.Models.Key.W => WpfKey.W,
            Core.Models.Key.X => WpfKey.X,
            Core.Models.Key.Y => WpfKey.Y,
            Core.Models.Key.Z => WpfKey.Z,
            Core.Models.Key.F1 => WpfKey.F1,
            Core.Models.Key.F2 => WpfKey.F2,
            Core.Models.Key.F3 => WpfKey.F3,
            Core.Models.Key.F4 => WpfKey.F4,
            Core.Models.Key.F5 => WpfKey.F5,
            Core.Models.Key.F6 => WpfKey.F6,
            Core.Models.Key.F7 => WpfKey.F7,
            Core.Models.Key.F8 => WpfKey.F8,
            Core.Models.Key.F9 => WpfKey.F9,
            Core.Models.Key.F10 => WpfKey.F10,
            Core.Models.Key.F11 => WpfKey.F11,
            Core.Models.Key.F12 => WpfKey.F12,
            Core.Models.Key.Space => WpfKey.Space,
            Core.Models.Key.Enter => WpfKey.Enter,
            Core.Models.Key.Tab => WpfKey.Tab,
            Core.Models.Key.Escape => WpfKey.Escape,
            Core.Models.Key.D0 => WpfKey.D0,
            Core.Models.Key.D1 => WpfKey.D1,
            Core.Models.Key.D2 => WpfKey.D2,
            Core.Models.Key.D3 => WpfKey.D3,
            Core.Models.Key.D4 => WpfKey.D4,
            Core.Models.Key.D5 => WpfKey.D5,
            Core.Models.Key.D6 => WpfKey.D6,
            Core.Models.Key.D7 => WpfKey.D7,
            Core.Models.Key.D8 => WpfKey.D8,
            Core.Models.Key.D9 => WpfKey.D9,
            Core.Models.Key.NumPad0 => WpfKey.NumPad0,
            Core.Models.Key.NumPad1 => WpfKey.NumPad1,
            Core.Models.Key.NumPad2 => WpfKey.NumPad2,
            Core.Models.Key.NumPad3 => WpfKey.NumPad3,
            Core.Models.Key.NumPad4 => WpfKey.NumPad4,
            Core.Models.Key.NumPad5 => WpfKey.NumPad5,
            Core.Models.Key.NumPad6 => WpfKey.NumPad6,
            Core.Models.Key.NumPad7 => WpfKey.NumPad7,
            Core.Models.Key.NumPad8 => WpfKey.NumPad8,
            Core.Models.Key.NumPad9 => WpfKey.NumPad9,

            _ => WpfKey.None
        };
    }

    // Упрощенные методы для совместимости с интерфейсом
    public async Task<bool> SetAutoStartAsync(bool enabled)
    {
        // Заглушка - автозапуск управляется на уровне приложения
        await Task.CompletedTask;
        Console.WriteLine($"Автозапуск {(enabled ? "включен" : "выключен")} (управляется приложением)");
        return true;
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        // Заглушка - автозапуск управляется на уровне приложения
        await Task.CompletedTask;
        return false;
    }

    public async Task ShowNotificationAsync(string title, string message)
    {
        // Заглушка - уведомления не нужны для геймпад-управления
        await Task.CompletedTask;
        Console.WriteLine($"Уведомление: {title} - {message}");
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            UnregisterGlobalHotkeyAsync().Wait();
            _isDisposed = true;
        }
    }
}