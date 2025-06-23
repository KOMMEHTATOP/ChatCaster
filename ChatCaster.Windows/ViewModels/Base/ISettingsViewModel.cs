using System.Threading.Tasks;

namespace ChatCaster.Windows.ViewModels.Base
{
    public interface ISettingsViewModel
    {
        /// <summary>
        /// Загружает настройки из конфигурации в UI
        /// </summary>
        Task LoadSettingsAsync();

        /// <summary>
        /// Применяет текущие настройки UI к конфигурации и сохраняет
        /// </summary>
        Task ApplySettingsAsync();

        /// <summary>
        /// Подписывается на события UI для автоприменения настроек
        /// </summary>
        void SubscribeToUIEvents();

        /// <summary>
        /// Очистка ресурсов при выгрузке
        /// </summary>
        void Cleanup();

        /// <summary>
        /// Инициализация специфичных для страницы данных
        /// </summary>
        Task InitializePageDataAsync();
    }
}
