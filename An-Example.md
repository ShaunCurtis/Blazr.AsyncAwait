# An Example

Our example is the following Component.

The only out of the ordinary code is a manual implementation of `IHandleEvent.HandleEventAsync`.

```csharp
@page "/"
@using System.Runtime.CompilerServices
@implements IHandleEvent

<PageTitle>Home</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<div class="mb-3">
    <button class="btn btn-primary" @onclick="Clicked">Standard Click</button>
</div>

<div class="bg-dark text-white m-2 p-2">
    <pre>
        @_log.ToString()
    </pre>
</div>

@code {
    private System.Text.StringBuilder _log = new();
    private string? _message;
    private SynchronizationContext? _sc = SynchronizationContext.Current;

    private Task Clicked( MouseEventArgs e )
    {
        _log.AppendLine($"Standard Processing at {DateTime.Now.ToLongTimeString()}");
        _message = await GetMessageAsync();
        _log.AppendLine($"Completed Processing at {DateTime.Now.ToLongTimeString()}");
    }

    private Task<string> GetMessageAsync()
    {
        _log.AppendLine($"Standard Processing at {DateTime.Now.ToLongTimeString()}");
        await Task.Yield();
        _log.AppendLine($"Completed Processing at {DateTime.Now.ToLongTimeString()}");
        return $"Processed at {DateTime.Now.ToLongTimeString()}";

    }

    /// <summary>
    /// Implements the UI event handler and overides the existing handler in ComponentBase
    /// </summary>
    /// <param name="item"></param>
    /// <param name="arg"></param>
    /// <returns></returns>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
    {
            var uiTask = item.InvokeAsync(arg);

            if (!uiTask.IsCompleted)
            {
                this.StateHasChanged();
                await uiTask;
            }

            this.StateHasChanged();
    }
```

The compiler will refactor this code into modified handlers and add two state machine objects to the class.

First the `HandleEventAsync` state machine.

There's a single conditional await so the code is split into two states and one awaiter.

```csharp
class HandleEventAsync_StateMachine
{
    private readonly Demo _parent;
    private EventCallbackWorkItem _eventCallbackWorkItem;
    private object? _eventCallbackWorkItemArgs;

    private readonly TaskCompletionSource _taskManager = new();
    private int _state = 0;

    // Tasks for each step
    private Task _state_0_Task = Task.CompletedTask;

    public Task Task => _taskManager.Task;

    public HandleEventAsync_StateMachine(Demo parent, EventCallbackWorkItem item, object? args)
    {
        _parent = parent;
        _eventCallbackWorkItem = item;
        _eventCallbackWorkItemArgs = args;
    }

    public void Execute()
    {
        try
        {
            if (_state == 0)
            {
                _state_0_Task = _eventCallbackWorkItem.InvokeAsync(_eventCallbackWorkItemArgs);;

                _state = 1;

                if (!_state_0_Task.IsCompleted)
                {
                    _parent.StateHasChanged();
                    _state_0_Task.ContinueWith(_ => Execute());
                    return;
                }
            }

            if (_state == 1)
            {
                _parent.StateHasChanged();
            }

            _taskManager.SetResult();

        }
        catch (Exception e)
        {
            _taskManager.SetException(e);
        }
    }
}
```

`IHandleEvent.HandleEventAsync` is refactored to use the state machine.

```csharp
Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
{
    var stateMachine = new HandleEventAsync_StateMachine(this, item, arg);
    stateMachine.Execute();
    return stateMachine.Task;
}
```

The Clicked state machine:

```csharp
class Clicked_StateMachine
{
    private readonly Demo _parent;
    private readonly TaskCompletionSource _taskManager = new();
    private int _state = 0;
    private Task _state_0_Task = Task.CompletedTask;

    public Task Task => _taskManager.Task;

    public Clicked_StateMachine(Demo parent)
       => _parent = parent;

    public void Execute()
    {
        try
        {
            if (_state == 0)
            {
                _parent._log.AppendLine($"State Machine Processing at {DateTime.Now.ToLongTimeString()}");
                _state_0_Task = TaskHelper.DoSomethingAsync();
                _state = 1;

                if (!_state_0_Task.IsCompleted)
                {
                    _state_0_Task.ContinueWith(_ => Execute());
                    return;
                }
            }

            if (_state == 1)
            {
                _parent._log.AppendLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");
            }

            _taskManager.SetResult();

        }
        // Something went wrong.  Pass the error to the caller through the completion task
        catch (Exception e)
        {
            _taskManager.SetException(e);
        }
    }
}
```

