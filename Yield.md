# The Async Series - Task.Yield

`Task.Yield()` is a mechanism for introducing a yield into a block of `Async/Await` code and forcing the scheduling of the continuation on either the *synchronisation context* or the Threadpool.

> There are concepts discussed in this post, such as the `Async State Machine`, that you may be unfamiliar with.  If so I suggest you read the [Understanding Asynchronous Behaviour Post](./Understanding-Asynchronous-Behaviour.md).

Consider this contrived UI event Handler.  

I've overridden the `IHandleEvent` handler so we manually call `StateHasChanged` where we need to.  There's no hidden functionality happening in the background.  You can see thw calls in the code.

```csharp
@implements IHandleEvent

<button class="btn btn-primary" @onclick="this.StandardHandler">Standard Handler</button>

<div class="bg-dark text-white p-2 m-2">
    <pre>
        @_sb.ToString()
    </pre>
</div>

@code {
    private StringBuilder _sb = new();

    private async Task StandardHandler()
    {
        _sb.AppendLine("Step1");
        StateHasChanged();
        await Task.CompletedTask;
        Thread.Sleep(500);

        _sb.AppendLine("Step2");
        StateHasChanged();
        await Task.CompletedTask;
        Thread.Sleep(500);

        _sb.AppendLine("Step3");
        StateHasChanged();
        await Task.CompletedTask;
        Thread.Sleep(500);

        _sb.AppendLine("Complete");
        StateHasChanged();
        await Task.CompletedTask;
    }

    // Overrides the ComponentBase handler removing all the automatic calls to StateHasChanged
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
        => item.InvokeAsync(arg);
}
```

When you run this code, the display only updates at the end.  It's all synchronous code running sequentially on the *synchronisation context*. There's no yielding of control.  The render fragment posted to the *synchronisation context* queue by `StateHasChanged` remains queued until `StandardHandler` completes.  Call `StateHasChanged` as often as you like, there's no render event until `StandardHandler` completes.

> Digression: `StateHasChanged` has a built in mechanism to detect when a render fragment is already queued and abort.   

Now introduce a `Task.Yield` into the mix:

```csharp
    private async Task StandardYield()
    {
        _sb.AppendLine("Step1");
        StateHasChanged();
        await Task.Yield();
        Thread.Sleep(500);

        _sb.AppendLine("Step2");
        StateHasChanged();
        await Task.Yield();
        Thread.Sleep(500);

        _sb.AppendLine("Step3");
        StateHasChanged();
        await Task.Yield();
        Thread.Sleep(500);

        _sb.AppendLine("Complete");
        StateHasChanged();
    }
```

And we see the stepped sequence in the display.

You can even write something like this.  `Task.Yield()` returns a `YieldAwaitable`, not a `Task`.  This code caches and reuses this object.

```csharp
    private YieldAwaitable Yield = Task.Yield();

    private async Task AlternativeStandardYield()
    {
        _sb.AppendLine("Step1");
        StateHasChanged();
        await Yield;
        Thread.Sleep(500);

        _sb.AppendLine("Step2");
        StateHasChanged();
        await Yield;
        Thread.Sleep(500);

        _sb.AppendLine("Step3");
        StateHasChanged();
        await Yield;
        Thread.Sleep(500);

        _sb.AppendLine("Complete");
        StateHasChanged();
    }
```

A `YieldAwaitable` returns a `YieldAwaiter` which has `IsCompleted` set to false, and posts any continuations passed through `OnCompleted` immediately to either the *synchronisation context* or the *threadpool*.

In the code `StateHasChanged` queues a render fragment and then `Yield` queues the continuation and completes freeing the *synchronisation context* thread.  The render fragment gets executed [and UI updated] followed by the continuation.

## Building A Yield Object

We can build our own yielding object.  

First it must implement the *Awaitable* pattern:

```csharp
public class BlazrYield
{
    public BlazrYield GetAwaiter()
        => this;
```

It returns itself, so must implement the *awaiter* pattern.

```csharp
public class BlazrYield : INotifyCompletion
{
    public bool IsCompleted;

    public BlazrYield GetAwaiter()
        => this;

    public void OnCompleted(Action continuation) {}

    public void GetResult() {}
}
```

`IsCompleted` needs to always return `false` to force the ASM to post a continuation to the *awaiter*.

```csharp
    public bool IsCompleted => false;
```

We capture the *synchronisation context* during initialization.

```csharp
    private SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
```

And restrict creation to a static constructor.

```csharp
    private BlazrYield() {}

    public static BlazrYield Yield()
    {
        var instance = new BlazrYield();
        return instance;
    }
}
```

Finally we code `OnCompleted`.  This method is called to add a continuation to the *awaiter*.  Normally this would add the provided `Action` to a internal queue for executing when the process behind the *awaiter* completes.  However, here it's scheduled immediately.

The code checks to see for a saved *synchronisation context*.  If true it creates a `SendOrPostCallback` delegate and posts it.  If not it creates a `WaitCallback` delegate and posts it to the threadpool queue.

```csharp
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
```

The completed code:

```csharp
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
```

## New Features

Net8 introduced some new versions of the `Task.ConfigureAwait` method.  You can now provide a `ConfigureAwaitOptions` Enum flag. 

We can replace our code above with the following:

```csharp
await MyTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding | ConfigureAwaitOptions.ContinueOnCapturedContext);
```

