using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

namespace Blazr.Async;

public class MyAwaitable
{
    private volatile int _result;
    private volatile Action? _continuation;
    private volatile Timer? _timer;
    private volatile SynchronizationContext? _capturedContext;
    private volatile bool _completed;
    private volatile bool _useContext;

    public bool IsCompleted => _completed;
    public int Result => RunToCompletionAndGetResult();

    private MyAwaitable()
    {
        _capturedContext = SynchronizationContext.Current;
    }

    private void TimerExpired(object? state)
    {
        Utilities.LogToConsole("Timer Expired");
        if (_completed)
            return;

        _result = 1;
        _completed = true;
    }

    public MyAwaiter GetAwaiter() => ConfigureAwait(true);

    public MyAwaiter ConfigureAwait(bool useContext)
    {
        _useContext = useContext;   
        return new MyAwaiter(this);
    }

    internal void SetContinuation(Action continuation)
        => _continuation = continuation;
 
    internal void ScheduleContinuation()
    {
        if (!_completed)
            return;

        if (_continuation is not null)
        {
            if (_useContext && _capturedContext != null)
                _capturedContext.Post(_ => _continuation(), null);
            else
                _continuation();
        }
    }

    internal int RunToCompletionAndGetResult()
    {
        var wait = new SpinWait();
        while (!_completed)
            wait.SpinOnce();
        return _result;
    }

    public static MyAwaiter Idle(int period)
    {
        var sc = SynchronizationContext.Current;
        MyAwaitable awaitable = new MyAwaitable();
        awaitable._timer = new(awaitable.TimerExpired, null, period, Timeout.Infinite);

        ThreadPool.QueueUserWorkItem((state) =>
        {
            SynchronizationContext.SetSynchronizationContext(sc);
            Utilities.LogToConsole("MyAwaitable waiting on timer to expire.");

            var wait = new SpinWait();
            while (!awaitable._completed)
                wait.SpinOnce();

            awaitable.ScheduleContinuation();
        });

        return awaitable.GetAwaiter();
    }
}

public readonly struct MyAwaiter : INotifyCompletion
{
    private readonly MyAwaitable awaitable;

    public MyAwaiter(MyAwaitable awaitable)
        =>  this.awaitable = awaitable;

    public MyAwaiter GetAwaiter() 
        => this;

    public bool IsCompleted => awaitable.IsCompleted;

    public int GetResult() => awaitable.RunToCompletionAndGetResult();

    public void OnCompleted(Action continuation)
    {
        awaitable.SetContinuation(continuation);
        awaitable.ScheduleContinuation();
    }
}