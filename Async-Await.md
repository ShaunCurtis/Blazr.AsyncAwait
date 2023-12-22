# Async/Await

Async/Await is fundamental building material in modern C# coding.  It's great blessing is it abstracts the programmer from the nitty gritty of the *Task Processing Library*.

Don't feel ashamed if you can't detail how it works: most programmers will say "Well, waffle, waffle, waffle...." after a quick consultation session at the coffee machine with collegues who have a similar shallow knowledge [but aren't prepared to admit it!]. 

In this short article I'll attempt to explain what happens below those high level language directives.

Consider this simple Blazor `Home` page:

```csharp
@page "/"

<PageTitle>Home</PageTitle>

<h1>Hello, world!</h1>

Welcome to your new app.

<div class="mb-3">
    <button class="btn btn-success" @onclick="Clicked">Responsive Click</button>
    <button class="btn btn-danger" @onclick="_Clicked">Unresponsive Click</button>
</div>

<div class="bg-dark text-white m-2 p-2">
    @_message
</div>

@code {
    private string? _message;

    private async Task Clicked()
    {
        _message = $"Processing at {DateTime.Now.ToLongTimeString()}";
        await TaskHelper.DoSomethingAsync();
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }

    private async Task _Clicked()
    {
        _message = $"Processing at {DateTime.Now.ToLongTimeString()}";
        await TaskHelper.PretendToDoSomethingAsync();
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }
}
```

*Responsive Click* steps through the two messages.  *Unresponsive Click* shows both messages at the end. 

`TaskHelper` looks like this:

```csharp
public static class TaskHelper
{
    public static Task DoSomethingAsync()
        => Task.Delay(1000);

    public static Task PretendToDoSomethingAsync()
    {
        Thread.Sleep(1000);
        return Task.CompletedTask;
    }
}
```

Those three lines are transposed into lower level C# code that implements a state machine.

`async` is a modifier and `await` is an operator.

1. The state machine is implemented as a class within the owner - in this case `Home`.  This gives it access to the private methods, properties and variables of `Home`. 

2. Each code block between `awaits` is a state.  Think of doing a `split` on `await`: one `await` will produce two states.

1. The constructor requires a reference to the parent - `_parent`.
 
1. It uses a `TaskCompletionSource` to control the task provided by the state machine.

1. There are global `Task` variables for all the async methods called.  In this case `_task1_Task` to assign `DoSomethingAsync` to when we call it.

1. The initial `_state` is set to `0`.
 
1. The state machine is run by calling `Execute`.

```csharp
    class Clicked_StateMachine
    {
        enum State { Start, Step1, }
        private readonly Home _parent;

        private readonly TaskCompletionSource _tcs = new();
        private State _state = State.Start;
        private Task _state1_Task = Task.CompletedTask;

        public Task Task => _tcs.Task;

        public Clicked_StateMachine(Home parent)
        {
            _parent = parent;
        }

        public void Execute()
        { }
    }
```

Now for the `Execute` detail.  Execution is wrapped in a `try` so we can pass the exception to the caller through the `TaskCompletionSource`.

```csharp
public void Execute()
{
    try
    {
        //...
    }
    // Something went wrong.  Pass the error to the caller through the completion task
    catch (Exception e)
    {
        _tcs.SetException(e);
    }
}
```
The *Start* step runs the code up to the first `await`.  It sets the message, calls `DoSomethingAsync` on the parent and assigns it to `_state1_Task`.  It sets the `_state` to the next state.  It then checks the state of `_state1_Task`.  The key observation to make is that if the task has yielded then a continuation is set on the task to call `Execute` when it completes.  If it has an exception or is cancelled then the appropriate state is set on the _taskManager.  If th task is complete then the method falls through into the next state and executes the next step synchronously.  There's no continuation and no yield.

```csharp
    if (_state == 0)
    {
        // The code from the start of the method to the first 'await'.
        {
            _parent._log.AppendLine($"State Machine Processing at {DateTime.Now.ToLongTimeString()}");
        }

        _state1_Task = TaskHelper.DoSomethingAsync();

        _state = 1;

        if (this.ReturnOnTaskStatus(_state1_Task))
            return;
    }
```

The two task check methods look like this.  `HandleTaskErrorOrCancellation` handles exceptions and Cancellation.  `ReturnOnTaskStatus` detects if the task ran synchronously and if it did returns `false`.

```csharp
private bool ReturnOnTaskStatus(Task task)
{
    if (task.IsCompleted)
        return false;

    if (!task.IsCompleted)
    {
        task.ContinueWith(_ => Execute());
        return true;
    }

    return HandleTaskErrorOrCancellation(task);
}

private bool HandleTaskErrorOrCancellation(Task task)
{

    if (task.Status == TaskStatus.Canceled)
    {
        _taskManager.SetCanceled();
        return true;
    }

    if (task.Status == TaskStatus.Faulted)
    {
        _taskManager.SetException(task.Exception?.InnerException ?? new Exception("Task just self destructed with no suicide note!"));
        return true;
    }

    return false;
}
```
Step 2 checks the stste of `_state1_Task` for exceptions and cancellation.  It then runs the code to completion [sets the message].  As there's no further awaits it falls out of the bottom to the finalization process.

```csharp
    // Step 1 - the first await block
    if (_state == 1)
    {
        if (this.HandleTaskErrorOrCancellation(_state1_Task))
            return;

        {
            _parent._log.AppendLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");
        }

        //No more await tasks so fall thro to bottom
    }
```

The finalization process is to set the task manager to complete.

```csharp
// No more steps, job done.  Set the Task to complete and finish.
_taskManager.SetResult();
```

Finally this code is plugged into `Clicked` in `Home`.  Note it's no longer `async` and returns a Task from the state machine to the UI event handler.

```csharp
    private Task Clicked()
    {
        var stateMachine = new Clicked_StateMachine(this);
        stateMachine.Execute();
        return stateMachine.Task;
    }
```

## The Real Thing

The code I've shown above isn't the actual code generated by the compiler.  That's a little more complicated because the code above doesn't address certain implementation and performance issues: it's fat and slow for the happy path.

However it does hopefully provide a good insight into what's going on.

An interesting point is that the state machine is compiled as a `class` in debug mode and a `struct` in release mode. 

## References

The primary resources for this article were:

[Sergey Tepliakov's Blog series](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/)

[Stephen Toub's Blog series](https://devblogs.microsoft.com/pfxteam/await-anything/)

[Stephen Cleary's various airings on the topic such as this one](https://blog.stephencleary.com/2023/11/configureawait-in-net-8.html)

The code example is based on Sergey Tepliakov's code. 