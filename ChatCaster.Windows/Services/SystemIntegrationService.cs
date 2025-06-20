using System.Runtime.InteropServices;
using System.Windows.Forms;
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

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // Константы для keybd_event
    private const int KEYEVENTF_KEYUP = 0x2;
    private const byte VK_SHIFT = 0x10;

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
                Thread.Sleep(50);

                // Отправляем текст напрямую через VK коды
                SendTextVirtualKeys(text);

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
    
    private void SendTextVirtualKeys(string text)
    {
        try
        {
            foreach (char c in text)
            {
                // Пропускаем управляющие символы
                if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')
                    continue;

                // Обрабатываем специальные символы
                if (c == '\r' || c == '\n')
                {
                    // Enter
                    keybd_event((byte)Keys.Return, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(10);
                    keybd_event((byte)Keys.Return, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(20);
                    continue;
                }

                if (c == '\t')
                {
                    // Tab
                    keybd_event((byte)Keys.Tab, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(10);
                    keybd_event((byte)Keys.Tab, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    Thread.Sleep(20);
                    continue;
                }

                // Для обычных символов используем VK коды
                var vk = VkKeyScan(c);
                if (vk != -1)
                {
                    byte virtualKey = (byte)(vk & 0xFF);
                    byte shiftState = (byte)((vk >> 8) & 0xFF);

                    // Если нужен Shift
                    if ((shiftState & 1) != 0)
                    {
                        keybd_event(VK_SHIFT, 0, 0, UIntPtr.Zero);
                        Thread.Sleep(10);
                    }

                    // Нажимаем клавишу
                    keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
                    Thread.Sleep(10);
                    keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                    // Отпускаем Shift
                    if ((shiftState & 1) != 0)
                    {
                        Thread.Sleep(10);
                        keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    }

                    Thread.Sleep(_typingDelayMs); // Настраиваемая задержка между символами
                }
            }

            Console.WriteLine("Текст отправлен через VK коды");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка VK ввода: {ex.Message}");
        }
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