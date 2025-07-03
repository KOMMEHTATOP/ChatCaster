using System.Runtime.InteropServices;
using ChatCaster.Core.Services;
using Microsoft.Extensions.Logging;

namespace ChatCaster.Windows.Services.IntegrationService;

/// <summary>
/// Реализация сервиса ввода текста для Windows
/// </summary>
public class TextInputService : ITextInputService
{
    private readonly IWindowService _windowService;
    private readonly ILogger<TextInputService> _logger;
    private int _typingDelayMs = 5;
    public TextInputService(IWindowService windowService, ILogger<TextInputService> logger)
    {
        _windowService = windowService;
        _logger = logger;
    }

    // WinAPI для ввода текста
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

    private const byte VK_CONTROL = 0x11;
    private const byte VK_A = 0x41;
    private const byte VK_DELETE = 0x2E;
    
    public TextInputService(IWindowService windowService)
    {
        _windowService = windowService;
    }

    public async Task<bool> SendTextAsync(string text)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Текст для ввода пустой");
                    return false;
                }

                _logger.LogInformation("Отправляем текст: '{Text}'", text);

                var activeWindow = _windowService.GetActiveWindowTitle();
                _logger.LogDebug("Активное окно: '{ActiveWindow}'", activeWindow);

                if (_windowService.IsOwnWindow(activeWindow))
                {
                    _logger.LogWarning("Отказ: попытка ввода в собственное окно ChatCaster");
                    return false;
                }

                if (!CanSendToActiveWindow())
                {
                    _logger.LogWarning("Отказ: невозможно отправить текст в активное окно");
                    return false;
                }

                Thread.Sleep(100);

                // Определяем тип Steam окна
                bool isSteamLibrary = activeWindow.ToLower().Contains("steam") && !activeWindow.ToLower().Contains("store");
                bool isSteamStore = activeWindow.ToLower().Contains("store") || 
                                   (activeWindow.ToLower().Contains("steam") && activeWindow.ToLower().Contains("поиск"));

                if (isSteamStore)
                {
                    _logger.LogDebug("Обнаружен Steam Store - используем веб-ввод");
                    SendTextForWebStore(text);
                }
                else if (isSteamLibrary)
                {
                    _logger.LogDebug("Обнаружена Steam Library - используем обычный ввод");
                    SendTextSendInput(text);
                }
                else if (_windowService.IsSteamWindow(activeWindow))
                {
                    _logger.LogDebug("Обнаружено Steam окно - используем веб-совместимый ввод");
                    SendTextForWebElements(text);
                }
                else
                {
                    SendTextSendInput(text);
                }

                _logger.LogInformation("Текст отправлен успешно");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка отправки текста");
                return false;
            }
        });
    }

    private void SendTextForWebStore(string text)
    {
        _logger.LogDebug("Отправляем в Steam Store: '{Text}'", text);
        
        // Очищаем поле перед вводом
        SendClearField();
        Thread.Sleep(100);
        
        // Используем более медленный ввод с паузами
        foreach (char c in text.Trim())
        {
            if (char.IsControl(c)) continue;
            
            SendCharWithScanCode(c);
            Thread.Sleep(50); // Увеличенная задержка для веб-элементов
        }
        
        _logger.LogDebug("Текст отправлен в Steam Store");
    }

    private void SendClearField()
    {
        _logger.LogTrace("Очищаем поле ввода");
    
        // Ctrl+A для выделения всего
        SendKeyCombo(VK_CONTROL, VK_A);
        Thread.Sleep(50);
    
        // Delete для очистки
        SendKey(VK_DELETE);
        Thread.Sleep(50);
    }    
    
    private void SendKeyCombo(byte key1, byte key2)
    {
        var inputs = new INPUT[4];
    
        // Key1 Down (например, Ctrl)
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key1,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    
        // Key2 Down (например, A)
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key2,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    
        // Key2 Up
        inputs[2] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key2,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    
        // Key1 Up
        inputs[3] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key1,
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
            _logger.LogWarning("SendKeyCombo failed for {Key1}+{Key2}, result: {Result}", key1, key2, result);
        }
    }

    public void SetTypingDelay(int delayMs)
    {
        _typingDelayMs = Math.Max(1, delayMs);
        Log($"Задержка ввода установлена: {_typingDelayMs}ms");
    }

    public bool CanSendToActiveWindow()
    {
        var activeWindow = _windowService.GetActiveWindowTitle();
    
        Log($"Проверка возможности ввода. Активное окно: '{activeWindow}'");
    
        if (_windowService.IsOwnWindow(activeWindow))
        {
            Log("❌ Нельзя вводить в собственное окно");
            return false;
        }

        // Проверяем, есть ли права на ввод
        bool hasInputRights = CheckInputRights();
        Log($"Права на ввод: {hasInputRights}");
    
        return hasInputRights;
    }

    private bool CheckInputRights()
    {
        // Проверяем, может ли приложение отправлять input
        var activeHandle = _windowService.GetActiveWindowHandle();
        if (activeHandle == IntPtr.Zero)
        {
            Log("❌ Нет активного окна");
            return false;
        }

        // Дополнительные проверки...
        return true;
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

    private static bool IsAscii(char c)
    {
        return c <= 127;
    }

    // Универсальный логгер в консоль с временем
    private void Log(string msg) => Console.WriteLine($"[TextInputService][{DateTime.Now:HH:mm:ss.fff}] {msg}");
}