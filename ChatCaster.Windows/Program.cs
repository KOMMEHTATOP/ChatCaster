using ChatCaster.Core.Constants;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Views;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Core.Models;
using ChatCaster.Core.Logging;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Input;
using ChatCaster.Core.Services.Overlay;
using ChatCaster.Core.Services.System;
using ChatCaster.Core.Services.UI;
using ChatCaster.Core.Updates;
using ChatCaster.SpeechRecognition.Whisper.Extensions;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.Windows.Managers.MainPage;
using ChatCaster.Windows.Managers.VoiceRecording;
using ChatCaster.Windows.Services.IntegrationService;
using ChatCaster.Windows.Services.OverlayService;
using ChatCaster.Windows.Services.Navigation;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChatCaster.Windows
{
    public class Program
    {
        private static IServiceProvider? _serviceProvider;
        private static Mutex? _singleInstanceMutex;

        // P/Invoke для работы с окнами
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        [STAThread]
        public static void Main(string[] args)
        {
            // Проверяем single instance
            if (!CheckSingleInstance())
            {
                return; // Приложение уже запущено, выходим
            }

            try
            {
                InitializeLogging();
                Log.Information("ChatCaster запускается...");

                // Создание и настройка DI контейнера
                var host = CreateHostBuilder(args).Build();
                _serviceProvider = host.Services;

                // Создание и запуск WPF приложения
                var app = new App();
                app.InitializeComponent();

                // Получаем главное окно и сервисы из DI
                var mainWindow = _serviceProvider.GetRequiredService<ChatCasterWindow>();
                var trayService = _serviceProvider.GetRequiredService<ITrayService>();
                var notificationService = _serviceProvider.GetRequiredService<INotificationService>();

                // Инициализируем TrayService
                trayService.Initialize();

                // Инициализируем NotificationService
                notificationService.InitializeAsync().GetAwaiter().GetResult();

                app.MainWindow = mainWindow;
                
                // Запуск приложения
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Критическая ошибка при запуске ChatCaster");
                MessageBox.Show(
                    $"Критическая ошибка при запуске приложения:\n{ex.Message}",
                    "ChatCaster - Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                Log.CloseAndFlush();
                _serviceProvider?.GetService<IHost>()?.Dispose();
                
                // Освобождаем mutex
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
        }

        /// <summary>
        /// Проверяет запущен ли уже экземпляр приложения
        /// </summary>
        private static bool CheckSingleInstance()
        {
            const string mutexName = "ChatCaster_SingleInstance_Mutex";
            
            try
            {
                _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);
                
                if (!createdNew)
                {
                    // Приложение уже запущено, пытаемся найти и активировать окно
                    Log.Information("ChatCaster уже запущен, активируем существующее окно");
                    ActivateExistingInstance();
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка проверки single instance");
                // В случае ошибки разрешаем запуск
                return true;
            }
        }

        /// <summary>
        /// Пытается найти и активировать окно уже запущенного экземпляра
        /// </summary>
        private static void ActivateExistingInstance()
        {
            try
            {
                var currentProcessName = Process.GetCurrentProcess().ProcessName;
                var processes = Process.GetProcessesByName(currentProcessName);

                foreach (var process in processes.Where(p => p.Id != Environment.ProcessId))
                {
                    var mainWindowHandle = process.MainWindowHandle;
                    if (mainWindowHandle != IntPtr.Zero)
                    {
                        // Если окно свернуто, восстанавливаем его
                        if (IsIconic(mainWindowHandle))
                        {
                            ShowWindow(mainWindowHandle, SW_RESTORE);
                        }
                        else
                        {
                            ShowWindow(mainWindowHandle, SW_SHOW);
                        }
                        
                        // Выводим на передний план
                        SetForegroundWindow(mainWindowHandle);
                        
                        Log.Information("Активировано существующее окно ChatCaster");
                        return;
                    }
                }

                Log.Warning("Не удалось найти окно запущенного экземпляра ChatCaster");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка активации существующего экземпляра");
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true)
                        .AddUserSecrets<Program>(optional: true);
                })
                .ConfigureServices((context, services) => { ConfigureServices(services, context.Configuration); });

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            Log.Debug("Настройка DI контейнера...");

            // === КОНФИГУРАЦИЯ ===
            services.AddSingleton(configuration);

            // Создаем единственный экземпляр AppConfig
            services.AddSingleton<AppConfig>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Инициализируем конфигурацию после создания сервисов
            services.AddSingleton<AppConfig>(provider =>
            {
                var configService = provider.GetRequiredService<IConfigurationService>();
                return configService.LoadConfigAsync().GetAwaiter().GetResult();
            });

            // === WHISPER МОДУЛЬ ===
            services.AddWhisperAsSpeechRecognition(config =>
            {
                // Получаем AppConfig из DI для настройки Whisper
                var serviceProvider = services.BuildServiceProvider();
                var appConfig = serviceProvider.GetRequiredService<AppConfig>();
                var speechConfig = appConfig.SpeechRecognition;

                config.ModelSize = GetWhisperModelSize(speechConfig);
                config.ThreadCount = Environment.ProcessorCount / 2;
                config.EnableGpu = speechConfig.UseGpuAcceleration;
                config.Language = speechConfig.Language;
                config.ModelPath = AppConstants.Paths.GetModelsDirectory();
                speechConfig.EngineSettings["ModelPath"] = config.ModelPath;
                Log.Information("🔍 [WHISPER_CONFIG] AppContext.BaseDirectory: {BaseDir}", AppContext.BaseDirectory);
                Log.Information("🔍 [WHISPER_CONFIG] ModelPath установлен: {ModelPath}", config.ModelPath);

                config.UseVAD = true;
                config.InitializationTimeoutSeconds = 30;
                config.RecognitionTimeoutSeconds = 60;
                config.MaxTokens = speechConfig.MaxTokens;
            });

            // === ОСНОВНЫЕ СЕРВИСЫ ===
            services.AddSingleton<ISystemIntegrationService, SystemIntegrationService>();
            services.AddSingleton<IOverlayService, WindowsOverlayService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();

            // === АУДИО СЕРВИСЫ ===
            services.AddSingleton<WindowsAudioCompatibility>();
            services.AddSingleton<IAudioCaptureService>(provider =>
            {
                return new AudioCaptureService(provider.GetRequiredService<WindowsAudioCompatibility>());
            });

            // === ГЕЙМПАД СЕРВИСЫ ===
            services.AddSingleton<Services.GamepadService.MainGamepadService>(provider =>
                new Services.GamepadService.MainGamepadService(
                    new Services.GamepadService.XInputProvider(),
                    provider.GetRequiredService<IConfigurationService>()
                ));
            services.AddSingleton<IGamepadService>(provider =>
                provider.GetRequiredService<Services.GamepadService.MainGamepadService>());

            // === ИНТЕГРАЦИОННЫЕ СЕРВИСЫ ===
            services.AddSingleton<IWindowService, WindowService>();
            services.AddSingleton<ITextInputService, TextInputService>();
            services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
            services.AddSingleton<ISystemNotificationService, SystemNotificationService>();
            services.AddSingleton<IStartupManagerService, WindowsStartupManagerService>();

            // === TRAY СЕРВИСЫ ===
            services.AddSingleton<ITrayService, TrayService>();
            services.AddSingleton<INotificationService, NotificationService>();

            // === VOICE RECORDING СЕРВИСЫ ===
            services.AddSingleton<IVoiceRecordingService>(provider =>
                new VoiceRecordingCoordinator(
                    provider.GetRequiredService<IAudioCaptureService>(),
                    provider.GetRequiredService<ISpeechRecognitionService>(),
                    provider.GetRequiredService<IConfigurationService>()
                ));

            services.AddSingleton<Services.GamepadService.GamepadVoiceCoordinator>(provider =>
                new Services.GamepadService.GamepadVoiceCoordinator(
                    provider.GetRequiredService<IGamepadService>(),
                    provider.GetRequiredService<IVoiceRecordingService>(),
                    provider.GetRequiredService<ISystemIntegrationService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<ITrayService>()
                ));

            // === ИНИЦИАЛИЗАЦИЯ И НАВИГАЦИЯ ===
            services.AddSingleton<ApplicationInitializationService>();

            // НАВИГАЦИЯ 
            services.AddSingleton<PageFactory>();
            services.AddSingleton<PageCacheManager>();
            services.AddSingleton<ViewModelCleanupService>();
            services.AddSingleton<ViewModels.Navigation.NavigationManager>();

            // === МЕНЕДЖЕРЫ MAINPAGE ===
            services.AddSingleton<RecordingStatusManager>();
            services.AddSingleton<DeviceDisplayManager>(provider =>
                new DeviceDisplayManager(
                    provider.GetRequiredService<IAudioCaptureService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<ILocalizationService>()
                ));

            // === КОМПОНЕНТЫ VIEWMODELS ===
            services.AddSingleton<ViewModels.Components.RecordingStatusComponentViewModel>();
            services.AddSingleton<ViewModels.Components.RecognitionResultsComponentViewModel>();

            // === VIEWMODELS ===
            services.AddSingleton<ChatCasterWindowViewModel>();
            services.AddSingleton<AudioSettingsViewModel>();
            services.AddSingleton<InterfaceSettingsViewModel>();
            services.AddSingleton<ControlSettingsViewModel>();
            services.AddSingleton<MainPageViewModel>();

            // === VIEWS ===
            services.AddSingleton<ChatCasterWindow>();

            // === СИСТЕМА ОБНОВЛЕНИЙ ===
            services.AddSingleton<IUpdateService, GitHubUpdateService>();

            Log.Information("DI контейнер настроен");
        }

        /// <summary>
        /// Получает размер модели из EngineSettings или возвращает дефолтный
        /// </summary>
        private static string GetWhisperModelSize(SpeechRecognitionConfig speechConfig)
        {
            if (speechConfig.EngineSettings.TryGetValue("ModelSize", out var modelSizeObj))
            {
                var modelSizeStr = modelSizeObj?.ToString();

                // Проверяем что это валидный размер
                if (!string.IsNullOrEmpty(modelSizeStr) &&
                    WhisperConstants.ModelSizes.All.Contains(modelSizeStr))
                {
                    return modelSizeStr;
                }
            }

            return WhisperConstants.ModelSizes.Base;
        }

        private static void InitializeLogging()
        {
            try
            {
                var loggingConfig = new LoggingConfig();

#if DEBUG
                loggingConfig.EnableConsoleLogging = true;
                loggingConfig.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
#else
                loggingConfig.EnableConsoleLogging = false;
                loggingConfig.MinimumLevel = Serilog.Events.LogEventLevel.Information;
#endif

                Log.Logger = LoggingConfiguration.CreateLogger(loggingConfig);
                Log.Information("Система логирования инициализирована");
            }
            catch (Exception ex)
            {
                Log.Information($"Ошибка инициализации логирования: {ex.Message}");
            }
        }
    }
}