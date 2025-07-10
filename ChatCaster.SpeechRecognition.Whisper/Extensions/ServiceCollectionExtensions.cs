using ChatCaster.Core.Services.Audio;
using ChatCaster.SpeechRecognition.Whisper.Models;
using ChatCaster.SpeechRecognition.Whisper.Services;
using ChatCaster.SpeechRecognition.Whisper.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ChatCaster.SpeechRecognition.Whisper.Extensions;

/// <summary>
/// Расширения для регистрации Whisper сервисов в DI контейнере
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Добавляет Whisper речевой движок в DI контейнер
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <param name="configureOptions">Опциональная конфигурация</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperSpeechRecognition(
        this IServiceCollection services, 
        Action<WhisperConfig>? configureOptions = null)
    {
        // Регистрируем конфигурацию
        var config = new WhisperConfig();
        configureOptions?.Invoke(config);
        services.AddSingleton(config);

        // Регистрируем основные сервисы
        RegisterWhisperServices(services);

        return services;
    }

    /// <summary>
    /// Добавляет Whisper речевой движок с готовой конфигурацией
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <param name="config">Готовая конфигурация Whisper</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperSpeechRecognition(
        this IServiceCollection services,
        WhisperConfig config)
    {
        services.AddSingleton(config);
        RegisterWhisperServices(services);
        return services;
    }

    /// <summary>
    /// Добавляет Whisper как реализацию ISpeechRecognitionService
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <param name="configureOptions">Опциональная конфигурация</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperAsSpeechRecognition(
        this IServiceCollection services,
        Action<WhisperConfig>? configureOptions = null)
    {
        // Добавляем Whisper сервисы
        services.AddWhisperSpeechRecognition(configureOptions);

        // Регистрируем как основную реализацию ISpeechRecognitionService
        services.AddSingleton<ISpeechRecognitionService>(provider =>
            provider.GetRequiredService<WhisperSpeechRecognitionService>());

        return services;
    }

    /// <summary>
    /// Добавляет только утилитарные сервисы Whisper (без основного движка)
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperUtilities(this IServiceCollection services)
    {
        services.TryAddSingleton<AudioConverter>();
        services.TryAddSingleton<ModelDownloader>();
        return services;
    }

    /// <summary>
    /// Добавляет Whisper с автоматическим скачиванием моделей
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <param name="modelSize">Размер модели для скачивания</param>
    /// <param name="configureOptions">Опциональная конфигурация</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperWithAutoDownload(
        this IServiceCollection services,
        string modelSize = "base",
        Action<WhisperConfig>? configureOptions = null)
    {
        services.AddWhisperSpeechRecognition(config =>
        {
            config.ModelSize = modelSize;
            configureOptions?.Invoke(config);
        });

        return services;
    }

    /// <summary>
    /// Проверяет и валидирует регистрацию Whisper сервисов
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>true если все сервисы зарегистрированы корректно</returns>
    public static bool ValidateWhisperRegistration(this IServiceCollection services)
    {
        // Простая проверка - есть ли нужные дескрипторы сервисов
        var hasConfig = services.Any(s => s.ServiceType == typeof(WhisperConfig));
        var hasAudioConverter = services.Any(s => s.ServiceType == typeof(AudioConverter));
        var hasModelDownloader = services.Any(s => s.ServiceType == typeof(ModelDownloader));
        var hasSpeechService = services.Any(s => s.ServiceType == typeof(WhisperSpeechRecognitionService));
    
        return hasConfig && hasAudioConverter && hasModelDownloader && hasSpeechService;
    }
    
    /// <summary>
    /// Внутренний метод для регистрации всех Whisper сервисов
    /// </summary>
    private static void RegisterWhisperServices(IServiceCollection services)
    {
        // Основные сервисы
        services.TryAddSingleton<WhisperSpeechRecognitionService>();
        services.TryAddSingleton<WhisperModelManager>();
        services.TryAddSingleton<WhisperAudioProcessor>();

        // Утилитарные сервисы
        services.TryAddSingleton<AudioConverter>();
        services.TryAddSingleton<ModelDownloader>();
        
        // HttpClient для ModelDownloader
        services.TryAddSingleton<HttpClient>();

        // Убеждаемся что есть логгер
        services.TryAddSingleton(typeof(ILogger<>), typeof(Logger<>));
    }

    /// <summary>
    /// Конфигурирует Whisper для разработки (отладочные настройки)
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperForDevelopment(this IServiceCollection services)
    {
        return services.AddWhisperSpeechRecognition(config =>
        {
            config.ModelSize = "tiny";           // Быстрая модель для разработки
            config.ThreadCount = 2;              // Меньше нагрузки на CPU
            config.EnableGpu = false;            // Отключаем GPU для совместимости
            config.RecognitionTimeoutSeconds = 30; // Короткий таймаут
            config.UseVAD = true;                // Включаем VAD для лучшей отзывчивости
        });
    }

    /// <summary>
    /// Конфигурирует Whisper для продакшена (оптимизированные настройки)
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperForProduction(this IServiceCollection services)
    {
        return services.AddWhisperSpeechRecognition(config =>
        {
            config.ModelSize = "base";           // Сбалансированная модель
            config.ThreadCount = Environment.ProcessorCount; // Используем все ядра
            config.EnableGpu = true;             // Включаем GPU если доступно
            config.RecognitionTimeoutSeconds = 60; // Больший таймаут
            config.UseVAD = true;                // VAD для качества
            config.InitializationTimeoutSeconds = 60; // Больше времени на инициализацию
        });
    }

    /// <summary>
    /// Конфигурирует Whisper для высокого качества (медленно, но точно)
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperForHighQuality(this IServiceCollection services)
    {
        return services.AddWhisperSpeechRecognition(config =>
        {
            config.ModelSize = "large-v3";       // Лучшая модель
            config.ThreadCount = Environment.ProcessorCount;
            config.EnableGpu = true;
            config.Temperature = 0.0f;           // Детерминистичность
            config.MaxTokens = 500;              // Больше токенов
            config.RecognitionTimeoutSeconds = 120; // Больший таймаут
            config.UseVAD = true;
        });
    }

    /// <summary>
    /// Конфигурирует Whisper для быстрого распознавания (быстро, но менее точно)
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>IServiceCollection для цепочки вызовов</returns>
    public static IServiceCollection AddWhisperForSpeed(this IServiceCollection services)
    {
        return services.AddWhisperSpeechRecognition(config =>
        {
            config.ModelSize = "tiny";           // Самая быстрая модель
            config.ThreadCount = Math.Max(2, Environment.ProcessorCount / 2);
            config.EnableGpu = true;             // GPU для скорости
            config.MaxTokens = 100;              // Меньше токенов
            config.RecognitionTimeoutSeconds = 15; // Короткий таймаут
            config.UseVAD = true;
        });
    }
}