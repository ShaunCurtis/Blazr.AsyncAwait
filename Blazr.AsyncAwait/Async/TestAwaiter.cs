using System.Runtime.CompilerServices;

namespace Blazr.AsyncAwait.Async;

public class TestAwaiter : INotifyCompletion
{
    private bool _isFinished;
    private int _result;
    private SynchronizationContext? _capturedContext;
    private Timer? _timer;

    public TestAwaiter()
    {
        _capturedContext = SynchronizationContext.Current;
    }

    public TestAwaiter IdleAsync(int period)
    {
        if (_timer is null)
            _timer = new(TimerExpired, null, period, Timeout.Infinite);

        return this;
    }

    private void TimerExpired(object? state)
    {
        _isFinished = true;
        _result = new Random().Next();
    }

    public TestAwaiter GetAwaiter() => this;

    public bool IsCompleted => _isFinished;

    public int GetResult()
    {
        if (!_isFinished)
        {
            var wait = new SpinWait();
            while (!_isFinished)
                wait.SpinOnce();
        }
        return _result;
    }

    public void OnCompleted(Action continuation)
    {
        if (_isFinished)
        {
            continuation();
            return;
        }

        if (_capturedContext != null)
            _capturedContext.Post(_ => continuation(), null);
        else
            continuation();
    }
}
