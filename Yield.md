# Task.Yield

`Task.Yield()` is a mechanism for introducing an immediate yield into a block of `Async/Await` code and forcing the scheduling of the continuation on either the *synchronisation context* or the Threadpool.

> There are concepts discussed in this post, such as the `Async State Machine`, that you may be unfamiliar with.  If so I suggest you read the [Understanding Asynchronous Behaviour Post](./Understanding-Asynchronous-Behaviour.md).

Consider this contrived UI event Handler:

```csharp
<button class="btn btn-primary" @onclick="this.StepYield">Yield</button>

<div class="bg-dark text-white p-2 m-2">
    <pre>
        @_sb.ToString()
    </pre>
</div>

@code {
    private StringBuilder _sb = new();

    private Task StepYield()
    {
        _sb.AppendLine("Step1");
        Thread.Sleep(1000);

        _sb.AppendLine("Step2");
        StateHasChanged();
        Thread.Sleep(1000);

        _sb.AppendLine("Step3");
        StateHasChanged();
        Thread.Sleep(1000);

        _sb.AppendLine("Complete");

        return Task.CompletedTask;
    }
}
```

When you run this code the display doesn't update until the end.  `StepYield` is synchronous code. There's no yielding of control.  The render fragment posted to the *synchronisation context* queue remains queued until `StepYield` completes.

Now introduce `Task.Yield` into the mix:

```csharp

    private async Task StepYield()
    {
        _sb.AppendLine("Step1");
        await Task.Yield();
        Thread.Sleep(1000);

        _sb.AppendLine("Step2");
        StateHasChanged();
        await Task.Yield();
        Thread.Sleep(1000);

        _sb.AppendLine("Step3");
        StateHasChanged();
        await Task.Yield();
        Thread.Sleep(1000);

        _sb.AppendLine("Complete");
    }
```

And we see the stepped sequence in the display.

You can even write something like this:

```csharp
    private YieldAwaitable Yield = Task.Yield();

    private async Task StepYield()
    {
        _sb.AppendLine("Step1");
        await Yield;
        Thread.Sleep(1000);

        _sb.AppendLine("Step2");
        StateHasChanged();
        await Yield;
        Thread.Sleep(1000);

        _sb.AppendLine("Step3");
        StateHasChanged();
        await Yield;
        Thread.Sleep(1000);

        _sb.AppendLine("Complete");
    }
```

Note that `Task.Yield()` returns a `YieldAwaitable`, not a `Task`.  We cache and reuse this object.  

Within the async state machine compiled from this code, each step's awaitable - `Yield` - is not complete, so the step schedules a continuation, and completes.  The RenderFragment queued by calling `StateHasChanged` is now at the front of the queue and run next.  It renders the component, updates the UI, and completes.  The continuation now runs.... and so on.

## Building A Yielder

We can build our own yielder.  First it needs to implement the *Awaitable* pattern:


```csharp
public class BlazrYield
{
    public BlazrYield GetAwaiter()
        => this;
```

And as it returns itself it needs to implement the *awaiter* pattern.

```csharp
public class BlazrYield : INotifyCompletion
{
    public bool IsCompleted;

    public BlazrYield GetAwaiter()
        => this;

    public void OnCompleted(Action continuation) { }

    public void GetResult() { }
}
```

`IsCompleted` needs to always return `false` to force the ASM to post a continuation to the *awaiter*.

```csharp
    public bool IsCompleted => false;
```

We need to capture thw *synchronisation context* which we do during initialization.

```csharp
    private SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;
```

We need to restrict creation so we only allow a static constructor:

```csharp
    private BlazrYield() { }

    public static BlazrYield Yield()
    {
        var instance = new BlazrYield();
        return instance;
    }
}
```

Finally we code `OnCompleted`.  This is the method called to add a continuation to the *awaiter*.  Normally this would add the provided `Action` to a internal queue for executing when the process behind the *awaiter* completes.  However, here we schedule the continuation immediately.

The code checks to see if we have a saved *synchronisation context*.  If we do we create a `SendOrPostCallback` delegate and post it.  If not we create a `WaitCallback` and post it to the Threadpool queue.

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

    private BlazrYield() { }

    public BlazrYield GetAwaiter()
        =>  return this;

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

    public void GetResult() { }

    public static BlazrYield Yield()
    {
        var instance = new BlazrYield();
        return instance;
    }
}
```

## New Features

Net8 introduced some new versions of the `Task.ConfigureAwait` method.  You can now provide a `ConfigureAwaitOptions` Enum flag. 

If you wrote something like:

```csharp
await Task.CompletedTask.ContinueWith((awaitable) => {_sb.AppendLine("Complete"); });
```

The continuation would be executed synchronously as part of the current code block running on the *synchronisation context*.  There would be no yield at the await.

You can now write:

```csharp
    private Task Clicked()
    {
        var task = Task.CompletedTask;
        _sb.AppendLine("Started");

        task.ContinueWith((awaitable) =>
        {
            Thread.Sleep(1000);
            _sb.AppendLine("Complete");
        })
        .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        return task;
    }
```

Which runs, but doesn't update the log for "Complete". Why?  We've now sc

```csharp
    private Task Clicked()
    {
        var task = Task.CompletedTask;
        _sb.AppendLine("Started");

        task.ContinueWith((awaitable) =>
        {
            Thread.Sleep(1000);
            _sb.AppendLine("Complete");
            StateHasChanged();
        })
        .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        return task;
    }
```



```csharp
await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding | ConfigureAwaitOptions.ContinueOnCapturedContext);
```

