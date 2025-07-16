using ChatCaster.Core.Models;
using ChatCaster.Core.Utilities;
using ChatCaster.Windows.Converters;
using ChatCaster.Windows.Interfaces;
using ChatCaster.Windows.Utilities;
using NHotkey;
using NHotkey.Wpf;
using Serilog;
using CoreKey = ChatCaster.Core.Models.Key;
using CoreModifierKeys = ChatCaster.Core.Models.ModifierKeys;

namespace ChatCaster.Windows.Managers
{
    /// <summary>
    /// Менеджер захвата клавиатурных комбинаций с правильной архитектурой
    /// Использует Core-определения клавиш, только конвертирует их в WPF для регистрации
    /// </summary>
    public sealed class KeyboardCaptureManager : ICaptureManager<KeyboardShortcut>
    {
        private readonly static ILogger _logger = Log.ForContext<KeyboardCaptureManager>();
        private readonly AppConfig _currentConfig;

        private static Dictionary<string, (CoreKey key, CoreModifierKeys modifiers)>? _hotkeyLookup;

        #region Events

        /// <summary>
        /// Событие успешного захвата комбинации клавиатуры
        /// </summary>
        public event Action<KeyboardShortcut>? CaptureCompleted;

        /// <summary>
        /// Событие таймаута захвата
        /// </summary>
        public event Action? CaptureTimeout;

        /// <summary>
        /// Событие изменения статуса захвата
        /// </summary>
        public event Action<string>? StatusChanged;

        /// <summary>
        /// Событие ошибки захвата
        /// </summary>
        public event Action<string>? CaptureError;

        #endregion

        #region Private Fields

        private readonly InputCaptureTimer _captureTimer;
        private readonly HashSet<string> _registeredHotkeys;
        private bool _isDisposed;

        #endregion

        #region Constructor

        public KeyboardCaptureManager(AppConfig currentConfig)
        {
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
            _captureTimer = new InputCaptureTimer();
            _registeredHotkeys = new HashSet<string>();

            _captureTimer.TimerExpired += OnCaptureTimerExpired;
        }

        #endregion

        #region ICaptureManager Implementation

        /// <summary>
        /// Активен ли процесс захвата
        /// </summary>
        public bool IsCapturing => _captureTimer.IsRunning;

        /// <summary>
        /// Доступна ли клавиатура для захвата (всегда true)
        /// </summary>
        public bool IsAvailable => true;

        /// <summary>
        /// Начинает процесс захвата клавиатурной комбинации
        /// </summary>
        /// <param name="timeoutSeconds">Время ожидания в секундах</param>
        public async Task StartCaptureAsync(int timeoutSeconds)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(KeyboardCaptureManager));

            if (IsCapturing)
                throw new InvalidOperationException("Захват уже активен");

