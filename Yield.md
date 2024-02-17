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


```csharp
public class BlazrYield : INotifyCompletion
{
    public bool IsCompleted => false;

    private SynchronizationContext? _synchronizationContext = SynchronizationContext.Current;

    private BlazrYield() { }

    public BlazrYield GetAwaiter()
    {
        return this;
    }

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







============================
It's purpose is to return a running `Task` to the caller.  The caller  This causes the caller code block to yield back to it's caller.  In general this releases the execution context [either the Synchronisation Context or the Threadpool scheduler] to process the next code block in it's queue.

It's high level code, abstracting the programmer from the nitty gritty of the *Task Processing Library*.

For a deep dive into it's workings we'll use a simple Blazor example: a button click handler.

Here's a simple home page with a button to carry out some synchronous blocking activity.

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
        await Task.Yield();
        // A blocking synchronous operation
        await TaskHelper.PretendToDoSomethingAsync();
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }
}
```

Run this and you will see the UI stays responsive, the first message is logged and displayed before the blocking code runs and finally displays the second message.

We can build our own version of `Yield` like this.

Create a `TaskCompletionSource`. We'll pass the `Task` associated with it to the caller. 

```csharp
public static class BlazrTask
{
    public static Task Yield()
    {
        var tcs = new TaskCompletionSource();
```

Create a completed Task i.e. a task with an `awaitable` that is complete.

```csharp
        var task = Task.CompletedTask;
```

Add a completion to the Task that sets the result of the `TaskCompletionSource`.  This `Posts` the continuation to the Dispatcher on the current thread/SC. 

```csharp
        task.ContinueWith(await =>
        {
            tcs.TrySetResult();
        });
```

The code finally runs to completion, returning the `tcs` task. 

```csharp
        return tcs.Task;
```

The continuation is queued on the current context behind the current execution block.  The current block returns the `tcs` running `Task` to the caller who yields back to the caller, .... 

To understand the wider context, there are some points of knowledge:

1. The Component almost certainly inherits from `ComponentBase` and implements a `IHandleEvent.HandleEventAsync` UI event handler the schedules a render event on the first yield of the actual handler and after the completion of the handler.

2. Calling `await` in itself doesn't cause a yield.  `await TaskHelper.PretendToDoSomethingAsync` may call a Task based method, but `PretendToDoSomethingAsync` is a block of synchronous code. The continuation code block is executed directly after the call to `PretendToDoSomethingAsync` completes.  There's no continuation created and posted to the current thread.

1. The default continuation behaviour is to schedule the continuation on the current thread.  In the current context, everything runs on the SC.
 
1. The Synchronisation Context prioritizes posted code over UI generated code.  Almost all activity is the result of UI events, so it attempts to complete what it's already started before reacting to new UI event. 

After posting the completion the following is queued on the SC:

1. The rest of the code for `Yield`.
1. The continuation.

The `Yield` code completes.  `Clicked` yields because the returned `Task` is not complete.  The `IHandleEvent.HandleEventAsync` queues a render event onto the renderer's queue.

1. The continuation.
1. Renderer service action.

The continuation runs and sets the `tcs` to complete, and thus it's associated  `Task` to complete.  The continuation for `Clicked` is now queued.

1. Renderer service action.
1. `Clicked` Continuation.

The Renderer services it's queue, and passes any DOM changes to the Browser DOM which updates the UI [and triggers an `AfterRender` UI Event].

1. `Clicked` Continuation.
1. `AfterRender` UI Event.

The `Clicked` Continuation completes and the `IEventHandle.EventHandler` queues a second render event on the completion of the handler.

1. `AfterRender` UI Event.
1. Renderer service action.

At this point prioritization comes into play.  The `AfterRender` UI Event is bumped: the Renderer service action is posted code and has a higher priority.

1. Renderer service action.
1. `AfterRender` UI Event.

The Renderer services it's queue, and passes any DOM changes to the Browser DOM which updates the UI [and triggers another `AfterRender` UI Event].

1. `AfterRender` UI Event.
1. `AfterRender` UI Event.

The two queued `AfterRender` events are run.



