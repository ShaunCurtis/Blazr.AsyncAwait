using Blazr.SyncronisationContext;
using System.Runtime.CompilerServices;

namespace Blazr.AsyncAwait.Async;

public class MyAwaitable
{
    private volatile bool completed;
    private volatile int result;
    private Action? continuation;
    private Timer? _timer;
    public bool IsCompleted => completed;

    public int Result => RunToCompletionAndGetResult();

    private MyAwaitable()
    {
    }

    private void TimerExpired(object? state)
    {
        Utilities.WriteToConsole("Timer Expired");
        if (completed)
            return;

        this.result = 1;
        completed = true;
    }

    public void Finish(int result)
    {
        if (completed)
            return;
        completed = true;
        this.result = result;
    }

    public MyAwaiter GetAwaiter() => ConfigureAwait(true);

    public MyAwaiter ConfigureAwait(bool captureContext)
        => new MyAwaiter(this, captureContext);

    internal void ScheduleContinuation(Action action) => continuation += action;

    internal int RunToCompletionAndGetResult()
    {
        var wait = new SpinWait();
        while (!completed)
            wait.SpinOnce();
        return result;
    }

    public static MyAwaiter Idle(int period)
    {
        var sc = SynchronizationContext.Current;
        MyAwaitable awaitable = new MyAwaitable();
        Task.Run(() =>
        {
            Utilities.WriteToConsole("MyAwaiter instance started");
            SynchronizationContext.SetSynchronizationContext(sc);
            var myAwaiter = new MyAwaitable();
            awaitable = myAwaiter;
            myAwaiter._timer = new(myAwaiter.TimerExpired, null, period, Timeout.Infinite);
            var wait = new SpinWait();
            while (!myAwaiter.completed)
                wait.SpinOnce();
        }
        );
        return awaitable.GetAwaiter();
    }
}

public class MyAwaiter : INotifyCompletion
{
    private readonly MyAwaitable awaitable;
    private readonly bool captureContext;
    SynchronizationContext? _capturedContext;

    public MyAwaiter(MyAwaitable awaitable, bool captureContext)
    {
        this.awaitable = awaitable;
        this.captureContext = captureContext;
        _capturedContext = SynchronizationContext.Current;
    }

    public MyAwaiter GetAwaiter() => this;

    public bool IsCompleted => awaitable.IsCompleted;

    public int GetResult() => awaitable.RunToCompletionAndGetResult();

    public void OnCompleted(Action continuation)
    {
        Utilities.WriteToConsole("OnCompleted Called");
        awaitable.ScheduleContinuation(() =>
        {
            if (captureContext && _capturedContext != null)
                _capturedContext.Post(_ => continuation(), null);
            else
                continuation();
        });
    }
}