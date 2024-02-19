# The Async Series - Task.Delay

`Task.Delay(x)` provides a mechanism for introducing an asynchronous yielding delay into a block of code.  

It returns a running awaitable`Task` [Completed = `false`]  which completes when the timer expires.  The continuation runs either on the Synchronisation Context or the Threadpool scheduler, depending on the `ConfigureAwait` setup.

In Blazor we often use it to emulate an async task in testing or demos, or to yield control during a synchronous block of code to display progress.

In this article I'll build a delay class to demonstrate how it works

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

First We need to implement the *Awatable* and *Awaiter* pattern.  Only objects that return an *Awaiter* can be awaited.  This implementation uses a single static constructor.

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

Next we:
1. Cache the *synchronisation context* on initialization.
2. Set up an internal `Queue` to hold any posted continuations.
3. Define a private `System.Threading.Timer` which is started on initialization.
4. A Callback for the timer which sets the *awaiter* to complete, disposes the timer and schedules the completions.

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

5. Define `OnCompleted`: the *awaiter* method called to add a completion to the *awaiter*.  Completions can be added at any time, including after `IsCompleted` is set to `true`.  `ScheduleContinuationsIfCompleted` will post any completion immediately if the *awaiter* has completed.

```csharp
    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        this.ScheduleContinuationsIfCompleted();
    }
```

Finally `ScheduleContinuationsIfCompleted`.  It exits early if called before the background process has set the *awaiter* to completed.  It checks the cached *synchonisation Context* state and if one exits it posts the continuations to it.  Otherwise, it posts the continuations to the threadpool dispatcher.

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

Timers are *posted* to the timer queue when they are created.  The Timer loop that services this queue runs on a background thread. The Timer service has responsibility for calling the callback when the timer expires.
 
The static `Delay` creates and instance of `BlazrDelay` with a timer queued on the timer queue, returns the new instance of itself and completes.  The thread, in our case the *synchonisation context*, is free to run any queued work.

When the timer expires, the timer loop schedules the callback on a threadpool thread.  It's a background singleton service with no concept of a user context *synchonisation context*.

The callback calls `ScheduleContinuationsIfCompleted` which does know about a *synchonisation context*.  It schedules the actual continuations on the context if one exists, or executes it on the current thread if there's no context.

This is demonstration code only.  Error checking, exception handling and edge case scenarios are not covered.  Use `Task.Delay` in production.  