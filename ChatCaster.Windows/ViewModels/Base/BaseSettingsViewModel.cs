using CommunityToolkit.Mvvm.ComponentModel;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;

namespace ChatCaster.Windows.ViewModels.Base
{
    public abstract partial class BaseSettingsViewModel : ViewModelBase, ISettingsViewModel
    {
        #region Protected Services

        protected readonly ConfigurationService? _configurationService;
        protected readonly ServiceContext? _serviceContext;
        #endregion

        #region Observable Properties

        [ObservableProperty]
        private bool _isLoadingUI = false;

        [ObservableProperty]
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        #endregion

        #region Constructor

        protected BaseSettingsViewModel(
            ConfigurationService? configurationService, 
            ServiceContext? serviceContext)
        {
            _configurationService = configurationService;
            _serviceContext = serviceContext;
        }

        #endregion

        #region ISettingsViewModel Implementation

        public async Task LoadSettingsAsync()
        {
            try
            {
                IsLoadingUI = true;
                StatusMessage = "Загрузка настроек...";

                // Загружаем базовые настройки
                await LoadBaseSettingsAsync();

                // Загружаем специфичные настройки страницы
                await LoadPageSpecificSettingsAsync();

                // Подписываемся на UI события
                SubscribeToUIEvents();

                StatusMessage = "Настройки загружены";
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка загрузки: {ex.Message}";
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка загрузки настроек: {ex.Message}");
            }
            finally
            {
                IsLoadingUI = false;
            }
        }

        public async Task ApplySettingsAsync()
        {
            if (IsLoadingUI || _serviceContext?.Config == null || _configurationService == null)
                return;

            try
            {
                StatusMessage = "Сохранение настроек...";

                // Применяем настройки к конфигурации
                await ApplySettingsToConfigAsync(_serviceContext.Config);

                // Сохраняем конфигурацию
                await _configurationService.SaveConfigAsync(_serviceContext.Config);

                // Применяем настройки к сервисам
                await ApplySettingsToServicesAsync();

                HasUnsavedChanges = false;
                StatusMessage = "Настройки сохранены";

                Console.WriteLine($"✅ [{GetType().Name}] Настройки успешно применены");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка сохранения: {ex.Message}";
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка применения настроек: {ex.Message}");
            }
        }

        public abstract void SubscribeToUIEvents();

        public virtual void Cleanup()
        {
            try
            {
                // Базовая очистка
                UnsubscribeFromUIEvents();
                
                // Специфичная очистка страницы
                CleanupPageSpecific();

                Console.WriteLine($"🧹 [{GetType().Name}] Cleanup завершен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка при cleanup: {ex.Message}");
            }
        }

        public async Task InitializePageDataAsync()
        {
            try
            {
                StatusMessage = "Инициализация данных...";

                // Инициализируем специфичные данные страницы
                await InitializePageSpecificDataAsync();

                StatusMessage = "Готово";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка инициализации: {ex.Message}";
                Console.WriteLine($"❌ [{GetType().Name}] Ошибка инициализации данных: {ex.Message}");
            }
        }

        #endregion

        #region Protected Virtual Methods (Override in derived classes)

        /// <summary>
        /// Загружает базовые настройки (по умолчанию)
        /// </summary>
        protected virtual async Task LoadBaseSettingsAsync()
        {
            // Базовая загрузка - может быть переопределена
            await Task.CompletedTask;
        }

        /// <summary>
        /// Загружает настройки специфичные для страницы
        /// </summary>
        protected abstract Task LoadPageSpecificSettingsAsync();

        /// <summary>
        /// Применяет текущие настройки UI к объекту конфигурации
        /// </summary>
        protected abstract Task ApplySettingsToConfigAsync(AppConfig config);

        /// <summary>
        /// Применяет настройки к сервисам (опционально)
        /// </summary>
        protected virtual async Task ApplySettingsToServicesAsync()
        {
            // По умолчанию ничего не делаем
            await Task.CompletedTask;
        }

        /// <summary>
        /// Инициализирует специфичные для страницы данные (устройства, модели и т.д.)
        /// </summary>
        protected virtual async Task InitializePageSpecificDataAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Отписывается от UI событий при очистке
        /// </summary>
        protected virtual void UnsubscribeFromUIEvents()
        {
            // По умолчанию ничего не делаем
        }

        /// <summary>
        /// Специфичная очистка для страницы
        /// </summary>
        protected virtual void CleanupPageSpecific()
        {
            // По умолчанию ничего не делаем
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Вызывается при изменении любого UI элемента для автоприменения
        /// </summary>
        protected async void OnUISettingChanged()
        {
            if (IsLoadingUI) return;

            HasUnsavedChanges = true;
            await ApplySettingsAsync();
        }

        /// <summary>
        /// Асинхронная версия для использования в ViewModels
        /// </summary>
        protected async Task OnUISettingChangedAsync()
        {
            if (IsLoadingUI) return;

            HasUnsavedChanges = true;
            await ApplySettingsAsync();
        }

        /// <summary>
        /// Безопасное обновление статуса UI
        /// </summary>
        protected void UpdateStatus(string message, bool isError = false)
        {
            StatusMessage = message;
            if (isError)
            {
                Console.WriteLine($"❌ [{GetType().Name}] {message}");
            }
            else
            {
                Console.WriteLine($"📝 [{GetType().Name}] {message}");
            }
        }

        /// <summary>
        /// Проверяет готовность для работы с настройками
        /// </summary>
        protected bool IsReadyForOperation()
        {
            if (IsLoadingUI)
            {
                Console.WriteLine($"⏳ [{GetType().Name}] Операция пропущена - идет загрузка UI");
                return false;
            }

            if (_serviceContext?.Config == null)
            {
                Console.WriteLine($"❌ [{GetType().Name}] ServiceContext.Config недоступен");
                return false;
            }

            if (_configurationService == null)
            {
                Console.WriteLine($"❌ [{GetType().Name}] ConfigurationService недоступен");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Помощник для привязки ComboBox к enum значениям
        /// </summary>
        protected void SelectComboBoxItemByTag<T>(System.Windows.Controls.ComboBox comboBox, T value) where T : struct, Enum
        {
            foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag is string tag && Enum.TryParse<T>(tag, out var itemValue) && itemValue.Equals(value))
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Помощник для получения выбранного enum значения из ComboBox
        /// </summary>
        protected T? GetSelectedComboBoxValue<T>(System.Windows.Controls.ComboBox comboBox) where T : struct, Enum
        {
            var selectedItem = comboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selectedItem?.Tag is string tag && Enum.TryParse<T>(tag, out var value))
            {
                return value;
            }
            return null;
        }

        #endregion

        #region Public Initialization Method

        /// <summary>
        /// Полная инициализация страницы настроек
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine($"🔥 [{GetType().Name}] Начинаем инициализацию");

                // 1. Инициализируем данные страницы
                await InitializePageDataAsync();

                // 2. Загружаем настройки
                await LoadSettingsAsync();

                Console.WriteLine($"✅ [{GetType().Name}] Инициализация завершена");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{GetType().Name}] Критическая ошибка инициализации: {ex.Message}");
                UpdateStatus($"Критическая ошибка: {ex.Message}", true);
            }
        }

        #endregion
    }
}