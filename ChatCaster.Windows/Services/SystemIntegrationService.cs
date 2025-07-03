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
    public SystemIntegrationService()
    {
        Console.WriteLine("🔥 SystemIntegrationService создан");
    }

    public event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;

    private bool _isDisposed;
    private KeyboardShortcut? _registeredHotkey;
    private int _typingDelayMs = 5; // Настраиваемая задержка между символами

    // Дополнительные константы для веб-элементов
    private const int KEYEVENTF_EXTENDEDKEY = 0x0001;

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
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
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

    // Универсальный логгер в консоль с временем
    private void Log(string msg) => Console.WriteLine($"[SystemIntegrationService][{DateTime.Now:HH:mm:ss.fff}] {msg}");

    public async Task<bool> SendTextAsync(string text)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    Log("Текст для ввода пустой");
                    return false;
                }

                Log($"Отправляем текст: '{text}'");

                // Получаем активное окно для логирования
                IntPtr foregroundWindow = GetForegroundWindow();

                if (foregroundWindow != IntPtr.Zero)
                {
                    int length = GetWindowTextLength(foregroundWindow);

                    if (length > 0)
                    {
                        var windowTitle = new System.Text.StringBuilder(length + 1);
                        GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
                        Log($"Активное окно: {windowTitle}");

                        // НОВАЯ ПРОВЕРКА: Не вводим текст в собственное окно!
                        if (windowTitle.ToString().Contains("ChatCaster"))
                        {
                            Log("Отказ: попытка ввода в собственное окно ChatCaster");
                            return false;
                        }
                    }
                }

                // Небольшая задержка для стабильности
                Thread.Sleep(100); // Увеличил до 100ms для Steam Input

                // Проверяем тип окна и выбираем метод ввода
                bool isSteam = false;

                if (foregroundWindow != IntPtr.Zero)
                {
                    int length = GetWindowTextLength(foregroundWindow);

                    if (length > 0)
                    {
                        var windowTitle = new System.Text.StringBuilder(length + 1);
                        GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);
                        isSteam = IsSteamWindow(windowTitle.ToString());
                    }
                }

                if (isSteam)
                {
                    Log("Обнаружено Steam окно - используем веб-совместимый ввод");
                    SendTextForWebElements(text);
                }
                else
                {
                    SendTextSendInput(text);
                }

                Log("Текст отправлен");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Ошибка отправки текста: {ex.Message}");
                return false;
            }
        });
    }

    private void SendTextSendInput(string text)
    {
        try
        {
            Log($"Отправка через SendInput: '{text}'");

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

            Log("Текст отправлен через SendInput");
        }
        catch (Exception ex)
        {
            Log($"Ошибка SendInput ввода: {ex.Message}");
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
            Log($"SendInput failed for key {virtualKey}, result: {result}");
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
            Log($"SendInput with Shift failed for key {virtualKey}, result: {result}");
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
            Log($"SendInput Unicode failed for char '{c}', result: {result}");
        }
    }

    private static bool IsAscii(char c)
    {
        return c <= 127;
    }

    private bool IsSteamWindow(string windowTitle)
    {
        return windowTitle.ToLower().Contains("steam") ||
               windowTitle.ToLower().Contains("store.steampowered.com");
    }

    private void SendTextForWebElements(string text)
    {
        Log($"Используем веб-совместимый ввод для: '{text}'");

        foreach (char c in text)
        {
            if (char.IsControl(c)) continue;

            // Используем SCANCODE для каждого символа
            SendCharWithScanCode(c);
            Thread.Sleep(15); // Увеличенная задержка для веб-элементов
        }
    }

    private void SendCharWithScanCode(char c)
    {
        short vk = VkKeyScan(c);
        if (vk == -1) return;

        byte virtualKey = (byte)(vk & 0xFF);
        uint scanCode = MapVirtualKey(virtualKey, 0);

        var inputs = new INPUT[2];

        // Key Down с SCANCODE
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)scanCode,
                    dwFlags = KEYEVENTF_SCANCODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key Up с SCANCODE
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)scanCode,
                    dwFlags = KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(2, inputs, INPUT.Size);
    }
    
    public async Task<bool> RegisterGlobalHotkeyAsync(KeyboardShortcut shortcut)
    {
        Console.WriteLine($"[SystemIntegration] Регистрируем хоткей: {shortcut.Modifiers}+{shortcut.Key}");

        try
        {
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

            // Конвертируем клавишу
            var key = ConvertKey(shortcut.Key);

            if (key == WpfKey.None)
            {
                Console.WriteLine($"❌ Неподдерживаемая клавиша: {shortcut.Key}");
                return false;
            }

            bool result = false;

            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // Быстрая очистка без логов
                        try
                        {
                            HotkeyManager.Current.Remove("ChatCasterVoiceInput");
                        }
                        catch
                        {
                        }

                        try
                        {
                            for (int i = 0; i < 100; i++)
                                HotkeyManager.Current.Remove($"TempCapture_{i}");
                        }
                        catch
                        {
                        }

                        // Минимальная задержка
                        Thread.Sleep(100);

                        // Регистрируем новый хоткей
                        HotkeyManager.Current.AddOrReplace("ChatCasterVoiceInput", key, modifiers, (sender, e) =>
                        {
                            Console.WriteLine($"🎯 Хоткей сработал: {shortcut.Modifiers}+{shortcut.Key}");

                            if (_registeredHotkey != null)
                            {
                                GlobalHotkeyPressed?.Invoke(this, _registeredHotkey);
                            }
                            else
                            {
                                Console.WriteLine($"❌ _registeredHotkey is NULL!");
                            }
                        });

                        _registeredHotkey = shortcut;
                        result = true;
                        Console.WriteLine($"✅ Хоткей зарегистрирован успешно");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Ошибка регистрации: {ex.Message}");
                        result = false;
                    }
                });
            }
            else
            {
                Console.WriteLine($"❌ Application.Current is NULL!");
                return false;
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Критическая ошибка: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnregisterGlobalHotkeyAsync()
    {
        Console.WriteLine($"📝 [SystemIntegration] UnregisterGlobalHotkeyAsync вызван");

        try
        {
            if (_registeredHotkey != null)
            {
                Console.WriteLine(
                    $"📝 [SystemIntegration] Есть зарегистрированный хоткей: {_registeredHotkey.Modifiers}+{_registeredHotkey.Key}");
                bool result = false;

                if (System.Windows.Application.Current != null)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            HotkeyManager.Current.Remove("ChatCasterVoiceInput");
                            result = true;
                            Console.WriteLine($"📝 [SystemIntegration] Глобальный хоткей отменен");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"📝 [SystemIntegration] Ошибка отмены глобального хоткея в UI потоке: {ex.Message}");
                            result = false;
                        }
                    });
                }
                else
                {
                    Console.WriteLine($"📝 [SystemIntegration] UI поток недоступен, считаем что хоткей уже отменен");
                    result = true;
                }

                _registeredHotkey = null;
                return result;
            }

            Console.WriteLine($"📝 [SystemIntegration] Зарегистрированный хоткей отсутствует, отменять нечего");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [SystemIntegration] Ошибка отмены глобального хоткея: {ex.Message}");
            return false;
        }
    }

    public void SetTypingDelay(int delayMs)
    {
        _typingDelayMs = Math.Max(1, delayMs); // Минимум 1ms
        Log($"Задержка ввода установлена: {_typingDelayMs}ms");
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
        await Task.CompletedTask;
        Log($"Автозапуск {(enabled ? "включен" : "выключен")} (управляется приложением)");
        return true;
    }

    public async Task<bool> IsAutoStartEnabledAsync()
    {
        await Task.CompletedTask;
        return false;
    }

    public async Task ShowNotificationAsync(string title, string message)
    {
        await Task.CompletedTask;
        Log($"Уведомление: {title} - {message}");
    }

    public void Dispose()
    {
        try 
        {
            // ✅ С таймаутом
            var task = UnregisterGlobalHotkeyAsync();
            if (task.Wait(1000)) // Ждем максимум 1 секунду
            {
                Console.WriteLine("✅ Хоткей снят успешно");
            }
            else
            {
                Console.WriteLine("⚠️ Таймаут снятия хоткея, принудительно завершаем");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка снятия хоткея: {ex.Message}");
        }
    }
}
