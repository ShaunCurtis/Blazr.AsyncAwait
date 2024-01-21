# An Async Await State Machine

The *Async Await State Machine* is a fundimental building block of the *Task Parallel Library*.

When you write this high level code:

```csharp
private async Task Clicked()
{
    await Task.Delay(3000);
}
```

It gets morphed into:

1. A state machine object within the container class.
1. A refactored `Clicked`.

A simplistic task machine based on the above code looks like this.  I've added comments to help understand it.

```csharp
private class BaseStateMachine
{
    private readonly MyComponent _parent;

    private readonly TaskCompletionSource _taskManager = new();
    private int _state = 0;

    public Task Task => _taskManager.Task;

    public BaseStateMachine(MyComponent parent)
    {
        _parent = parent;
    }

    public void MoveNext()
    {
        try
        {
            // Initial State - run on first pass
            if (_state == 0)
            {
                // first sync code block
                Console.WriteLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");

                // First await Task.  We just start it.
                var task1 = Task.Delay(3000);

                // Sets the state to the next step
                _state = 1;

                // If the task yielded then the awaiter is running on a difffernt thread
                // We add a continuation to call this Execute method when it completes
                // and complete.  Our work is done.
                if (!task1.IsCompleted)
                {
                    task1.ContinueWith(_ => MoveNext());
                    return;
                }

                // If the task was sync with no yield i.e. it ran to completion, we drop out to the next state and continue execution
            }

            // We reach this point either because we fell thro from the previous task because it executed synchronously
            // Or the task in the previous state completed and the continuation ran and called us.
            if (_state == 1)
            {
                // The second Sync Block of code
                Console.WriteLine($"State Machine Processing completed at {DateTime.Now.ToLongTimeString()}");
            }

            // As many states as there are awaits in the original code block

            // The last state block doesn't run any async code so falls out the bottom

            // Set the state machine's Task to complete
            _taskManager.SetResult();
            // and finish

        }
        // Something went wrong.  Pass the error to the caller through the completion task
        catch (Exception e)
        {
            _taskManager.SetException(e);
        }
    }
}
```

`Clicked` has been carved up into two code blocks, separated by the await.  Each is a state. If the code had contained two awaits there would be three state code blocks.

Here's the refactored `Clicked`.

```csharp
private Task Clicked()
{
    var stateMachine = new BaseStateMachine(this);
    stateMachine.MoveNext();
    return stateMachine.Task;
}
```

The first call to `Execute` runs the code to the first yield - in this case the Task.Delay(3000).  The returned task is not complete, so `Execute` adds a continuation to call itself, and returns.

`Clicked` returns the the state machine task and completes.

At this point we get a Task object returned to us.  It's either:

1. Completed.  The code block ran to completion: all done.  It's safe to get the result from the returned task. 

2. Incomplete.  There's still code running on a background thread working on getting a result.  Trying to get the result will block the current thread.
 
The most important thing to understand is code on the current thread [the synchronisation contextcompleted and returned a `Task` object.  It yielded and returned control to the caller.

Executing the code below the `await` is no longer the responsibility of the current executing block of code.  That code has been wrapped into a *continuation* and passed to the task by calling `ContinueWith`.  It's now the background thread's responsibility to execute that code when it gets the result for the current operation.

This is where the synchronisation context comes into play.  The awaitable Task captures the synchronisation context from the original thread.  If `ConfigureAwait` is true [the default], the background thread posts the continuation back to the synchronisation context.

The code running   
. The code block spawned a process running on another thread and returned., and then  There's some code  an *awaitable* object waiting on a separate thread for `Task.Delay` to complete.  It holds a reference to `Execute` in the `Clicked_StateMachine` instance created in the `Home` page.  When Task.Delay completes, it sets the Task to complete and posts the continuation to the synchronisation context.

`Execute` runs with a state of `1` on the synchronisation context.  It executes the final code block, sets the state machine task to complete and exits.  Job done.

### Take Aways

There are no `async` or `await` statements in the morphed code.  There's just code running on threads (and blocking them), and getting posted to the synchronisation context to be run sequentially.