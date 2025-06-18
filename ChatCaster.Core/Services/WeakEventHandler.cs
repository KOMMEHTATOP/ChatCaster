using ChatCaster.Core.Events;

namespace ChatCaster.Core.Services;

/// <summary>
/// Слабая ссылка на обработчик событий для предотвращения утечек памяти
/// </summary>
public class WeakEventHandler<T> where T : ChatCasterEvent
{
    private readonly WeakReference _targetRef;
    private readonly string _methodName;

    public WeakEventHandler(Action<T> handler)
    {
        _targetRef = new WeakReference(handler.Target);
        _methodName = handler.Method.Name;
    }

    public bool TryExecute(T eventData)
    {
        var target = _targetRef.Target;
        if (target == null)
            return false; // Объект собран GC

        try
        {
            var method = target.GetType().GetMethod(_methodName);
            method?.Invoke(target, new object[] { eventData });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsAlive => _targetRef.IsAlive;
}
