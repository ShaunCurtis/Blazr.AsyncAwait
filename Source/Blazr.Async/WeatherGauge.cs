using System.Runtime.CompilerServices;

namespace Blazr.Async;

public class TemperatureGauge
{
    private TemperatureAwaiter _awaiter = new();

    public TemperatureAwaiter GetAwaiter() => _awaiter;

    public static TemperatureGauge GetTemperatureAsync()
    {
        var work = new TemperatureGauge();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(1000);
            work._awaiter.SetResult(Random.Shared.Next(-40, 60));
        });
        return work;
    }
}

public class TemperatureAwaiter : INotifyCompletion
{
    private volatile Queue<Action> _continuations;
    private volatile SynchronizationContext? _synchronizationContext;
    private volatile int _result;

    public bool IsCompleted { get; private set; }

    public TemperatureAwaiter()
    {
        _continuations = new();
        _synchronizationContext = SynchronizationContext.Current;
    }

    public int GetResult()
    {
        var wait = new SpinWait();
        while (!this.IsCompleted)
            wait.SpinOnce();
        return _result;
    }

    /// <summary>
    /// Sets the result on the awaiter, sets IsCompleted as true
    /// and runs the continuation
    /// </summary>
    /// <param name="value"></param>
    public void SetResult(int value)
    {
        _result = value;
        this.IsCompleted = true;
        this.ScheduleContinuationsIfCompleted();
    }

    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        // We need to run the queued continuations immediately if the awaitable has already completed
        this.ScheduleContinuationsIfCompleted();
    }

    private void ScheduleContinuationsIfCompleted()
    {
        if (!this.IsCompleted)
            return;

        while (_continuations.Count > 0)
        {
            var continuation = _continuations.Dequeue();

            if (_synchronizationContext != null)
                _synchronizationContext.Post(_ => continuation(), null);

            else
                continuation();
        }
    }
}
