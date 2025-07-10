using ChatCaster.Core.Events;
using ChatCaster.Core.Models;

namespace ChatCaster.Core.Services.Core;

/// <summary>
/// Сервис управления конфигурацией
/// </summary>
public interface IConfigurationService
{
    event EventHandler<ConfigurationChangedEvent>? ConfigurationChanged;

    Task<AppConfig> LoadConfigAsync();
    Task SaveConfigAsync(AppConfig config);

    AppConfig CurrentConfig { get; }
    string ConfigPath { get; }
}