And the refactored `Clicked`

```csharp
private Task Clicked()
{
    var stateMachine = new Clicked_StateMachine(this);
    stateMachine.Execute();
    return stateMachine.Task;
}
```

## The Action

The button is the broswer is wired through the JS event into the Blazor JS code which pushes the event through JS Interop to the Renderer.

When the button is clicked the event flows through to the Renderer.  It ties the event back to `Clicked` handler in the `Demo` component.

It checks if `Demo` implements `IHandleEvent`  If so it invokes `HandleEventAsync` on the *Synchronisation Context* passing it a reference to the handler and the arguments.  If it doesn't implement the interface, it invokes the handler directly.

Basically we get a post to the *Synchronisation Context* queue.

```
SynchronizationContext.Current?.Post(_ =>
    {
        HandleEventAsync(item, arg);
    },
    null);
```

This executes the refactored  `HandleEventAsync` which creates and executes the `HandleEventAsync_StateMachine` to this line:

```csharp
    _state_0_Task = _eventCallbackWorkItem.InvokeAsync(_eventCallbackWorkItemArgs); 
```

This executes the refactored `Clicked` which creates and executes the `Clicked_StateMachine` to this line.  Until now everything has been synchronous code running in sequence and stepping into the methods.

```csharp
   _state_0_Task = Task.Delay(1000);
```

`Task.Delay` yields back control to the caller before it's complete.  `Clicked_StateMachine.Execute` adds a continuation to `Clicked_StateMachine._state_0_Task` to call itself and completes.

```csharp
   _state_0_Task.ContinueWith(_ => Execute());
```

At this point the context and callstack looks like this:

```text
IHandleEvent.HandleEventAsync
  => HandleEventAsync_StateMachine.Execute
      => Clicked
        => Clicked_StateMachine.Execute
```

`Clicked` executes to completion and returns the `Clicked_StateMachine` Task [which is still running] to `HandleEventAsync_StateMachine.Execute`.

This continues and executes this code.  `StateHasChanged` queues a render event onto the Renderer's queue.

```csharp
    if (!_state_0_Task.IsCompleted)
    {
        _parent.StateHasChanged();
        _state_0_Task.ContinueWith(_ => Execute());
        return;
    }
```

The SC has:

```text
IHandleEvent.HandleEventAsync
Renderer Queue Service Request


[awaitable Continuation] HandleEventAsync_StateMachine.Execute
[awaitable Continuatiion] Clicked_StateMachine.Execute.Execute
[awaitable Continuation] Task.Delay waiting on a Timer Callback
```

Adds a continuation to `_state_0_Task` to call itself and completes.

```text
IHandleEvent.HandleEventAsync
  => HandleEventAsync_StateMachine.Execute
      => Clicked
        => Clicked_StateMachine.Execute
```

`IHandleEvent.HandleEventAsync` runs to completion and passes the `HandleEventAsync_StateMachine` task to the SC.

The SC has:

```text
Renderer Queue Service Request


[awaitable Continuation] HandleEventAsync_StateMachine.Execute
[awaitable Continuation] Clicked_StateMachine.Execute
[awaitable Continuation] Task.Delay waiting on a Timer Callback
```

So it services the Renderer Queue [and renders the component and it's renbder tree where required by Parameter changes].


When the timer callback occurs on `Task.Delay(1000)` it sets thw result on it's awaitable Task.

```text
Clicked_StateMachine.Execute [State 1]


[awaitable Continuation] HandleEventAsync_StateMachine.Execute
```

`Clicked_StateMachine.Execute` runs to completion, calls StateHasChanged and sets the result on the Task.

```text
HandleEventAsync_StateMachine.Execute [State 1]
Renderer Queue Service Request
```






