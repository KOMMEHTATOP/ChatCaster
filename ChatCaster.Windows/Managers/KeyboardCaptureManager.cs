using ChatCaster.Core.Models;
using ChatCaster.Windows.Converters;
using ChatCaster.Windows.Interfaces;
using ChatCaster.Windows.Utilities;
using NHotkey;
using NHotkey.Wpf;
using Serilog;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace ChatCaster.Windows.Managers
{
    /// <summary>
    /// Менеджер захвата клавиатурных комбинаций с упрощенной логикой
    /// </summary>
    public sealed class KeyboardCaptureManager : ICaptureManager<KeyboardShortcut>
    {
        private readonly static ILogger _logger = Log.ForContext<KeyboardCaptureManager>();

        private static Dictionary<string, (Key key, ModifierKeys modifiers)>? _hotkeyLookup;
        
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

        // Ограниченный набор клавиш для захвата (вместо 100+ хоткеев)
        private readonly static Key[] CaptureKeys = 
        {
            // Функциональные клавиши (наиболее популярные для хоткеев)
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6, 
            Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
            
            // Numpad (часто используется в играх)
            Key.NumPad0, Key.NumPad1, Key.NumPad2, Key.NumPad3, Key.NumPad4, 
            Key.NumPad5, Key.NumPad6, Key.NumPad7, Key.NumPad8, Key.NumPad9,
            
            // Навигационные клавиши
            Key.Insert, Key.Delete, Key.Home, Key.End, Key.PageUp, Key.PageDown,
            
            // Стрелки
            Key.Up, Key.Down, Key.Left, Key.Right,
            
            // Специальные клавиши для удобства тестирования
            Key.Space, Key.Tab, Key.Escape,
            
            // Буквы для отладки (можно убрать после тестирования)
            Key.R, Key.Q, Key.W, Key.E, Key.V
        };

        private readonly static ModifierKeys[] CaptureModifiers = 
        {
            ModifierKeys.Control,
            ModifierKeys.Shift,
            ModifierKeys.Alt,
            ModifierKeys.Control | ModifierKeys.Shift,
            ModifierKeys.Control | ModifierKeys.Alt,
            ModifierKeys.Shift | ModifierKeys.Alt
        };

        #endregion

        #region Constructor

        public KeyboardCaptureManager()
        {
            _captureTimer = new InputCaptureTimer();
            _registeredHotkeys = new HashSet<string>();
            
            // Подписываемся на события таймера
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
                // Регистрируем ограниченный набор хоткеев
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

            // Создаем обработчик события
            EventHandler<HotkeyEventArgs> hotkeyHandler = OnHotkeyPressed;

            // Регистрируем только осмысленные комбинации
            foreach (var modifier in CaptureModifiers)
            {
                foreach (var key in CaptureKeys)
                {
                    try
                    {
                        var hotkeyName = $"KeyCapture_{hotkeyIndex++}";
                        
                        HotkeyManager.Current.AddOrReplace(
                            hotkeyName, 
                            key, 
                            modifier, 
                            hotkeyHandler);
                        
                        _registeredHotkeys.Add(hotkeyName);
                        registeredCount++;
                        
                        // Отладка для первых нескольких регистраций
                        if (registeredCount <= 5)
                        {
                            _logger.Debug("Registered hotkey: {HotkeyName} = {Key} + {Modifier}", hotkeyName, key, modifier);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug("Failed to register hotkey {Key} + {Modifier}: {Error}", key, modifier, ex.Message);
                    }
                }
            }
            
            _logger.Debug("Total registered hotkeys: {Count}", registeredCount);
        }

        private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            _logger.Debug("OnHotkeyPressed: {Name}", e.Name);
            _logger.Debug("IsCapturing: {IsCapturing}, _isDisposed: {IsDisposed}", IsCapturing, _isDisposed);
            
            if (!IsCapturing || _isDisposed) 
            {
                _logger.Debug("Hotkey rejected - not capturing or disposed");
                return;
            }

            try
            {
                // Получаем информацию о нажатой комбинации из имени хоткея
                if (TryGetHotkeyInfo(e.Name, out var key, out var modifiers))
                {
                    _logger.Debug("Detected hotkey: {Key} + {Modifiers}", key, modifiers);
                    
                    var keyboardShortcut = WpfCoreConverter.CreateKeyboardShortcut(key, modifiers);
                    
                    if (keyboardShortcut != null)
                    {
                        _logger.Information("Captured keyboard shortcut: {Shortcut}", keyboardShortcut.DisplayText);
                        
                        // Останавливаем захват
                        _captureTimer.Stop();
                        ClearRegisteredHotkeys();
                        _logger.Debug("Capture stopped");
                        
                        StatusChanged?.Invoke($"Захвачена комбинация: {keyboardShortcut.DisplayText}");
                        CaptureCompleted?.Invoke(keyboardShortcut);
                    }
                    else
                    {
                        _logger.Warning("Failed to create shortcut for {Key} + {Modifiers}", key, modifiers);
                        CaptureError?.Invoke("Неподдерживаемая комбинация клавиш");
                    }
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

        private static Dictionary<string, (Key key, ModifierKeys modifiers)> GetHotkeyLookup()
        {
            if (_hotkeyLookup == null)
            {
                _hotkeyLookup = new Dictionary<string, (Key, ModifierKeys)>();
                int hotkeyIndex = 0;
        
                foreach (var modifier in CaptureModifiers)
                {
                    foreach (var captureKey in CaptureKeys)
                    {
                        var hotkeyName = $"KeyCapture_{hotkeyIndex++}";
                        _hotkeyLookup[hotkeyName] = (captureKey, modifier);
                    }
                }
            }
            return _hotkeyLookup;
        }

        private bool TryGetHotkeyInfo(string hotkeyName, out Key key, out ModifierKeys modifiers)
        {
            if (GetHotkeyLookup().TryGetValue(hotkeyName, out var result))
            {
                key = result.key;
                modifiers = result.modifiers;
                return true;
            }
    
            key = Key.None;
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