using System.Windows.Controls;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Windows.Views.ViewSettings;
using Serilog;

namespace ChatCaster.Windows.Services.Navigation
{
    /// <summary>
    /// Сервис для очистки ViewModels при закрытии приложения
    /// Отвечает за правильный cleanup всех ресурсов ViewModels
    /// </summary>
    public class ViewModelCleanupService
    {
        /// <summary>
        /// Выполняет cleanup всех ViewModels в переданных страницах
        /// </summary>
        public void CleanupAllViewModels(Dictionary<string, Page> pages, MainPageViewModel? mainPageViewModel)
        {
            if (pages == null)
            {
                Log.Warning("ViewModelCleanupService: pages коллекция null, пропускаем cleanup");
                return;
            }

            Log.Information("ViewModelCleanupService: начинаем cleanup всех ViewModels");

            try
            {
                // Выполняем cleanup в UI потоке
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Сначала очищаем Singleton MainPageViewModel
                    CleanupMainPageViewModel(mainPageViewModel);

                    // Затем очищаем ViewModels остальных страниц
                    CleanupPageViewModels(pages);
                });

                Log.Information("ViewModelCleanupService: cleanup всех ViewModels завершен");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ViewModelCleanupService: критическая ошибка при cleanup ViewModels");
            }
        }

        /// <summary>
        /// Выполняет cleanup конкретной ViewModel
        /// </summary>
        public void CleanupViewModel(object? viewModel, string viewModelName)
        {
            if (viewModel == null)
            {
                Log.Debug("ViewModelCleanupService: {ViewModelName} is null, пропускаем cleanup", viewModelName);
                return;
            }

            try
            {
                Log.Debug("ViewModelCleanupService: выполняем cleanup для {ViewModelName}", viewModelName);

                switch (viewModel)
                {
                    case MainPageViewModel mainPageVM:
                        mainPageVM.Cleanup();
                        break;

                    case AudioSettingsViewModel audioVM:
                        audioVM.Cleanup();
                        break;

                    case InterfaceSettingsViewModel interfaceVM:
                        interfaceVM.Cleanup();
                        break;

                    case ControlSettingsViewModel controlVM:
                        controlVM.Cleanup();
                        break;

                    default:
                        Log.Warning("ViewModelCleanupService: неизвестный тип ViewModel: {Type}", viewModel.GetType().Name);
                        break;
                }

                Log.Debug("ViewModelCleanupService: cleanup для {ViewModelName} завершен", viewModelName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ViewModelCleanupService: ошибка cleanup для {ViewModelName}", viewModelName);
            }
        }

        private void CleanupMainPageViewModel(MainPageViewModel? mainPageViewModel)
        {
            if (mainPageViewModel != null)
            {
                Log.Debug("ViewModelCleanupService: вызываем cleanup для Singleton MainPageViewModel");
                CleanupViewModel(mainPageViewModel, "MainPageViewModel");
            }
            else
            {
                Log.Debug("ViewModelCleanupService: MainPageViewModel не инициализирован");
            }
        }

        private void CleanupPageViewModels(Dictionary<string, Page> pages)
        {
            foreach (var kvp in pages)
            {
                var pageTag = kvp.Key;
                var page = kvp.Value;

                try
                {
                    Log.Debug("ViewModelCleanupService: обрабатываем страницу: {PageTag}", pageTag);

                    switch (page)
                    {
                        case MainPageView:
                            // MainPageViewModel уже очищен выше как Singleton
                            Log.Debug("ViewModelCleanupService: MainPageView - ViewModel уже очищен");
                            break;

                        case AudioSettingsView audioPage:
                            CleanupViewModel(audioPage.DataContext, "AudioSettingsViewModel");
                            break;

                        case InterfaceSettingsView interfacePage:
                            CleanupViewModel(interfacePage.DataContext, "InterfaceSettingsViewModel");
                            break;

                        case ControlSettingsView controlPage:
                            CleanupViewModel(controlPage.DataContext, "ControlSettingsViewModel");
                            break;

                        default:
                            Log.Warning("ViewModelCleanupService: неизвестный тип страницы: {PageType}", page.GetType().Name);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ViewModelCleanupService: ошибка cleanup страницы {PageTag}", pageTag);
                }
            }
        }
    }
}