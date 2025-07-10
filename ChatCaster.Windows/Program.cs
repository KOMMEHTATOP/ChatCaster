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
using ChatCaster.SpeechRecognition.Whisper.Extensions;
using ChatCaster.SpeechRecognition.Whisper.Constants;
using ChatCaster.Windows.Services.IntegrationService;
using ChatCaster.Windows.Services.OverlayService;
using Serilog;

namespace ChatCaster.Windows
{
    public class Program
    {
        private static IServiceProvider? _serviceProvider;

        [STAThread]
        public static void Main(string[] args)
        {
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
                Log.Information("TrayService инициализирован");
                
                // Инициализируем NotificationService (заменяет TrayNotificationCoordinator)
                notificationService.InitializeAsync().GetAwaiter().GetResult();
                Log.Information("NotificationService инициализирован");

                app.MainWindow = mainWindow;
                
                Log.Information("ChatCaster успешно инициализирован");
                
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
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services, context.Configuration);
                });

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            Log.Debug("Настройка DI контейнера...");

            // === КОНФИГУРАЦИЯ ===
            services.AddSingleton(configuration);
            
            // Загружаем AppConfig из файла
            var appConfig = LoadAppConfig();
            services.AddSingleton(appConfig);

            // === НОВЫЙ WHISPER МОДУЛЬ ===
            var speechConfig = appConfig.SpeechRecognition;
            
            services.AddWhisperAsSpeechRecognition(config => {
                config.ModelSize = GetWhisperModelSize(speechConfig);
                config.ThreadCount = Environment.ProcessorCount / 2;
                config.EnableGpu = speechConfig.UseGpuAcceleration;
                config.Language = speechConfig.Language;
                config.ModelPath = "Models"; 
                config.UseVAD = true;
                config.InitializationTimeoutSeconds = 30;
                config.RecognitionTimeoutSeconds = 60;
                config.MaxTokens = speechConfig.MaxTokens;
            });

            // === ОСНОВНЫЕ СЕРВИСЫ ===
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddSingleton<ISystemIntegrationService, SystemIntegrationService>();
            services.AddSingleton<IOverlayService, WindowsOverlayService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();

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
            
            // === TRAY СЕРВИСЫ ===
            services.AddSingleton<ITrayService, TrayService>();
            services.AddSingleton<INotificationService, NotificationService>();
            
            // === ОСТАЛЬНЫЕ СЕРВИСЫ ===
            // VoiceRecordingService зависит от других сервисов
            services.AddSingleton<IVoiceRecordingService>(provider =>
                new VoiceRecordingService(
                    provider.GetRequiredService<IAudioCaptureService>(),
                    provider.GetRequiredService<ISpeechRecognitionService>(),
                    provider.GetRequiredService<IConfigurationService>()
                ));

            // GamepadVoiceCoordinator с ITrayService
            services.AddSingleton<Services.GamepadService.GamepadVoiceCoordinator>(provider =>
                new Services.GamepadService.GamepadVoiceCoordinator(
                    provider.GetRequiredService<IGamepadService>(),
                    provider.GetRequiredService<IVoiceRecordingService>(),
                    provider.GetRequiredService<ISystemIntegrationService>(),
                    provider.GetRequiredService<IConfigurationService>(),
                    provider.GetRequiredService<ITrayService>()
                ));

            // === VIEWMODELS ===
            services.AddSingleton<ChatCasterWindowViewModel>();

            // === VIEWS ===
            services.AddSingleton<ChatCasterWindow>();

            Log.Information("DI контейнер настроен успешно");
        }

        private static AppConfig LoadAppConfig()
        {
            try
            {
                var configService = new ConfigurationService();
                return configService.LoadConfigAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Не удалось загрузить конфигурацию, используем дефолтную");
                return new AppConfig();
            }
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