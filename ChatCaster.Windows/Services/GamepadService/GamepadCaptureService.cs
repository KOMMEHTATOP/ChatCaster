using ChatCaster.Core.Events;
using ChatCaster.Core.Models;
using ChatCaster.Core.Services;
using ChatCaster.Core.Constants;

namespace ChatCaster.Windows.Services.GamepadService;

/// <summary>
/// Сервис для захвата новых комбинаций геймпада
/// Работает НАД основным MainGamepadService, а не создает свой
/// </summary>
public class GamepadCaptureService : IDisposable
{
    public event EventHandler<GamepadShortcut>? ShortcutCaptured;
    public event EventHandler<string>? CaptureStatusChanged;
    
    private readonly MainGamepadService _mainGamepadService;
    private CancellationTokenSource? _captureTokenSource;
    private bool _isCapturing = false;
    private bool _isDisposed = false;
    
    // Накопление нажатых кнопок для формирования комбинации
    private readonly HashSet<GamepadButton> _accumulatedButtons = new();
    private DateTime _firstButtonPressTime = DateTime.MinValue;
    
    public bool IsCapturing => _isCapturing;
    
    public GamepadCaptureService(MainGamepadService mainGamepadService)
    {
        _mainGamepadService = mainGamepadService ?? throw new ArgumentNullException(nameof(mainGamepadService));
    }
    
    /// <summary>
    /// Начинает захват комбинации геймпада
    /// </summary>
    public async Task<bool> StartCaptureAsync(int timeoutSeconds = 30)
    {
        if (_isCapturing)
        {
            Console.WriteLine("🎮 [Capture] Захват уже активен");
            return false;
        }
        
        try
        {
            _isCapturing = true;
            _captureTokenSource = new CancellationTokenSource();
            _accumulatedButtons.Clear();
            _firstButtonPressTime = DateTime.MinValue;
            
            Console.WriteLine("🎮 [Capture] Начинаем захват комбинации геймпада");
            CaptureStatusChanged?.Invoke(this, "Нажмите любую комбинацию кнопок на геймпаде...");
            
            // Запускаем захват с таймаутом
            await CaptureWithTimeout(timeoutSeconds, _captureTokenSource.Token);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [Capture] Ошибка захвата: {ex.Message}");
            CaptureStatusChanged?.Invoke(this, $"Ошибка: {ex.Message}");
            StopCapture();
            return false;
        }
    }
    
