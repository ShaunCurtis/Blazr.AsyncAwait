using System.Diagnostics;
using System.Runtime.CompilerServices;
using static Blazr.AsyncAwait.Async.DbCallEmulatorUtility;

namespace Blazr.AsyncAwait.Async;

public class EmulateADbCall : INotifyCompletion
{
    private int _millisecs = 10;
    private Timer? _timer = null;
    private volatile bool _complete;

    public event Action? Completed;
    public bool IsCompleted => _complete;

    public EmulateADbCall(int millisecs)
    {
        _millisecs = millisecs;

        if (millisecs < 0)
            throw new ArgumentOutOfRangeException(nameof(millisecs));

        _timer = new Timer(this.Complete, null, _millisecs, -1);
    }

    public void GetResult()
    {
        if (!IsCompleted)
        {
            var wait = new SpinWait();
            while (!IsCompleted)
                wait.SpinOnce();
        }
        return;
    }

    private void Complete(object? statusInfo)
    {
        _complete = true;
        // dispose and release the timer for GC.
        _timer?.Dispose();
        _timer = null;
    }

    public void OnCompleted(Action continuation)
    {
        if (IsCompleted)
        {
            continuation();
            return;
        }

        var capturedContext = SynchronizationContext.Current;

        if (capturedContext != null)
            capturedContext.Post(_ => continuation(), null);
        else
            continuation();
    }

    public EmulateADbCall GetAwaiter()
    {
        return  this;
    }
}

static class DbCallEmulatorUtility
{

    public struct Awaiter : INotifyCompletion
    {
        private readonly EmulateADbCall _awaitable;

        public Awaiter(EmulateADbCall awaitable)
            => _awaitable = awaitable;

        public void GetResult()
        {
            if (!IsCompleted)
            {
                var wait = new SpinWait();
                while (!IsCompleted)
                    wait.SpinOnce();
            }
            return;
        }

        public bool IsCompleted => _awaitable.IsCompleted;

        public void OnCompleted(Action continuation)
        {
            if (IsCompleted)
            {
                continuation();
                return;
            }
            var capturedContext = SynchronizationContext.Current;

            _awaitable.Completed += () =>
            {
                if (capturedContext != null)
                    capturedContext.Post(_ => continuation(), null);
                else
                    continuation();
            };
        }
    }

    public static Awaiter GetAwaiter(this EmulateADbCall emulator)
    {
        return new Awaiter(emulator);
    }
}

