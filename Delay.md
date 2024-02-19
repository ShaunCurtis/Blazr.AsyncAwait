# The Async Series - Task.Delay

`Task.Delay(x)` provides a mechanism for introducing an asynchronous yielding delay into a block of code.  

It's returns a running awaitable `Task` which completes when the timer expires.  The continuation will run on either the Synchronisation Context or the Threadpool scheduler, depending on the `ConfigureAwait` setup.

In Blazor we often use it to either emulate an async task in testing or demos, or to yield control during a synchronous block of code to display progress.

In this article we'll build our own delay mechanism to demonstrate how it works

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

First the *Awatable* and *Awaiter* pattern implementations.  Note the single static constructor setting up the delay period.

```csharp
using System.Runtime.CompilerServices;
namespace Blazr.Async;

public class BlazrDelay : INotifyCompletion
{
    public bool IsCompleted { get; private set; }
    private BlazrDelay(int delay) {}
    public BlazrDelay GetAwaiter() => this;
    public void OnCompleted(Action continuation) {}
    public void GetResult() { }

    public static BlazrDelay Delay(int delay)
    {
        var instance = new BlazrDelay(delay);
        return instance;
    }
}
```

Next:
1. Caching the *synchronisation context* on initialization.
2. Defining an internal `Queue` to hold any posted continuations.  We need to allow for more that one, and a queue is the simplest way to implement this.
3. A private `System.Threading.Timer` which is setup and started on initialization.
4. The Callback for the timer which sets the *awaiter* to complete, disposes the timer and schedules the completions.

```csharp
    private volatile SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
    private volatile Queue<Action> _continuations = new();
    private Timer _timer;

    private BlazrDelay(int delay)
    {
        _timer = new Timer(this.OnTimerExpired, null, delay, -1);
    }

    private void OnTimerExpired(object? state)
    {
        _timer.Dispose();
        this.IsCompleted = true;
        this.ScheduleContinuationsIfCompleted();
    }
```

Next, `OnCompleted`.  This is called to add a completion to the *awaiter*.  It calls `ScheduleContinuationsIfCompleted` because you can add a completion to an *awaiter* after it has completed.  Note that it will be scheduled immediately.

```csharp
    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        this.ScheduleContinuationsIfCompleted();
    }
```

Finally `ScheduleContinuationsIfCompleted`.  It early exits if called before the background process has set the *awaiter* to completed.  It checks the cached *synchonisation Context* state and if one exits it posts the continuations to it.  Otherwise, it posts the continuations to the threadpool dispatcher.

```csharp
     private void ScheduleContinuationsIfCompleted()
    {
        if (!this.IsCompleted)
            return;

        while (_continuations.Count > 0)
        {
            var continuation = _continuations.Dequeue();

            if (_synchronizationContext != null)
                _synchronizationContext.Post(_ => continuation(), null);

            else
                continuation();
        }
    }
```

Some Important points:

Timers are *posted* to the timer queue when they are created.  The Timer loop that services this queue is running on a background thread. The Timer service, running on another thread, has responsibility for calling the callback when the timer expires.
 
The static `Delay` creates and instance of `BlazrDelay` with a timer queued on the timer queue, returns the new instance of itself and completes.  The thread, in our case the *synchonisation context*, is free to run any queued work.

When the timer expires the timer loop schedules the callback on a threadpool thread.  It's a background singleton service with no concept of a user context *synchonisation context*.

The callback calls `ScheduleContinuationsIfCompleted`.  It's does know about a *synchonisation context* and schedules the actual continuations on the context if one exists, or executes it on the current thread if there's no context.

This is demonstration code only.  Error checking, exception handling and edge case scenarios are not covered.  Use `Task.Delay` in production.  