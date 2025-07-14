using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Input;
using ChatCaster.Windows.Converters;
using NHotkey.Wpf;
using Serilog;

namespace ChatCaster.Windows.Services.IntegrationService;

public class GlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private readonly static ILogger _logger = Log.ForContext<GlobalHotkeyService>();

    public event EventHandler<KeyboardShortcut>? GlobalHotkeyPressed;

    private KeyboardShortcut? _registeredHotkey;
    private bool _isCapturingHotkey = false;

    public bool IsRegistered => _registeredHotkey != null;
    public KeyboardShortcut? CurrentShortcut => _registeredHotkey;

    /// <summary>
    /// Устанавливает режим захвата клавиш. В этом режиме глобальные хоткеи игнорируются.
    /// </summary>
    public void SetCaptureMode(bool isCapturing)
    {
        _logger.Debug("Устанавливаем режим захвата: {IsCapturing}", isCapturing);
        _isCapturingHotkey = isCapturing;
    }

    public async Task<bool> RegisterAsync(KeyboardShortcut shortcut)
    {
        _logger.Debug("Регистрируем хоткей: {Modifiers}+{Key}", shortcut.Modifiers, shortcut.Key);

        try
        {
            // Используем конвертер вместо дублирования логики
            var modifiers = WpfCoreConverter.ConvertToWpf(shortcut.Modifiers);
            var key = WpfCoreConverter.ConvertToWpf(shortcut.Key);

            if (key == null)
            {
                _logger.Warning("Неподдерживаемая клавиша: {Key}", shortcut.Key);
                return false;
            }

            bool result = false;

            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        // Очистка предыдущих хоткеев
                        try
                        {
                            HotkeyManager.Current.Remove("ChatCasterVoiceInput");
                        }
                        catch { }

                        // Небольшая задержка для очистки
                        await Task.Delay(100);

                        // Регистрируем новый хоткей
                        HotkeyManager.Current.AddOrReplace("ChatCasterVoiceInput", key.Value, modifiers, (sender, e) =>
                        {
                            // Проверяем режим захвата - если активен, игнорируем хоткей
                            if (_isCapturingHotkey)
                            {
                                _logger.Debug("Хоткей проигнорирован (активен режим захвата): {Modifiers}+{Key}", shortcut.Modifiers, shortcut.Key);
                                return;
                            }

                            _logger.Debug("Хоткей сработал: {Modifiers}+{Key}", shortcut.Modifiers, shortcut.Key);
                            GlobalHotkeyPressed?.Invoke(this, shortcut);
                        });

                        _registeredHotkey = shortcut;
                        result = true;
                        _logger.Information("Хоткей зарегистрирован успешно: {DisplayText}", shortcut.DisplayText);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Ошибка регистрации хоткея");
                        result = false;
                    }
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Критическая ошибка регистрации хоткея");
            return false;
        }
    }

    public async Task<bool> UnregisterAsync()
    {
        _logger.Debug("Отмена регистрации хоткея");

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
                            _logger.Debug("Глобальный хоткей отменен");
                        }
                        catch (Exception ex)
                        {
                            _logger.Warning(ex, "Ошибка отмены хоткея");
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
            _logger.Error(ex, "Ошибка отмены хоткея");
            return false;
        }
    }

    public void Dispose()
    {
        try
        {
            var task = UnregisterAsync();
            if (task.Wait(1000))
            {
                _logger.Debug("Хоткей снят при Dispose");
            }
            else
            {
                _logger.Warning("Таймаут снятия хоткея при Dispose");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Ошибка снятия хоткея при Dispose");
        }
    }
}