using ChatCaster.Core.Events;
using ChatCaster.Core.Resources;
using ChatCaster.Core.Services.Core;
using Serilog;
using System.Globalization;

namespace ChatCaster.Core.Services.System
{
    public class LocalizationService : ILocalizationService
    {
        private readonly IConfigurationService _configService;

        public LocalizationService(IConfigurationService configService)
        {
            _configService = configService;
            _configService.ConfigurationChanged += OnConfigurationChanged;
            // Устанавливаем язык из конфигурации при инициализации
            SetLanguage(_configService.CurrentConfig.System?.SelectedLanguage ?? "ru-RU");
        }

        public event EventHandler LanguageChanged;

        public void SetLanguage(string culture)
        {
            Log.Debug("LocalizationService: устанавливаем язык {Culture}", culture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
            Log.Debug("LocalizationService: текущая культура установлена в {Culture}", Thread.CurrentThread.CurrentUICulture.Name);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        } 
        public string GetString(string key)
        {
            return Strings.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture) ?? key;
        }

        private void OnConfigurationChanged(object sender, ConfigurationChangedEvent e)
        {
            Log.Information("🔔 LocalizationService получил ConfigurationChanged: {SettingName} = {NewValue}", 
                e.SettingName, e.NewValue);

            if (e.SettingName == "System.SelectedLanguage" && e.NewValue is string newLanguage)
            {
                Log.Information("🔄 LocalizationService переключает язык на: {Language}", newLanguage);

                SetLanguage(newLanguage);
            }
        }
    }
}
