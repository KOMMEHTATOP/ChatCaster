using ChatCaster.Core.Models;
using ChatCaster.Core.Services.Audio;
using ChatCaster.Core.Services.Core;
using ChatCaster.Core.Services.Overlay;
using ChatCaster.Core.Services.System;
using ChatCaster.Windows.Services.GamepadService;
using Serilog;

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

        public ApplicationInitializationService(
            IConfigurationService configurationService,
            ISpeechRecognitionService speechService,
            IAudioCaptureService audioService,
            IOverlayService overlayService,
            ISystemIntegrationService systemService,
            GamepadVoiceCoordinator gamepadVoiceCoordinator)
        {
            _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
            _speechService = speechService ?? throw new ArgumentNullException(nameof(speechService));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _overlayService = overlayService ?? throw new ArgumentNullException(nameof(overlayService));
            _systemService = systemService ?? throw new ArgumentNullException(nameof(systemService));
            _gamepadVoiceCoordinator =
                gamepadVoiceCoordinator ?? throw new ArgumentNullException(nameof(gamepadVoiceCoordinator));
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
                Log.Information("ApplicationInitializationService: конфигурация загружена");

                // 2. Проверяем первый запуск и устанавливаем defaults
                await EnsureDefaultConfigurationAsync(config);

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
            Log.Information("🔍 ДИАГНОСТИКА: SelectedLanguage в начале EnsureDefault = {Lang}", 
                config?.System?.SelectedLanguage);

            bool configChanged = false;

            // Проверяем и устанавливаем Whisper модель по умолчанию
            if (string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                Log.Information("ApplicationInitializationService: новая установка, применяем дефолтные настройки");

                // Устанавливаем дефолтную модель Whisper
                config.SpeechRecognition.EngineSettings["ModelSize"] = "tiny";
                configChanged = true;

            }

            // Проверяем и устанавливаем аудио устройство по умолчанию
            if (string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                try
                {
                    // Получаем список доступных устройств
                    var devices = await _audioService.GetAvailableDevicesAsync();
                    var defaultDevice = devices.FirstOrDefault(d => d.IsDefault)
                                        ?? devices.FirstOrDefault();

                    if (defaultDevice != null)
                    {
                        config.Audio.SelectedDeviceId = defaultDevice.Id;
                        configChanged = true;
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

            // Сохраняем конфигурацию только если были изменения
            if (configChanged)
            {
                Log.Information("🔍 ДИАГНОСТИКА: SelectedLanguage перед сохранением = {Lang}", 
                    config.System?.SelectedLanguage);

                await _configurationService.SaveConfigAsync(config);
                Log.Information("ApplicationInitializationService: дефолтная конфигурация сохранена");
            }
        }


        private async Task InitializeSpeechRecognitionAsync(AppConfig config)
        {
            var speechInitialized = await _speechService.InitializeAsync(config.SpeechRecognition);
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
