using ChatCaster.Core.Models;

namespace ChatCaster.Windows.ViewModels.Settings.Speech
{
    /// <summary>
    /// Элемент селектора модели Whisper для UI
    /// </summary>
    public class WhisperModelItem
    {
        public WhisperModel Model { get; }
        public string DisplayName { get; }

        public WhisperModelItem(WhisperModel model, string displayName)
        {
            Model = model;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is WhisperModelItem other)
            {
                return Model == other.Model;
            }
            return false;
        }

        public override int GetHashCode() => Model.GetHashCode();
    }

    /// <summary>
    /// Фабрика для создания доступных моделей Whisper
    /// </summary>
    public static class WhisperModelFactory
    {
        /// <summary>
        /// Создает список всех доступных моделей Whisper
        /// </summary>
        public static List<WhisperModelItem> CreateAvailableModels()
        {
            return new List<WhisperModelItem>
            {
                new(WhisperModel.Tiny, "Tiny (~76 MB)"),
                new(WhisperModel.Base, "Base (~145 MB)"),
                new(WhisperModel.Small, "Small (~476 MB)"),
                new(WhisperModel.Medium, "Medium (~1.5 GB)"),
                new(WhisperModel.Large, "Large (~3.0 GB)")
            };
        }

        /// <summary>
        /// Получает модель по умолчанию
        /// </summary>
        public static WhisperModelItem GetDefaultModel()
        {
            var models = CreateAvailableModels();
            return models.First(m => m.Model == WhisperModel.Base);
        }

        /// <summary>
        /// Находит модель по enum значению
        /// </summary>
        public static WhisperModelItem? FindByModel(WhisperModel model)
        {
            var models = CreateAvailableModels();
            return models.FirstOrDefault(m => m.Model == model);
        }
    }
}