# Task.Delay

`Task.Delay(x)` provides a mechanism for introducing an async yielding delay into a block of code.  

It's returns an awaitable `Task` which completes when the timer expires.  This normally cascades the yield back to the threading context [either the Synchronisation Context or the Threadpool scheduler] to process the next code block in it's queue.  The code after the delay is scheduled to execute after the delay elaspes.

It's high level code, abstracting the programmer from the nitty gritty of the *Task Processing Library*.

For a deep dive into it's workings we'll use a simple Blazor example: a button click handler.

Here's a simple home page with a button to emulate asynchronous activity such as a database call.

```csharp
@page "/"

<PageTitle>Home</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<div class="mb-3">
    <button class="btn btn-primary" @onclick="Clicked">Click</button>
</div>

<div class="bg-dark text-white m-2 p-2">
    @_message
</div>

@code {
    private string? _message;

    private async Task Clicked()
    {
        _message = $"Processing at {DateTime.Now.ToLongTimeString()}";
        await Task.Delay(100);
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }
}
```

First a state object to track the delay.

It has:

1. A manual Task controlled by and instance of a `TaskCompletionSource`.
1. A Timer.
1. An internal bool for tracking completion
1. A Timer event handler.

```csharp
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
            // dispose and release the timer for GC.
            this.Timer?.Dispose();
            this.Timer = null;
        }
    }
}
```

The Delay method is now simple.

1. Creates an instance of `BlazrTaskState`.
2. Starts a new timer and assigns it to `BlazrTaskState.Timer`.
3. Returns the state Task. 

```csharp
    public static Task Delay(int millisecondsDelay)
    {
        var state = new BlazrTaskState();

        if (millisecondsDelay < 0)
            throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

        state.Timer = new Timer(state.Complete, state, millisecondsDelay, -1);

        return state.Task;
    }
```

At this point we have:

1. A `BlazrTaskState` instance referenced by the threading context waiting for the timer to complete.
2. A Timer instance referenced by the `BlazrTaskState` instance.
3. An awaitable Task with a continuation on the threading context containing the code in `Clicked` beyond the await.

When the threading context expires the timer, it calls the provided callback.  This:
 
1. Sets `_stateManager` to complete.
2. Disposes the timer and sets the reference to `null`.  It's now in a state for the GC to destroy it.
3. The threading context queues the continuation as the Task awaitable is now complete.
