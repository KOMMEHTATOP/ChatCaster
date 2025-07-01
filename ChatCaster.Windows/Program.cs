using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Views;
using ChatCaster.Windows.ViewModels;
using ChatCaster.Core.Services;
using ChatCaster.Core.Models;
using ChatCaster.Core.Logging;
using ChatCaster.SpeechRecognition.Whisper.Extensions;
using ChatCaster.SpeechRecognition.Whisper.Constants;
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
                // Инициализация логирования ПЕРЕД созданием DI
                InitializeLogging();
                
                Log.Information("ChatCaster запускается...");

                // Создание и настройка DI контейнера
                var host = CreateHostBuilder(args).Build();
                _serviceProvider = host.Services;

                // ✅ ДОБАВЛЕНА ДИАГНОСТИКА DI
                Console.WriteLine("=== ПРОВЕРКА DI ===");
                var gamepadFromDI = _serviceProvider.GetRequiredService<IGamepadService>();
                Console.WriteLine($"GamepadService из DI: {gamepadFromDI.GetType().Name} - HashCode: {gamepadFromDI.GetHashCode()}");

                // Создание и запуск WPF приложения
                var app = new App();
                app.InitializeComponent();
                
                // ✅ ДОБАВЛЕНА ДИАГНОСТИКА СОЗДАНИЯ ОКНА
                Console.WriteLine("=== СОЗДАНИЕ ОКНА ===");
                // Получаем главное окно из DI
                var mainWindow = _serviceProvider.GetRequiredService<ChatCasterWindow>();
                Console.WriteLine($"MainWindow создан: {mainWindow.GetType().Name}");
                
                // ✅ СОЗДАЕМ TrayService ПОСЛЕ создания окна
                Console.WriteLine("=== СОЗДАНИЕ TrayService ===");
                var trayService = new TrayService(mainWindow);
                
                // ✅ УСТАНАВЛИВАЕМ TrayService везде где нужно
                var viewModel = _serviceProvider.GetRequiredService<ChatCasterWindowViewModel>();
                var gamepadCoordinator = _serviceProvider.GetRequiredService<Services.GamepadService.GamepadVoiceCoordinator>();
                
                // Устанавливаем TrayService в окно, ViewModel и GamepadCoordinator
                mainWindow.SetTrayService(trayService);
                viewModel.SetTrayService(trayService);
                gamepadCoordinator.SetTrayService(trayService);
                Console.WriteLine("TrayService создан и установлен во все компоненты");
                
                // ✅ ПРОВЕРЯЕМ HASHCODE ПОСЛЕ СОЗДАНИЯ ОКНА
                var gamepadAfterWindow = _serviceProvider.GetRequiredService<IGamepadService>();
                Console.WriteLine($"GamepadService после создания окна: HashCode {gamepadAfterWindow.GetHashCode()}");
                
                // ✅ ДОПОЛНИТЕЛЬНАЯ ДИАГНОСТИКА
                Console.WriteLine("=== VIEWMODEL УЖЕ СОЗДАН ===");
                Console.WriteLine($"ViewModel создан: {viewModel.GetType().Name}");
                
                app.MainWindow = mainWindow;
                
                Log.Information("ChatCaster успешно инициализирован");
                
                // ✅ ДОБАВЛЕНА ПРОВЕРКА ПЕРЕД ЗАПУСКОМ
                Console.WriteLine("=== ЗАПУСК WPF ===");
                Console.WriteLine($"MainWindow установлено: {app.MainWindow != null}");
                
                // Запуск приложения
                app.Run(mainWindow);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Критическая ошибка при запуске ChatCaster");
                Console.WriteLine($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
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
                    // Настройка конфигурации
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
            services.AddSingleton<IConfiguration>(configuration);
            
            // Загружаем AppConfig из файла
            var appConfig = LoadAppConfig();
            services.AddSingleton(appConfig);

            // === НОВЫЙ WHISPER МОДУЛЬ ===
            // Получаем настройки Whisper из конфига
            var speechConfig = appConfig.SpeechRecognition ?? new SpeechRecognitionConfig();
            
            // Используем ВАШИ extension methods из ServiceCollectionExtensions.cs
            services.AddWhisperAsSpeechRecognition(config => {
                config.ModelSize = GetWhisperModelSize(speechConfig);
                config.ThreadCount = Environment.ProcessorCount / 2;
                config.EnableGpu = speechConfig.UseGpuAcceleration;
                config.Language = speechConfig.Language ?? WhisperConstants.Languages.Russian;
                config.ModelPath = "Models"; // Путь к моделям
                config.UseVAD = true;
                config.InitializationTimeoutSeconds = 30;
                config.RecognitionTimeoutSeconds = 60;
                config.MaxTokens = speechConfig.MaxTokens;
            });

            // === ОСТАЛЬНЫЕ СЕРВИСЫ ===
            services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
            services.AddSingleton<ISystemIntegrationService, SystemIntegrationService>();
            services.AddSingleton<IOverlayService, OverlayService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();
            // ✅ ИСПРАВЛЕНИЕ: Регистрируем конкретный класс сначала, потом интерфейс
            services.AddSingleton<Services.GamepadService.MainGamepadService>();
            services.AddSingleton<IGamepadService>(provider => 
                provider.GetRequiredService<Services.GamepadService.MainGamepadService>());

            // VoiceRecordingService зависит от других сервисов
            services.AddSingleton<IVoiceRecordingService>(provider =>
                new VoiceRecordingService(
                    provider.GetRequiredService<IAudioCaptureService>(),
                    provider.GetRequiredService<ISpeechRecognitionService>(),
                    provider.GetRequiredService<IConfigurationService>()
                ));

            // GamepadVoiceCoordinator
            services.AddSingleton<Services.GamepadService.GamepadVoiceCoordinator>();

            // ✅ УБИРАЕМ TrayService из DI - создадим вручную после создания окна
            // TrayService будет создан в Main после создания ChatCasterWindow

            // ✅ УДАЛЕН ServiceContext - больше не нужен!

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
            // Пытаемся получить размер модели из EngineSettings
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
            
            // Если не найден или невалидный - возвращаем дефолтный
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
                Console.WriteLine($"Ошибка инициализации логирования: {ex.Message}");
            }
        }
    }
}