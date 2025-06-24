using ChatCaster.Core.Models;
using ChatCaster.Windows.Interfaces;
using ChatCaster.Windows.Utilities;
using NHotkey;
using NHotkey.Wpf;

namespace ChatCaster.Windows.Managers
{
    /// <summary>
    /// Менеджер захвата клавиатурных комбинаций с упрощенной логикой
    /// </summary>
    public sealed class KeyboardCaptureManager : ICaptureManager<KeyboardShortcut>
    {
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
        private static readonly System.Windows.Input.Key[] CaptureKeys = 
        {
            // Функциональные клавиши (наиболее популярные для хоткеев)
            System.Windows.Input.Key.F1, System.Windows.Input.Key.F2, System.Windows.Input.Key.F3, 
            System.Windows.Input.Key.F4, System.Windows.Input.Key.F5, System.Windows.Input.Key.F6, 
            System.Windows.Input.Key.F7, System.Windows.Input.Key.F8, System.Windows.Input.Key.F9, 
            System.Windows.Input.Key.F10, System.Windows.Input.Key.F11, System.Windows.Input.Key.F12,
            
            // Numpad (часто используется в играх)
            System.Windows.Input.Key.NumPad0, System.Windows.Input.Key.NumPad1, System.Windows.Input.Key.NumPad2, 
            System.Windows.Input.Key.NumPad3, System.Windows.Input.Key.NumPad4, System.Windows.Input.Key.NumPad5, 
            System.Windows.Input.Key.NumPad6, System.Windows.Input.Key.NumPad7, System.Windows.Input.Key.NumPad8, 
            System.Windows.Input.Key.NumPad9,
            
            // Навигационные клавиши
            System.Windows.Input.Key.Insert, System.Windows.Input.Key.Delete, 
            System.Windows.Input.Key.Home, System.Windows.Input.Key.End, 
            System.Windows.Input.Key.PageUp, System.Windows.Input.Key.PageDown,
            
            // Стрелки
            System.Windows.Input.Key.Up, System.Windows.Input.Key.Down, 
            System.Windows.Input.Key.Left, System.Windows.Input.Key.Right,
            
            // Специальные клавиши для удобства тестирования
            System.Windows.Input.Key.Space, System.Windows.Input.Key.Tab, System.Windows.Input.Key.Escape,
            
            // Буквы для отладки (можно убрать после тестирования)
            System.Windows.Input.Key.R, System.Windows.Input.Key.Q, 
            System.Windows.Input.Key.W, System.Windows.Input.Key.E, System.Windows.Input.Key.V
        };

        private static readonly System.Windows.Input.ModifierKeys[] CaptureModifiers = 
        {
            System.Windows.Input.ModifierKeys.Control,
            System.Windows.Input.ModifierKeys.Shift,
            System.Windows.Input.ModifierKeys.Alt,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift,
            System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt,
            System.Windows.Input.ModifierKeys.Shift | System.Windows.Input.ModifierKeys.Alt
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
                            System.Diagnostics.Debug.WriteLine($"[KeyboardCapture] Registered: {hotkeyName} = {key} + {modifier}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Логируем конфликты для отладки
                        System.Diagnostics.Debug.WriteLine($"[KeyboardCapture] Failed to register {key} + {modifier}: {ex.Message}");
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[KeyboardCapture] Total registered hotkeys: {registeredCount}");
        }

private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] OnHotkeyPressed: {e.Name}");
            System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] IsCapturing: {IsCapturing}, _isDisposed: {_isDisposed}");
            
            if (!IsCapturing || _isDisposed) 
            {
                System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] ОТКЛОНЕНО - не захватываем или disposed");
                return;
            }

            try
            {
                // Получаем информацию о нажатой комбинации из имени хоткея
                if (TryGetHotkeyInfo(e.Name, out var key, out var modifiers))
                {
                    System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] Detected: {key} + {modifiers}");
                    
                    var keyboardShortcut = WpfCoreConverter.CreateKeyboardShortcut(key, modifiers);
                    
                    if (keyboardShortcut != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] Created shortcut: {keyboardShortcut.DisplayText}");
                        
                        // Останавливаем захват
                        _captureTimer.Stop();
                        ClearRegisteredHotkeys();
                        System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] Захват остановлен");
                        
                        StatusChanged?.Invoke($"Захвачена комбинация: {keyboardShortcut.DisplayText}");
                        System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] StatusChanged вызвано");
                        
                        CaptureCompleted?.Invoke(keyboardShortcut);
                        System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] CaptureCompleted вызвано");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] Failed to create shortcut for {key} + {modifiers}");
                        CaptureError?.Invoke("Неподдерживаемая комбинация клавиш");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⌨️ [KeyboardCapture] Failed to parse hotkey: {e.Name}");
                    CaptureError?.Invoke("Ошибка распознавания комбинации");
                }
                
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [KeyboardCapture] Exception: {ex.Message}");
                CaptureError?.Invoke($"Ошибка обработки клавиш: {ex.Message}");
                StopCapture();
            }
        }

        private bool TryGetHotkeyInfo(string hotkeyName, out System.Windows.Input.Key key, out System.Windows.Input.ModifierKeys modifiers)
        {
            // Простой способ получить информацию о хоткее через поиск
            // В реальном приложении можно использовать Dictionary для производительности
            
            int hotkeyIndex = 0;
            
            foreach (var modifier in CaptureModifiers)
            {
                foreach (var captureKey in CaptureKeys)
                {
                    var expectedName = $"KeyCapture_{hotkeyIndex++}";
                    
                    if (expectedName == hotkeyName)
                    {
                        key = captureKey;
                        modifiers = modifier;
                        return true;
                    }
                }
            }
            
            key = System.Windows.Input.Key.None;
            modifiers = System.Windows.Input.ModifierKeys.None;
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

        #region Public Methods

        /// <summary>
        /// Получает список поддерживаемых клавиш для захвата
        /// </summary>
        /// <returns>Массив поддерживаемых клавиш</returns>
        public static System.Windows.Input.Key[] GetSupportedKeys()
        {
            return CaptureKeys;
        }

        /// <summary>
        /// Получает список поддерживаемых модификаторов
        /// </summary>
        /// <returns>Массив поддерживаемых модификаторов</returns>
        public static System.Windows.Input.ModifierKeys[] GetSupportedModifiers()
        {
            return CaptureModifiers;
        }

        /// <summary>
        /// Проверяет, поддерживается ли комбинация для захвата
        /// </summary>
        /// <param name="key">Клавиша</param>
        /// <param name="modifiers">Модификаторы</param>
        /// <returns>true если комбинация поддерживается</returns>
        public static bool IsCombinationSupported(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            return Array.Exists(CaptureKeys, k => k == key) && 
                   Array.Exists(CaptureModifiers, m => m == modifiers);
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