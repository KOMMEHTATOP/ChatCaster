using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Overlay;
using ChatCaster.Core.Services.System;
using ChatCaster.Windows.Services.GamepadService;
using Serilog;
using System.IO;

namespace ChatCaster.Windows.Services
{
    /// <summary>
    /// Сервис для инициализации всех компонентов приложения при запуске
    /// Выносим логику инициализации из ChatCasterWindowViewModel
    /// </summary>
    public class ApplicationInitializationService
    {
        private readonly IConfigurationService _configurationService;
        private readonly ISpeechRecognitionService _speechService;
        private readonly IAudioCaptureService _audioService;
        private readonly IOverlayService _overlayService;
        private readonly ISystemIntegrationService _systemService;
        private readonly GamepadVoiceCoordinator _gamepadVoiceCoordinator;
        private readonly IStartupManagerService _startupManagerService;


        public ApplicationInitializationService(
            IConfigurationService configurationService,
            ISpeechRecognitionService speechService,
            IAudioCaptureService audioService,
            IOverlayService overlayService,
            ISystemIntegrationService systemService,
            GamepadVoiceCoordinator gamepadVoiceCoordinator,
            IStartupManagerService startupManagerService)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _gamepadVoiceCoordinator =
                gamepadVoiceCoordinator ?? throw new ArgumentNullException(nameof(gamepadVoiceCoordinator));
            _startupManagerService = startupManagerService ?? throw new ArgumentNullException(nameof(startupManagerService));
        }

        /// <summary>
        /// Инициализирует все сервисы приложения
        /// </summary>
        public async Task<AppConfig> InitializeApplicationAsync()
        {
            try
            {
                // 1. Загружаем конфигурацию
                var config = _configurationService.CurrentConfig;
                Log.Information("🔍 [PATH] Current directory: {CurrentDir}", Directory.GetCurrentDirectory());
                Log.Information("🔍 [PATH] Base directory: {BaseDir}", AppContext.BaseDirectory);
                Log.Information("🔍 [PATH] Models path: {ModelsPath}",
                    Path.Combine(AppContext.BaseDirectory, "Models")); // ← ИСПРАВЛЕНО

                // 2. Проверяем первый запуск и устанавливаем defaults
                await EnsureDefaultConfigurationAsync(config);

                var modelsDir = Path.Combine(AppContext.BaseDirectory, "Models");
                var modelFile = Path.Combine(modelsDir, "ggml-tiny.bin");
                Log.Information("🔍 [CHECK] Models directory exists: {Exists}", Directory.Exists(modelsDir));
                Log.Information("🔍 [CHECK] Model file exists: {Exists}, Path: {Path}", File.Exists(modelFile), modelFile);

                // 3. Инициализируем все сервисы
                await InitializeSpeechRecognitionAsync(config);
                await InitializeAudioAsync(config);
                await InitializeOverlayAsync(config);
                await InitializeHotkeysAsync(config);
                await InitializeGamepadAsync();

                return config;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ApplicationInitializationService: критическая ошибка инициализации");
                throw;
            }
        }

