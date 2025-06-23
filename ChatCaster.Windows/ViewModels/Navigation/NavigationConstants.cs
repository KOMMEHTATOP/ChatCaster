using System.Windows.Media;

namespace ChatCaster.Windows.ViewModels.Navigation
{
    public static class NavigationConstants
    {
        // Страницы
        public const string MainPage = "Main";
        public const string AudioPage = "Audio";
        public const string InterfacePage = "Interface";
        public const string ControlPage = "Control";

        // Цвета кнопок
        public readonly static SolidColorBrush ActiveButtonBrush = new(Color.FromRgb(0x0e, 0x63, 0x9c));
        public readonly static SolidColorBrush InactiveButtonBrush = Brushes.Transparent;

        // Статусы
        public const string StatusReady = "Готов к записи";
        public const string StatusRecording = "Запись...";
        public const string StatusProcessing = "Обработка...";
    }
}
