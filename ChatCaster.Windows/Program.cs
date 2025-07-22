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
using System.IO;

namespace ChatCaster.Windows
{
    public class Program
    {
        private static IServiceProvider? _serviceProvider;

        [STAThread]
        public static void Main(string[] args)
        {
            // СИМУЛЯЦИЯ автозапуска - меняем рабочую директорию
            //Directory.SetCurrentDirectory(@"C:\Windows\system32");

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
                config.ModelPath = Path.Combine(AppContext.BaseDirectory, "Models");
                speechConfig.EngineSettings["ModelPath"] = config.ModelPath;
                Log.Information("🔍 [WHISPER_CONFIG] AppContext.BaseDirectory: {BaseDir}", AppContext.BaseDirectory);
                Log.Information("🔍 [WHISPER_CONFIG] ModelPath установлен: {ModelPath}", config.ModelPath);

                config.UseVAD = true;
                config.InitializationTimeoutSeconds = 30;
                config.RecognitionTimeoutSeconds = 60;
                config.MaxTokens = speechConfig.MaxTokens;
            });

            // === ОСНОВНЫЕ СЕРВИСЫ ===
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddSingleton<ISystemIntegrationService, SystemIntegrationService>();
            services.AddSingleton<IOverlayService, WindowsOverlayService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();

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
