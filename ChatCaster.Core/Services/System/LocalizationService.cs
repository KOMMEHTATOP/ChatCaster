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
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        } 
        public string GetString(string key)
        {
            return Strings.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture) ?? key;
        }

        private void OnConfigurationChanged(object sender, ConfigurationChangedEvent e)
        {
            if (e.SettingName == "System.SelectedLanguage" && e.NewValue is string newLanguage)
            {
                SetLanguage(newLanguage);
            }
        }
    }
}
