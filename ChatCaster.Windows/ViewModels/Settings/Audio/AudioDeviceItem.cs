namespace ChatCaster.Windows.ViewModels.Settings.Audio
{
    /// <summary>
    /// Элемент селектора аудио устройства для UI
    /// </summary>
    public class AudioDeviceItem
    {
        public string Id { get; }
        public string Name { get; }
        public bool IsDefault { get; }

        public AudioDeviceItem(string id, string name, bool isDefault)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            IsDefault = isDefault;
        }

        public override string ToString() => Name;

        public override bool Equals(object? obj)
        {
            if (obj is AudioDeviceItem other)
            {
                return Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode() => Id.GetHashCode();
    }

    /// <summary>
    /// Элемент селектора языка для UI
    /// </summary>
    public class LanguageItem
    {
        public string Code { get; }
        public string DisplayName { get; }

        public LanguageItem(string code, string displayName)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }

        public override string ToString() => DisplayName;

        public override bool Equals(object? obj)
        {
            if (obj is LanguageItem other)
            {
                return Code == other.Code;
            }
            return false;
        }

        public override int GetHashCode() => Code.GetHashCode();
    }
}
