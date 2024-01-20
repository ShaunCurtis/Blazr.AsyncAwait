using System.Runtime.CompilerServices;

namespace Blazr.AsyncAwait.Async;

public class MyAwaitable
{
    private volatile bool finished;
    public bool IsFinished => finished;
    public event Action Finished;
    public MyAwaitable(bool finished) => this.finished = finished;
    public void Finish()
    {
        if (finished) return;
        finished = true;
        Finished?.Invoke();
    }
    public MyAwaiter GetAwaiter() => new MyAwaiter(this);
}

public class MyAwaiter : INotifyCompletion
{
    private readonly MyAwaitable awaitable;
    private int result;

    public MyAwaiter(MyAwaitable awaitable)
    {
        this.awaitable = awaitable;
        if (IsCompleted)
            SetResult();
    }

    public bool IsCompleted => awaitable.IsFinished;

    public int GetResult()
    {
        if (!IsCompleted)
        {
            var wait = new SpinWait();
            while (!IsCompleted)
                wait.SpinOnce();
        }
        return result;
    }

    public void OnCompleted(Action continuation)
    {
        if (IsCompleted)
        {
            continuation();
            return;
        }
        var capturedContext = SynchronizationContext.Current;
        awaitable.Finished += () =>
        {
            SetResult();
            if (capturedContext != null)
                capturedContext.Post(_ => continuation(), null);
            else
                continuation();
        };
    }

    private void SetResult()
    {
        result = new Random().Next();
    }
}