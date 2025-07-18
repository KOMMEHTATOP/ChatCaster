using System.IO;
using System.Text.Json;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Constants; 
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
        // кроссплатформенный метод из Core
        ConfigPath = AppConstants.Paths.GetConfigFilePath();
        
        // Создаем все необходимые папки если их нет
        AppConstants.Paths.EnsureDirectoriesExist();
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
                var defaultConfig = new AppConfig();
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }
            
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
            }
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
            // Сохраняем старое значение языка для сравнения
            var oldLanguage = CurrentConfig.System?.SelectedLanguage;
            
            var jsonText = JsonSerializer.Serialize(config, GetJsonOptions());
            await File.WriteAllTextAsync(ConfigPath, jsonText);
            
            // Обновляем кеш
            CurrentConfig = config;
            
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEvent
            {
                SettingName = "ConfigurationSaved"
            });
            
            // Отправляем специфичное событие, если язык изменился
            if (oldLanguage != config.System?.SelectedLanguage)
            {
                ConfigurationChanged?.Invoke(this, new ConfigurationChangedEvent
                {
                    SettingName = "System.SelectedLanguage",
                    OldValue = oldLanguage,
                    NewValue = config.System?.SelectedLanguage
                });
            }
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