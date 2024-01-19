# Task.Yield

`Task.Yield()` is a mechanism for introducing an async yield into a block of code.  It's purpose is to return a running `Task` to the caller.  This causes the caller code block to yield back to it's caller.  In general this releases the execution context [either the Synchronisation Context or the Threadpool scheduler] to process the next code block in it's queue.

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



