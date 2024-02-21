using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

namespace Blazr.Async;

public interface IMyAwaitable
{
    public bool IsCompleted { get; }
    public int Result { get; }
    public MyAwaiter GetAwaiter();
    public MyAwaiter ConfigureAwait(bool useContext);
    public void OnCompleted(Action continuation);
    public abstract static MyAwaiter Idle(int period);
}


public class MyAwaitable
{
    private volatile int _result;
    private volatile Queue<Action> _continuations = new();
    private Timer? _timer;
    private volatile SynchronizationContext? _capturedContext;
    private volatile bool _completed;
    private volatile bool _runOnCapturedContext;

    public bool IsCompleted => _completed;

    // Private constructor.  An instance can onky bw created through static methods
    private MyAwaitable()
    {
        // Capture the current sync context so we can run the continuation in the correct context
        _capturedContext = SynchronizationContext.Current;
    }

    public MyAwaiter GetAwaiter() => ConfigureAwait(true);

    public MyAwaiter ConfigureAwait(bool useContext)
    {
        _runOnCapturedContext = useContext;
        // Return a new instance of the awaiter
        return new MyAwaiter(this);
    }

    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        this.ScheduleContinuationIfCompleted();
    }

    private void ScheduleContinuationIfCompleted()
    {
        // Do nothing if the awaitable is still running
        if (!_completed)
            return;

        // The awaitable has completed.
        // Run the continuations in the correct context based on _runOnCapturedContext
        while (_continuations.Count > 0)
        {
            var continuation = _continuations.Dequeue();
            if (_continuations.Count() > 0)
            {
                var completedContinuations = new List<Action>();

                if (_runOnCapturedContext && _capturedContext != null)
                    _capturedContext.Post(_ => continuation(), null);

                else
                    continuation();
            }
        }
    }

    public int GetResult()
    {
        // block the thread until completed
        // and then return the result
        var wait = new SpinWait();
        while (!_completed)
            wait.SpinOnce();
        return _result;
    }

    internal void TimerExpired(object? state)
    {
        Utilities.LogToConsole("Timer Expired");
        _result = 1;
        _completed = true;
    }

    // Schedule the continuation when the timer expires and sets _completed to true.
    internal void WaitOnCompletion(object? state)
    {
        SynchronizationContext.SetSynchronizationContext(_capturedContext);
        Utilities.LogToConsole("MyAwaitable waiting on timer to expire.");

        var wait = new SpinWait();
        while (!_completed)
            wait.SpinOnce();

        this.ScheduleContinuationIfCompleted();
    }

    public static MyAwaiter Idle(int period)
    {
        // Create an awaitable instance
        MyAwaitable awaitable = new MyAwaitable();
        // Set up the instance timer with the correct wait period
        awaitable._timer = new(awaitable.TimerExpired, null, period, Timeout.Infinite);

        // Spin off a waiter on a separate thread so we can pass control back [Yield] to the caller.
        // Check CPU usage to confirm low usage footprint
        ThreadPool.QueueUserWorkItem(awaitable.WaitOnCompletion);

        // Return the awaiter to the caller
        return awaitable.GetAwaiter();
    }
}

public readonly struct MyAwaiter : INotifyCompletion
{
    private readonly MyAwaitable awaitable;

    public bool IsCompleted => awaitable.IsCompleted;

    public MyAwaiter(MyAwaitable awaitable) => this.awaitable = awaitable;

    public MyAwaiter GetAwaiter() => this;

    public int GetResult() => awaitable.GetResult();

    public void OnCompleted(Action continuation) => awaitable.OnCompleted(continuation);
}
