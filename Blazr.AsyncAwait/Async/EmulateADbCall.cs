using System.Runtime.CompilerServices;

namespace Blazr.AsyncAwait.Async;

public struct EmulateADbCall : INotifyCompletion
{
    private int _millisecs = 10;
    private Timer? _timer = null;
    private bool _complete;

    public EmulateADbCall(int millisecs)
    {
        _millisecs = millisecs;

        if (millisecs < 0)
            throw new ArgumentOutOfRangeException(nameof(millisecs));

        _timer = new Timer(this.Complete, null, _millisecs, -1);
    }

    private void Complete(object? statusInfo)
    {
        _complete = true;
        // dispose and release the timer for GC.
        _timer?.Dispose();
        _timer = null;
    }

    public bool IsCompleted => _complete;

    public void GetResult()
    {
        // Spin if the timer hasn't completed
        if (!_complete)
        {
            var wait = new SpinWait();
            while (!_complete)
                wait.SpinOnce();
        }
        // Return the result
        return;
    }

    public void OnCompleted(Action continuation)
    {
        // Get the Synchronisation Context if one exista
        var capturedContext = SynchronizationContext.Current;

        // Run the continuation on the Synchronisation Context if it exists
        if (capturedContext != null)
            capturedContext.Post(_ => continuation(), null);
        else
            continuation();
    }

    public EmulateADbCall GetAwaiter()
    {
        return this;
    }
}


