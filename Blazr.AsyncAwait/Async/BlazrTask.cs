using System.Reflection.Metadata.Ecma335;

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

        // create a completed Task
        var task = Task.CompletedTask;

        // Add a continuation tp set the result on the TaskCompletionSource
        task.ContinueWith(await =>
        {
            tcs.TrySetResult();
        });

        return tcs.Task;
    }
}

internal sealed class BlazrTaskState
{
    private TaskCompletionSource _stateManager = new TaskCompletionSource();
    private bool _complete;

    internal Timer? Timer;
    internal Task Task => _stateManager.Task;

    internal void Complete(object? statusInfo)
    {
        if (!_complete)
        {
            _complete = true;
            _stateManager.TrySetResult();
            this.Timer?.Dispose();
        }
    }
}

