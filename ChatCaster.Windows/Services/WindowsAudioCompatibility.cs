using System.Runtime.InteropServices;
using Serilog;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Проверка совместимости аудио API с версией Windows
/// </summary>
public class WindowsAudioCompatibility
{
    private readonly ILogger _logger = Log.ForContext<WindowsAudioCompatibility>();

    /// <summary>
    /// Проверяет поддержку WASAPI на текущей системе
    /// </summary>
    public bool IsWasapiSupported()
    {
        try
        {
            // WASAPI поддерживается с Windows Vista (6.0) и выше
            var version = Environment.OSVersion.Version;
            bool isSupported = version.Major >= 6;
            
            _logger.Information("Windows версия: {Version}, WASAPI поддержка: {Supported}", 
                version, isSupported);
                
            return isSupported;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Не удалось определить поддержку WASAPI, используем WaveIn");
            return false;
        }
    }

    /// <summary>
    /// Получает информацию о системе для отладки
    /// </summary>
    public string GetSystemInfo()
    {
        try
        {
            var osVersion = Environment.OSVersion;
            var is64Bit = Environment.Is64BitOperatingSystem;
            var framework = RuntimeInformation.FrameworkDescription;
            
            return $"OS: {osVersion.VersionString}, 64-bit: {is64Bit}, Framework: {framework}";
        }
        catch
        {
            return "Системная информация недоступна";
        }
    }
}
