# An Example

Our example is the following Component.

The only out of the ordinary code is a manual implementation of `IHandleEvent.HandleEventAsync`.

```csharp
@page "/"
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

    private async Task Clicked()
    {
        _log.AppendLine($"Standard Processing at {DateTime.Now.ToLongTimeString()}");
        await BlazrTask.Delay(1000);
        _log.AppendLine($"Completed Processing at {DateTime.Now.ToLongTimeString()}");
    }

    async Task IHandleEvent.HandleEventAsync(Microsoft.AspNetCore.Components.EventCallbackWorkItem item, object? arg)
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

The conpiler will refactoring this code into modified handlers and add two state machine objects to the class.

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

```
