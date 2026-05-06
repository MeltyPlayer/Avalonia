using System;
using System.Threading;

namespace Avalonia.Reactive;

public struct StructAnonymousDisposable : IDisposable
{
    private volatile Action? _dispose;
    public StructAnonymousDisposable(Action dispose)
    {
        _dispose = dispose;
    }
    public bool IsDisposed => _dispose == null;
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

public struct StructAnonymousDisposable<T> : IDisposable
{
    private T _value;
    private volatile Action<T>? _dispose;

    public StructAnonymousDisposable(T value, Action<T> dispose)
    {
        _value = value;
        _dispose = dispose;
    }
    public bool IsDisposed => _dispose == null;
    public void Dispose()
    {
        Interlocked.Exchange(ref _dispose, null)?.Invoke(_value);
    }
}
