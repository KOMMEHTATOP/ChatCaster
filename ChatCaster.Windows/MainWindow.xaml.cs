using System.ComponentModel;
using NHotkey;
using NHotkey.Wpf;
using System.Drawing;
using System.Windows;
using System.Windows.Media; 
using System.Windows.Forms;
using ChatCaster.Core.Models;
using ChatCaster.Core.Events;
using ChatCaster.Windows.Services;
using ChatCaster.Windows.Views;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfKey = System.Windows.Input.Key;
using WpfModifierKeys = System.Windows.Input.ModifierKeys;

namespace ChatCaster.Windows;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly AudioCaptureService _audioService;
    private readonly SpeechRecognitionService _speechService;
    private readonly GamepadService _gamepadService;
    private readonly SystemIntegrationService _systemService;
    private readonly OverlayService _overlayService;
    private readonly ServiceContext _serviceContext;

    private readonly ConfigurationService _configService;

    private readonly string _normalIconPath = "Resources/mic_normal.ico";
    private readonly List<byte[]> _audioBuffer = new();
    private System.Threading.Timer? _recordingTimer;

    private bool _isRecording;

    // Конфигурация приложения
    private AppConfig? _currentConfig;

    // System Tray
    private NotifyIcon? _notifyIcon;
    private bool _isClosingToTray = true;
    private bool _hasShownTrayNotification = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        _audioService = new AudioCaptureService();
        _speechService = new SpeechRecognitionService();
        _gamepadService = new GamepadService();
        _systemService = new SystemIntegrationService();
        _overlayService = new OverlayService();
        _configService = new ConfigurationService();
    
        _serviceContext = new ServiceContext(_currentConfig)
        {
            GamepadService = _gamepadService,
            AudioService = _audioService,
            SpeechService = _speechService,
            SystemService = _systemService,
            OverlayService = _overlayService
        };

        
        // Подписываемся на события геймпада
        _gamepadService.GamepadConnected += OnGamepadConnected;
        _gamepadService.GamepadDisconnected += OnGamepadDisconnected;
        _gamepadService.ShortcutPressed += OnGamepadShortcutPressed;

        // Подписываемся на события системы
        _systemService.GlobalHotkeyPressed += OnGlobalHotkeyPressed;

        // Инициализируем System Tray
        InitializeSystemTray();
        InitializeHotKeys();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void InitializeSystemTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon(_normalIconPath), Text = "ChatCaster - Готов к работе", Visible = true
        };

        // Двойной клик - показать главное окно
        _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();

        // Контекстное меню
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("📋 Показать окно", null, (s, e) => ShowMainWindow());
        contextMenu.Items.Add("⚙️ Настройки", null, (s, e) => ShowSettings());
        contextMenu.Items.Add("-"); // Разделитель
        contextMenu.Items.Add("🎮 Тест геймпада", null, (s, e) => TestGamepad());
        contextMenu.Items.Add("🔊 Тест микрофона", null, (s, e) => TestMicrophone());
        contextMenu.Items.Add("-"); // Разделитель
        contextMenu.Items.Add("ℹ️ О программе", null, (s, e) => AboutButton_Click(null!, null!));
        contextMenu.Items.Add("❌ Выход", null, (s, e) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try // Добавляем try блок
        {
            System.Diagnostics.Debug.WriteLine("[MAIN] Загружаем конфигурацию...");
            _currentConfig = await _configService.LoadConfigAsync(); // Теперь _currentConfig не null здесь

            // Обновляем ServiceContext с загруженным конфигом
            _serviceContext.Config = _currentConfig;

            System.Diagnostics.Debug.WriteLine($"[MAIN] Конфигурация загружена. AllowCompleteExit = {_currentConfig.System.AllowCompleteExit}");

            await InitializeAsync();

            // Сворачиваем в трей при запуске только если включена соответствующая настройка
            if (_currentConfig.System.StartMinimized)
            {
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Hide();
            }
            // По умолчанию показываем главное окно
        }
        catch (Exception ex) // Добавляем catch блок для обработки всех исключений
        {
            // Здесь обрабатываем ошибку. Например, можно вывести сообщение пользователю,
            // записать в лог, или показать статус ошибки.
            UpdateStatus($"Критическая ошибка при загрузке: {ex.Message}", WpfColors.Red);
            UpdateTrayStatus("Ошибка загрузки");
            MessageBox.Show($"Произошла критическая ошибка при запуске приложения: {ex.Message}\nПриложение будет закрыто.",
                "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown(); // Закрываем приложение при критической ошибке
        }
    }
    private async Task InitializeAsync()
    {
        try
        {
            // Убеждаемся, что _currentConfig не null.
            // Если LoadConfigAsync мог вернуть null, то здесь создаем дефолтный.
            // Или можно вывести ошибку и не продолжать инициализацию.
            if (_currentConfig == null)
            {
                _currentConfig = new AppConfig(); // Или другую логику обработки, например, return;
                System.Diagnostics.Debug.WriteLine("[MAIN] _currentConfig был null, инициализирован новым AppConfig.");
            }

            // Применяем конфигурацию overlay
            await _overlayService.ApplyConfigAsync(_currentConfig.Overlay);

            // Загружаем информацию о выбранном устройстве (но не показываем выбор)
            await UpdateSelectedDevice();

            // Инициализируем Whisper с настройками из конфигурации
            await _speechService.InitializeAsync(_currentConfig.Whisper);

            // Запускаем мониторинг геймпада
            if (_currentConfig.Input.EnableGamepadControl)
            {
                await _gamepadService.StartMonitoringAsync(_currentConfig.Input);
                Console.WriteLine("Мониторинг геймпада запущен");
            }

            // Регистрируем глобальные хоткеи если включены
            if (_currentConfig.Input.EnableKeyboardControl && _currentConfig.Input.KeyboardShortcut != null)
            {
                await _systemService.RegisterGlobalHotkeyAsync(_currentConfig.Input.KeyboardShortcut);
                Console.WriteLine("Глобальные хоткеи зарегистрированы");
            }

            UpdateStatus("Готов к записи", WpfColors.LimeGreen);
            UpdateTrayStatus("Готов к записи");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка инициализации: {ex.Message}", WpfColors.Red);
            UpdateTrayStatus("Ошибка инициализации");
        }
    }

    private async Task UpdateSelectedDevice()
    {
        try
        {
            var devices = await _audioService.GetAvailableDevicesAsync();
            var selectedDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
            
            if (selectedDevice != null)
            {
                DeviceText.Text = $"Устройство: {selectedDevice.Name}";
            }
            else
            {
                DeviceText.Text = "Устройство: Не найдено";
            }
        }
        catch (Exception ex)
        {
            DeviceText.Text = "Устройство: Ошибка загрузки";
            Console.WriteLine($"Ошибка загрузки устройств: {ex.Message}");
        }
    }

    private void InitializeHotKeys()
    {
        try
        {
            // Используем алиасы для WPF типов
            HotkeyManager.Current.AddOrReplace("Record", WpfKey.R, WpfModifierKeys.Control | WpfModifierKeys.Shift,
                OnRecordHotkey);

            UpdateTrayStatus("Готов к записи (Ctrl+Shift+R)");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка регистрации горячих клавиш: {ex.Message}", WpfColors.Orange);
        }
    }

    // Обработчики событий геймпада
    private void OnGamepadConnected(object? sender, GamepadConnectedEvent e)
    {
        Console.WriteLine($"Геймпад подключен: {e.GamepadInfo.Name}");
        UpdateTrayStatus($"Геймпад подключен: {e.GamepadInfo.Name}");

        // Показываем уведомление о подключении
        _notifyIcon?.ShowBalloonTip(2000, "ChatCaster",
            $"Геймпад подключен: {e.GamepadInfo.Name}", ToolTipIcon.Info);
    }

    private void OnGamepadDisconnected(object? sender, GamepadDisconnectedEvent e)
    {
        Console.WriteLine($"Геймпад отключен: индекс {e.GamepadIndex}");
        UpdateTrayStatus("Геймпад отключен");

        // Показываем уведомление об отключении
        _notifyIcon?.ShowBalloonTip(2000, "ChatCaster",
            "Геймпад отключен", ToolTipIcon.Warning);
    }

    private async void OnGamepadShortcutPressed(object? sender, GamepadShortcutPressedEvent e)
    {
        try
        {
            Console.WriteLine("Нажата комбинация на геймпаде: " + e.Shortcut.PrimaryButton + "+" + e.Shortcut.SecondaryButton);
        
            // ВАЖНО: Переключаемся на UI поток для работы с WPF элементами
            await Dispatcher.InvokeAsync(async () =>
            {
                await ToggleRecording();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки геймпада: {ex.Message}");
        }
    }
    
    
    private async void OnGlobalHotkeyPressed(object? sender, KeyboardShortcut shortcut)
    {
        System.Diagnostics.Debug.WriteLine($"[MAIN] ⭐ СРАБОТАЛ глобальный хоткей: {shortcut.Modifiers}+{shortcut.Key}");
        Console.WriteLine($"Нажата глобальная комбинация: {shortcut.Modifiers}+{shortcut.Key}");
        await ToggleRecording();
    }

    private async void OnRecordHotkey(object? sender, HotkeyEventArgs e)
    {
        if (e.Name == "Record")
        {
            await ToggleRecording();
            e.Handled = true;
        }
    }

    private readonly object _recordingLock = new object();
    private bool _isProcessingToggle = false;

    private async Task ToggleRecording()
    {
        // Защита от race condition
        lock (_recordingLock)
        {
            if (_isProcessingToggle)
            {
                Console.WriteLine("ToggleRecording уже обрабатывается, игнорируем...");
                return;
            }
            _isProcessingToggle = true;
        }

        try
        {
            if (!_isRecording)
            {
                await StartRecording();
            }
            else
            {
                await StopRecording();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка переключения записи: {ex.Message}");
            UpdateStatus("Ошибка", WpfColors.Red);
            UpdateTrayStatus("Ошибка");
        }
        finally
        {
            lock (_recordingLock)
            {
                _isProcessingToggle = false;
            }
        }
    }    
    
    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        await ToggleRecording();
    }

private async Task StartRecording()
    {
        Console.WriteLine($"[ДИАГНОСТИКА] StartRecording вызван, _isRecording = {_isRecording}");

        // УСИЛЕННАЯ защита от двойного запуска
        if (_isRecording)
        {
            Console.WriteLine("[ДИАГНОСТИКА] Запись уже идет, игнорируем StartRecording");
            return;
        }

        // Показываем overlay
        if (_currentConfig.Overlay.IsEnabled)
        {
            await _overlayService.ShowAsync(RecordingStatus.Recording);
        }

        try
        {
            // Атомарно устанавливаем флаг СРАЗУ
            _isRecording = true;
            _audioBuffer.Clear();

            // Отписываемся на случай если уже подписаны
            _audioService.AudioDataReceived -= OnAudioDataReceived;
            // Подписываемся на события аудио
            _audioService.AudioDataReceived += OnAudioDataReceived;

            // Обновляем UI
            RecordButton.Content = "⏹️ Остановить";
            RecordButton.Background = new SolidColorBrush(WpfColors.Crimson);
            UpdateStatus("Запись...", WpfColors.Orange);
            UpdateTrayStatus("Запись голоса...");

            // Запускаем захват аудио с дополнительной проверкой
            if (!_audioService.IsCapturing)
            {
                await _audioService.StartCaptureAsync(_currentConfig.Audio);
            }
            else
            {
                Console.WriteLine("[ДИАГНОСТИКА] Аудио сервис уже захватывает, используем существующий");
            }

            // Автоматическая остановка через таймер (работает в трее!)
            _recordingTimer?.Dispose();
            _recordingTimer = new System.Threading.Timer(async _ =>
            {
                if (_isRecording)
                {
                    try
                    {
                        await StopRecording();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка автостопа: {ex.Message}");
                    }
                }
            }, null, _currentConfig.Audio.MaxRecordingSeconds * 1000, Timeout.Infinite);
        }
        catch (Exception ex)
        {
            // Сбрасываем состояние при ошибке
            _isRecording = false;
            _audioService.AudioDataReceived -= OnAudioDataReceived;

            // Останавливаем таймер
            _recordingTimer?.Dispose();
            _recordingTimer = null;

            RecordButton.Content = "🎙️ Записать";
            RecordButton.Background = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212));

            UpdateStatus($"Ошибка записи: {ex.Message}", WpfColors.Red);
            UpdateTrayStatus("Ошибка записи");

            // Скрываем overlay при ошибке
            if (_currentConfig.Overlay.IsEnabled)
            {
                await _overlayService.HideAsync();
            }
        }
    }

    private void OnAudioDataReceived(object? sender, byte[] audioData)
    {
        // Только если еще записываем
        if (_isRecording)
        {
            Console.WriteLine($"Получен аудио блок: {audioData.Length} байт, всего в буфере: {_audioBuffer.Count} блоков");
            _audioBuffer.Add(audioData);
        }
    }

private async Task StopRecording()
{
    // Защита от повторного вызова
    if (!_isRecording) return;

    // Обновляем overlay на статус обработки
    if (_currentConfig.Overlay.IsEnabled)
    {
        await _overlayService.UpdateStatusAsync(RecordingStatus.Processing);
    }

    try
    {
        // Сразу сбрасываем флаг записи
        _isRecording = false;

        // Останавливаем захват аудио
        await _audioService.StopCaptureAsync();

        // Отписываемся от событий
        _audioService.AudioDataReceived -= OnAudioDataReceived;

        // Обновляем статус
        UpdateStatus("Обработка...", WpfColors.Yellow);
        UpdateTrayStatus("Обработка...");

        // Объединяем все аудио данные
        var allAudioData = _audioBuffer.SelectMany(x => x).ToArray();
        Console.WriteLine($"Собрано аудио данных: {allAudioData.Length} байт");

        // Отправляем на распознавание
        var result = await _speechService.RecognizeAsync(allAudioData);
        
        // ДИАГНОСТИКА: Выводим результат в консоль
        Console.WriteLine($"РЕЗУЛЬТАТ РАСПОЗНАВАНИЯ: Success={result.Success}, Text='{result.RecognizedText}', Error='{result.ErrorMessage}'");

        if (result.Success && !string.IsNullOrWhiteSpace(result.RecognizedText))
        {
            RecognitionResultText.Text = result.RecognizedText;
            RecognitionResultText.Foreground = new SolidColorBrush(WpfColors.White);

            ConfidenceText.Text = $"Уверенность: {result.Confidence:P1}";
            ProcessingTimeText.Text = $"Время: {result.ProcessingTime.TotalMilliseconds:F0} мс";

            Console.WriteLine($"Автоматически вводим текст: '{result.RecognizedText}'");

            // Небольшая задержка перед вводом для стабильности
            await Task.Delay(100);

            // Отправляем текст в активное окно
            bool textSent = await _systemService.SendTextAsync(result.RecognizedText);

            // Всегда показываем успех, даже если не смогли ввести текст (например, в собственное окно)
            UpdateStatus("Готов к записи", WpfColors.LimeGreen);
            UpdateTrayStatus("Готов к записи");

            // Показываем уведомление об успехе
            if (_currentConfig.System.ShowNotifications)
            {
                _notifyIcon?.ShowBalloonTip(2000, "ChatCaster",
                    $"Текст введен: {result.RecognizedText}", ToolTipIcon.Info);
            }

            // Показываем успех в overlay и скрываем
            if (_currentConfig.Overlay.IsEnabled)
            {
                await _overlayService.UpdateStatusAsync(RecordingStatus.Completed);
                await Task.Delay(2000);
                await _overlayService.HideAsync();
            }
        }
        else
        {
            // Обрабатываем случай когда Success=true но текст пустой ИЛИ Success=false
            string errorMessage = !result.Success 
                ? result.ErrorMessage ?? "Неизвестная ошибка"
                : "Не удалось распознать речь (пустой результат)";
                
            RecognitionResultText.Text = $"Ошибка: {errorMessage}";
            RecognitionResultText.Foreground = new SolidColorBrush(WpfColors.Crimson);
            UpdateStatus("Готов к записи", WpfColors.LimeGreen);
            UpdateTrayStatus("Готов к записи");

            // Показываем ошибку в overlay
            if (_currentConfig.Overlay.IsEnabled)
            {
                await _overlayService.UpdateStatusAsync(RecordingStatus.Error, "Ошибка распознавания");
                await Task.Delay(2000);
                await _overlayService.HideAsync();
            }
        }
    }
    catch (Exception ex)
    {
        UpdateStatus($"Ошибка обработки: {ex.Message}", WpfColors.Red);
        UpdateTrayStatus("Ошибка обработки");

        // Показываем ошибку в overlay
        if (_currentConfig.Overlay.IsEnabled)
        {
            await _overlayService.UpdateStatusAsync(RecordingStatus.Error, "Ошибка обработки");
            await Task.Delay(2000);
            await _overlayService.HideAsync();
        }
    }
    finally
    {
        // Гарантированно сбрасываем состояние UI
        _isRecording = false;

        // Останавливаем таймер
        _recordingTimer?.Dispose();
        _recordingTimer = null;

        RecordButton.Content = "🎙️ Записать";
        RecordButton.Background = new SolidColorBrush(WpfColor.FromRgb(0, 120, 212));

        // Принудительно обновляем иконку трея
        UpdateTrayStatus("Готов к записи");
    }
}

    private void UpdateStatus(string message, WpfColor color)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(color);
    }

    private void UpdateTrayStatus(string status)
    {
        try
        {
            if (_notifyIcon != null)
            {
                // Ограничиваем длину до 120 символов
                string trayText = status.Length > 120 ? status.Substring(0, 117) + "..." : status;
                _notifyIcon.Text = trayText;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления статуса трея: {ex.Message}");
        }
    }    
    
    // Методы управления окнами
    private void ShowMainWindow()
    {
        try
        {
            // Проверяем, что окно не закрывается
            if (_notifyIcon == null || !this.IsLoaded)
                return;

            this.Show();
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
            this.Focus();
        }
        catch (InvalidOperationException)
        {
            // Окно уже закрывается, игнорируем
        }
    }

    private async void ShowSettings()
    {
        try
        {
            ShowMainWindow(); // Сначала показываем главное окно
            
            var settingsWindow = new SettingsWindow(_serviceContext);
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsChanged)
            {
                _currentConfig = settingsWindow.Config;
                _serviceContext.Config = _currentConfig; // Обновляем контекст
                await ApplyNewSettings();
                UpdateStatus("Настройки применены", WpfColors.LimeGreen);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка открытия настроек: {ex.Message}", WpfColors.Red);
            MessageBox.Show($"Ошибка открытия настроек: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TestGamepad()
    {
        try
        {
            var gamepads = await _gamepadService.GetConnectedGamepadsAsync();
            var gamepadCount = gamepads.Count();

            if (gamepadCount > 0)
            {
                var gamepadNames = string.Join("\n", gamepads.Select(g => $"- {g.Name}"));
                MessageBox.Show($"Найдено геймпадов: {gamepadCount}\n\n{gamepadNames}",
                    "Тест геймпада", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Геймпады не найдены.\nПроверьте подключение Xbox контроллера.",
                    "Тест геймпада", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка тестирования геймпада: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TestMicrophone()
    {
        try
        {
            bool micTest = await _audioService.TestMicrophoneAsync();

            if (micTest)
            {
                MessageBox.Show("Микрофон работает корректно!",
                    "Тест микрофона", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Проблемы с микрофоном.\nПроверьте настройки аудио.",
                    "Тест микрофона", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка тестирования микрофона: {ex.Message}",
                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ExitApplication()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[TRAY] ExitApplication: Начало принудительного закрытия");
        
            // Устанавливаем флаг принудительного закрытия СРАЗУ
            _isClosingToTray = false;
        
            // Скрываем и отключаем tray icon ПЕРВЫМ ДЕЛОМ
            if (_notifyIcon != null)
            {
                System.Diagnostics.Debug.WriteLine("[TRAY] Отключаем NotifyIcon");
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            // Асинхронно останавливаем сервисы БЕЗ Wait()
            System.Diagnostics.Debug.WriteLine("[TRAY] Останавливаем сервисы асинхронно");
        
            // Создаем задачи остановки, но НЕ ждем их завершения
            var stopTasks = new List<Task>();
        
            if (_gamepadService != null)
                stopTasks.Add(_gamepadService.StopMonitoringAsync());
            
            if (_systemService != null)
                stopTasks.Add(_systemService.UnregisterGlobalHotkeyAsync());
        
            // Dispose сервисов синхронно
            _overlayService?.Dispose();
        
            // Даем задачам максимум 1 секунду на завершение
            var timeoutTask = Task.Delay(1000);
            var allStopTasks = Task.WhenAll(stopTasks);
        
            await Task.WhenAny(allStopTasks, timeoutTask);
        
            System.Diagnostics.Debug.WriteLine("[TRAY] Вызываем Application.Shutdown");
        
            // Принудительно закрываем приложение
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TRAY] КРИТИЧЕСКАЯ ОШИБКА в ExitApplication: {ex.Message}");
        
            // Если что-то пошло не так, принудительно завершаем процесс
            try
            {
                Environment.Exit(0);
            }
            catch
            {
                // Последний рубеж - убиваем процесс
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
        }
    }
    
    // Обработчики событий UI
    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsWindow = new SettingsWindow(_serviceContext);
            settingsWindow.Owner = this;

            if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsChanged)
            {
                _currentConfig = settingsWindow.Config;
                _serviceContext.Config = _currentConfig; // Обновляем контекст
                await ApplyNewSettings();
                UpdateStatus("Настройки применены", WpfColors.LimeGreen);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка открытия настроек: {ex.Message}", WpfColors.Red);
            MessageBox.Show($"Ошибка открытия настроек: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ApplyNewSettings()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MAIN] Применяем новые настройки...");
        
            // Сохраняем конфигурацию
            await _configService.SaveConfigAsync(_currentConfig);
            System.Diagnostics.Debug.WriteLine("[MAIN] Конфигурация сохранена на диск");

            // Применяем настройки Whisper
            await _speechService.InitializeAsync(_currentConfig.Whisper);

            // Применяем настройки overlay
            await _overlayService.ApplyConfigAsync(_currentConfig.Overlay);
            
            // Обновляем информацию о выбранном устройстве
            await UpdateSelectedDevice();
            
            // Применяем настройки горячих клавиш
            await _systemService.UnregisterGlobalHotkeyAsync();
            
            if (_currentConfig.Input.EnableKeyboardControl && _currentConfig.Input.KeyboardShortcut != null)
            {
                await _systemService.RegisterGlobalHotkeyAsync(_currentConfig.Input.KeyboardShortcut);
                System.Diagnostics.Debug.WriteLine($"[MAIN] Зарегистрирован новый хоткей: {_currentConfig.Input.KeyboardShortcut.Modifiers}+{_currentConfig.Input.KeyboardShortcut.Key}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MAIN] Хоткей НЕ регистрируется: EnableKeyboardControl={_currentConfig.Input.EnableKeyboardControl}, KeyboardShortcut={_currentConfig.Input.KeyboardShortcut}");
            }

            // Перезапускаем мониторинг геймпада с новыми настройками
            await _gamepadService.StopMonitoringAsync();

            if (_currentConfig.Input.EnableGamepadControl)
            {
                await _gamepadService.StartMonitoringAsync(_currentConfig.Input);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Ошибка применения настроек: {ex.Message}", WpfColors.Red);
        }
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("ChatCaster v1.0.0\n\nГолосовой ввод для игр с поддержкой геймпада\n\n" +
                        "Технологии: WPF, NAudio, Whisper.net, XInput\n\n" +
                        "Управление:\n" +
                        "• Геймпад: LB + RB (настраивается)\n" +
                        "• Клавиатура: Ctrl+Shift+R",
            "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Обработка закрытия окна
    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[CLOSING] AllowCompleteExit = {_currentConfig.System.AllowCompleteExit}");
        System.Diagnostics.Debug.WriteLine($"[CLOSING] _isClosingToTray = {_isClosingToTray}");
        
        if (!_currentConfig.System.AllowCompleteExit)
        {
            System.Diagnostics.Debug.WriteLine("[CLOSING] Сворачиваем в трей");
            e.Cancel = true;
            this.Hide();
            this.ShowInTaskbar = false;

            // Показываем уведомление только в первый раз
            if (!_hasShownTrayNotification && _currentConfig.System.ShowNotifications)
            {
                _notifyIcon?.ShowBalloonTip(3000, "ChatCaster",
                    "Приложение свернуто в системный трей. Двойной клик для возврата.",
                    ToolTipIcon.Info);
                _hasShownTrayNotification = true;
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[CLOSING] Разрешаем полное закрытие");
            
            // ВАЖНО: При полном закрытии убираем NotifyIcon
            try
            {
                if (_notifyIcon != null)
                {
                    System.Diagnostics.Debug.WriteLine("[CLOSING] Удаляем NotifyIcon при полном закрытии");
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
                
                // Останавливаем сервисы асинхронно БЕЗ ожидания
                System.Diagnostics.Debug.WriteLine("[CLOSING] Останавливаем сервисы");
                
                // Создаем задачи остановки, но НЕ ждем их
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _gamepadService?.StopMonitoringAsync();
                        await _systemService?.UnregisterGlobalHotkeyAsync();
                        _overlayService?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CLOSING] Ошибка остановки сервисов: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CLOSING] Ошибка при закрытии: {ex.Message}");
            }
            
            // Позволяем окну закрыться
            // e.Cancel остается false
        }
    }
}