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
        await DoSomethingAsync();
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }

    private async Task _Clicked()
    {
        _message = $"Processing at {DateTime.Now.ToLongTimeString()}";
        await PretendToDoSomethingAsync();
        _message = $"Completed Processing at {DateTime.Now.ToLongTimeString()}";
    }
}
```

*Responsive Click* steps through the two messages.  *Unresponsive Click* shows both messages at the end. 

Those three lines are transposed into lower level C# code that implements a state machine.

`async` is a modifier and `await` is an operator.

1. The state machine is implemented as a class within the owner - in this case `Home`.  This gives it access to the private methods, properties and variables of `Home`. 

2. Each code block between `awaits` is a state.  Think of doing a `split` on `await`: one `await` will produce two states.

1. The constructor requires a reference to the parent - `_parent`.
 
1. It uses a `TaskCompletionSource` to control the task provided by the state machine.


When the compiler excounters that `async` and finds an `await` in the code block it totally rebuilds the code into a state machine class.

The class framework for `Clicked` looks like this.

1. It's within the primary class, so has access to all the private resources of the parent.
1. There's only one yield, so there's only two states: *Start* and *Step1*.
2. The constructor requires a reference to the parent.
3. We use a `TaskCompletionSource` to control the task we return to the caller.
4. There are global `Task` variables for all the async methods called.  In this case `_doSomethingAsync_Task` to assign `DoSomethingAsync` to when we call it.
5. The initial `_state` is set to `Start`.
6. The state machine is run by calling `Execute`.

```csharp
    class Clicked_StateMachine
    {
        enum State { Start, Step1, }
        private readonly Home _parent;

        private readonly TaskCompletionSource _tcs = new();
        private State _state = State.Start;
        private Task _doSomethingAsync_Task = Task.CompletedTask;

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
The *Start* step runs the code up to the first `await`.  It sets the message, calls `DoSomethingAsync` on the parent and assigns it to `_doSomethingAsync_Task`.  It sets the `_state` to the next state and sets the continuation on the first Task.  The continuation is a recursion: it calls itself.  The order here is critical.  The continuation isn't set when the Task is originally called because at that point `_state` is still `State.Start` and we create a black hole.

If `_doSomethingAsync_Task` is already complete the continuation will get scheduled immediately.

```csharp
    if (_state == State.Start)
    {
        // The code from the start of the method to the first 'await'.
        {
            _parent._message = $"Processing at {DateTime.Now.ToLongTimeString()}";
            _doSomethingAsync_Task = _parent.DoSomethingAsync();
        }
        // Update state and schedule continuation
        {
            _state = State.Step1;
            _doSomethingAsync_Task.ContinueWith(_ => Execute());
        }
        // all do for this state so return
        return;
    }
```
Step 2 is run when `_doSomethingAsync_Task` completes and the continuation is run.  It checks for errors or a cancellation and applies these to the `TaskCompletionSource` if they have occured.

It then runs the code to completion [sets the message] and finally sets the result on the `TaskCompletionSource`: this sets it to completed.

```csharp
    // Step 2
    if (_state == State.Step1)
    {
        // If the task was cancelled then set _tcs to canceled and return
        if (_doSomethingAsync_Task.Status == TaskStatus.Canceled)
        {
            _tcs.SetCanceled();
            return;
        }

        // If the task was faulted then set the exception in _tcs and return
        if (_doSomethingAsync_Task.Status == TaskStatus.Faulted)
        {
            _tcs.SetException(_doSomethingAsync_Task.Exception?.InnerException ?? new Exception("DoSomethingAsync just self destructed with no suicide note!"));
            return;
        }

        // The code following the first 'await' to the next await or the end.
        {
            _parent._message = $"Processing completed at {DateTime.Now.ToLongTimeString()}";
            // No more steps, job done.  Set the Task to complete and finish.
            _tcs.SetResult();
        }
    }
```

Finally plug this code into `Home`.  Note it's no longer `async` and returns a Task to the UI event handler to `await`.

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