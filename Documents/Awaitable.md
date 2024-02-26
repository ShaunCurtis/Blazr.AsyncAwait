# The Async Series - Awaitables and Awaiters

Any method marked with `await` must be *awaitable*.  The *awaitable* pattern implements a single method.

```csharp
    public Awaiter GetAWaiter();
```

And the *awaiter* pattern the `awaitable` returns.

```csharp
    public bool IsCompleted;
    public void OnCompleted(Action continuation);
    public void GetResult();
```

 It provides: 

1. A bool readonly property to check if the awaitable is complete.
2. A method to post continuations to be run when the awaitable is complete.
3. A method to get the result on completion.

To summarise:

 - An *Awaitable* is an object that executes some form of asynchronous behaviour and implements a `GetAwaiter` method. 
 - An *Awaiter* is an object returned by `GetAwaiter`.

`Task`, in it's various guises, implements this functionality.  It's `GetAwaiter` returns itself.

## Implementation

Implementing a production customer awaiter is complex.  The one I'll build here is simplistic, but demonstrates the principles.


### TemperatureAwaiter

This is the awaiter.  It implements used the *Awaiter* pattern.

It captures the *synchronization context* on initialization.  `IsCompleted` is false and it has an internal `Queue` of registered continuations.  

The background process calls `SetResult` when it completes.  This sets the result value, `IsCompleted` as true and calls `ScheduleContinuationsIfCompleted`.  

`ScheduleContinuationsIfCompleted` manages scheduling the continuations.  It checks the state of `IsCompleted` because it's also called from `OnCompleted`.  Why? If the state of the awaiter is completed, the posted continuation needs to be scheduled immediately.  

`GetResult` can only return a result after the result has been set by the background process.  It uses the `SpinWait` TPL primitive to block the caller until it is set.

```csharp
public class TemperatureAwaiter : INotifyCompletion
{
    private volatile Queue<Action> _continuations;
    private volatile SynchronizationContext? _synchronizationContext;
    private volatile int _result;

    public bool IsCompleted { get; private set; }

    public TemperatureAwaiter()
    {
        _continuations = new();
        _synchronizationContext = SynchronizationContext.Current;
    }

    public int GetResult()
    {
        var wait = new SpinWait();
        while (!this.IsCompleted)
            wait.SpinOnce();
        return _result;
    }

    public void SetResult(int value)
    {
        _result = value;
        this.IsCompleted = true;
        this.ScheduleContinuationsIfCompleted();
    }

    public void OnCompleted(Action continuation)
    {
        _continuations.Enqueue(continuation);
        // We need to run the queued continuation immediately if the awaitable has already completed
        this.ScheduleContinuationsIfCompleted();
    }

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
}
```

### TemperatureGauge

Most of the code is fairly self-explanatory. `TemperatureGauge` implements `GetAwaiter` so can be awaited.

`GetTemperatureAsync` creates a new instance of `TemperatureGauge`,  spins off an anonymous method to get the actual value to another thread and returns `TemperatureGauge`.  The spunoff method emulates requesting and reading the sensor from a sensor network and then calls `SetResult` to complete the awaiter. 

```csharp
public class TemperatureGauge
{
    private TemperatureAwaiter _awaiter = new();

    public TemperatureAwaiter GetAwaiter() => _awaiter;

    public static TemperatureGauge GetTemperatureAsync()
    {
        var work = new TemperatureGauge();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(1000);
            work._awaiter.SetResult(Random.Shared.Next(-40, 60));
        });
        return work;
    }
}
```

## Demo

`OnGetTemperatureAsync` isn't awaited.  It implements asynchronous behaviour directly.  It calls `GetTemperatureAsync` and get's it's waiter.  It then schedules the continuation and returns a completed Task.  The continuation will be run when the background process completes.

```csharp
@page "/weather"
@implements IHandleEvent
<PageTitle>Weather</PageTitle>
@using Blazr.Async
<h1>Weather</h1>

<p>Weather, but not as you know it.</p>
<div>
    <button class="btn btn-primary" @onclick="OnGetTemperatureAsync">Get the Temperature</button>
</div>

<div class="bg-dark text-white m-2 p-2">
    @if (_processing)
    {
        <pre>Waiting on Gauge</pre>
    }
    else
    {
        <pre>Temperature &deg;C : @(_temperature?.ToString() ?? "null")</pre>
    }
</div>

@code {
    private int? _temperature;
    private bool _processing;

    private Task OnGetTemperatureAsync()
    {
        _processing = true;
        StateHasChanged();

        var awaiter = TemperatureGauge.GetTemperatureAsync().GetAwaiter();

        awaiter.OnCompleted(() =>
        {
            _temperature = awaiter.GetResult();
            _processing = false;
            StateHasChanged();
        });

        return Task.CompletedTask;
    }

    // Overrides the ComponentBase handler
    // removing all the automatic calls to StateHasChanged
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
        => item.InvokeAsync(arg);

}
```

### ConfigureAwait

Calling `ConfigureAwait` on an *awaitable* doesn't set status fields in the awaitable to configure where the contuation is run.

*Do Some Work* will still be run on the  *synchronisation context*, not be run on a threadpool thread.  `task.ConfigureAwait` returns a  `ConfiguredTaskAwaitable` which in the code is discarded. returns a `ConfiguredTaskAwaiter`

```csharp
var task = Task.CompletedTask;
task.ConfigureAwait(ConfigureAwaitOptions.None);
task.ContinueWith(_ =>
{
    //Do some work
});
```

This will do as requested.

```csharp
var configuredAwaitable = Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.None);
var configuredAwaiter = configuredAwaitable.GetAwaiter();
configuredAwaiter.OnCompleted(() =>
{
    //Do some work
});
```

## Some Key Points

1. A call to a method returning a Task returns a `Task<T>`, not `T`.  The way you write the code suggests result is `T`.  The Dev environment even tells you so.  That's just syntactic sugar.  Behind the scenes the code is calling `GetResult()` on the completed `Task<T>`.  Miss out the `await` and `result` with now be a `Task<T>.
  
2. `Task` and all it incarnations respect `SynchronizationContext.Current`, and run the continuation on that context if configured to do so through `ConfigureAwait`. 

3. Async methods that need to await a result [from another process] must run on a separate background thread.  The action of awaiting blocks the thread.  Switching this await, along with responsibility to schedule the continuations, to a separate thread frees the main thread.  This is the process of yielding.  You can see this in the example above. 

4. You can set more than one continuation on an awaitable, and you can pass a continuation to a completed awaiter and it will be executed.  

5. It should be clear from the above code why calling `GetResult` blocks the current thread and causes deadlocks.