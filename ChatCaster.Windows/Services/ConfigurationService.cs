using System.IO;
using System.Text.Json;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Core.Events;
using System.Text.Json.Serialization;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Сервис для сохранения и загрузки конфигурации приложения
/// </summary>
public class ConfigurationService : IConfigurationService
{
    public event EventHandler<ConfigurationChangedEvent>? ConfigurationChanged;

    public AppConfig CurrentConfig { get; private set; } = new();
    public string ConfigPath { get; }

    public ConfigurationService()
    {
        var configDirectory =
            // Папка конфигурации в AppData пользователя
            Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ChatCaster");
            
        ConfigPath = Path.Combine(configDirectory, "config.json");
        
        // Создаем папку если её нет
        Directory.CreateDirectory(configDirectory);
    }

    /// <summary>
    /// Загружает конфигурацию из файла или создает дефолтную
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                System.Diagnostics.Debug.WriteLine("[CONFIG] Файл конфигурации не найден, создаем дефолтный");
                var defaultConfig = new AppConfig();
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            System.Diagnostics.Debug.WriteLine($"[CONFIG] Загружаем конфигурацию из: {ConfigPath}");
            
            var jsonText = await File.ReadAllTextAsync(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(jsonText, GetJsonOptions());
            
            if (config == null)
            {
                System.Diagnostics.Debug.WriteLine("[CONFIG] Ошибка десериализации, используем дефолтную конфигурацию");
                CurrentConfig = new AppConfig();
                return CurrentConfig;
            }

            // Обновляем кеш (событие для загрузки файла не стреляем)
            CurrentConfig = config;

            System.Diagnostics.Debug.WriteLine("[CONFIG] Конфигурация успешно загружена");
            return CurrentConfig;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Ошибка загрузки конфигурации: {ex.Message}");
            CurrentConfig = new AppConfig(); // Возвращаем дефолтную при ошибке
            return CurrentConfig;
        }
    }

    /// <summary>
    /// Сохраняет конфигурацию в файл
    /// </summary>
    public async Task SaveConfigAsync(AppConfig config)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Сохраняем конфигурацию в: {ConfigPath}");
            
            var jsonText = JsonSerializer.Serialize(config, GetJsonOptions());
            await File.WriteAllTextAsync(ConfigPath, jsonText);
            
            // Обновляем кеш (без события - событие стреляется при изменении конкретных настроек)
            CurrentConfig = config;
            
            System.Diagnostics.Debug.WriteLine("[CONFIG] Конфигурация успешно сохранена");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Ошибка сохранения конфигурации: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Проверяет существует ли файл конфигурации
    /// </summary>
    public bool ConfigFileExists()
    {
        return File.Exists(ConfigPath);
    }

    /// <summary>
    /// Создает резервную копию конфигурации
    /// </summary>
    public bool CreateBackup()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return false;

            var backupPath = ConfigPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(ConfigPath, backupPath);
            
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Создана резервная копия: {backupPath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Ошибка создания резервной копии: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Уведомляет об изменении конкретной настройки (для использования в UI)
    /// </summary>
    public void NotifySettingChanged(string settingName, object? oldValue, object? newValue)
    {
        try
        {
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEvent
            {
                SettingName = settingName,
                OldValue = oldValue,
                NewValue = newValue
            });
            
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Настройка изменена: {settingName} = {newValue}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Ошибка уведомления об изменении: {ex.Message}");
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true, // Красивое форматирование
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = 
            {
                // Добавим конвертеры для енумов если нужно
                new JsonStringEnumConverter()
            }
        };
    }
}