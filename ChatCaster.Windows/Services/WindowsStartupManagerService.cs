using Microsoft.Win32;
using ChatCaster.Core.Services.System;
using Serilog;
using System.IO;
using System.Reflection;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Windows-специфичная реализация управления автозапуском через реестр
/// </summary>
public class WindowsStartupManagerService : IStartupManagerService
{
    private static readonly ILogger _logger = Log.ForContext<WindowsStartupManagerService>();
    
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApplicationName = "ChatCaster";

    /// <summary>
    /// Проверяет, включен ли автозапуск приложения
    /// </summary>
    public async Task<bool> IsStartupEnabledAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
                var value = key?.GetValue(ApplicationName) as string;
                var currentPath = GetApplicationPath();
                
                // Проверяем что ключ существует и путь совпадает с текущим
                bool isEnabled = !string.IsNullOrEmpty(value) && 
                                string.Equals(value, currentPath, StringComparison.OrdinalIgnoreCase);
                
                _logger.Debug("Проверка автозапуска: {IsEnabled}, RegistryValue: {RegistryValue}, CurrentPath: {CurrentPath}", 
                    isEnabled, value, currentPath);
                
                return isEnabled;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Ошибка проверки состояния автозапуска");
                return false;
            }
        });
    }

    /// <summary>
    /// Включает автозапуск приложения
    /// </summary>
    public async Task EnableStartupAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var applicationPath = GetApplicationPath();
                
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key == null)
                {
                    _logger.Error("Не удалось открыть ключ реестра для записи: {RegistryKey}", StartupRegistryKey);
                    throw new InvalidOperationException($"Не удалось открыть ключ реестра: {StartupRegistryKey}");
                }

                key.SetValue(ApplicationName, applicationPath);
                _logger.Information("Автозапуск включен: {ApplicationPath}", applicationPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Ошибка включения автозапуска");
                throw;
            }
        });
    }

    /// <summary>
    /// Выключает автозапуск приложения
    /// </summary>
    public async Task DisableStartupAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
                if (key == null)
                {
                    _logger.Warning("Ключ реестра не найден: {RegistryKey}", StartupRegistryKey);
                    return;
                }

                // Проверяем существует ли значение перед удалением
                if (key.GetValue(ApplicationName) != null)
                {
                    key.DeleteValue(ApplicationName);
                    _logger.Information("Автозапуск отключен");
                }
                else
                {
                    _logger.Debug("Значение автозапуска уже отсутствует в реестре");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Ошибка отключения автозапуска");
                throw;
            }
        });
    }

    /// <summary>
    /// Устанавливает автозапуск в соответствии с переданным значением
    /// </summary>
    public async Task SetStartupAsync(bool enabled)
    {
        if (enabled)
        {
            await EnableStartupAsync();
        }
        else
        {
            await DisableStartupAsync();
        }
    }

    /// <summary>
    /// Получает полный путь к исполняемому файлу приложения
    /// </summary>
    private static string GetApplicationPath()
    {
        // Для single-file приложений используем AppContext.BaseDirectory + имя процесса
        var processName = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processName) && File.Exists(processName))
        {
            return processName;
        }

        // Fallback: используем базовую директорию + имя исполняемого файла
        var baseDirectory = AppContext.BaseDirectory;
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "ChatCaster.Windows";
        var exePath = Path.Combine(baseDirectory, $"{assemblyName}.exe");
    
        if (File.Exists(exePath))
        {
            return exePath;
        }

        // Последний fallback
        return Path.Combine(baseDirectory, "ChatCaster.Windows.exe");
    }
    
}