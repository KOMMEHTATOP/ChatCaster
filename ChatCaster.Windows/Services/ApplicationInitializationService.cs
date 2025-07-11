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
            _gamepadVoiceCoordinator = gamepadVoiceCoordinator ?? throw new ArgumentNullException(nameof(gamepadVoiceCoordinator));
        }

        /// <summary>
        /// Инициализирует все сервисы приложения
        /// </summary>
        public async Task<AppConfig> InitializeApplicationAsync()
        {
            try
            {
                Log.Information("ApplicationInitializationService: начинаем инициализацию приложения");

                // 1. Загружаем конфигурацию
                await _configurationService.LoadConfigAsync();
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

                Log.Information("ApplicationInitializationService: инициализация завершена успешно");
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
                    Log.Debug("ApplicationInitializationService: регистрируем хоткей: {Key} + {Modifiers}",
                        config.Input.KeyboardShortcut.Key, config.Input.KeyboardShortcut.Modifiers);

                    var registered = await _systemService.RegisterGlobalHotkeyAsync(config.Input.KeyboardShortcut);
                    Log.Information("ApplicationInitializationService: хоткей зарегистрирован: {Registered}", registered);
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
            if (string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                Log.Information("ApplicationInitializationService: новая установка, применяем дефолтные настройки");

                // Устанавливаем дефолтную модель Whisper
                config.SpeechRecognition.EngineSettings["ModelSize"] = "tiny";

                // Сохраняем обновленный конфиг
                await _configurationService.SaveConfigAsync(config);
                Log.Information("ApplicationInitializationService: дефолтная модель Whisper установлена: tiny");
            }
        }

        private async Task InitializeSpeechRecognitionAsync(AppConfig config)
        {
            Log.Information("ApplicationInitializationService: инициализируем Whisper модуль");
            var speechInitialized = await _speechService.InitializeAsync(config.SpeechRecognition);
            Log.Information("ApplicationInitializationService: сервис распознавания речи инициализирован: {Success}", speechInitialized);
        }

        private async Task InitializeAudioAsync(AppConfig config)
        {
            Log.Debug("ApplicationInitializationService: применяем аудио настройки");

            if (!string.IsNullOrEmpty(config.Audio.SelectedDeviceId))
            {
                await _audioService.SetActiveDeviceAsync(config.Audio.SelectedDeviceId);
                Log.Information("ApplicationInitializationService: активное аудио устройство установлено: {DeviceId}",
                    config.Audio.SelectedDeviceId);
            }
            else
            {
                Log.Warning("ApplicationInitializationService: в конфигурации не указано аудио устройство");
            }
        }

        private async Task InitializeOverlayAsync(AppConfig config)
        {
            Log.Debug("ApplicationInitializationService: применяем конфигурацию к OverlayService");
            await _overlayService.ApplyConfigAsync(config.Overlay);
            Log.Information("ApplicationInitializationService: конфигурация OverlayService применена: Position={Position}, Opacity={Opacity}",
                config.Overlay.Position, config.Overlay.Opacity);
        }

        private async Task InitializeHotkeysAsync(AppConfig config)
        {
            await RegisterHotkeysAsync(config);
        }

        private async Task InitializeGamepadAsync()
        {
            Log.Debug("ApplicationInitializationService: инициализируем GamepadVoiceCoordinator");
            var gamepadInitialized = await _gamepadVoiceCoordinator.InitializeAsync();
            Log.Information("ApplicationInitializationService: геймпад инициализирован: {Initialized}", gamepadInitialized);
        }
    }
}