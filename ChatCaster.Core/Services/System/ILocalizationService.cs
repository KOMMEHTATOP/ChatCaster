namespace ChatCaster.Core.Services.System
{
    public interface ILocalizationService
    {
        string GetString(string key);
        void SetLanguage(string culture);
        event EventHandler LanguageChanged;
    }
}
