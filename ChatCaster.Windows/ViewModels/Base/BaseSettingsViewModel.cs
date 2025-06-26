using CommunityToolkit.Mvvm.ComponentModel;
using ChatCaster.Windows.Services;
using ChatCaster.Core.Models;
using Serilog;

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
                Log.Error(ex, "[{ViewModelName}] Ошибка загрузки настроек", GetType().Name);
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

                // ✅ ДИАГНОСТИКА: Логируем применение настроек
                Log.Information("[{ViewModelName}] === ПРИМЕНЕНИЕ НАСТРОЕК ===", GetType().Name);

                // Применяем настройки к конфигурации
                await ApplySettingsToConfigAsync(_serviceContext.Config);

                // Сохраняем конфигурацию
                await _configurationService.SaveConfigAsync(_serviceContext.Config);
                Log.Information("[{ViewModelName}] Конфигурация сохранена в файл", GetType().Name);

                // Применяем настройки к сервисам
                await ApplySettingsToServicesAsync();
                Log.Information("[{ViewModelName}] Настройки применены к сервисам", GetType().Name);

                HasUnsavedChanges = false;
                StatusMessage = "Настройки сохранены";

                Log.Information("[{ViewModelName}] Настройки успешно применены", GetType().Name);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ошибка сохранения: {ex.Message}";
                Log.Error(ex, "[{ViewModelName}] Ошибка применения настроек", GetType().Name);
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

                Log.Debug("[{ViewModelName}] Cleanup завершен", GetType().Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{ViewModelName}] Ошибка при cleanup", GetType().Name);
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
                Log.Error(ex, "[{ViewModelName}] Ошибка инициализации данных", GetType().Name);
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

            // ✅ ДИАГНОСТИКА: Логируем изменение настроек в UI
            Log.Debug("[{ViewModelName}] UI настройка изменена, применяем...", GetType().Name);

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
                Log.Error("[{ViewModelName}] {Message}", GetType().Name, message);
            }
            else
            {
                Log.Information("[{ViewModelName}] {Message}", GetType().Name, message);
            }
        }

        /// <summary>
        /// Проверяет готовность для работы с настройками
        /// </summary>
        protected bool IsReadyForOperation()
        {
            if (IsLoadingUI)
            {
                Log.Debug("[{ViewModelName}] Операция пропущена - идет загрузка UI", GetType().Name);
                return false;
            }

            if (_serviceContext?.Config == null)
            {
                Log.Warning("[{ViewModelName}] ServiceContext.Config недоступен", GetType().Name);
                return false;
            }

            if (_configurationService == null)
            {
                Log.Warning("[{ViewModelName}] ConfigurationService недоступен", GetType().Name);
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
                Log.Information("[{ViewModelName}] Начинаем инициализацию", GetType().Name);

                // 1. Инициализируем данные страницы
                await InitializePageDataAsync();

                // 2. Загружаем настройки
                await LoadSettingsAsync();

                Log.Information("[{ViewModelName}] Инициализация завершена", GetType().Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{ViewModelName}] Критическая ошибка инициализации", GetType().Name);
                UpdateStatus($"Критическая ошибка: {ex.Message}", true);
            }
        }

        #endregion
    }
}