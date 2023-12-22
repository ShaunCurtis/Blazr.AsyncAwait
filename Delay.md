# Task.Delay

`Task.Delay(x)` is a mechanism for introducing an async yielding delay into a block of code.  It's purpose is to return a running `Task` to the caller.  This causes the caller code block to yield back to it's caller.  In general this releases the execution context [either the Synchronisation Context or the Threadpool scheduler] to process the next code block in it's queue.  The code after the delay runs after the delay elaspes.

It's high level code, abstracting the programmer from the nitty gritty of the *Task Processing Library*.

For a deep dive into it's workings we'll use a simple Blazor example: a button click handler.

Here's a simple home page with a button to emulate some asynchronous  activity such as a database call.

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

First we need a state object to tack the delay.

It has:

1. A manual Task controlled by _stateManager.
1. A Timer
1. An internal bool `_compkete` for tracking completion
1. A Timer event handler - `Complete`.

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
            this.Timer?.Dispose();
        }
    }
}
```

The Delay method is now simple.

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