        /// <summary>
        /// Регистрирует глобальные хоткеи
        /// </summary>
        public async Task<bool> RegisterHotkeysAsync(AppConfig config)
        {
            try
            {
                if (config.Input.KeyboardShortcut != null)
                {
                    var registered = await _systemService.RegisterGlobalHotkeyAsync(config.Input.KeyboardShortcut);
                    return registered;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ApplicationInitializationService: ошибка регистрации хоткеев");
                return false;
            }
        }

        private async Task EnsureDefaultConfigurationAsync(AppConfig config)
        {
            bool configChanged = false;

            // Проверяем и устанавливаем ModelPath
            if (!config.SpeechRecognition.EngineSettings.ContainsKey("ModelPath") ||
                string.IsNullOrEmpty(config.SpeechRecognition.EngineSettings["ModelPath"]?.ToString()))
            {
                config.SpeechRecognition.EngineSettings["ModelPath"] = Path.Combine(AppContext.BaseDirectory, "Models");
                configChanged = true;
                Log.Information("Установлен дефолтный ModelPath: {ModelPath}",
                    config.SpeechRecognition.EngineSettings["ModelPath"]);
            }

            // Проверяем и устанавливаем Whisper модель по умолчанию
            if (!config.SpeechRecognition.EngineSettings.ContainsKey("ModelSize") ||
                string.IsNullOrEmpty(config.SpeechRecognition.EngineSettings["ModelSize"]?.ToString()))
            {
                config.SpeechRecognition.EngineSettings["ModelSize"] = "tiny";
                configChanged = true;
                Log.Information("Установлена дефолтная модель Whisper: tiny");
            }

            // Применяем настройку автозапуска согласно конфигурации
            try
            {
                await _startupManagerService.SetStartupAsync(config.System.StartWithSystem);
                Log.Information("Настройка автозапуска применена: {StartWithSystem}", config.System.StartWithSystem);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка применения настройки автозапуска");
            }

            // Проверяем и устанавливаем аудио устройство по умолчанию
            if (string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                try
                {
                    var devices = await _audioService.GetAvailableDevicesAsync();
                    var defaultDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();

                    if (defaultDevice != null)
                    {
                        config.Audio.SelectedDeviceId = defaultDevice.Id;
                        configChanged = true;
                        Log.Information("Установлено дефолтное аудио устройство: {DeviceName}", defaultDevice.Name);
                    }
                    else
                    {
                        Log.Warning("ApplicationInitializationService: не найдено ни одного аудио устройства");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ApplicationInitializationService: ошибка получения аудио устройств");
                }
            }

            if (configChanged)
            {
                await _configurationService.SaveConfigAsync(config);
            }
        }

        private async Task InitializeSpeechRecognitionAsync(AppConfig config)
        {
            // Проверим наличие модели ПЕРЕД инициализацией
            var modelsDir = Path.Combine(AppContext.BaseDirectory, "Models");
            var modelFile = Path.Combine(modelsDir, "ggml-tiny.bin");

            Log.Information("🔍 [INIT] Models directory exists: {Exists}", Directory.Exists(modelsDir));
            Log.Information("🔍 [INIT] Model file exists BEFORE init: {Exists}, Path: {Path}", File.Exists(modelFile),
                modelFile);

            Log.Information("🔍 [INIT] Начинаем инициализацию Whisper с моделью: {ModelSize}",
                config.SpeechRecognition.EngineSettings.TryGetValue("ModelSize", out var model) ? model : "unknown");

            bool speechInitialized = false;

            try
            {
                speechInitialized = await _speechService.InitializeAsync(config.SpeechRecognition);
                Log.Information("🔍 [INIT] Результат инициализации: {Success}", speechInitialized);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "🔍 [INIT] ИСКЛЮЧЕНИЕ при инициализации Whisper: {Type}, {Message}",
                    ex.GetType().Name, ex.Message);
            }

            Log.Information("🔍 [INIT] Model file exists AFTER init: {Exists}", File.Exists(modelFile));
            Log.Information("🔍 [INIT] Speech service initialized: {Success}, IsInitialized: {IsInitialized}",
                speechInitialized, _speechService.IsInitialized);
        }


        private async Task InitializeAudioAsync(AppConfig config)
        {
            if (!string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                try
                {
                    var deviceSet = await _audioService.SetActiveDeviceAsync(config.Audio.SelectedDeviceId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ApplicationInitializationService: ошибка установки аудио устройства: {DeviceId}",
                        config.Audio.SelectedDeviceId);
                }
            }
            else
            {
                Log.Warning("ApplicationInitializationService: в конфигурации не указано аудио устройство");
            }
        }

        private async Task InitializeOverlayAsync(AppConfig config)
        {
            await _overlayService.ApplyConfigAsync(config.Overlay);
        }

        private async Task InitializeHotkeysAsync(AppConfig config)
        {
            await RegisterHotkeysAsync(config);
        }

        private async Task InitializeGamepadAsync()
        {
            var gamepadInitialized = await _gamepadVoiceCoordinator.InitializeAsync();
        }
    }
}
