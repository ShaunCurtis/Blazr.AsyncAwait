using System.Runtime.CompilerServices;

namespace Blazr.Async;

public class BlazrYield : INotifyCompletion
{
    public bool IsCompleted => false;

    private SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;

    private BlazrYield() {}

    public BlazrYield GetAwaiter()
        => this;

    public void OnCompleted(Action continuation)
    {
        if (_synchronizationContext != null)
        {
            var post = new SendOrPostCallback((state) =>
            {
                continuation.Invoke();
            });

            _synchronizationContext.Post(post, null);
        }

        else
        {
            var workItem = new WaitCallback((state) =>
            {
                continuation.Invoke();
            });

            ThreadPool.QueueUserWorkItem(workItem);
        }
    }

    public void GetResult() {}

    public static BlazrYield Yield()
    {
        var instance = new BlazrYield();
        return instance;
    }
}
