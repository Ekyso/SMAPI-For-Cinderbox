using System;
using System.Collections.Concurrent;
using System.Threading;

namespace StardewModdingAPI.Framework;

/// <summary>A thread-safe object pool that reduces GC pressure by reusing instances.</summary>
/// <typeparam name="T">The type of objects to pool.</typeparam>
internal sealed class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly Func<T> _factory;
    private readonly Action<T>? _reset;
    private readonly int _maxSize;
    private int _currentCount;

    /// <summary>Construct an instance.</summary>
    /// <param name="factory">Factory function to create new instances. Defaults to parameterless constructor.</param>
    /// <param name="reset">Optional action to reset an object before reuse.</param>
    /// <param name="maxSize">Maximum number of objects to keep in the pool.</param>
    public ObjectPool(Func<T>? factory = null, Action<T>? reset = null, int maxSize = 100)
    {
        _factory = factory ?? (() => new T());
        _reset = reset;
        _maxSize = maxSize;
    }

    /// <summary>Rent an object from the pool, creating a new one if the pool is empty.</summary>
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            Interlocked.Decrement(ref _currentCount);
            _reset?.Invoke(item);
            return item;
        }
        return _factory();
    }

    /// <summary>Return an object to the pool for reuse.</summary>
    /// <param name="item">The object to return.</param>
    public void Return(T item)
    {
        if (_currentCount < _maxSize)
        {
            _pool.Add(item);
            Interlocked.Increment(ref _currentCount);
        }
    }

    /// <summary>The current number of objects in the pool.</summary>
    public int Count => Volatile.Read(ref _currentCount);
}
