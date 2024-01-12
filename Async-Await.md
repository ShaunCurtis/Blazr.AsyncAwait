# Async/Await

Async/Await is fundamental building material in modern C# coding.  It's great blessing is it abstracts the programmer from the nitty gritty of the *Task Processing Library*.  

The downside is it's success: programmers just use it without the need to understand what's really going on.  Good in most instacnes, but when it doesn't work as advertised, it's very hard ro understand why not. 

There are several very good articles available on the subject.  The problem is that they assume a level of knowledge that most programmers don't have.  In this short article I'll attempt to bring that required knowledge down to the level of normal mortal programmers.

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

*Responsive Click* shows each message in turn.  *Unresponsive Click* shows both messages on completion. 

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

### The Async State Machine 

When those three lines are compiled they are first transposed into lower level C# code that implements a state machine.

1. `async` is a modifier and `await` is an operator.

1. The state machine is implemented as a class within the parent class - in this case `Home`.  This gives it access to the private methods, properties and variables of `Home`. 

2. Each code block between `awaits` is a state.  Think of applying a `split` on `await`: one `await` will produce two states.

1. The constructor requires a reference to the parent - `_parent`.
 
1. It uses a `TaskCompletionSource` to control the task provided by the state machine.

1. There are global `Task` variables for all the async methods called.  In this case `_task1_Task` to assign `DoSomethingAsync` to when we call it.

1. The initial `_state` is set to `0`.
 
1. The state machine is run by calling `Execute`.

Here's the skeleton for out three liner.

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

The `Execute` detail.  Execution is wrapped in a `try` so we can pass any exception to the caller through the `TaskCompletionSource`.

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
The *State 0* step runs the code up to the first `await`.  It:

1. Sets the message. 
2. Calls `DoSomethingAsync` on the parent and assigns it to `_state1_Task`.  
3. Sets the `_state` to the next state.  
4. Checks the state of `_state1_Task`.  
   
 - If the task has yielded then a continuation is set on the task to call `Execute` when it completes.  
 - If it has an exception or is cancelled then the appropriate state is set on the _taskManager.  
 - If the task is complete then the method falls through into the next state and executes the next step synchronously.  There's no continuation and no yield.

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

The two helper methods look like this.

 - `HandleTaskErrorOrCancellation` handles exceptions and Cancellation.  
 - `ReturnOnTaskStatus` detects if the task ran synchronously and if it did returns `false`.

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
Step 1 first checks `_state1_Task` for exceptions and cancellation.  If it completed successfully it runs the code to completion [sets the message].  As there's no further awaits it falls out of the bottom to the finalization process.

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

The code I've built above isn't the actual code generated by the compiler.  That's a little more complicated because the code above doesn't address certain implementation and performance issues: it's a bit fat and slow.

However it hopefully provides a good insight into what's going on.

An interesting point is that the state machine is compiled as a `class` in debug mode and a `struct` in release mode. 

## References

The primary resources for this article were:

[Sergey Tepliakov's Blog series](https://devblogs.microsoft.com/premier-developer/dissecting-the-async-methods-in-c/)

[Stephen Toub's Blog series](https://devblogs.microsoft.com/pfxteam/await-anything/)

[Stephen Cleary's various airings on the topic such as this one](https://blog.stephencleary.com/2023/11/configureawait-in-net-8.html)

The code example is based on Sergey Tepliakov's code. 