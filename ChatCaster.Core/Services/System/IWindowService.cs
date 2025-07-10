namespace ChatCaster.Core.Services.System;

    /// <summary>
    /// Сервис для работы с окнами системы
    /// </summary>
    public interface IWindowService
    {
        /// <summary>
        /// Получает заголовок активного окна
        /// </summary>
        string GetActiveWindowTitle();
    
        /// <summary>
        /// Проверяет, является ли окно собственным окном приложения
        /// </summary>
        bool IsOwnWindow(string windowTitle);
    
        /// <summary>
        /// Получает handle активного окна
        /// </summary>
        IntPtr GetActiveWindowHandle();
    }


