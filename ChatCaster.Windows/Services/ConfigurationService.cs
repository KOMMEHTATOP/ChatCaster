using System.IO;
using System.Text.Json;
using ChatCaster.Core.Models;
using System.Text.Json.Serialization;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Сервис для сохранения и загрузки конфигурации приложения
/// </summary>
public class ConfigurationService
{
    private readonly string _configDirectory;
    private readonly string _configFilePath;

    public ConfigurationService()
    {
        // Папка конфигурации в AppData пользователя
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ChatCaster");
            
        _configFilePath = Path.Combine(_configDirectory, "config.json");
        
        // Создаем папку если её нет
        Directory.CreateDirectory(_configDirectory);
    }

    /// <summary>
    /// Загружает конфигурацию из файла или создает дефолтную
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[CONFIG] Файл конфигурации не найден, создаем дефолтный");
                var defaultConfig = new AppConfig();
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            System.Diagnostics.Debug.WriteLine($"[CONFIG] Загружаем конфигурацию из: {_configFilePath}");
            
            var jsonText = await File.ReadAllTextAsync(_configFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(jsonText, GetJsonOptions());
            
            if (config == null)
            {
                System.Diagnostics.Debug.WriteLine("[CONFIG] Ошибка десериализации, используем дефолтную конфигурацию");
                return new AppConfig();
            }

            System.Diagnostics.Debug.WriteLine("[CONFIG] Конфигурация успешно загружена");
            return config;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Ошибка загрузки конфигурации: {ex.Message}");
            return new AppConfig(); // Возвращаем дефолтную при ошибке
        }
    }

    /// <summary>
    /// Сохраняет конфигурацию в файл
    /// </summary>
    public async Task SaveConfigAsync(AppConfig config)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Сохраняем конфигурацию в: {_configFilePath}");
            
            var jsonText = JsonSerializer.Serialize(config, GetJsonOptions());
            await File.WriteAllTextAsync(_configFilePath, jsonText);
            
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
        return File.Exists(_configFilePath);
    }

    /// <summary>
    /// Получает путь к файлу конфигурации
    /// </summary>
    public string GetConfigFilePath()
    {
        return _configFilePath;
    }

    /// <summary>
    /// Создает резервную копию конфигурации
    /// </summary>
    public async Task<bool> CreateBackupAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
                return false;

            var backupPath = _configFilePath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(_configFilePath, backupPath);
            
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Создана резервная копия: {backupPath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG] Ошибка создания резервной копии: {ex.Message}");
            return false;
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