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
    private const byte VK_CONTROL = 0x11;
    private const byte VK_A = 0x41;
    private const byte VK_DELETE = 0x2E;
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
                    _logger.LogWarning("Текст для ввода пустой");
                    return false;
                }

                _logger.LogInformation("Отправляем текст: '{Text}'", text);

                var activeWindow = _windowService.GetActiveWindowTitle();
                _logger.LogDebug("Активное окно: '{ActiveWindow}'", activeWindow);

                // Единственная проверка - не отправляем в собственное окно
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

                // Универсальный ввод для всех приложений
                SendTextSendInput(text);

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

    public async Task<bool> ClearActiveFieldAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var activeWindow = _windowService.GetActiveWindowTitle();
                _logger.LogInformation("Очищаем активное поле в окне: '{ActiveWindow}'", activeWindow);

                if (_windowService.IsOwnWindow(activeWindow))
                {
                    _logger.LogWarning("Отказ: попытка очистки в собственном окне ChatCaster");
                    return false;
                }

                SendClearField();
                _logger.LogInformation("Поле успешно очищено");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка очистки активного поля");
                return false;
            }
        });
    }

    public async Task<bool> SelectAllTextAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var activeWindow = _windowService.GetActiveWindowTitle();
                _logger.LogDebug("Выделяем весь текст в окне: '{ActiveWindow}'", activeWindow);

                if (_windowService.IsOwnWindow(activeWindow))
                {
                    _logger.LogWarning("Отказ: попытка выделения в собственном окне ChatCaster");
                    return false;
                }

                // Отправляем Ctrl+A
                SendKeyCombo(VK_CONTROL, VK_A);

                _logger.LogDebug("Текст выделен");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка выделения текста");
                return false;
            }
        });
    }

    public void SetTypingDelay(int delayMs)
    {
        _typingDelayMs = Math.Max(1, delayMs);
        _logger.LogDebug("Задержка ввода установлена: {Delay}ms", _typingDelayMs);
    }

    public bool CanSendToActiveWindow()
    {
        var activeWindow = _windowService.GetActiveWindowTitle();

        _logger.LogTrace("Проверка возможности ввода. Активное окно: '{ActiveWindow}'", activeWindow);

        if (_windowService.IsOwnWindow(activeWindow))
        {
            _logger.LogTrace("Нельзя вводить в собственное окно");
            return false;
        }

        // Проверяем, есть ли права на ввод
        bool hasInputRights = CheckInputRights();
        _logger.LogTrace("Права на ввод: {HasRights}", hasInputRights);

        return hasInputRights;
    }

    private bool CheckInputRights()
    {
        // Проверяем, может ли приложение отправлять input
        var activeHandle = _windowService.GetActiveWindowHandle();
        if (activeHandle == IntPtr.Zero)
        {
            _logger.LogTrace("Нет активного окна");
            return false;
        }

        // Дополнительные проверки...
        return true;
    }

    private void SendClearField()
    {
        _logger.LogTrace("Очищаем поле ввода");
        TrySmartClear();
    }

    private bool TrySmartClear()
    {
        _logger.LogDebug("Пробуем универсальную очистку");
    
        // Способ 1: Стандартный Ctrl+A + Delete
        SendKeyCombo(VK_CONTROL, VK_A);
        Thread.Sleep(50);
        SendKey(VK_DELETE);
    
        // Способ 2: Если не сработало - пробуем Backspace
        _logger.LogDebug("Дополнительно очищаем через Backspace");
        ClearWithBackspace();
    
        return true;
    }

    private void ClearWithBackspace()
    {
        // Универсально для любого поля - просто много Backspace
        for (int i = 0; i < 200; i++) // Достаточно для любого поля
        {
            SendKey(0x08); // VK_BACK
            Thread.Sleep(5);
        }
    }
    
    private void SendTextSendInput(string text)
    {
        try
        {
            _logger.LogDebug("Отправка через SendInput: '{Text}'", text);

            foreach (char c in text)
            {
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    continue;

                if (c == '\r' || c == '\n')
                {
                    SendKey(VK_RETURN);
                    continue;
                }

                if (c == '\t')
                {
                    SendKey(VK_TAB);
                    continue;
                }

                SendUnicodeChar(c);
                Thread.Sleep(_typingDelayMs); 
            }

            _logger.LogDebug("Текст отправлен через SendInput");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка SendInput ввода");
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
            _logger.LogWarning("SendInput failed for key {VirtualKey}, result: {Result}", virtualKey, result);
        }
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

    private void SendUnicodeChar(char c)
    {
        var inputs = new INPUT[2];

        // Key Down - чистый Unicode (без VK кодов)
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,                    // Важно: VK = 0 для Steam
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE, // Только Unicode
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key Up
        inputs[1] = inputs[0];
        inputs[1].U.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;

        uint result = SendInput(2, inputs, INPUT.Size);
        if (result != 2)
        {
            _logger.LogWarning("SendInput Unicode failed for char '{Char}', result: {Result}", c, result);
        }
    }
}