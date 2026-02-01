using System.Collections.Concurrent;

namespace AgentRouting.Infrastructure;

/// <summary>
/// Abstraction for middleware state storage.
/// Allows middleware to store state in a way that can evolve over time
/// (e.g., from in-memory to distributed cache like Redis).
/// </summary>
public interface IStateStore
{
    /// <summary>
    /// Gets a value by key.
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// Sets a value by key.
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>
    /// Tries to get a value by key.
    /// </summary>
    bool TryGet<T>(string key, out T? value);

    /// <summary>
    /// Removes a value by key.
    /// </summary>
    bool Remove(string key);

    /// <summary>
    /// Gets or adds a value, using the factory if the key doesn't exist.
    /// </summary>
    T GetOrAdd<T>(string key, Func<string, T> factory);

    /// <summary>
    /// Clears all stored values.
    /// </summary>
    void Clear();
}

/// <summary>
/// In-memory implementation of IStateStore using ConcurrentDictionary.
/// Thread-safe for concurrent access.
/// </summary>
public class InMemoryStateStore : IStateStore
{
    private readonly ConcurrentDictionary<string, object> _store = new();

    public T? Get<T>(string key)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }

    public void Set<T>(string key, T value)
    {
        if (value != null)
        {
            _store[key] = value;
        }
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_store.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public bool Remove(string key)
    {
        return _store.TryRemove(key, out _);
    }

    public T GetOrAdd<T>(string key, Func<string, T> factory)
    {
        var result = _store.GetOrAdd(key, k => factory(k)!);
        return (T)result;
    }

    public void Clear()
    {
        _store.Clear();
    }
}
