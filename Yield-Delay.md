# What is Task.Yield and TaskDelay?

There are high level code, abstractiung the programmer from the nitty gritty of the *Task Processing Library*.

Let's look at a simple Blazor example: a button click handler.

Here's a simple home page:

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
        Thread.Sleep(1000);
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }
}
```

Run this and you will see the UI stay responsive, the first message flashes before the blocking code runs and finally displays the second message.

We can build out own version of `Yield` like this.

This code is run on the `Synchronisation Context`.

Create a `TaskCompletionSource`. We'll pass the `Task` associated with it to the caller. 

```csharp
public static class BlazrTask
{
    public static Task Yield()
    {
        var tcs = new TaskCompletionSource();
```

Create a Completed Task i.e. a task with an `awaitable` that is complete.

```csharp
        var task = Task.CompletedTask;
```

Add a completion to the Task that sets the result of the `TaskCompletionSource`.  This `Posts` the continuation to the Dispatcher on the SC. 

```csharp
        task.ContinueWith(await =>
        {
            tcs.TrySetResult();
        });
```

What's now queued on the SC is:

1. The rest of the code for `Yield`.
1. The continuation.

When the `Yield` code completes, `Clicked` yields because the returned `Task` is running.  The `IEventHandle.EventHandler` queues a render event onto the renderer's queue.

1. The continuation.
1. Renderer service action.

Next the continuation is run that sets the `Yield` `Task` to complete.  The continuation for `Clicked` is now queued.

1. Renderer service action.
1. `Clicked` Continuation.

The Renderer services it's queue, and passes any DOM changes to the Browser DOM which updates the UI [which triggers an `AfterRender` UI Event].

1. `Clicked` Continuation.
1. `AfterRender` UI Event.

The `Clicked` Continuation completes synchronously.  The `IEventHandle.EventHandler` queues a second render event when it completes.

1. `AfterRender` UI Event.
1. Renderer service action.

At this point prioritization comes into play.  The `AfterRender` UI Event is bumped: the Renderer service action is a higher priority.

1. Renderer service action.
1. `AfterRender` UI Event.

  The Renderer services it's queue, and passes any DOM changes to the Browser DOM which updates the UI [which triggers an `AfterRender` UI Event].

1. `AfterRender` UI Event.
1. `AfterRender` UI Event.

The two queued `AfterRender` events are run.


Return the task from the `TaskCompletionSource`.

```csharp
        return tcs.Task;
```

What now gets run depends on what's queued on the SC.  The manager prioritizes posted code over UI events, so    


