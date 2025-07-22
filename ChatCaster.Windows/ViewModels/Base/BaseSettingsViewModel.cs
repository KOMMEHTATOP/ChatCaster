using CommunityToolkit.Mvvm.ComponentModel;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Core;
using Serilog;

namespace ChatCaster.Windows.ViewModels.Base
{
    public abstract partial class BaseSettingsViewModel : ViewModelBase, ISettingsViewModel
    {
        #region Protected Services

        protected readonly IConfigurationService _configurationService;
        protected readonly AppConfig _currentConfig;

        #endregion

        #region Observable Properties

        [ObservableProperty]
        private bool _isLoadingUI;

        [ObservableProperty]
        private bool _isInitialized;

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        #endregion

        #region Constructor

        protected BaseSettingsViewModel(
            IConfigurationService configurationService, 
            AppConfig currentConfig)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
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
            if (IsLoadingUI)
                return;

            try
            {
                StatusMessage = "Сохранение настроек...";

                // Логируем применение настроек
                Log.Information("[{ViewModelName}] === ПРИМЕНЕНИЕ НАСТРОЕК ===", GetType().Name);

                // Применяем настройки к конфигурации
                await ApplySettingsToConfigAsync(_currentConfig);

                Log.Debug("После сохранения - Config HashCode: {HashCode}, AllowCompleteExit: {Value}", 
                    _currentConfig.GetHashCode(), 
                    _currentConfig.System?.AllowCompleteExit);

                // Сохраняем конфигурацию
                await _configurationService.SaveConfigAsync(_currentConfig);
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
        protected async Task LoadBaseSettingsAsync()
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
        /// Асинхронная версия для использования в ViewModels
        /// </summary>
        protected async Task OnUISettingChangedAsync()
        {
            if (IsLoadingUI) return;

            // Логируем изменение настроек
            // в UI
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
        
        #endregion

        #region Public Initialization Method

        /// <summary>
        /// Полная инициализация страницы настроек
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await InitializePageDataAsync();
                await LoadSettingsAsync();
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