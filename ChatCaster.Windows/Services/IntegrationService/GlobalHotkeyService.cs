using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using NHotkey.Wpf;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;

namespace ChatCaster.Windows.Services.IntegrationService;

public class GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    public event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;

    private KeyboardShortcut? _registeredHotkey;

    public bool IsRegistered => _registeredHotkey != null;
    public KeyboardShortcut? CurrentShortcut => _registeredHotkey;

    public async Task<bool> RegisterAsync(KeyboardShortcut shortcut)
    {
        Console.WriteLine($"[GlobalHotkeyService] Регистрируем хоткей: {shortcut.Modifiers}+{shortcut.Key}");

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
                        // Очистка предыдущих хоткеев
                        try
                        {
                            HotkeyManager.Current.Remove("ChatCasterVoiceInput");
                        }
                        catch { }

                        Thread.Sleep(100);

                        // Регистрируем новый хоткей
                        HotkeyManager.Current.AddOrReplace("ChatCasterVoiceInput", key, modifiers, (sender, e) =>
                        {
                            Console.WriteLine($"🎯 Хоткей сработал: {shortcut.Modifiers}+{shortcut.Key}");
                            GlobalHotkeyPressed?.Invoke(this, shortcut);
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

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Критическая ошибка: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UnregisterAsync()
    {
        Console.WriteLine($"[GlobalHotkeyService] UnregisterAsync вызван");

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
                            Console.WriteLine($"[GlobalHotkeyService] Глобальный хоткей отменен");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[GlobalHotkeyService] Ошибка отмены хоткея: {ex.Message}");
                            result = false;
                        }
                    });
                }
                else
                {
                    result = true;
                }

                _registeredHotkey = null;
                return result;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [GlobalHotkeyService] Ошибка отмены хоткея: {ex.Message}");
            return false;
        }
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

    public void Dispose()
    {
        try
        {
            var task = UnregisterAsync();
            if (task.Wait(1000))
            {
                Console.WriteLine("✅ Хоткей снят успешно");
            }
            else
            {
                Console.WriteLine("⚠️ Таймаут снятия хоткея");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка снятия хоткея: {ex.Message}");
        }
    }
}