    /// <summary>
    /// Останавливает захват
    /// </summary>
    public void StopCapture()
    {
        if (!_isCapturing)
            return;
            
        try
        {
            _captureTokenSource?.Cancel();
            _isCapturing = false;
            _accumulatedButtons.Clear();
            
            Console.WriteLine("🎮 [Capture] Захват остановлен");
            CaptureStatusChanged?.Invoke(this, "Захват остановлен");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [Capture] Ошибка остановки: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Захват с таймаутом - работает с основным сервисом
    /// </summary>
    private async Task CaptureWithTimeout(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var timeoutTask = Task.Delay(timeoutSeconds * 1000, cancellationToken);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            // Проверяем таймаут
            if (timeoutTask.IsCompleted)
            {
                Console.WriteLine("⏰ [Capture] Таймаут захвата");
                CaptureStatusChanged?.Invoke(this, "Время ожидания истекло");
                StopCapture();
                return;
            }
            
            // Получаем состояние через ОСНОВНОЙ сервис
            var currentState = _mainGamepadService.GetCurrentState();
            if (currentState == null)
            {
                await Task.Delay(AppConstants.CapturePollingRateMs, cancellationToken);
                continue;
            }
            
            // Обрабатываем состояние кнопок
            await ProcessGamepadState(currentState, cancellationToken);
            
            await Task.Delay(AppConstants.CapturePollingRateMs, cancellationToken); // Используем константу из Core
        }
    }
    
    /// <summary>
    /// Обрабатывает состояние геймпада и накапливает нажатые кнопки
    /// </summary>
    private async Task ProcessGamepadState(GamepadState currentState, CancellationToken cancellationToken)
    {
        var currentlyPressed = currentState.GetPressedButtons().ToHashSet();
        
        // Если есть нажатые кнопки
        if (currentlyPressed.Count > 0)
        {
            // Запоминаем время первого нажатия
            if (_firstButtonPressTime == DateTime.MinValue)
            {
                _firstButtonPressTime = DateTime.Now;
                Console.WriteLine("🎮 [Capture] Началось нажатие кнопок");
            }
            
            // Добавляем новые кнопки в накопитель
            foreach (var button in currentlyPressed)
            {
                if (_accumulatedButtons.Add(button)) // Add возвращает true если элемент новый
                {
                    Console.WriteLine($"🎮 [Capture] Добавлена кнопка: {button}");
                }
            }
            
            Console.WriteLine($"🎮 [Capture] Нажатые кнопки: {string.Join(", ", currentlyPressed)}");
            Console.WriteLine($"🎮 [Capture] Всего в комбинации: {string.Join(", ", _accumulatedButtons)}");
        }
        else if (_accumulatedButtons.Count > 0)
        {
            // Все кнопки отпущены, но у нас есть накопленная комбинация
            var holdTime = DateTime.Now - _firstButtonPressTime;
            
            // Проверяем минимальное время удержания (используем константу из Core)
            if (holdTime.TotalMilliseconds >= AppConstants.MinHoldTimeMs)
            {
                Console.WriteLine("🎮 [Capture] Кнопки отпущены");
                
                // Создаем шорткат из накопленных кнопок
                var shortcut = CreateShortcutFromAccumulatedButtons();
                Console.WriteLine($"🎮 [Capture] Комбинация захвачена: {shortcut.DisplayText}");
                
                // Уведомляем о захвате
                ShortcutCaptured?.Invoke(this, shortcut);
                CaptureStatusChanged?.Invoke(this, $"Захвачена комбинация: {shortcut.DisplayText}");
                
                StopCapture();
                return;
            }
            else
            {
                Console.WriteLine($"🎮 [Capture] Слишком быстрое нажатие ({holdTime.TotalMilliseconds:F0}ms), сбрасываем");
                _accumulatedButtons.Clear();
                _firstButtonPressTime = DateTime.MinValue;
            }
        }
    }
    
    /// <summary>
    /// Создает GamepadShortcut из накопленных кнопок
    /// </summary>
    private GamepadShortcut CreateShortcutFromAccumulatedButtons()
    {
        var buttonsList = _accumulatedButtons.ToList();
        
        if (buttonsList.Count == 1)
        {
            // Одна кнопка - дублируем её для совместимости с Core
            return new GamepadShortcut
            {
                PrimaryButton = buttonsList[0],
                SecondaryButton = buttonsList[0], 
                RequireBothButtons = false,
                HoldTimeMs = AppConstants.MinHoldTimeMs // Используем константу из Core
            };
        }
        else if (buttonsList.Count >= 2)
        {
            // Комбинация - берем первые две кнопки (можно отсортировать для консистентности)
            return new GamepadShortcut
            {
                PrimaryButton = buttonsList[0],
                SecondaryButton = buttonsList[1],
                RequireBothButtons = true,
                HoldTimeMs = AppConstants.MinHoldTimeMs // Используем константу из Core
            };
        }
        else
        {
            // Fallback (не должно происходить)
            return new GamepadShortcut
            {
                PrimaryButton = GamepadButton.A,
                SecondaryButton = GamepadButton.B,
                RequireBothButtons = false,
                HoldTimeMs = AppConstants.MinHoldTimeMs
            };
        }
    }
    
    public void Dispose()
    {
        if (!_isDisposed)
        {
            StopCapture();
            _captureTokenSource?.Dispose();
            _isDisposed = true;
        }
    }
}