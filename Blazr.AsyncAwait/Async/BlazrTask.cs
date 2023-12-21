namespace Blazr.AsyncAwait;

public static class BlazrTask
{
    public static Task Delay(int millisecondsDelay)
    {
        var state = new BlazrTaskState();

        if (millisecondsDelay < 0)
            throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

        state.Timer = new Timer(state.Complete, state, millisecondsDelay, -1);

        return state.Task;
    }

    public static Task Yield()
    {
        var tcs = new TaskCompletionSource();

        try
        {
            // create a task with nothing to do and start it
            var yieldingTask = new Task(() => { });
            yieldingTask.Start();

            // create the continuation
            yieldingTask.ContinueWith(await =>
            {
                // finally set the TaskCompletionSource as complete
                tcs.TrySetResult();
            });
        }
        catch (Exception exception)
        {
            tcs.SetException(exception);
        }

        return tcs.Task;
    }
}

file sealed class BlazrTaskState
{
    private TaskCompletionSource _tcs = new TaskCompletionSource();
    private bool _complete;

    internal Timer? Timer;
    internal Task Task => _tcs.Task;

    internal void Complete(object? statusInfo)
    {
        if (!_complete)
        {
            _complete = true;
            _tcs.TrySetResult();
            this.Timer?.Dispose();
        }
    }
}