            try
            {
                // Регистрируем хоткеи используя Core-определения
                RegisterCaptureHotkeys();

                // Запускаем таймер
                _captureTimer.Start(timeoutSeconds);

                StatusChanged?.Invoke("Нажмите комбинацию клавиш...");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _captureTimer.Stop();
                ClearRegisteredHotkeys();
                CaptureError?.Invoke($"Ошибка начала захвата: {ex.Message}");
            }
        }

        /// <summary>
        /// Останавливает процесс захвата
        /// </summary>
        public void StopCapture()
        {
            if (_isDisposed) return;

            _captureTimer.Stop();
            ClearRegisteredHotkeys();

            StatusChanged?.Invoke("Захват остановлен");
        }

        #endregion

        #region Private Methods

        private void RegisterCaptureHotkeys()
        {
            ClearRegisteredHotkeys(); // Очищаем предыдущие регистрации

            int hotkeyIndex = 0;
            int registeredCount = 0;
            int skippedCount = 0;

            // Получаем текущую активную комбинацию для исключения
            var currentShortcut = _currentConfig.Input.KeyboardShortcut;

            EventHandler<HotkeyEventArgs> hotkeyHandler = OnHotkeyPressed;

            // ✅ ИСПОЛЬЗУЕМ CORE-ОПРЕДЕЛЕНИЯ вместо WPF массивов
            foreach (var coreModifiers in SupportedKeysProvider.AllSupportedModifiers)
            {
                foreach (var coreKey in SupportedKeysProvider.AllSupportedKeys)
                {
                    try
                    {
                        // Проверяем, не является ли это текущей активной комбинацией
                        if (currentShortcut != null &&
                            currentShortcut.Key == coreKey &&
                            currentShortcut.Modifiers == coreModifiers)
                        {
                            _logger.Debug("Пропускаем регистрацию текущей активной комбинации: {Key} + {Modifiers}", coreKey, coreModifiers);
                            hotkeyIndex++;
                            skippedCount++;
                            continue;
                        }

                        // ✅ КОНВЕРТИРУЕМ Core → WPF только для регистрации
                        var wpfKey = WpfCoreConverter.ConvertToWpf(coreKey);
                        var wpfModifiers = WpfCoreConverter.ConvertToWpf(coreModifiers);

                        if (wpfKey == null)
                        {
                            _logger.Debug("Невозможно сконвертировать Core клавишу в WPF: {CoreKey}", coreKey);
                            hotkeyIndex++;
                            continue;
                        }

                        var hotkeyName = $"KeyCapture_{hotkeyIndex++}";

                        // Регистрируем в WPF используя NHotkey
                        HotkeyManager.Current.AddOrReplace(
                            hotkeyName,
                            wpfKey.Value,
                            wpfModifiers,
                            hotkeyHandler);

                        _registeredHotkeys.Add(hotkeyName);
                        registeredCount++;

                        // Логируем только первые несколько для отладки
                        if (registeredCount <= 5)
                        {
                            _logger.Debug("Registered hotkey: {HotkeyName} = {CoreKey} + {CoreModifiers} → {WpfKey} + {WpfModifiers}", 
                                hotkeyName, coreKey, coreModifiers, wpfKey, wpfModifiers);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug("Failed to register hotkey {CoreKey} + {CoreModifiers}: {Error}", coreKey, coreModifiers, ex.Message);
                    }
                }
            }

            _logger.Information("Зарегистрировано хоткеев: {RegisteredCount}, пропущено: {SkippedCount} (всего возможных: {TotalCount}, текущая активная: {CurrentShortcut})",
                registeredCount, skippedCount, SupportedKeysProvider.GetTotalCombinationsCount(), currentShortcut?.DisplayText ?? "нет");
        }

        private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            _logger.Debug("OnHotkeyPressed: {Name}", e.Name);

            if (!IsCapturing || _isDisposed)
            {
                _logger.Debug("Hotkey rejected - not capturing or disposed");
                return;
            }

            try
            {
                if (TryGetHotkeyInfo(e.Name, out var coreKey, out var coreModifiers))
                {
                    _logger.Debug("Detected hotkey: {CoreKey} + {CoreModifiers}", coreKey, coreModifiers);

                    // ✅ СОЗДАЕМ KeyboardShortcut напрямую из Core типов
                    var keyboardShortcut = new KeyboardShortcut
                    {
                        Key = coreKey,
                        Modifiers = coreModifiers
                    };

                    // Проверка дубликата с текущей комбинацией
                    var currentShortcut = _currentConfig.Input.KeyboardShortcut;
                    _logger.Debug("🔍 Проверка дубликата:");
                    _logger.Debug("🔍 Нажата комбинация: {Key} + {Modifiers}", keyboardShortcut.Key, keyboardShortcut.Modifiers);
                    _logger.Debug("🔍 Текущая комбинация: {CurrentKey} + {CurrentModifiers}", currentShortcut?.Key, currentShortcut?.Modifiers);

                    if (currentShortcut != null &&
                        currentShortcut.Key == keyboardShortcut.Key &&
                        currentShortcut.Modifiers == keyboardShortcut.Modifiers)
                    {
                        _logger.Warning("🔍 ДУБЛИКАТ ОБНАРУЖЕН! Игнорируем захват");
                        CaptureError?.Invoke("Нажата текущая комбинация. Попробуйте другую.");
                        return;
                    }

                    // ✅ ВАЛИДАЦИЯ через Core утилиты
                    if (!SupportedKeysProvider.IsShortcutSupported(keyboardShortcut))
                    {
                        _logger.Warning("Неподдерживаемая комбинация: {Key} + {Modifiers}", keyboardShortcut.Key, keyboardShortcut.Modifiers);
                        CaptureError?.Invoke("Неподдерживаемая комбинация клавиш");
                        return;
                    }

                    _logger.Debug("🔍 Комбинация уникальна и поддерживается, продолжаем захват");
                    _logger.Information("Captured keyboard shortcut: {Shortcut}", keyboardShortcut.DisplayText);

                    // Останавливаем захват
                    _captureTimer.Stop();
                    ClearRegisteredHotkeys();

                    StatusChanged?.Invoke($"Захвачена комбинация: {keyboardShortcut.DisplayText}");
                    CaptureCompleted?.Invoke(keyboardShortcut);
                }
                else
                {
                    _logger.Warning("Failed to parse hotkey: {Name}", e.Name);
                    CaptureError?.Invoke("Ошибка распознавания комбинации");
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing hotkey");
                CaptureError?.Invoke($"Ошибка обработки клавиш: {ex.Message}");
                StopCapture();
            }
        }

        private static Dictionary<string, (CoreKey key, CoreModifierKeys modifiers)> GetHotkeyLookup()
        {
            if (_hotkeyLookup == null)
            {
                _hotkeyLookup = new Dictionary<string, (CoreKey, CoreModifierKeys)>();
                int hotkeyIndex = 0;

                // ✅ ИСПОЛЬЗУЕМ CORE-ОПРЕДЕЛЕНИЯ для lookup
                foreach (var coreModifiers in SupportedKeysProvider.AllSupportedModifiers)
                {
                    foreach (var coreKey in SupportedKeysProvider.AllSupportedKeys)
                    {
                        var hotkeyName = $"KeyCapture_{hotkeyIndex++}";
                        _hotkeyLookup[hotkeyName] = (coreKey, coreModifiers);
                    }
                }
            }

            return _hotkeyLookup;
        }

        private bool TryGetHotkeyInfo(string hotkeyName, out CoreKey key, out CoreModifierKeys modifiers)
        {
            if (GetHotkeyLookup().TryGetValue(hotkeyName, out var result))
            {
                key = result.key;
                modifiers = result.modifiers;
                return true;
            }

            key = default;
            modifiers = ModifierKeys.None;
            return false;
        }

        private void ClearRegisteredHotkeys()
        {
            foreach (var hotkeyName in _registeredHotkeys)
            {
                try
                {
                    HotkeyManager.Current.Remove(hotkeyName);
                }
                catch
                {
                    // Игнорируем ошибки при удалении
                }
            }

            _registeredHotkeys.Clear();
        }

        private void OnCaptureTimerExpired()
        {
            if (_isDisposed) return;

            ClearRegisteredHotkeys();
            CaptureTimeout?.Invoke();
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Освобождает ресурсы менеджера
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            // Останавливаем захват
            StopCapture();

            // Освобождаем таймер
            _captureTimer.TimerExpired -= OnCaptureTimerExpired;
            _captureTimer.Dispose();

            // Очищаем события
            CaptureCompleted = null;
            CaptureTimeout = null;
            StatusChanged = null;
            CaptureError = null;

            _isDisposed = true;
        }

        #endregion
    }
}