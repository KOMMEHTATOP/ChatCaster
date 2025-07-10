using System.IO;
using System.Text.Json;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Services.Core;
using Serilog;
using System.Text.Json.Serialization;

namespace ChatCaster.Windows.Services;

/// <summary>
/// Сервис для сохранения и загрузки конфигурации приложения
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly static ILogger _logger = Log.ForContext<ConfigurationService>();

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
                _logger.Information("Файл конфигурации не найден, создаем дефолтный");
                var defaultConfig = new AppConfig();
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            _logger.Debug("Загружаем конфигурацию из: {ConfigPath}", ConfigPath);
        
            var jsonText = await File.ReadAllTextAsync(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(jsonText, GetJsonOptions());
        
            if (config == null)
            {
                _logger.Warning("Ошибка десериализации, используем дефолтную конфигурацию");
                CurrentConfig = new AppConfig();
                return CurrentConfig;
            }

            // Обновляем кеш
            CurrentConfig = config;

            if (ConfigurationChanged != null)
            {
                ConfigurationChanged.Invoke(this, new ConfigurationChangedEvent
                {
                    SettingName = "ConfigurationLoaded",
                    OldValue = null,
                    NewValue = config
                });
            
                _logger.Debug("Событие ConfigurationLoaded поднято для {Count} подписчиков", 
                    ConfigurationChanged.GetInvocationList().Length);
            }

            _logger.Information("Конфигурация успешно загружена");
            return CurrentConfig;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка загрузки конфигурации");
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
            _logger.Debug("Сохраняем конфигурацию в: {ConfigPath}", ConfigPath);
        
            // ДИАГНОСТИКА: Логируем что сохраняем
            _logger.Debug("Сохранение - AllowCompleteExit = {AllowCompleteExit}, Config HashCode = {HashCode}", 
                config.System?.AllowCompleteExit, config.GetHashCode());
        
            var jsonText = JsonSerializer.Serialize(config, GetJsonOptions());
            await File.WriteAllTextAsync(ConfigPath, jsonText);
        
            // ДИАГНОСТИКА: Проверяем что записалось в файл
            var savedText = await File.ReadAllTextAsync(ConfigPath);
            _logger.Debug("В файле сохранено allowCompleteExit: {HasField}", savedText.Contains("allowCompleteExit"));
        
            // Обновляем кеш
            CurrentConfig = config;
            
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEvent
            {
                SettingName = "ConfigurationSaved"
            });

            // ДИАГНОСТИКА: Проверяем кеш после обновления
            _logger.Debug("CurrentConfig.AllowCompleteExit = {AllowCompleteExit}, HashCode = {HashCode}", 
                CurrentConfig.System?.AllowCompleteExit, CurrentConfig.GetHashCode());
        
            _logger.Debug("Конфигурация успешно сохранена");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка сохранения конфигурации");
            throw;
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